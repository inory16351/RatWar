// 전장 화면: 바닥(진짜 평면) · 언덕(상자) · 소품/경치/병사(세운 판때기) · 시야 안개 · 이동 범위 · 효과
// 웹 view.js 의 가짜 원근(proj · drawWarp · reproject · zEff)은 버림 — 원근 카메라가 해 줌 (핸드오프 '유니티로 옮길 때')
// 좌표: 칸 (x, z) → 월드 (x, 높이 × EL, D − 1 − z). z = 0(고양이 후방)이 가장 멀리.
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class BoardView : MonoBehaviour
{
    public static BoardView I;
    public const float EL = 0.36f; // 높이 1 = 26/72 칸

    // 칸 가로 배수 (웹 PROP_W)
    static readonly Dictionary<char, float> PROP_W = new Dictionary<char, float>
    {
        ['S'] = 1.12f, ['C'] = 0.86f, ['W'] = 1.25f, ['T'] = 1.3f, ['X'] = 1.12f, ['M'] = 1.0f, ['G'] = 1.2f, ['R'] = 3.0f, ['H'] = 1.25f,
        // 소품 시트 A
        ['D'] = 0.95f, ['B'] = 1.1f, ['U'] = 1.15f, ['K'] = 1.2f, ['F'] = 1.2f, ['L'] = 1.1f, ['Q'] = 0.95f, ['J'] = 1.15f,
        ['Y'] = 1.2f, ['V'] = 1.5f, ['P'] = 1.3f, ['A'] = 1.25f, ['E'] = 1.1f, ['Z'] = 1.3f, ['N'] = 1.3f, ['I'] = 0.75f,
    };
    // 맵 바깥 흩뿌림에 쓰는 소품 (높은 것 · 낮은 것)
    static readonly char[] OUT_TALL = { 'T', 'H', 'W', 'Y', 'P', 'A', 'E', 'Z', 'N', 'I', 'V' };
    static readonly char[] OUT_LOW = { 'S', 'C', 'X', 'D', 'B', 'U', 'K', 'F', 'L', 'J' };
    static readonly Color HAZE = new Color(0.71f, 0.68f, 0.58f); // #B5AD95

    Transform groundRoot, propRoot, unitRoot, fxRoot, hiRoot, decoRoot, stainRoot;
    Transform backdrop;
    GroundBaker baker; MeshRenderer groundQuad; int fogVer = -1;
    readonly List<GameObject> hillTops = new List<GameObject>();
    readonly Dictionary<int, GameObject> propGO = new Dictionary<int, GameObject>();
    readonly List<GameObject> hiPool = new List<GameObject>(); int hiUsed;
    LineRenderer pathLine;
    readonly List<GameObject> fireGO = new List<GameObject>();
    MaterialPropertyBlock mpb;
    bool dirty = true;
    public Tile hoverTile;
    WorldMap W => Battle.world;

    void Awake()
    {
        I = this;
        groundRoot = transform.Find("Ground"); propRoot = transform.Find("Props"); unitRoot = transform.Find("Units");
        fxRoot = transform.Find("Fx"); hiRoot = transform.Find("Highlights"); decoRoot = transform.Find("Deco"); stainRoot = transform.Find("Stains");
        backdrop = transform.Find("Backdrop");
        mpb = new MaterialPropertyBlock();
    }

    // ── 그리는 순서 (웹 painter's order: 맵 줄 깊이 순) ──
    // 전장 판때기는 바닥 위에 늘 그려지니(ZTest Always) 앞뒤는 이 값으로만 정함. 뒤(+Z) 일수록 먼저.
    // 카메라 거리로 정렬하면 넓은 판(트럭)이 옆의 앞줄 소품을 덮어 버림 → 줄 깊이로만.
    // 같은 줄: 소품 +0 · 병사 +3 · 효과 +6. 바닥 자국 = −500, 먼 배경 띠 = 음수(−100 근처)
    public static int DepthOrder(float worldZ) => 3000 - Mathf.RoundToInt(worldZ * 20);

    // ── 좌표 ──
    public static Vector3 TilePos(int x, int z) { var w = Battle.world; var t = World.TileAt(w, x, z); return new Vector3(x, (t != null ? t.h : 0) * EL, w.D - 1 - z); }
    static float TileH(int x, int z) { var t = World.TileAt(Battle.world, x, z); return t != null ? t.h : 0; }
    public static Vector3 Foot(float px, float pz)
    {
        int x0 = Mathf.FloorToInt(px), z0 = Mathf.FloorToInt(pz); float a = px - x0, b = pz - z0;
        // 칸 가운데 기준 높이를 섞음 (언덕 오르내림)
        int rx = Mathf.RoundToInt(px), rz = Mathf.RoundToInt(pz);
        float h = (TileH(x0, z0) * (1 - a) + TileH(x0 + 1, z0) * a) * (1 - b) + (TileH(x0, z0 + 1) * (1 - a) + TileH(x0 + 1, z0 + 1) * a) * b;
        if (Mathf.Abs(px - rx) < 0.01f && Mathf.Abs(pz - rz) < 0.01f) h = TileH(rx, rz);
        return new Vector3(px, h * EL, Battle.world.D - 1 - pz);
    }

    // ── 전장 만들기 ──
    public void Build()
    {
        foreach (var r in new[] { groundRoot, propRoot, unitRoot, fxRoot, hiRoot, decoRoot, stainRoot }) Tpl.Clear(r);
        propGO.Clear(); hiPool.Clear(); fireGO.Clear(); fx.Clear(); pools.Clear(); hiUsed = 0; pathLine = null;
        var w = W;
        // 바닥 = 구운 그림 한 장 (맵 + 둘레). 언덕 칸은 상자 + 윗면 판(같은 그림의 그 칸 부분)
        baker = new GroundBaker(w, BACK_ROWS, BACK_ROWS + 4, 8);
        baker.BakeAll();
        groundQuad = Tpl.Spawn<MeshRenderer>("TileTpl", groundRoot);
        groundQuad.name = "BakedGround";
        groundQuad.transform.position = new Vector3(baker.OX + baker.BW / 2f, 0.001f, baker.OZ + baker.BD / 2f);
        groundQuad.transform.localScale = new Vector3(baker.BW, baker.BD, 1);
        SetTex(groundQuad, baker.tex, new Vector4(1, 1, 0, 0), Color.white);
        // 시야 안개 = 칸당 1px 그림(전역 _FowTex). 판때기 셰이더(SpriteFog)가 월드 위치로 읽어 채도 · 밝기를 빼고,
        // 연무 판(FogMist)은 바닥 둘레까지 깔아 맵 바깥도 늘 안개 (웹 규칙)
        baker.BakeFog(Battle.vis); fogVer = -1;
        Shader.SetGlobalTexture("_FowTex", baker.fog);
        Shader.SetGlobalVector("_FowRect", new Vector4(-0.5f, -0.5f, w.W, w.D));
        Shader.SetGlobalFloat("_FowOn", 1);
        Shader.SetGlobalTexture("_MistTex", FogCloud.Get()); // 연무 두 겹은 판때기 셰이더가 바닥 · 물체 모두에 입힘 (웹 drawLivingFog)
        hillTops.Clear();
        foreach (var t in w.tiles)
        {
            if (t.h == 0) continue;
            var p = TilePos(t.x, t.z);
            var hb = Tpl.Spawn("HillTpl", groundRoot);
            hb.transform.position = new Vector3(p.x, t.h * EL / 2, p.z);
            hb.transform.localScale = new Vector3(1, t.h * EL, 1);
            var top = Tpl.Spawn<MeshRenderer>("TileTpl", groundRoot); top.name = "HillTop";
            top.transform.position = p + Vector3.up * 0.002f;
            SetTex(top, baker.tex, TileST(t.x, t.z, baker.BW, baker.BD, baker.OX, baker.OZ), Color.white);
        }
        BuildDeco();
        PlaceBackdrop();
        foreach (var u in Battle.units)
        {
            var uv = Tpl.Spawn<UnitView>("UnitTpl", unitRoot);
            uv.name = u.name;
            uv.Init(u);
        }
        var goal = w.goal;
        CameraRig.I.bounds = new Rect(0, 0, w.W - 1, w.D - 1);
        var first = Battle.units.FirstOrDefault(u => u.side == "rat");
        if (first != null) CameraRig.I.Focus(Foot(first.px, first.pz) + Vector3.forward * 3, true); // 분대보다 조금 앞(적 쪽)을 봄
        dirty = true; lastGround = null;
        RefreshGround();
    }

    public void MarkDirty() => dirty = true;
    void OnDisable() => Shader.SetGlobalFloat("_FowOn", 0); // 사령부 화면 등에선 시야 안개 끔
    void NoFow(Renderer r) { if (!r) return; r.GetPropertyBlock(mpb); mpb.SetFloat("_FowAmt", 0); r.SetPropertyBlock(mpb); }

    void SetTex(Renderer r, Texture t, Vector4 st, Color c)
    {
        r.GetPropertyBlock(mpb);
        mpb.SetTexture("_MainTex", t); mpb.SetVector("_MainTex_ST", st); mpb.SetColor("_Color", c);
        r.SetPropertyBlock(mpb);
    }
    // 큰 그림(칸 nw × nd, 왼쪽 아래 월드 = ox, oz) 에서 칸 하나 (x, z) 의 UV 범위
    Vector4 TileST(int x, int z, int nw, int nd, float ox, float oz)
    {
        float wz = W.D - 1 - z;
        return new Vector4(1f / nw, 1f / nd, (x - 0.5f - ox) / nw, (wz - 0.5f - oz) / nd);
    }

    // 바닥 다시 굽기 · 소품 다시 맞춤 (폭발 · 갉기 후). 바뀐 칸만 다시 구움
    string[] lastGround;
    void RefreshGround()
    {
        dirty = false;
        var w = W;
        if (lastGround == null || lastGround.Length != w.tiles.Length) lastGround = w.tiles.Select(t => t.ground).ToArray();
        foreach (var t in w.tiles)
        {
            int i = t.z * w.W + t.x;
            if (lastGround[i] != t.ground) { lastGround[i] = t.ground; baker.Rebake(t.x, t.z); }
            // 소품
            bool want = t.HasProp; float pw = want ? PROP_W[t.prop] : 0;
            if (want && t.prop == 'R')
            {
                // 트럭 잔해는 가로로 이어진 칸들에 한 장: 이어진 줄의 가운데 칸에만, 폭 = 줄 길이
                int x0 = t.x, x1 = t.x;
                while (World.TileAt(w, x0 - 1, t.z)?.prop == 'R') x0--;
                while (World.TileAt(w, x1 + 1, t.z)?.prop == 'R') x1++;
                int len = x1 - x0 + 1;
                want = t.x == x0 + len / 2;
                pw = len >= 3 ? 3.0f : 1.6f;
            }
            if (want && !propGO.ContainsKey(i)) propGO[i] = PropBoard(t.prop, TilePos(t.x, t.z), pw, false, 0);
            if (!want && propGO.TryGetValue(i, out var go)) { Destroy(go); propGO.Remove(i); }
        }
    }

    // 세운 판때기 하나 (소품 · 경치). widthTiles = 칸 가로 배수
    GameObject PropBoard(char prop, Vector3 foot, float widthTiles, bool flip, float scaleJ, string deco = null, Transform parent = null)
    {
        var a = deco != null ? ArtLib.Deco(deco) : ArtLib.Asset("props", prop.ToString());
        if (a == null || a.sprite == null) return null;
        var root = Tpl.Spawn("RigJoint", parent ? parent : propRoot);
        root.name = deco ?? prop.ToString();
        root.transform.position = foot;
        var sr = Tpl.Spawn<SpriteRenderer>("SpriteTpl", root.transform);
        sr.sprite = a.sprite;
        sr.sortingOrder = DepthOrder(foot.z);
        float k = widthTiles * (1 + scaleJ) / a.w; // 월드 단위 / 원본 px
        var sz = a.sprite.bounds.size;
        sr.transform.localScale = new Vector3(a.w * k / sz.x * (flip ? -1 : 1), a.h * k / sz.y, 1);
        sr.transform.localPosition = new Vector3((a.w / 2 - a.px) * k * (flip ? -1 : 1), (a.py - a.h / 2) * k, 0);
        return root;
    }

    // ── 맵 바깥 경치 (웹 buildDeco: 맵 글자로 씨앗 → 임무마다 같은 모양) ──
    const int BACK_ROWS = 14;
    void BuildDeco()
    {
        var w = W;
        string key = string.Concat(w.tiles.Select(t => t.HasProp ? t.prop : t.ground[0]));
        long seed = 0;
        foreach (var ch in key) seed = (seed * 31 + ch) % 2147483647;
        if (seed == 0) seed = 7;
        float rr() { seed = seed * 16807 % 2147483647; return seed / 2147483647f; }
        var bigNames = ArtLib.DecoNames().ToList();
        var bigs = new List<Vector2>();
        for (int z = -BACK_ROWS; z < w.D + BACK_ROWS; z++)
            for (int x = -BACK_ROWS; x < w.W + BACK_ROWS; x++)
            {
                if (x >= 0 && z >= 0 && x < w.W && z < w.D) continue;
                int edge = Mathf.Min(Mathf.Abs(x < 0 ? x : x - w.W + 1), Mathf.Abs(z < 0 ? z : z - w.D + 1));
                if (rr() > 0.15f + Mathf.Min(0.1f, edge * 0.015f)) continue;
                float kind = rr();
                // 앞(카메라 쪽) 바깥 줄은 시야를 가리니 높은 것 빼고 낮은 것만
                bool front = z >= w.D;
                if (edge >= 2 && !front && rr() < 0.42f && !bigs.Any(o => Vector2.Distance(o, new Vector2(x, z)) < 4.5f))
                {
                    string nm = bigNames[Mathf.FloorToInt(rr() * bigNames.Count)];
                    float jx = (rr() - 0.5f) * 0.6f, jz = (rr() - 0.5f) * 0.4f, s = 0.85f + rr() * 0.3f; bool fl = rr() < 0.5f;
                    bigs.Add(new Vector2(x, z));
                    PropBoard('\0', new Vector3(x + jx, 0, w.D - 1 - z - jz), ArtLib.Deco(nm).tiles, fl, s - 1, nm, decoRoot);
                    continue;
                }
                // 죽은 나무 30% · 높은 소품 25% · 낮은 소품 33% · 빈 구덩이 12% (소품 시트 A 까지 섞음)
                float pick = rr();
                char prop = kind < 0.3f ? 'T' : kind < 0.55f ? OUT_TALL[Mathf.FloorToInt(pick * OUT_TALL.Length)] : kind < 0.88f ? OUT_LOW[Mathf.FloorToInt(pick * OUT_LOW.Length)] : 'o';
                float jx2 = (rr() - 0.5f) * 0.6f, jz2 = (rr() - 0.5f) * 0.5f, s2 = 0.85f + rr() * 0.35f; bool fl2 = rr() < 0.5f;
                if (prop == 'o' || (front && System.Array.IndexOf(OUT_TALL, prop) >= 0)) continue;
                PropBoard(prop, new Vector3(x + jx2, 0, w.D - 1 - z - jz2), PROP_W[prop] == 3 ? 1.6f : PROP_W[prop], fl2, s2 - 1, null, decoRoot);
            }
    }

    // ── 배경 층 (웹: 먼 도시 → 먼 언덕 → 먼 들판 → 가까운 들판, 그 위로 하늘 · 폭격기 · 가까운 구름) ──
    // 웹은 층마다 화면 위치 · 흐름 속도를 손으로 맞췄지만, 여기선 진짜 거리에 세워서 시차가 저절로 생김.
    // 크기는 "각 층 윗부분이 앞 층 위로 조금씩 삐져나오게" 고름 → 아래쪽은 앞 층 · 바닥 뒤에 숨어 이어 붙은 자국이 없음.
    // 공기 원근은 URP 안개 하나로 (멀수록 #B5AD95 로 흐려짐). 하늘 · 구름은 안개 밖(_NoFog).
    // 층 하나가 화면 가로를 다 못 덮으면 하이라키의 원본 판을 옆으로 복사해 이어 붙임
    struct Layer { public string name; public float z, h, bottom; }
    readonly List<GameObject> layerCopies = new List<GameObject>();
    readonly List<Transform> clouds = new List<Transform>();
    float cloudW, backZ;
    void PlaceBackdrop()
    {
        if (!backdrop) return;
        foreach (var g in layerCopies) if (g) Destroy(g);
        layerCopies.Clear(); clouds.Clear();
        var w = W;
        float cx = (w.W - 1) / 2f;
        backZ = w.D - 1 + BACK_ROWS; // 맵 뒤 경치 끝
        var layers = new[]
        {
            // 아래 30% 쯤은 땅(또는 앞 층) 밑으로 묻음 — 그림 아래 띠가 바닥과 맞닿는 선이 안 보이게 (웹 hide)
            new Layer { name = "Field", z = backZ + 8, h = 3.4f, bottom = -1.1f },
            new Layer { name = "Hills", z = backZ + 26, h = 6f, bottom = -1.8f },
            new Layer { name = "Far", z = backZ + 55, h = 11f, bottom = -3f },
            new Layer { name = "Clouds", z = backZ + 150, h = 105f, bottom = 7f },
            new Layer { name = "Sky", z = backZ + 200, h = 150f, bottom = -8f },
        };
        foreach (var L in layers)
        {
            var src = backdrop.Find(L.name); if (!src) continue;
            var sr = src.GetComponent<SpriteRenderer>(); if (!sr || !sr.sprite) continue;
            var sz = sr.sprite.bounds.size;
            float s = L.h / sz.y, bw = sz.x * s;
            // 가장 멀리 뺀 카메라(맵 앞 40) 에서 가로 66° 를 덮을 만큼
            float cover = 2 * (L.z + 40) * 0.66f + 30;
            int n = Mathf.Max(1, Mathf.CeilToInt(cover / bw)); if (n % 2 == 0) n++;
            for (int i = 0; i < n; i++)
            {
                var t = i == 0 ? src : Instantiate(src.gameObject, backdrop).transform;
                if (i > 0) { t.name = L.name + "_" + i; layerCopies.Add(t.gameObject); }
                int k = (i + 1) / 2 * (i % 2 == 1 ? 1 : -1); // 0, +1, −1, +2, −2 …
                t.localScale = new Vector3(s * (k % 2 != 0 ? -1 : 1) * 1.0005f, s, 1); // 이웃끼리 좌우 뒤집어 이음매 무늬가 맞물리게
                t.position = new Vector3(cx + k * bw, L.bottom + L.h / 2, L.z);
                NoFow(t.GetComponent<SpriteRenderer>()); // 먼 후경은 시야 안개 대신 공기 원근만
                if (L.name == "Clouds") { clouds.Add(t); cloudW = bw; }
            }
        }
        // 양옆 날개 층: 맵 옆 바깥에 들판 · 언덕 판을 앞에서 뒤까지 깊이마다 엇갈려 세움 (연극 무대 날개처럼)
        // 전용 날개 그림(wing_near · mid · far, ChatGPT)을 깊이에 따라: 앞 = 진하고 높은 것, 뒤로 갈수록 옅고 낮은 것 → 뒤 후경으로 이어짐
        // 멀수록 안개에 녹음. 소품과 같은 깊이 정렬(sortingOrder 0)이라 앞뒤가 맞음. 원본 판은 꺼 두고 복사본만 켬
        var nearSrc = backdrop.Find("WingNear"); var midSrc = backdrop.Find("WingMid"); var farSrc = backdrop.Find("WingFar");
        var smokeW = ArtLib.Fx("smoke");
        int wi = 0;
        if (nearSrc && midSrc && farSrc)
            foreach (int side in new[] { -1, 1 })
                for (float z = -8; z < backZ + 4; z += 6.5f, wi++)
                {
                    int tier = z < w.D * 0.45f ? 0 : z < backZ - 8 ? 1 : 2; // 0 앞 · 1 가운데 · 2 뒤
                    var src = tier == 0 ? nearSrc : tier == 1 ? midSrc : farSrc;
                    var sz = src.GetComponent<SpriteRenderer>().sprite.bounds.size;
                    float h = (tier == 0 ? 3.6f : tier == 1 ? 3.0f : 2.4f) * (0.85f + Mathf.PerlinNoise(wi * 0.37f, side) * 0.35f), s = h / sz.y, bw = sz.x * s;
                    // 맵 가장자리에서 경치 띠(14칸) 바깥쯤부터, 뒤로 갈수록 조금 더 바깥으로 (원근으로 벌어지는 느낌)
                    float edge = (w.W - 1) / 2f + 9 + (z + 8) * 0.18f + Mathf.PerlinNoise(wi * 0.71f, 3) * 5;
                    var t = Instantiate(src.gameObject, backdrop).transform;
                    t.name = "Wing_" + (side < 0 ? "L" : "R") + wi; layerCopies.Add(t.gameObject);
                    var sr = t.GetComponent<SpriteRenderer>(); sr.sortingOrder = DepthOrder(z); sr.enabled = true;
                    t.localScale = new Vector3(s * (wi % 2 == 0 ? 1 : -1), s, 1);
                    t.position = new Vector3(cx + side * (edge + bw / 2), -h * 0.32f + h / 2, z);
                    // 밑동 연기: 판 아래 띠가 바닥과 만나는 선을 덮음
                    if (smokeW.Length > 0)
                        for (int k = 0; k < 3; k++)
                        {
                            var p = Tpl.Spawn<SpriteRenderer>("SpriteTpl", backdrop);
                            p.name = "WingHaze"; p.sprite = smokeW[(wi + k) % smokeW.Length]; p.sortingOrder = DepthOrder(z - 0.3f) + 1;
                            float m = Mathf.Max(p.sprite.bounds.size.x, p.sprite.bounds.size.y), ps = bw * 0.55f / m;
                            p.transform.localScale = new Vector3(ps, ps * 0.32f, 1);
                            p.transform.position = new Vector3(cx + side * (edge + bw * (0.2f + k * 0.3f)), 0.15f, z - 0.3f);
                            p.color = new Color(HAZE.r, HAZE.g, HAZE.b, 0.5f);
                            layerCopies.Add(p.gameObject);
                        }
                }
        // 바깥 땅: 지평선까지. 무늬는 구운 바닥과 같은 월드 기준(3칸 = 한 장)으로 맞춰 이음매 없음
        var outer = backdrop.Find("OuterGround");
        if (outer)
        {
            float sx = 900, sz = 520, oz = backZ + 150;
            outer.position = new Vector3(cx, -0.01f, oz);
            outer.localScale = new Vector3(sx, sz, 1);
            var r = outer.GetComponent<MeshRenderer>();
            var tex = ArtLib.Asset("terrain", "grass")?.sprite?.texture;
            float tw = GroundBaker.TEX_TILES;
            SetTex(r, tex, new Vector4(sx / tw, sz / tw, ((cx - sx / 2) / tw) % 1, ((oz - sz / 2) / tw) % 1), Color.white);
        }
        bomberT = bomberDur; // 첫 편대는 조금 기다렸다가
        // 지평선 연기 띠 (웹 smokeBand · drawLivingFog): 층이 맞닿는 선 위에 안개색 연기 덩어리를 깔아 녹임
        foreach (var p in hazePuffs) if (p.sr) Destroy(p.sr.gameObject);
        hazePuffs.Clear();
        var smoke = ArtLib.Fx("smoke");
        if (smoke.Length > 0)
        {
            // (깊이, 높이, 크기, 정렬, 진하기): 바닥 ↔ 들판 이음, 언덕 ↔ 먼 마을 사이, 먼 마을 ↔ 하늘 이음
            var bands = new[] { (backZ + 6.5f, 0.35f, 7f, -65, 0.55f), (backZ + 40f, 2.5f, 18f, -85, 0.5f), (backZ + 58f, 7.5f, 30f, -92, 0.6f) };
            foreach (var (bz, by, size, order, alpha) in bands)
            {
                float cover = 2 * (bz + 40) * 0.66f + 40, step = size * 0.45f;
                for (float x = cx - cover / 2; x < cx + cover / 2; x += step)
                {
                    var sr = Tpl.Spawn<SpriteRenderer>("SpriteTpl", backdrop);
                    sr.name = "Haze"; sr.sprite = smoke[Random.Range(0, smoke.Length)]; sr.sortingOrder = order;
                    float m = Mathf.Max(sr.sprite.bounds.size.x, sr.sprite.bounds.size.y), s = size * (0.8f + Random.value * 0.5f) / m;
                    sr.transform.localScale = new Vector3(s * (Random.value < 0.5f ? -1 : 1), s * 0.45f, 1);
                    sr.transform.position = new Vector3(x + Random.value * step, by + Random.value * size * 0.06f, bz + Random.value * 2);
                    sr.color = new Color(HAZE.r, HAZE.g, HAZE.b, alpha * (0.7f + Random.value * 0.3f));
                    NoFow(sr);
                    hazePuffs.Add((sr, 0.25f + Random.value * 0.3f, cover));
                }
            }
        }
    }
    readonly List<(SpriteRenderer sr, float speed, float cover)> hazePuffs = new List<(SpriteRenderer, float, float)>();

    // 하늘: 구름은 바람에 흐르고, 가끔 폭격기 편대가 지나감 (웹 drawBomber)
    float bomberT, bomberDur = 26, bomberY; int bomberDir = 1;
    readonly List<Transform> bombers = new List<Transform>();
    void UpdateSky(float dt)
    {
        float mid = (W.W - 1) / 2f;
        foreach (var (sr, speed, cover) in hazePuffs)
        {
            if (!sr) continue;
            var p = sr.transform.position; p.x += dt * speed;
            if (p.x > mid + cover / 2) p.x -= cover;
            sr.transform.position = p;
        }
        if (cloudW > 0)
            foreach (var c in clouds)
            {
                var p = c.position; p.x += dt * 0.6f;
                float cx = (W.W - 1) / 2f, half = cloudW * clouds.Count / 2f;
                if (p.x > cx + half) p.x -= cloudW * clouds.Count;
                c.position = p;
            }
        var src = backdrop ? backdrop.Find("Bomber") : null;
        if (!src) return;
        bomberT += dt;
        if (bomberT > bomberDur)
        {
            foreach (var b in bombers) if (b) Destroy(b.gameObject);
            bombers.Clear();
            bomberT = -20 - Random.value * 25; bomberY = 30 + Random.value * 25; bomberDir = Random.value < 0.5f ? 1 : -1;
            int n = 2 + Random.Range(0, 3);
            for (int i = 0; i < n; i++)
            {
                var b = Instantiate(src.gameObject, backdrop).transform;
                b.name = "Bomber_" + i; b.GetComponent<SpriteRenderer>().enabled = true;
                bombers.Add(b);
            }
        }
        if (bomberT < 0 || bombers.Count == 0) return;
        float k = bomberT / bomberDur, z = backZ + 130, span = 340, cxm = (W.W - 1) / 2f;
        for (int i = 0; i < bombers.Count; i++)
        {
            float x = cxm + bomberDir * (-span / 2 + span * k) - bomberDir * i * 5.5f;
            bombers[i].position = new Vector3(x, bomberY + (i % 2 == 1 ? 1.6f : 0) + i * 0.4f, z + i * 2);
            var sz = bombers[i].GetComponent<SpriteRenderer>().sprite.bounds.size;
            float s = 6f / sz.x;
            bombers[i].localScale = new Vector3(s * bomberDir, s, 1);
        }
    }

    // ── 매 프레임 ──
    void Update()
    {
        if (W == null || baker == null) return;
        if (dirty) RefreshGround();
        var w = W;
        // 시야 안개: 안 보이는 칸 = 어둡게 + 연무 (칸당 1px 그림 다시 굽기)
        if (fogVer != Battle.version) { fogVer = Battle.version; baker.BakeFog(Battle.vis); }
        UpdateSky(Time.deltaTime);
        // 고양이: 보일 때만 (시체는 본 적 있으면)
        foreach (var u in Battle.units)
            if (u.view) u.view.gameObject.SetActive(u.side == "rat" || u.controlled > 0 || u.visible || (u.corpse && u.seen) || (u.downed && u.seen));
        UpdateHighlights();
        UpdateFx(Time.deltaTime);
        UpdateFires();
    }

    // ── 이동 범위 · 대상 표시 ──
    GameObject Hi(Vector3 p, Color c, float size = 0.94f)
    {
        GameObject g;
        if (hiUsed < hiPool.Count) g = hiPool[hiUsed]; else { g = Tpl.Spawn("HighlightTpl", hiRoot); hiPool.Add(g); }
        hiUsed++;
        g.SetActive(true);
        g.transform.position = p + Vector3.up * 0.03f;
        g.transform.localScale = new Vector3(size, size, 1);
        var mr = g.GetComponent<MeshRenderer>();
        mr.GetPropertyBlock(mpb); mpb.SetColor("_Color", c); mr.SetPropertyBlock(mpb);
        return g;
    }

    static readonly Color C_MOVE = new Color(0.56f, 0.82f, 1f, 0.17f), C_DASH = new Color(1f, 0.81f, 0.29f, 0.12f), C_HOVER = new Color(1, 1, 1, 0.4f);
    static readonly Color C_TILE = new Color(1f, 0.55f, 0.3f, 0.3f), C_AOE = new Color(1f, 0.35f, 0.25f, 0.45f), C_GOAL = new Color(1f, 0.81f, 0.29f, 0.5f), C_TGT = new Color(1f, 0.42f, 0.36f, 0.6f);
    World.Reach cachedReach; int reachVer = -1; Unit reachUnit;

    void UpdateHighlights()
    {
        hiUsed = 0;
        var w = W; var u = Battle.sel;
        if (w.goal != null) foreach (var t in w.tiles) if (World.Dist(t.x, t.z, w.goal.x, w.goal.z) <= w.goal.r) Hi(TilePos(t.x, t.z), C_GOAL);
        bool myTurn = Battle.turn == "rat" && Battle.over == null && u != null;
        List<Pt> path = null;
        if (myTurn && Battle.mode == "move" && u.ap > 0)
        {
            if (reachVer != Battle.version || reachUnit != u) { cachedReach = World.ReachOf(w, u, u.mob * u.ap); reachVer = Battle.version; reachUnit = u; }
            foreach (var kv in cachedReach.Stops)
            {
                if (kv.Value == 0) continue;
                int x = kv.Key % w.W, z = kv.Key / w.W;
                Hi(TilePos(x, z), kv.Value <= u.mob ? C_MOVE : C_DASH);
            }
            if (hoverTile != null && cachedReach.Cost(hoverTile.x, hoverTile.z) is float hc && hc > 0) path = cachedReach.Path(hoverTile.x, hoverTile.z);
        }
        if (myTurn && Battle.mode == "tile" && Battle.ability != null)
        {
            var A = GameData.ABILITIES[Battle.ability];
            foreach (var t in w.tiles) if (Battle.TileTargetOk(u, Battle.ability, t)) Hi(TilePos(t.x, t.z), C_TILE);
            if (hoverTile != null && Battle.TileTargetOk(u, Battle.ability, hoverTile) && A.radius > 0)
                foreach (var t in w.tiles) if (World.Dist(t.x, t.z, hoverTile.x, hoverTile.z) <= A.radius + u.blast) Hi(TilePos(t.x, t.z), C_AOE);
        }
        if (myTurn && Battle.mode == "target") { var tg = Hud.I ? Hud.I.CurrentTarget : null; if (tg != null) Hi(Foot(tg.px, tg.pz), C_TGT, 1.1f); }
        if (hoverTile != null && Battle.turn == "rat") Hi(TilePos(hoverTile.x, hoverTile.z), C_HOVER, 0.98f);
        if (u != null && Battle.turn == "rat") Hi(Foot(u.px, u.pz), new Color(1f, 0.81f, 0.29f, 0.55f), 0.7f);
        for (int i = hiUsed; i < hiPool.Count; i++) hiPool[i].SetActive(false);
        // 경로 선
        if (pathLine == null) pathLine = Tpl.Spawn<LineRenderer>("LineTpl", hiRoot);
        if (path != null && path.Count > 0)
        {
            pathLine.gameObject.SetActive(true);
            pathLine.positionCount = path.Count + 1;
            pathLine.SetPosition(0, Foot(u.px, u.pz) + Vector3.up * 0.06f);
            for (int i = 0; i < path.Count; i++) pathLine.SetPosition(i + 1, TilePos(path[i].x, path[i].z) + Vector3.up * 0.06f);
            pathLine.startColor = pathLine.endColor = new Color(0.56f, 0.82f, 1f, 0.9f);
        }
        else pathLine.gameObject.SetActive(false);
    }

    // ── 칸 · 유닛 고르기 ──
    public Tile PickTile(Vector2 screen)
    {
        var w = W; var cr = CameraRig.I;
        // 언덕 윗면 먼저
        var top = cr.GroundAt(screen, EL);
        var t1 = World.TileAt(w, Mathf.RoundToInt(top.x), w.D - 1 - Mathf.RoundToInt(top.z));
        if (t1 != null && t1.h > 0) return t1;
        var g = cr.GroundAt(screen, 0);
        return World.TileAt(w, Mathf.RoundToInt(g.x), w.D - 1 - Mathf.RoundToInt(g.z));
    }
    public Unit UnitAtScreen(Vector2 screen)
    {
        var cam = CameraRig.I.cam; Unit best = null; float bd = 1e9f;
        foreach (var u in Battle.units)
        {
            if (!u.view || !u.view.gameObject.activeSelf || (!u.alive && !u.downed)) continue;
            var f = cam.WorldToScreenPoint(u.view.transform.position);
            var tp = cam.WorldToScreenPoint(u.view.Top);
            if (f.z < 0) continue;
            float hw = Mathf.Abs(tp.y - f.y) * 0.32f;
            if (Mathf.Abs(screen.x - f.x) < hw && screen.y >= f.y - 6 && screen.y <= tp.y && f.z < bd) { best = u; bd = f.z; }
        }
        return best;
    }

    // ── 효과 (판때기 스프라이트 + 간단한 물리) ──
    class Fx
    {
        public SpriteRenderer sr; public Sprite[] frames; public float life, age, grav, size0, size1, groundY = -99, spin;
        public Vector3 vel; public Color col; public bool flat, stain, fadeOut = true; public LineRenderer line;
        public bool gib, rest; // 떨어져 나간 부위: 튕기다 멈춰 바닥에 남음 (웹 gibs)
    }
    // 커지는 피 웅덩이 (웹 stains pool: grow)
    class Pool { public SpriteRenderer sr; public float size, max; }
    readonly List<Pool> pools = new List<Pool>();
    static readonly Color BLOOD_D = new Color(0.478f, 0.063f, 0.094f), BLOOD_L = new Color(0.604f, 0.11f, 0.141f), POOL_D = new Color(0.416f, 0.055f, 0.086f);
    readonly List<Fx> fx = new List<Fx>();

    Fx Spawn(Sprite[] frames, Vector3 p, float size, float life, Color col, Transform parent = null)
    {
        if (frames == null || frames.Length == 0 || frames[0] == null) return null;
        var sr = Tpl.Spawn<SpriteRenderer>("SpriteTpl", parent ? parent : fxRoot);
        sr.transform.position = p;
        sr.sprite = frames[0]; sr.color = col; sr.sortingOrder = DepthOrder(p.z) + 6;
        var f = new Fx { sr = sr, frames = frames, life = life, size0 = size, size1 = size, col = col };
        SetSize(f, size);
        fx.Add(f);
        return f;
    }
    static void SetSize(Fx f, float size)
    {
        var s = f.sr.sprite; if (!s) return;
        float m = Mathf.Max(s.bounds.size.x, s.bounds.size.y);
        float k = size / m;
        f.sr.transform.localScale = new Vector3(k, k, k);
    }

    void UpdateFx(float dt)
    {
        for (int i = fx.Count - 1; i >= 0; i--)
        {
            var f = fx[i];
            f.age += dt;
            float k = Mathf.Clamp01(f.age / f.life);
            if (f.line)
            {
                var c = f.col; c.a *= 1 - k; f.line.startColor = f.line.endColor = c;
                if (f.age >= f.life) { Destroy(f.line.gameObject); fx.RemoveAt(i); }
                continue;
            }
            if (!f.sr) { fx.RemoveAt(i); continue; }
            if (f.gib)
            {
                if (f.rest) continue;
                f.vel.y -= f.grav * dt;
                var tr = f.sr.transform;
                tr.position += f.vel * dt; tr.Rotate(0, 0, f.spin * dt);
                // 날아가며 피를 흘림
                if (Random.value < 0.5f) { var d = Spawn(FX("drop"), tr.position, 0.04f + Random.value * 0.04f, 1, BLOOD_D); if (d != null) { d.vel = new Vector3((Random.value - 0.5f) * 0.4f, 0, 0); d.grav = 12; d.groundY = f.groundY - 0.04f; d.stain = true; d.fadeOut = false; } }
                if (tr.position.y <= f.groundY)
                {
                    tr.position = new Vector3(tr.position.x, f.groundY, tr.position.z);
                    f.vel.y *= -0.32f; f.vel.x *= 0.55f; f.vel.z *= 0.55f; f.spin *= 0.5f;
                    if (Mathf.Abs(f.vel.y) < 0.6f)
                    {
                        f.rest = true;
                        f.sr.sortingOrder = DepthOrder(tr.position.z) - 2; // 같은 줄 시체 · 소품 밑
                        AddPool(tr.position, 0.55f);
                    }
                }
                continue;
            }
            if (f.vel != Vector3.zero || f.grav != 0)
            {
                f.vel.y -= f.grav * dt;
                f.sr.transform.position += f.vel * dt;
                if (f.spin != 0) f.sr.transform.Rotate(0, 0, f.spin * dt);
                if (f.sr.transform.position.y <= f.groundY)
                {
                    // 땅에 닿음: 피 · 조각 → 바닥 자국
                    if (f.stain) Stain(f.sr.transform.position, f.frames, f.size0 * 2.2f, f.col);
                    Destroy(f.sr.gameObject); fx.RemoveAt(i); continue;
                }
            }
            if (f.frames.Length > 1) f.sr.sprite = f.frames[Mathf.Min(f.frames.Length - 1, (int)(k * f.frames.Length))];
            if (f.size1 != f.size0) SetSize(f, Mathf.Lerp(f.size0, f.size1, k));
            if (f.fadeOut) { var c = f.col; c.a *= 1 - k * k; f.sr.color = c; }
            if (f.age >= f.life) { Destroy(f.sr.gameObject); fx.RemoveAt(i); }
        }
        for (int i = pools.Count - 1; i >= 0; i--)
        {
            var p = pools[i];
            if (!p.sr) { pools.RemoveAt(i); continue; }
            p.size = Mathf.Min(p.max, p.size + dt * 0.35f);
            p.sr.transform.localScale = PoolScale(p.sr.sprite, p.size);
            if (p.size >= p.max) pools.RemoveAt(i);
        }
    }

    // 바닥에 눕힌 그림 크기: 낮은 시점이면 바닥 그림이 납작하게 눌려 거의 안 보임 → 깊이 쪽(세로)을 늘려 웹처럼 2:1 타원으로 보이게
    static Vector3 PoolScale(Sprite s, float size)
    {
        float m = Mathf.Max(s.bounds.size.x, s.bounds.size.y);
        return new Vector3(size / m, size / m * 1.6f, 1);
    }
    // 커지는 피 웅덩이: 짙은 바탕 + 밝은 속 (웹 drawStain pool)
    void AddPool(Vector3 p, float max)
    {
        var a = Stain(p, FX("blood"), 0.1f, POOL_D, 0.95f);
        var b = Stain(p + new Vector3(-max * 0.12f, 0.0004f, 0.02f), FX("blood"), 0.06f, BLOOD_L, 0.8f);
        if (a) pools.Add(new Pool { sr = a, size = 0.1f, max = max });
        if (b) pools.Add(new Pool { sr = b, size = 0.06f, max = max * 0.55f });
    }

    // 바닥 자국 (핏자국 · 그을음): 바닥에 눕힌 납작 그림
    int stainCount;
    SpriteRenderer Stain(Vector3 p, Sprite[] frames, float size, Color col, float maxA = 0.85f)
    {
        if (frames == null || frames.Length == 0 || !frames[0]) return null;
        var sr = Tpl.Spawn<SpriteRenderer>("SpriteTpl", stainRoot);
        sr.sprite = frames[Random.Range(0, frames.Length)];
        var hgt = TileH(Mathf.RoundToInt(p.x), W.D - 1 - Mathf.RoundToInt(p.z)) * EL;
        sr.transform.position = new Vector3(p.x, hgt + 0.012f + (stainCount++ % 50) * 0.0002f, p.z);
        // 눕히고 살짝만 돌림 (많이 돌리면 깊이 쪽 늘림이 옆으로 가 버림)
        sr.transform.rotation = Quaternion.Euler(90, 0, 0) * Quaternion.Euler(0, 0, Random.Range(-30f, 30f));
        sr.flipX = Random.value < 0.5f;
        sr.transform.localScale = PoolScale(sr.sprite, size);
        col.a = Mathf.Min(col.a, maxA);
        sr.color = col; sr.sortingOrder = -500; // 바닥 자국: 모든 판때기 밑
        if (stainRoot.childCount > 700) Destroy(stainRoot.GetChild(0).gameObject);
        return sr;
    }

    static Sprite[] FX(string n) => ArtLib.Fx(n);
    static Color Col(string hex) => SkillsData.Hex(hex);

    public void Muzzle(Unit a, Unit b, bool hit, bool crit, string weapon)
    {
        if (!a.view || !b.view) return;
        var m = a.view.Muzzle; var c = b.view.Chest;
        bool big = weapon == "sniper" || weapon == "shotgun";
        var f = Spawn(FX("flash"), m, big ? 0.7f : 0.45f, big ? 0.12f : 0.08f, Color.white); if (f != null) f.fadeOut = false;
        var miss = hit ? Vector3.zero : new Vector3((Random.value - 0.5f) * 1.2f, (Random.value - 0.3f) * 0.8f, 0);
        var line = Tpl.Spawn<LineRenderer>("LineTpl", fxRoot);
        line.positionCount = 2; line.SetPosition(0, m); line.SetPosition(1, c + miss);
        line.widthMultiplier = 0.035f;
        fx.Add(new Fx { line = line, life = 0.12f, col = new Color(1f, 0.93f, 0.7f, 0.9f) });
        if (hit) { var s = Spawn(FX("spark"), c, crit ? 0.9f : 0.5f, 0.2f, Color.white); }
        for (int i = 0; i < (big ? 4 : 2); i++)
        {
            var sm = Spawn(FX("smoke"), m, 0.25f, 0.9f + Random.value * 0.5f, new Color(0.85f, 0.82f, 0.75f, 0.5f));
            if (sm != null) { sm.vel = new Vector3((Random.value - 0.5f) * 0.4f, 0.3f + Random.value * 0.3f, 0); sm.size1 = 0.6f; }
        }
        if (!hit) Dust(c + miss + Vector3.down * 0.5f);
        float kick = weapon == "sniper" ? 6 : weapon == "shotgun" ? 5 : 3;
        var dir = (c - m).normalized;
        CameraRig.I.Kick(new Vector2(-dir.x, -dir.y) * kick * 0.6f);
        CameraRig.I.Shake(kick * (crit ? 1.6f : 0.7f));
    }
    void Dust(Vector3 p) { for (int i = 0; i < 3; i++) { var d = Spawn(FX("smoke"), p, 0.3f, 0.5f, new Color(0.6f, 0.52f, 0.4f, 0.6f)); if (d != null) d.vel = new Vector3((Random.value - 0.5f) * 0.8f, 0.6f, 0); } }

    public void Blood(Unit u, int n)
    {
        if (!u.view) return;
        var c = u.view.Chest; float gy = u.view.transform.position.y;
        // 웹: 검붉은 두 톤 방울이 사방으로 튀고, 떨어진 자리마다 바닥 방울 자국 + 맞은 자리 튐 그림
        for (int i = 0; i < n * 2; i++) Drop(c + Vector3.up * (Random.value - 0.5f) * 0.3f, new Vector3((Random.value - 0.5f) * 3.4f, 1.0f + Random.value * 2.8f, (Random.value - 0.5f) * 1.4f), gy);
        var sp = Spawn(FX("blood"), c, 0.35f + n * 0.02f, 0.3f, BLOOD_L); if (sp != null) sp.size1 = sp.size0 * 1.6f;
        Stain(new Vector3(c.x + (Random.value - 0.5f) * 0.4f, gy, c.z + (Random.value - 0.5f) * 0.3f), FX("blood"), 0.25f + n * 0.025f, BLOOD_D);
    }
    void Drop(Vector3 p, Vector3 v, float gy, float size = 0)
    {
        var b = Spawn(FX("drop"), p, size > 0 ? size : 0.05f + Random.value * 0.08f, 3, Random.value < 0.6f ? BLOOD_D : BLOOD_L);
        if (b == null) return;
        b.vel = v; b.grav = 12; b.groundY = gy + (Random.value - 0.3f) * 0.12f + 0.01f; b.stain = true; b.fadeOut = false;
        b.sr.transform.rotation = Quaternion.Euler(0, 0, Random.Range(0, 360f));
    }

    public void Explosion(int tx, int tz, float radius)
    {
        var p = TilePos(tx, tz);
        var b = Spawn(FX("boom"), p + Vector3.up * radius * 0.5f, radius * 2.0f, 0.5f, Color.white); if (b != null) b.fadeOut = false;
        for (int i = 0; i < 9; i++)
        {
            var s = Spawn(FX("smoke"), p + new Vector3((Random.value - 0.5f) * 1.4f, Random.value * 0.3f, (Random.value - 0.5f) * 0.6f), 0.6f + Random.value * 0.5f, 1.8f + Random.value, new Color(0.35f, 0.32f, 0.28f, 0.85f));
            if (s != null) { s.vel = new Vector3((Random.value - 0.5f) * 0.4f, 0.4f + Random.value * 0.4f, 0); s.size1 = s.size0 * 2.2f; }
        }
        Chunks(p, 12, new[] { Col("#4b3f31"), Col("#6b5a44") }, 1.2f);
        Stain(p, FX("smoke"), radius * 1.5f, new Color(0.1f, 0.08f, 0.06f, 0.8f));
        CameraRig.I.Shake(14);
    }
    void Chunks(Vector3 p, int n, Color[] cols, float power)
    {
        for (int i = 0; i < n; i++)
        {
            var ch = Spawn(FX("smoke"), p + Vector3.up * 0.2f, 0.07f + Random.value * 0.08f, 3, cols[i % cols.Length]);
            if (ch == null) continue;
            ch.vel = new Vector3((Random.value - 0.5f) * 5 * power, (2.5f + Random.value * 3) * power, (Random.value - 0.5f) * 2); ch.grav = 14; ch.groundY = p.y; ch.fadeOut = false; ch.spin = 400;
        }
    }
    public void Debris(int x, int z, char prop) => Chunks(TilePos(x, z), 10, prop == 'S' ? new[] { Col("#a08a5e"), Col("#7f6c48") } : new[] { Col("#7a6440"), Col("#5a4a30") }, 1f);
    public void Dig(int x, int z)
    {
        Chunks(TilePos(x, z), 14, new[] { Col("#5a4630"), Col("#7a6448") }, 0.8f); CameraRig.I.Shake(4);
        Stain(TilePos(x, z), FX("smoke"), 0.9f, new Color(0.12f, 0.09f, 0.06f, 0.95f)); // 땅굴 입구
    }

    public void Slash(Unit a, Unit b) { if (!b.view) return; var s = Spawn(FX("spark"), b.view.Chest, 1.1f, 0.28f, Col("#ffe9b0")); if (s != null) s.size1 = 1.6f; CameraRig.I.Shake(6); }
    public void Ring(Unit u, Color c, float size)
    {
        if (!u.view) return;
        var r = Spawn(FX("shield"), u.view.transform.position + Vector3.up * 0.05f, size * 0.2f, 0.7f, c);
        if (r != null) { r.size1 = size; r.sr.transform.rotation = Quaternion.Euler(90, 0, 0); }
    }

    // 쓰러짐: 1초 동안 리그 넘어짐 + 부위 떨어져 나감
    public async Awaitable Die(Unit u, string gore)
    {
        u.st.walk = null; u.inCover = false;
        if (u.view) u.view.aiming = false;
        // 앞 · 뒤 모습 리그엔 팔 · 정강이 부위가 없어 잘리지 않았음 (특히 고양이) → 쓰러질 땐 늘 옆모습
        u.forceSide = true;
        var parts = gore switch
        {
            "decap" => new[] { "head" }, "sever" => new[] { "arm" }, "burst" => new[] { "head", "arm", "shinN" },
            "arm" => new[] { "arm" }, "leg" => new[] { "shinN" }, _ => new string[0],
        };
        bool cut = false;
        await View.Tween(1000, k =>
        {
            u.st.death = k;
            if (!cut && k > 0.12f && parts.Length > 0) { cut = true; foreach (var p in parts) Detach(u, p, gore == "burst" ? 1.3f : 1); }
        });
        u.st.death = 1;
        // 몸 밑으로 번지는 웅덩이 (잘렸으면 더 크게)
        if (u.view) AddPool(u.view.transform.position + new Vector3(u.view.curFlip ? 0.4f : -0.4f, 0, 0.05f), parts.Length > 0 ? 1.5f : 1.1f);
    }

    void Detach(Unit u, string part, float power)
    {
        if (!u.view) return;
        var (s, pos, scale, rot) = u.view.PartSnapshot(part);
        if (!s) return;
        u.hide.Add(part);
        var sr = Tpl.Spawn<SpriteRenderer>("SpriteTpl", fxRoot);
        sr.sprite = s; sr.sortingOrder = DepthOrder(pos.z) + 4;
        sr.transform.position = pos; sr.transform.rotation = Quaternion.Euler(0, 0, rot); sr.transform.localScale = scale;
        float dir = u.view.curFlip ? 1 : -1;
        // 웹 detach: 튕기다 멈추면 바닥에 남고 웅덩이가 번짐. 날 동안 피를 흘림
        float gy = u.view.transform.position.y + 0.04f + Random.value * 0.05f;
        var f = new Fx { sr = sr, frames = new[] { s }, life = 999, vel = new Vector3(dir * (1.7f + Random.value * 2.2f) * power, (3.6f + Random.value * 3f) * power, (Random.value - 0.5f) * 1.2f), grav = 18, spin = (Random.value - 0.5f) * 800, groundY = gy, fadeOut = false, gib = true };
        f.size0 = f.size1 = 0; // 크기는 그대로
        fx.Add(f);
        // 잘린 자리에서 피가 뿜어짐
        for (int i = 0; i < 26; i++) Drop(pos, new Vector3(dir * Random.value * 2.6f, 1.6f + Random.value * 3f, (Random.value - 0.5f) * 1.2f), u.view.transform.position.y, 0.06f + Random.value * 0.09f);
        _ = Spurt(u, part, dir);
        Blood(u, 10);
        Chunks(pos, 6, new[] { Col("#5a0c12"), Col("#8a2a2a") }, 0.8f);
    }

    // 잘린 자리(목 · 어깨 · 무릎)에서 잠깐 동안 피가 뿜어져 나옴
    async Awaitable Spurt(Unit u, string part, float dir)
    {
        float end = Time.time + 1.4f;
        while (Time.time < end && u.view)
        {
            var c = u.view.Chest;
            for (int i = 0; i < 2; i++) Drop(c, new Vector3(dir * (0.5f + Random.value * 1.4f), 0.8f + Random.value * 1.6f, (Random.value - 0.5f) * 0.5f), u.view.transform.position.y);
            await Awaitable.WaitForSecondsAsync(0.05f);
        }
    }

    public async Awaitable Lob(Unit a, int tx, int tz, float ms)
    {
        await View.Tween(260, k => a.st.throwK = k * 0.5f);
        var m = a.view ? a.view.Muzzle : Foot(a.px, a.pz); var e = TilePos(tx, tz);
        _ = ThrowFinish(a);
        var g = Spawn(new[] { ArtLib.Asset("weapons", "grenades")?.sprite }, m, 0.22f, 99, Color.white); if (g != null) g.fadeOut = false;
        await View.Tween(ms, k => { if (g != null && g.sr) { g.sr.transform.position = Vector3.Lerp(m, e, k) + Vector3.up * Mathf.Sin(k * Mathf.PI) * 2.6f; g.sr.transform.Rotate(0, 0, 12); } });
        if (g != null && g.sr) { Destroy(g.sr.gameObject); fx.Remove(g); }
    }
    async Awaitable ThrowFinish(Unit a) { await View.Tween(260, k => a.st.throwK = 0.5f + k * 0.5f); a.st.throwK = 0; }

    public async Awaitable Shell(Unit a, int tx, int tz, float ms)
    {
        var m = a.view ? a.view.Muzzle : Foot(a.px, a.pz); var e = TilePos(tx, tz);
        Spawn(FX("flash"), m + Vector3.up * 0.3f, 0.6f, 0.1f, Color.white);
        var g = Spawn(FX("smoke"), m, 0.15f, 99, new Color(0.17f, 0.17f, 0.15f)); if (g != null) g.fadeOut = false;
        await View.Tween(ms * 0.4f, k => { if (g?.sr) g.sr.transform.position = m + Vector3.up * k * 9; });
        View.FocusPoint(e);
        await View.Tween(ms * 0.6f, k => { if (g?.sr) g.sr.transform.position = e + Vector3.up * (1 - k) * 11; });
        if (g?.sr) { Destroy(g.sr.gameObject); fx.Remove(g); }
    }

    public async Awaitable Flame(Unit a, int tx, int tz)
    {
        var m = a.view ? a.view.Muzzle : Foot(a.px, a.pz); var e = TilePos(tx, tz) + Vector3.up * 0.3f;
        var fire = FX("fire");
        await View.Tween(650, k =>
        {
            for (int i = 0; i < 2; i++)
            {
                float q = Random.value;
                var f = Spawn(fire, Vector3.Lerp(m, e, q * k * 1.1f) + Vector3.up * (Random.value - 0.5f) * 0.2f, 0.3f + q * 0.6f, 0.45f, Color.white);
                if (f != null) f.vel = new Vector3((Random.value - 0.5f) * 0.5f, 0.5f + Random.value * 0.5f, 0);
            }
            CameraRig.I.Shake(3);
        });
    }

    public async Awaitable Psi(Unit a, Unit b, Color col)
    {
        if (!a.view || !b.view) return;
        var s = a.view.Chest; var e = b.view.Chest;
        Spawn(FX("psi"), s, 0.9f, 0.6f, col);
        var orb = Spawn(FX("psi"), s, 0.4f, 99, col);
        await View.Tween(500, k => { if (orb?.sr) orb.sr.transform.position = Vector3.Lerp(s, e, k) + Vector3.up * Mathf.Sin(k * Mathf.PI) * 0.7f; });
        if (orb?.sr) { Destroy(orb.sr.gameObject); fx.Remove(orb); }
        var big = Spawn(FX("psi"), e, 0.6f, 0.9f, col); if (big != null) big.size1 = 1.6f;
    }

    // 쥐떼 전령: 작은 무리(연기 꼬리)가 이쪽에서 저쪽으로
    public void RatsRun(Unit a, Unit b)
    {
        if (!a.view || !b.view) return;
        var s = a.view.transform.position; var e = b.view.transform.position;
        for (int i = 0; i < 7; i++)
        {
            var r = Spawn(FX("smoke"), s + Vector3.up * 0.08f, 0.14f, 0.9f + i * 0.06f, new Color(0.35f, 0.3f, 0.26f, 0.9f));
            if (r != null) { r.vel = (e - s) / (0.9f + i * 0.06f) + new Vector3(0, 0, (i - 3) * 0.05f); r.fadeOut = false; }
        }
    }

    // 불 난 칸
    public void FiresChanged()
    {
        foreach (var g in fireGO) if (g) Destroy(g);
        fireGO.Clear();
        foreach (var f in Battle.fires.Select(f => (f.x, f.z)).Distinct())
        {
            var sr = Tpl.Spawn<SpriteRenderer>("SpriteTpl", fxRoot);
            sr.transform.position = TilePos(f.x, f.z) + new Vector3((Random.value - 0.5f) * 0.3f, 0.25f, 0);
            sr.sprite = FX("fire")[0]; sr.sortingOrder = DepthOrder(sr.transform.position.z) + 2;
            float m = Mathf.Max(sr.sprite.bounds.size.x, sr.sprite.bounds.size.y);
            sr.transform.localScale = Vector3.one * 0.7f / m;
            fireGO.Add(sr.gameObject);
        }
    }
    void UpdateFires()
    {
        var fr = FX("fire"); if (fr.Length == 0) return;
        for (int i = 0; i < fireGO.Count; i++) if (fireGO[i]) fireGO[i].GetComponent<SpriteRenderer>().sprite = fr[(int)(Time.time * 10 + i) % fr.Length];
    }
}

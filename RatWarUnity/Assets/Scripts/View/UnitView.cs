// 병사 한 명의 판때기: 리그 파츠(스프라이트)를 시점마다(front · side · back) 쌓아 두고 매 프레임 포즈를 입힘
// 웹 rig.js draw 를 옮김. 리그 px 좌표(y 아래로)를 그대로 쓰려고 맨 위 틀을 y 뒤집기(-k)로 둠
//   UnitTpl(이 컴포넌트, 발 밑) → Rig(크기 · 좌우 뒤집기) → 시점 → Pose(루트 이동 · 회전) → Ground(-발 자리) → 관절들
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class UnitView : MonoBehaviour
{
    public const float UNIT_H = 1.64f; // 서 있는 병사 키 (칸 가로 = 1)

    class Joint { public Transform t; public RigPart p; public SpriteRenderer sr; }
    class ViewSet
    {
        public GameObject root; public Transform pose, ground; public RigView V;
        public Dictionary<string, Joint> joints = new Dictionary<string, Joint>();
        public GameObject crouch; public Transform crouchHead; public RigComposite crouchC;
        public GameObject[] throwG; public Transform[] throwHead;
        public float legK = 1;
    }

    public Unit u;
    RigData R, B; // R = 그릴 리그(장비 버전 포함), B = 머리 · 웅크림을 가진 병종 기본 리그
    readonly Dictionary<string, ViewSet> sets = new Dictionary<string, ViewSet>();
    Transform rig; Vector3 rigBase;
    UnityEngine.Rendering.SortingGroup sg;
    float k;
    public bool aiming;
    public string curView = "front"; public bool curFlip;
    readonly List<SpriteRenderer> all = new List<SpriteRenderer>();

    public void Init(Unit unit)
    {
        u = unit; u.view = this;
        bool rat = u.r != null;
        R = ArtLib.RigFor(rat, u.cls, rat ? u.r.gear : 0, u.abil.Contains("flame"));
        B = ArtLib.BaseRig(rat, u.cls);
        k = UNIT_H * (u.def.aura > 0 ? 1.05f : 1) / 470f;
        rig = transform.Find("Rig"); rigBase = rig.localPosition;
        foreach (var vn in new[] { "front", "side", "back" }) if (R.views.ContainsKey(vn)) sets[vn] = Build(vn);
        Update();
    }

    Sprite HeadSprite(RigData r, string vn)
    {
        if (!r.views.TryGetValue(vn, out var V) || !V.parts.TryGetValue("head", out var hd)) return null;
        return u.faceIdx >= 0 && u.faceIdx < hd.faceImgs.Length && hd.faceImgs[u.faceIdx] ? hd.faceImgs[u.faceIdx] : hd.img;
    }

    SpriteRenderer Img(Transform parent, Sprite s, float x, float y, float w, float h, int order)
    {
        var sr = Tpl.Spawn<SpriteRenderer>("SpriteTpl", parent);
        sr.sprite = s;
        sr.sortingOrder = order;
        if (s)
        {
            var sz = s.bounds.size;
            sr.transform.localPosition = new Vector3(x + w / 2, y + h / 2, 0);
            sr.transform.localScale = new Vector3(w / sz.x, -h / sz.y, 1); // 위 틀이 y 를 뒤집었으니 그림은 다시 뒤집음
        }
        all.Add(sr);
        return sr;
    }

    ViewSet Build(string vn)
    {
        var V = R.views[vn];
        var S = new ViewSet { V = V };
        // 다리 길이 (엉덩이 축 → 발끝) 를 쥐 기준(옆 180 · 앞 183px)에 견줌
        if (V.parts.TryGetValue("thighN", out var th) && V.parts.TryGetValue("shinN", out var sh)) S.legK = (sh.y + sh.h - th.pivot.y) / 180f;
        else if (V.parts.TryGetValue("legL", out var lg)) S.legK = (lg.y + lg.h - lg.pivot.y) / 183f;
        S.legK = Mathf.Clamp(S.legK, 0.4f, 1.2f);
        // 긴 외투 고양이 옆모습: 가까운 허벅지가 외투 위에 그려져 걸을 때 바지가 외투를 뚫고 나왔음 → 외투 밑으로
        var order = new List<string>(V.order);
        if (R.kind == "cat" && order.Contains("thighN") && order.Contains("torso")) { order.Remove("thighN"); order.Insert(order.IndexOf("torso"), "thighN"); }
        S.root = Tpl.Spawn("RigJoint", rig); S.root.name = vn;
        S.pose = Tpl.Spawn("RigJoint", S.root.transform).transform; S.pose.name = "Pose";
        S.ground = Tpl.Spawn("RigJoint", S.pose).transform; S.ground.name = "Ground";
        S.ground.localPosition = new Vector3(-V.ground.x, -V.ground.y, 0);
        // 관절: 부모부터
        Joint Make(string n)
        {
            if (S.joints.TryGetValue(n, out var j)) return j;
            var p = V.parts[n];
            Transform parent = S.ground; Vector2 pp = Vector2.zero;
            if (p.parent != null && V.parts.ContainsKey(p.parent)) { var pj = Make(p.parent); parent = pj.t; pp = pj.p.pivot; }
            var t = Tpl.Spawn("RigJoint", parent).transform; t.name = n;
            t.localPosition = new Vector3(p.pivot.x - pp.x, p.pivot.y - pp.y, 0);
            j = new Joint { t = t, p = p };
            var im = n == "head" ? (HeadSprite(R, vn) ?? p.img) : p.img;
            j.sr = Img(t, im, p.x - p.pivot.x, p.y - p.pivot.y, p.w, p.h, order.IndexOf(n));
            if (u.r != null && u.r.prosthetic.Count > 0)
            {
                if (u.r.prosthetic.Contains("arm") && n == "arm") j.sr.color = new Color(0.68f, 0.7f, 0.74f);
                if (u.r.prosthetic.Contains("leg") && (n == "shinN" || n == "legR")) j.sr.color = new Color(0.6f, 0.44f, 0.3f);
            }
            return S.joints[n] = j;
        }
        foreach (var n in V.parts.Keys) Make(n);
        // 앞 · 뒤 웅크림: 웅크린 몸 그림 + 목에 머리 파츠 (병종 것 → 없으면 같은 종 기본 리그 것)
        var CB = B.views.TryGetValue(vn, out var bv) && bv.crouch != null ? B : ArtLib.Rig(R.kind)?.views.TryGetValue(vn, out var kv) == true && kv.crouch != null ? ArtLib.Rig(R.kind) : null;
        if (CB != null)
        {
            var C = CB.views[vn].crouch;
            S.crouchC = C;
            S.crouch = Tpl.Spawn("RigJoint", S.ground); S.crouch.name = "Crouch";
            Img(S.crouch.transform, C.img, C.x, C.y, C.w, C.h, 0);
            S.crouchHead = HeadOn(S.crouch.transform, C.neck, C.headView ?? vn, 1);
        }
        // 앞 · 뒤 던지기: 머리 없는 던지기 몸 2장 + 목에 머리
        var TB = new[] { R, B, ArtLib.Rig(R.kind) }.FirstOrDefault(r => r != null && r.views.TryGetValue(vn, out var tv) && tv.throwFrames != null && tv.throwFrames.Length >= 2);
        if (vn != "side" && TB != null)
        {
            var T = TB.views[vn].throwFrames;
            S.throwG = new GameObject[2]; S.throwHead = new Transform[2];
            for (int f = 0; f < 2; f++)
            {
                S.throwG[f] = Tpl.Spawn("RigJoint", S.ground); S.throwG[f].name = "Throw" + f;
                Img(S.throwG[f].transform, T[f].img, T[f].x, T[f].y, T[f].w, T[f].h, 0);
                S.throwHead[f] = HeadOn(S.throwG[f].transform, T[f].neck, vn, 1);
            }
        }
        return S;
    }

    // 목 자리(neck)에 개체 머리 파츠
    Transform HeadOn(Transform parent, Vector2 neck, string hv, int order)
    {
        var hb = B.views.TryGetValue(hv, out var V) && V.parts.ContainsKey("head") ? B : R;
        if (!hb.views.TryGetValue(hv, out V) || !V.parts.TryGetValue("head", out var hd)) return null;
        var j = Tpl.Spawn("RigJoint", parent).transform; j.name = "Head";
        j.localPosition = new Vector3(neck.x, neck.y, 0);
        Img(j, HeadSprite(hb, hv), hd.x - hd.pivot.x, hd.y - hd.pivot.y, hd.w, hd.h, order);
        return j;
    }

    // 병사 그림 방향: 세계 방향으로 앞 · 옆 · 뒤 고름 (카메라는 늘 쥐 쪽에서 고양이 쪽을 봄)
    (string view, bool flip) ViewOf()
    {
        float dx = u.fdx, dz = u.fdz;
        bool hasThrow = sets.TryGetValue(dz > 0 ? "front" : "back", out var ts) && ts.throwG != null;
        if (u.forceSide || (u.st.throwK > 0 && !hasThrow) || u.mortarT) return ("side", dx < 0 || (dx == 0 && dz < 0));
        if (Mathf.Abs(dx) >= Mathf.Abs(dz) * 0.75f) return ("side", dx < 0);
        return (dz > 0 ? "front" : "back", false);
    }

    void Update()
    {
        if (u == null) return;
        float dt = Time.deltaTime, t = Time.time;
        var st = u.st;
        // 동작 상태 감쇠 (웹 view.js 루프)
        st.recoil = Mathf.Max(0, st.recoil - dt * 7);
        st.hit = Mathf.Max(0, st.hit - dt * 3.5f);
        st.aim = Mathf.Clamp01(st.aim + (aiming ? dt * 6 : -dt * 2.5f));
        if (u.faceT > 0) { u.faceT -= dt; if (u.faceT <= 0 && u.alive) u.face = null; }
        float wantCrouch = (u.alive && u.inCover && st.walk == null && !aiming) || u.mortarT ? 1 : 0;
        st.crouch += (wantCrouch - st.crouch) * Mathf.Min(1, dt * 8);

        transform.position = BoardView.Foot(u.px, u.pz);
        // 그리는 순서 = 지금 서 있는 줄 깊이. 선 병사는 같은 줄 소품보다 위,
        // 쓰러진 몸은 같은 줄 소품 밑 (옆으로 길게 누워 옆 칸 모래주머니 위에 얹혀 보이던 것 막음)
        if (!sg) sg = GetComponent<UnityEngine.Rendering.SortingGroup>();
        sg.sortingOrder = BoardView.DepthOrder(transform.position.z) + (u.alive && st.death == null ? 3 + (Mathf.RoundToInt(u.px) & 1) : -1);
        // ↑ 같은 줄 옆 칸 병사끼리 순서가 같으면 프레임마다 앞뒤가 바뀌어 깜빡임 → 칸 홀짝으로 갈라 고정
        var (vn, flip) = ViewOf();
        if (!sets.ContainsKey(vn)) vn = sets.Keys.First();
        curView = vn; curFlip = flip;
        rig.localScale = new Vector3(flip ? -k : k, -k, 1);
        foreach (var kv in sets) kv.Value.root.SetActive(kv.Key == vn);
        var S = sets[vn];

        // 어느 그림으로: 던지기(앞 · 뒤) → 웅크림(앞 · 뒤) → 리그
        bool throwMode = st.throwK > 0 && vn != "side" && S.throwG != null && st.death == null;
        bool crouchMode = !throwMode && S.crouch != null && st.crouch > 0.55f && st.death == null && vn != "side";
        foreach (var j in S.joints.Values) j.t.gameObject.SetActive(!throwMode && !crouchMode && !u.hide.Contains(j.p.name));
        // 숨긴 부위(잘림)는 자식까지 꺼지지 않게: 그림만 끔
        foreach (var j in S.joints.Values) if (u.hide.Contains(j.p.name)) { j.t.gameObject.SetActive(!throwMode && !crouchMode); j.sr.enabled = false; }
        if (S.crouch) S.crouch.SetActive(crouchMode);
        if (S.throwG != null) for (int f = 0; f < 2; f++) S.throwG[f].SetActive(throwMode && (st.throwK < 0.5f ? 0 : 1) == f);

        S.pose.localPosition = Vector3.zero; S.pose.localRotation = Quaternion.identity;
        float br = Mathf.Sin(t * 2.2f + st.seed);
        if (throwMode)
        {
            float tk = st.throwK; int f = tk < 0.5f ? 0 : 1;
            float lean = f == 1 ? Mathf.Sin((tk - 0.5f) * 2 * Mathf.PI) * 5 : -Mathf.Sin(tk * Mathf.PI) * 3;
            S.pose.localRotation = Quaternion.Euler(0, 0, lean * 0.3f);
            if (S.throwHead[f]) S.throwHead[f].localRotation = Quaternion.Euler(0, 0, lean);
        }
        else if (crouchMode)
        {
            S.pose.localPosition = new Vector3(-st.hit * 6, br * 1.2f, 0);
            if (S.crouchHead) S.crouchHead.localRotation = Quaternion.Euler(0, 0, br * 0.8f - st.hit * 6);
        }
        else
        {
            var P = RigPose.Get(vn, st, t, S.legK);
            S.pose.localPosition = new Vector3(P.dx, P.dy, 0);
            S.pose.localRotation = Quaternion.Euler(0, 0, P.rot);
            foreach (var j in S.joints.Values)
            {
                var p = j.p; Vector2 pp = p.parent != null && S.V.parts.TryGetValue(p.parent, out var par) ? par.pivot : Vector2.zero;
                var o = P.off.TryGetValue(p.name, out var ov) ? ov : Vector2.zero;
                j.t.localPosition = new Vector3(p.pivot.x - pp.x + o.x, p.pivot.y - pp.y + o.y, 0);
                j.t.localRotation = Quaternion.Euler(0, 0, P.A(p.name));
                var sc = P.sc.TryGetValue(p.name, out var sv) ? sv : Vector2.one;
                j.t.localScale = new Vector3(sc.x, sc.y, 1);
            }
        }
        // 쓰러진 몸: 발 축으로 눕히면 몸 절반이 땅 밑으로 내려감 → 바닥을 안 가리는 판(ZTest Always)이라 앞줄 땅 위에 떠 보였음.
        // 관절 축(몸 한가운데 선)이 땅 바로 위에 오게 들어 올림. 그림 테두리(투명 여백 포함)로 재면 너무 떠서 관절로 잼
        // 누운 몸은 세운 판이라 몸 두께(고양이 ≈ 0.7칸)가 그대로 위로 서서 벽처럼 떠 보임 → 세로로 눌러 바닥에 깔린 듯이
        rig.localPosition = rigBase;
        if (st.death != null)
        {
            float lay = Mathf.Clamp01((st.death.Value - 0.5f) / 0.5f);
            rig.localScale = new Vector3(rig.localScale.x, rig.localScale.y * (1 - 0.45f * lay), 1);
            float minY = float.MaxValue;
            foreach (var j in S.joints.Values) minY = Mathf.Min(minY, j.t.position.y);
            float lift = transform.position.y + 0.1f * lay - minY;
            if (lift > 0 && lift < 1.2f) rig.localPosition = rigBase + Vector3.up * lift;
        }
        // 색: 지배당함 · 매복(반투명)
        var col = u.tint; if (u.hidden) col.a = 0.55f;
        foreach (var sr in all) if (sr) sr.color = BaseCol(sr) * col;
    }

    // 의수 · 의족 색을 지키면서 틴트 곱하기
    readonly Dictionary<SpriteRenderer, Color> baseCol = new Dictionary<SpriteRenderer, Color>();
    Color BaseCol(SpriteRenderer sr)
    {
        if (!baseCol.TryGetValue(sr, out var c)) { c = sr.color; baseCol[sr] = c; }
        return new Color(c.r, c.g, c.b, 1);
    }

    // 부위 하나의 지금 월드 자리 + 그림 (절단 조각용)
    public (Sprite s, Vector3 pos, Vector3 scale, float rot) PartSnapshot(string part)
    {
        if (!sets.TryGetValue(curView, out var S) || !S.joints.TryGetValue(part, out var j)) return (null, transform.position, Vector3.one, 0);
        return (j.sr.sprite, j.sr.transform.position, j.sr.transform.lossyScale, j.sr.transform.eulerAngles.z);
    }

    // 머리 · 가슴 · 총구 자리 (효과 · 팝업용)
    public Vector3 Chest => transform.position + Vector3.up * UNIT_H * 0.55f * BodyK;
    public Vector3 Top => transform.position + Vector3.up * (UNIT_H + 0.15f) * Mathf.Max(0.35f, BodyK);
    float BodyK => u.st.death != null || !u.alive ? 0.12f : 1 - 0.42f * Mathf.Min(1, u.st.crouch);
    public Vector3 Muzzle
    {
        get
        {
            var p = transform.position;
            if (curView == "side") return p + new Vector3((curFlip ? -1 : 1) * UNIT_H * 0.55f, UNIT_H * 0.55f * BodyK, -0.05f);
            if (u.st.throwK > 0) return p + new Vector3((curView == "front" ? -1 : 1) * UNIT_H * 0.2f, UNIT_H * 0.95f, -0.05f);
            return p + new Vector3(curView == "front" ? 0.07f : -0.07f, UNIT_H * (curView == "front" ? 0.5f : 0.75f) * BodyK, curView == "front" ? -0.1f : 0.1f);
        }
    }
}

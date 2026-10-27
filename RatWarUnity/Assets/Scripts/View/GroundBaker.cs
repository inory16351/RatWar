// 바닥 굽기 (웹 view.js buildGround 를 옮김): 맵 + 둘레를 그림 한 장으로 구워 칸 경계 없이 지형을 섞음
// 칸마다 지형 종류(풀밭 · 진흙 · 길 · 구덩이)를 둘레 칸들과 부드러운 무게로 섞고, 위치를 잡음(노이즈)으로 살짝 흔들어 경계를 자연스럽게.
// 웹은 이걸 매 프레임 사다리꼴로 휘어 그렸지만(가짜 원근), 유니티는 바닥 판에 한 번 입히면 진짜 원근 카메라가 해 줌.
// 시야 안개도 칸당 1px 그림을 늘려(쌍선형) 경계가 부드럽게 (웹 buildFog).
using System.Collections.Generic;
using UnityEngine;

public class GroundBaker
{
    public readonly int PPT;            // 칸당 px: 큰 맵일수록 낮춤 (굽는 시간 ~1초 유지)
    public const float TEX_TILES = 3f;  // 지형 무늬 한 장 = 3칸
    public readonly int mx, mzBack, mzFront; // 맵 둘레 칸 수 (옆 · 뒤 · 앞)
    public int BW, BD;                  // 구운 영역 칸 수
    public Texture2D tex, fog;
    readonly WorldMap w;
    readonly Dictionary<string, Texture2D> src = new Dictionary<string, Texture2D>();
    static readonly string[] KINDS = { "grass", "mud", "road", "dry", "wet" };

    public GroundBaker(WorldMap world, int side, int back, int front)
    {
        w = world; mx = side; mzBack = back; mzFront = front;
        BW = w.W + mx * 2; BD = w.D + mzBack + mzFront;
        PPT = Mathf.Clamp(Mathf.RoundToInt(Mathf.Sqrt(1_400_000f / (BW * BD))), 16, 24); // 픽셀 수 ≈ 140만 이하
        foreach (var k in KINDS) src[k] = ArtLib.Asset("terrain", k)?.sprite?.texture;
    }

    // 구운 영역의 월드 범위: x = [ox, ox + BW], 월드 Z = [oz, oz + BD]
    public float OX => -mx - 0.5f;
    public float OZ => -mzFront - 0.5f; // 월드 Z (맵 z = D − 1 − Z)

    // 칸 (tx, tz: 맵 좌표, 맵 밖 포함) 의 지형 종류
    int KindAt(int tx, int tz)
    {
        var t = World.TileAt(w, tx, tz);
        if (t != null) return t.ground == "mud" ? 1 : t.ground == "road" ? 2 : t.ground == "crater" ? 3 : 0;
        // 맵 밖: 풀밭 바탕에 마른 땅 · 진흙 얼룩 (칸 좌표로 정해진 값)
        float n = Mathf.PerlinNoise(tx * 0.21f + 3.1f, tz * 0.21f + 7.7f);
        return n > 0.7f ? 3 : n < 0.22f ? 1 : 0;
    }
    bool CraterAt(int tx, int tz) { var t = World.TileAt(w, tx, tz); return t != null && t.ground == "crater"; }

    public void BakeAll()
    {
        if (!tex) { tex = new Texture2D(BW * PPT, BD * PPT, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, anisoLevel = 4, name = "BakedGround" }; }
        BakeRect(0, 0, BW * PPT, BD * PPT);
        tex.Apply(true);
    }

    // 맵 칸 하나가 바뀌었을 때 (폭발 → 구덩이) 그 둘레만 다시 굽기
    public void Rebake(int x, int z)
    {
        int cx = x + mx, cz = (w.D - 1 - z) + mzFront; // 구운 그림 칸 좌표 (아래 = 앞)
        int x0 = Mathf.Max(0, (cx - 2) * PPT), y0 = Mathf.Max(0, (cz - 2) * PPT);
        int x1 = Mathf.Min(BW * PPT, (cx + 3) * PPT), y1 = Mathf.Min(BD * PPT, (cz + 3) * PPT);
        BakeRect(x0, y0, x1 - x0, y1 - y0);
        tex.Apply(true);
    }

    void BakeRect(int px0, int py0, int pw, int ph)
    {
        var buf = new Color32[pw * ph];
        var wts = new float[KINDS.Length];
        for (int py = 0; py < ph; py++)
            for (int px = 0; px < pw; px++)
            {
                int gx = px0 + px, gy = py0 + py;
                // 구운 그림 px → 월드 (x, Z) → 맵 칸 좌표 (연속)
                float wx = OX + (gx + 0.5f) / PPT, wz = OZ + (gy + 0.5f) / PPT;
                float tz = w.D - 1 - wz;
                // 경계를 흔들기
                float jx = (Mathf.PerlinNoise(wx * 1.7f, wz * 1.7f) - 0.5f) * 0.55f, jz = (Mathf.PerlinNoise(wx * 1.7f + 40, wz * 1.7f + 40) - 0.5f) * 0.55f;
                float sx = wx + jx, sz = tz + jz;
                int ix = Mathf.RoundToInt(sx), iz = Mathf.RoundToInt(sz);
                System.Array.Clear(wts, 0, wts.Length);
                float crater = 0;
                for (int dz = -1; dz <= 1; dz++)
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        int nx = ix + dx, nz = iz + dz;
                        float ddx = sx - nx, ddz = sz - nz, d2 = ddx * ddx + ddz * ddz;
                        float k = Mathf.Exp(-d2 / 0.22f);
                        wts[KindAt(nx, nz)] += k;
                        // 구덩이: 칸 가운데 납작한 타원으로 어둡게 (진짜 위치 기준, 흔들기 조금)
                        if (CraterAt(nx, nz)) { float rx = (wx - nx) / 0.42f, rz = (tz - nz) / 0.34f; crater = Mathf.Max(crater, 1 - Mathf.Clamp01(rx * rx + rz * rz)); }
                    }
                float sum = 0; for (int i = 0; i < wts.Length; i++) sum += wts[i];
                float u = wx / TEX_TILES, v = wz / TEX_TILES;
                Color c = Color.black;
                for (int i = 0; i < wts.Length; i++)
                {
                    if (wts[i] <= 0.001f || !src[KINDS[i]]) continue;
                    c += src[KINDS[i]].GetPixelBilinear(u, v) * (wts[i] / sum);
                }
                // 구덩이 = 어두운 흙 테두리 + 더 어두운 안쪽
                if (crater > 0) c = Color.Lerp(c, new Color(0.22f, 0.18f, 0.13f), Mathf.SmoothStep(0, 1, crater) * 0.75f);
                // 둘레 바깥 2.5칸은 투명하게 녹여 바깥 들판 판과 이음매 없이
                float edgeX = Mathf.Min(gx, BW * PPT - 1 - gx) / (float)PPT, edgeY = Mathf.Min(gy, BD * PPT - 1 - gy) / (float)PPT;
                c.a = Mathf.Clamp01(Mathf.Min(edgeX, edgeY) / 2.5f);
                buf[py * pw + px] = c;
            }
        tex.SetPixels32(px0, py0, pw, ph, buf);
    }

    // 시야 안개: 맵 칸당 1px (보임 = 투명, 안 보임 = 어두운 연무)
    public void BakeFog(HashSet<int> vis)
    {
        if (!fog) fog = new Texture2D(w.W, w.D, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = "FogOfWar" };
        var px = new Color32[w.W * w.D];
        var dark = new Color32(0, 0, 0, 255);   // a = 안 보임 (셰이더가 채도 · 밝기 · 연무로 바꿈)
        var clear = new Color32(0, 0, 0, 0);
        for (int z = 0; z < w.D; z++)
            for (int x = 0; x < w.W; x++)
                px[(w.D - 1 - z) * w.W + x] = vis.Contains(z * w.W + x) ? clear : dark; // 그림 아래쪽 = 앞(맵 z 큰 쪽)
        fog.SetPixels32(px);
        fog.Apply(false);
    }
}

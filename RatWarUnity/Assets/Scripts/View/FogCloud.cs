// 시야 안개 연무 무늬 (웹 view.js cloudTex 그대로): 값 잡음 4겹을 이어 붙는 256 그림으로
// 색 = 베이지(168~198), 투명도 = (v − 0.36) × 2.4 → 덩어리진 구름. 한 번 만들어 전역 _MistTex 로
using UnityEngine;

public static class FogCloud
{
    static Texture2D tex;

    public static Texture2D Get()
    {
        if (tex) return tex;
        const int N = 256;
        float[] Grid(int n, long seed)
        {
            var a = new float[n * n]; long s = seed;
            for (int i = 0; i < n * n; i++) { s = s * 16807 % 2147483647; a[i] = s / 2147483647f; }
            return a;
        }
        var oct = new (int n, float w, float[] g)[] { (4, 0.5f, Grid(4, 11)), (8, 0.28f, Grid(8, 23)), (16, 0.15f, Grid(16, 37)), (32, 0.07f, Grid(32, 51)) };
        float Sm(float t) => t * t * (3 - 2 * t);
        var px = new Color32[N * N];
        for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                float v = 0;
                foreach (var (n, w, G) in oct)
                {
                    float fx = (float)x / N * n, fy = (float)y / N * n;
                    int x0 = Mathf.FloorToInt(fx), y0 = Mathf.FloorToInt(fy);
                    float tx = Sm(fx - x0), ty = Sm(fy - y0);
                    float At(int i, int j) => G[(j % n) * n + (i % n)];
                    v += w * ((At(x0, y0) * (1 - tx) + At(x0 + 1, y0) * tx) * (1 - ty) + (At(x0, y0 + 1) * (1 - tx) + At(x0 + 1, y0 + 1) * tx) * ty);
                }
                float a = Mathf.Clamp01((v - 0.36f) * 2.4f);
                px[y * N + x] = new Color32((byte)(168 + v * 30), (byte)(164 + v * 28), (byte)(150 + v * 26), (byte)(a * 200));
            }
        tex = new Texture2D(N, N, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear, name = "FogCloud" };
        tex.SetPixels32(px);
        tex.Apply(true);
        return tex;
    }
}

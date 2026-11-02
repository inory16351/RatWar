using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// 육각 칸 (평평한 윗변). 웹은 SVG 다각형
public class UIHex : MaskableGraphic
{
    public Color edge = new Color(0, 0, 0, 0.6f);
    public float width = 3;      // 테두리 두께
    public bool pulse;           // 테두리 깜빡임 (찍을 수 있는 칸)
    public Color badge = Color.clear; // 오른쪽 아래 ✓ 배지 (찍은 칸)

    void Update()
    {
        if (pulse) SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        var r = rectTransform.rect; float R = Mathf.Min(r.width, r.height) / 2;
        var c = r.center;
        vh.AddVert(c, color, Vector2.zero);
        for (int i = 0; i < 6; i++)
        {
            float a = Mathf.PI / 3 * i;
            vh.AddVert(c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * (R - 2), color, Vector2.zero);
        }
        for (int i = 0; i < 6; i++) vh.AddTriangle(0, 1 + i, 1 + (i + 1) % 6);
        // 테두리
        var e = edge;
        if (pulse) e.a *= 0.55f + 0.45f * Mathf.Sin(Time.unscaledTime * 5f);
        int b = vh.currentVertCount;
        for (int i = 0; i < 6; i++)
        {
            float a = Mathf.PI / 3 * i;
            var d = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
            vh.AddVert(c + d * R, e, Vector2.zero);
            vh.AddVert(c + d * (R - width), e, Vector2.zero);
        }
        for (int i = 0; i < 6; i++)
        {
            int o = b + i * 2, n = b + (i + 1) % 6 * 2;
            vh.AddTriangle(o, n, n + 1); vh.AddTriangle(o, n + 1, o + 1);
        }
        if (badge.a > 0) Badge(vh, c + new Vector2(R * 0.48f, -R * 0.5f), R * 0.36f);
    }

    // 동그라미 + 체크 표시
    void Badge(VertexHelper vh, Vector2 p, float br)
    {
        var dark = new Color(0.08f, 0.06f, 0.04f, 1);
        Disc(vh, p, br + 2, dark);
        Disc(vh, p, br, badge);
        float w = br * 0.22f;
        Line(vh, p + new Vector2(-br * 0.5f, 0), p + new Vector2(-br * 0.12f, -br * 0.4f), w, Color.white);
        Line(vh, p + new Vector2(-br * 0.12f, -br * 0.4f), p + new Vector2(br * 0.55f, br * 0.42f), w, Color.white);
    }

    static void Disc(VertexHelper vh, Vector2 p, float rad, Color col)
    {
        int b = vh.currentVertCount;
        vh.AddVert(p, col, Vector2.zero);
        for (int i = 0; i < 16; i++)
        {
            float a = Mathf.PI * 2 / 16 * i;
            vh.AddVert(p + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * rad, col, Vector2.zero);
        }
        for (int i = 0; i < 16; i++) vh.AddTriangle(b, b + 1 + i, b + 1 + (i + 1) % 16);
    }

    static void Line(VertexHelper vh, Vector2 a, Vector2 z, float w, Color col)
    {
        var n = new Vector2(-(z - a).y, (z - a).x).normalized * w / 2;
        var d = (z - a).normalized * w / 2;
        int b = vh.currentVertCount;
        vh.AddVert(a - d + n, col, Vector2.zero); vh.AddVert(z + d + n, col, Vector2.zero);
        vh.AddVert(z + d - n, col, Vector2.zero); vh.AddVert(a - d - n, col, Vector2.zero);
        vh.AddTriangle(b, b + 1, b + 2); vh.AddTriangle(b, b + 2, b + 3);
    }

    public override bool Raycast(Vector2 sp, Camera cam) => base.Raycast(sp, cam);
}

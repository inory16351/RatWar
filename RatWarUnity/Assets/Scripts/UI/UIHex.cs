using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// 육각 칸 (평평한 윗변). 웹은 SVG 다각형
public class UIHex : MaskableGraphic
{
    public Color edge = new Color(0, 0, 0, 0.6f);
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
        int b = vh.currentVertCount;
        for (int i = 0; i < 6; i++)
        {
            float a = Mathf.PI / 3 * i;
            var d = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
            vh.AddVert(c + d * R, edge, Vector2.zero);
            vh.AddVert(c + d * (R - 3), edge, Vector2.zero);
        }
        for (int i = 0; i < 6; i++)
        {
            int o = b + i * 2, n = b + (i + 1) % 6 * 2;
            vh.AddTriangle(o, n, n + 1); vh.AddTriangle(o, n + 1, o + 1);
        }
    }
    public override bool Raycast(Vector2 sp, Camera cam) => base.Raycast(sp, cam);
}

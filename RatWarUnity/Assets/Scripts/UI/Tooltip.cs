using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// 롤식 툴팁 창: 마우스를 따라다님
public class Tooltip : MonoBehaviour
{
    public static Tooltip I;
    public TipSource owner;
    Text text; RectTransform rt, canvasRt;
    void Awake() { I = this; text = UIK.T(transform, "Text"); rt = (RectTransform)transform; canvasRt = (RectTransform)GetComponentInParent<Canvas>().transform; gameObject.SetActive(false); }
    public void Show(string s) { text.text = s; gameObject.SetActive(true); Place(); }
    public void Hide() { gameObject.SetActive(false); }
    void Update() => Place();
    void Place()
    {
        var m = UnityEngine.InputSystem.Mouse.current; if (m == null) return;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRt, m.position.ReadValue(), null, out var lp);
        LayoutRebuilder.ForceRebuildLayoutImmediate(rt);
        var half = canvasRt.rect.size / 2; var sz = rt.rect.size;
        float x = lp.x + 18, y = lp.y + sz.y + 12;
        if (x + sz.x > half.x - 8) x = lp.x - sz.x - 18;
        if (y > half.y - 8) y = lp.y - 22;
        rt.anchoredPosition = new Vector2(x, y);
    }
}

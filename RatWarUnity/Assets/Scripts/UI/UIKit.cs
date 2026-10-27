// UI 공용: 색 · 찾기 도우미 · 쥐 초상(리그 앞모습 파츠를 UI 그림으로 쌓음) · 육각 칸 · 마우스 툴팁
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public static class UIK
{
    public static readonly Color INK = SkillsData.Hex("#ece4cc"), DIM = SkillsData.Hex("#a59c84"), RAT = SkillsData.Hex("#8fd0ff"), CAT = SkillsData.Hex("#ff6b5b"),
        GOLD = SkillsData.Hex("#ffcf4a"), PSY = SkillsData.Hex("#c9a3ff"), OK = SkillsData.Hex("#9fdc8a"), NEG = SkillsData.Hex("#ff7a6a");
    public static string Hex(Color c) => "#" + ColorUtility.ToHtmlStringRGB(c);
    public static string Col(string s, Color c) => $"<color={Hex(c)}>{s}</color>";
    public static string Dim(string s) => Col(s, DIM);

    public static Transform F(Transform t, string path) { var r = t.Find(path); if (!r) Debug.LogError($"UI 없음: {t.name}/{path}"); return r; }
    public static Text T(Transform t, string path) => F(t, path)?.GetComponent<Text>();
    public static Image I(Transform t, string path) => F(t, path)?.GetComponent<Image>();
    public static Button B(Transform t, string path) => F(t, path)?.GetComponent<Button>();
    public static void Show(Transform t, bool on) { if (t && t.gameObject.activeSelf != on) t.gameObject.SetActive(on); }
    public static void OnClick(Button b, UnityEngine.Events.UnityAction a) { b.onClick.RemoveAllListeners(); b.onClick.AddListener(a); }

    // 스트레스 막대: 바깥(Image) 안의 Fill(Image, Filled) 길이 · 색
    public static void Bar(Transform bar, float k, Color c)
    {
        var f = bar ? bar.Find("Fill")?.GetComponent<Image>() : null;
        if (!f) return;
        f.fillAmount = Mathf.Clamp01(k); f.color = c;
    }

    public static void Tip(Component c, string text)
    {
        var t = c.GetComponent<TipSource>() ?? c.gameObject.AddComponent<TipSource>();
        t.text = text;
    }

    // ── 쥐 초상: 병종 리그 앞모습 + 개체 얼굴 (상반신 또는 전신) ──
    // slot = 초상을 넣을 자리(RectMask2D). full = 전신, dead = 회색
    public static void Portrait(RectTransform slot, RatRecord r, bool full = false, bool dead = false)
    {
        Tpl.Clear(slot);
        var cls = Camp.ClassOf(r);
        var st = Camp.Stats(r);
        var R = ArtLib.RigFor(true, cls, r.gear, st.abil.Contains("flame"));
        var Bb = ArtLib.BaseRig(true, cls);
        if (R == null || !R.views.TryGetValue("front", out var V)) return;
        float size = Mathf.Min(slot.rect.width, slot.rect.height);
        if (size <= 1) size = slot.sizeDelta.x > 0 ? slot.sizeDelta.x : 64;
        float h = size * (full ? 0.95f : 3.1f), footY = full ? size * 0.98f : size * 0.42f + h * 0.875f;
        float k = h / 470f;
        var root = Tpl.Spawn<RectTransform>("UIPart", slot);
        root.name = "Rig";
        root.GetComponent<Image>().enabled = false;
        root.anchorMin = root.anchorMax = new Vector2(0.5f, 1); root.pivot = new Vector2(0, 1);
        root.anchoredPosition = new Vector2(0, -footY); root.sizeDelta = Vector2.zero; root.localScale = new Vector3(k, k, 1);
        var col = dead ? new Color(0.5f, 0.5f, 0.5f) : Color.white;
        foreach (var n in V.order)
        {
            if (!V.parts.TryGetValue(n, out var p)) continue;
            Sprite s = p.img;
            if (n == "head")
            {
                var hb = Bb.views.TryGetValue("front", out var bv) && bv.parts.TryGetValue("head", out var bh) ? bh : p;
                var rh = p.faceImgs.Length > r.face ? p.faceImgs[r.face] : null;
                s = rh ?? (hb.faceImgs.Length > r.face ? hb.faceImgs[r.face] : null) ?? p.img;
            }
            var im = Tpl.Spawn<Image>("UIPart", root);
            im.name = n; im.sprite = s; im.color = col;
            if (r.prosthetic.Contains("leg") && n == "legR") im.color = col * new Color(0.6f, 0.44f, 0.3f);
            var rt = im.rectTransform;
            rt.anchorMin = rt.anchorMax = Vector2.zero; rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = new Vector2(p.x - V.ground.x, -(p.y - V.ground.y));
            rt.sizeDelta = new Vector2(p.w, p.h);
        }
    }
}

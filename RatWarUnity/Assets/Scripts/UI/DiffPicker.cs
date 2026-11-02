// 새 전쟁 난이도 고르기: 카드 4장 (이름 · 한 줄 소개 · 전투 보정 · 전쟁 수치) → '전쟁 시작'
// 고른 난이도는 그 전쟁 동안 고정 (Camp.Fresh 가 시작 보급품까지 맞춤). 씬에 따로 두지 않고 코드로 만듦
using System;
using UnityEngine;
using UnityEngine.UI;

public static class DiffPicker
{
    static GameObject root;

    public static void Open(Transform canvas, int pick, Action<int> onStart, Action onBack)
    {
        if (root) UnityEngine.Object.Destroy(root);
        root = new GameObject("DiffPicker", typeof(RectTransform), typeof(Image));
        var rt = (RectTransform)root.transform;
        rt.SetParent(canvas, false); Stretch(rt);
        root.GetComponent<Image>().color = new Color(0.05f, 0.04f, 0.03f, 0.985f); // 뒤 클릭 막음

        Label(rt, "새 전쟁 — 난이도", 52, true, UIK.INK, new Vector2(0, 450), new Vector2(1400, 80));
        Label(rt, UIK.Dim("전쟁을 시작하면 끝날 때까지 바꿀 수 없습니다"), 24, false, UIK.INK, new Vector2(0, 395), new Vector2(1400, 40));

        var cards = new Image[RULES.DIFFS.Length];
        Button go = null;
        // 고른 카드 = 밝게 + 금테, 나머지 = 어둡게
        void Select(int i)
        {
            pick = i;
            for (int k = 0; k < cards.Length; k++)
            {
                cards[k].color = k == i ? Color.white : new Color(0.55f, 0.55f, 0.55f);
                var ol = cards[k].GetComponent<Outline>(); ol.enabled = k == i;
            }
            go.GetComponentInChildren<Text>().text = $"{RULES.DIFFS[i].name}(으)로 전쟁 시작";
        }
        const float W = 400, H = 680, GAP = 26;
        float x0 = -(RULES.DIFFS.Length - 1) * (W + GAP) / 2;
        for (int i = 0; i < RULES.DIFFS.Length; i++)
        {
            int n = i; var D = RULES.DIFFS[i];
            var card = Box(rt, "out/ui/panel_sq.png", new Vector2(x0 + i * (W + GAP), 0), new Vector2(W, H));
            cards[i] = card;
            var ol = card.gameObject.AddComponent<Outline>(); ol.effectColor = UIK.GOLD; ol.effectDistance = new Vector2(4, -4);
            var b = card.gameObject.AddComponent<Button>(); b.onClick.AddListener(() => Select(n));
            Label(card.transform, D.name, 40, true, i == RULES.DIFFS.Length - 1 ? UIK.CAT : UIK.GOLD, new Vector2(0, 262), new Vector2(W - 100, 56));
            Label(card.transform, D.tag, 21, false, UIK.DIM, new Vector2(0, 218), new Vector2(W - 100, 32));
            var t = Label(card.transform, Body(D), 21, false, UIK.INK, new Vector2(0, -50), new Vector2(W - 110, 470));
            t.alignment = TextAnchor.UpperLeft; t.lineSpacing = 1.1f;
        }

        go = Box(rt, "out/ui/btn_hi.png", new Vector2(150, -440), new Vector2(480, 84)).gameObject.AddComponent<Button>();
        Label(go.transform, "", 30, true, UIK.INK, Vector2.zero, new Vector2(400, 70));
        go.onClick.AddListener(() => { Close(); onStart(pick); });
        var back = Box(rt, "out/ui/btn.png", new Vector2(-240, -440), new Vector2(220, 84)).gameObject.AddComponent<Button>();
        Label(back.transform, "뒤로", 28, false, UIK.INK, Vector2.zero, new Vector2(200, 70));
        back.onClick.AddListener(() => { Close(); onBack?.Invoke(); });
        Select(Mathf.Clamp(pick, 0, RULES.DIFFS.Length - 1));
    }

    public static void Close() { if (root) UnityEngine.Object.Destroy(root); root = null; }

    // 카드 본문: 전투 보정 + 전쟁 수치 (병장과 다른 것만 색)
    static string Body(RULES.Diff D)
    {
        var B = RULES.DIFFS[RULES.defaultDiff];
        string V(string s, bool diff, bool good) => diff ? UIK.Col(s, good ? UIK.OK : UIK.NEG) : s;
        string inv = D.invade < 0 ? "한 턴 침공 −1" : D.invade > 0 ? "8턴부터 침공 +1" : "침공 기본";
        return $"{UIK.Col("<b>전투</b>", UIK.GOLD)}\n{D.desc.Replace(" · ", "\n")}\n\n{UIK.Col("<b>전쟁</b>", UIK.GOLD)}\n"
            + V($"위협 단계: {D.threatStep:0.#}턴마다", D.threatStep != B.threatStep, D.threatStep > B.threatStep) + "\n"
            + V($"시작 보급품 {D.supplies}", D.supplies != B.supplies, D.supplies > B.supplies) + "\n"
            + V($"턴 수입 ×{D.income:0.##}", D.income != B.income, D.income > B.income) + "\n"
            + V(inv, D.invade != 0, D.invade < 0) + "\n"
            + V($"자동 방어 {(D.hold >= 0 ? "+" : "−")}{Mathf.Abs(D.hold) * 100:0}%", D.hold != 0, D.hold > 0);
    }

    static void Stretch(RectTransform r) { r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = r.offsetMax = Vector2.zero; }

    static Image Box(Transform parent, string sprite, Vector2 pos, Vector2 size)
    {
        var g = new GameObject("Box", typeof(RectTransform), typeof(Image));
        var r = (RectTransform)g.transform; r.SetParent(parent, false);
        r.anchoredPosition = pos; r.sizeDelta = size;
        var im = g.GetComponent<Image>(); im.sprite = ArtLib.Sprite(sprite); im.type = Image.Type.Sliced;
        return im;
    }

    static Text Label(Transform parent, string s, int size, bool bold, Color c, Vector2 pos, Vector2 box)
    {
        var g = new GameObject("Text", typeof(RectTransform), typeof(Text));
        var r = (RectTransform)g.transform; r.SetParent(parent, false);
        r.anchoredPosition = pos; r.sizeDelta = box;
        var t = g.GetComponent<Text>();
        t.font = bold ? GameRoot.I.fontBold : GameRoot.I.fontMedium; t.fontSize = size; t.color = c;
        t.alignment = TextAnchor.MiddleCenter; t.supportRichText = true; t.raycastTarget = false;
        t.horizontalOverflow = HorizontalWrapMode.Wrap; t.verticalOverflow = VerticalWrapMode.Overflow;
        t.text = s;
        return t;
    }
}

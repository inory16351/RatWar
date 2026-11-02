// 새 전쟁 난이도 고르기: 카드 4장 (이름 · 한 줄 소개 · 전투 보정 · 전쟁 수치) → '전쟁 시작'
// 화면은 하이라키 Canvas/DiffPicker ('RatWar/메뉴 UI 만들기'), 여기선 글 채우기 · 버튼 연결만
// 고른 난이도는 그 전쟁 동안 고정 (Camp.Fresh 가 시작 보급품까지 맞춤)
using System;
using UnityEngine;
using UnityEngine.UI;

public static class DiffPicker
{
    static Action onBack;

    public static void Open(int pick, Action<int> onStart, Action back)
    {
        onBack = back;
        var root = GameObject.Find("Canvas").transform.Find("DiffPicker");
        var start = UIK.B(root, "Start");
        void Select(int i)
        {
            pick = i;
            for (int k = 0; k < RULES.DIFFS.Length; k++)
            {
                var card = root.Find($"Card{k}");
                card.GetComponent<Image>().color = k == i ? Color.white : new Color(0.55f, 0.55f, 0.55f); // 고른 카드 = 밝게 + 금테
                card.GetComponent<Outline>().enabled = k == i;
            }
            start.GetComponentInChildren<Text>().text = $"{RULES.DIFFS[i].name}(으)로 전쟁 시작";
        }
        for (int i = 0; i < RULES.DIFFS.Length; i++)
        {
            int n = i; var D = RULES.DIFFS[i]; var card = root.Find($"Card{i}");
            UIK.T(card, "Name").text = D.name;
            UIK.T(card, "Name").color = i == RULES.DIFFS.Length - 1 ? UIK.CAT : UIK.GOLD;
            UIK.T(card, "Tag").text = D.tag;
            UIK.T(card, "Body").text = Body(D);
            UIK.OnClick(card.GetComponent<Button>(), () => Select(n));
        }
        UIK.OnClick(start, () => { Menus.Show("DiffPicker", false); onStart(pick); });
        UIK.OnClick(UIK.B(root, "Back"), Back);
        Select(Mathf.Clamp(pick, 0, RULES.DIFFS.Length - 1));
        Menus.Show("DiffPicker", true);
    }

    public static void Back() { Menus.Show("DiffPicker", false); var b = onBack; onBack = null; b?.Invoke(); }

    // 카드 본문: 전투 보정 + 전쟁 수치 (병장보다 유리 = 초록, 불리 = 빨강)
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
}

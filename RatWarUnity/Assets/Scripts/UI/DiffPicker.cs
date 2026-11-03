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
            if (!RULES.Unlocked(i)) return;
            pick = i;
            for (int k = 0; k < RULES.DIFFS.Length; k++)
            {
                var card = root.Find($"Card{k}");
                card.GetComponent<Image>().color = k == i ? Color.white : new Color(0.55f, 0.55f, 0.55f); // 고른 카드 = 밝게 + 금테
                card.GetComponent<Outline>().enabled = k == i;
            }
            start.GetComponentInChildren<Text>().text = Loc.F("ui.diff.start", Loc.T(RULES.DIFFS[i].name));
        }
        for (int i = 0; i < RULES.DIFFS.Length; i++)
        {
            int n = i; var D = RULES.DIFFS[i]; var card = root.Find($"Card{i}");
            UIK.T(card, "Name").text = Loc.T(D.name);
            UIK.T(card, "Name").color = i == RULES.DIFFS.Length - 1 ? UIK.CAT : UIK.GOLD;
            UIK.T(card, "Tag").text = Loc.T(D.tag);
            UIK.T(card, "Body").text = RULES.Unlocked(i) ? Body(D) : UIK.Dim(Loc.F("ui.diff.locked", Loc.T(RULES.DIFFS[RULES.defaultDiff].name)));
            UIK.OnClick(card.GetComponent<Button>(), () => Select(n));
        }
        UIK.OnClick(start, () => { Menus.Show("DiffPicker", false); onStart(pick); });
        UIK.OnClick(UIK.B(root, "Back"), Back);
        pick = Mathf.Clamp(pick, 0, RULES.DIFFS.Length - 1);
        if (!RULES.Unlocked(pick)) pick = RULES.defaultDiff;
        Select(pick);
        Menus.Show("DiffPicker", true);
    }

    public static void Back() { Menus.Show("DiffPicker", false); var b = onBack; onBack = null; b?.Invoke(); }

    // 카드 본문: 전투 보정 + 전쟁 수치 (병장보다 유리 = 초록, 불리 = 빨강)
    static string Body(RULES.Diff D)
    {
        var B = RULES.DIFFS[RULES.defaultDiff];
        string V(string s, bool diff, bool good) => diff ? UIK.Col(s, good ? UIK.OK : UIK.NEG) : s;
        string inv = Loc.T(D.invade < 0 ? "ui.diff.invade.less" : D.invade > 0 ? "ui.diff.invade.more" : "ui.diff.invade.base");
        return $"{UIK.Col($"<b>{Loc.T("ui.diff.battle")}</b>", UIK.GOLD)}\n{Loc.T(D.desc).Replace(" · ", "\n")}\n\n{UIK.Col($"<b>{Loc.T("ui.diff.war")}</b>", UIK.GOLD)}\n"
            + V(Loc.F("ui.diff.threat", D.threatStep), D.threatStep != B.threatStep, D.threatStep > B.threatStep) + "\n"
            + V(Loc.F("ui.diff.land", D.land), D.land != B.land, D.land > B.land) + "\n"
            + V(Loc.F("ui.diff.supplies", D.supplies), D.supplies != B.supplies, D.supplies > B.supplies) + "\n"
            + V(Loc.F("ui.diff.income", D.income), D.income != B.income, D.income > B.income) + "\n"
            + V(inv, D.invade != 0, D.invade < 0) + "\n"
            + V(Loc.F("ui.diff.hold", D.hold >= 0 ? "+" : "−", Mathf.Abs(D.hold) * 100), D.hold != 0, D.hold > 0);
    }
}

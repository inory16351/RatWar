// 끝 화면 (작전 결과)
using UnityEngine;

public class EndScreen : MonoBehaviour
{
    public static EndScreen I;
    void Awake() { I = this; gameObject.SetActive(false); }

    public void Show(string result, Camp.SettleOut o)
    {
        gameObject.SetActive(true);
        var p = UIK.F(transform, "Panel");
        bool win = result == "win";
        UIK.T(p, "Title").text = win ? "작전 성공" : "작전 실패";
        UIK.T(p, "Title").color = win ? UIK.GOLD : UIK.CAT;
        UIK.T(p, "Body").text = $"{Battle.mission.name} — {(win ? "고양이들이 물러갔다." : "분대가 무너졌다. 살아 돌아온 쥐는 많지 않다.")} 보급품 +{o.reward}"
            + (o.drafted > 0 ? "\n" + UIK.Dim($"빈자리를 채우려고 징집병 {o.drafted}마리가 사령부에 도착했다.") : "");
        var box = UIK.F(p, "Reps");
        Tpl.Clear(box);
        foreach (var x in o.report)
        {
            var row = Tpl.Spawn("RepRow", box).transform;
            UIK.Portrait((RectTransform)UIK.F(row, "Portrait"), x.r, dead: x.dead);
            string notes;
            if (x.dead) notes = UIK.Col($"전사 — {x.how}", UIK.NEG) + "  " + UIK.Dim($"“{x.r.story}”");
            else
            {
                var n = new System.Collections.Generic.List<string> { $"처치 {x.kills}" };
                if (x.promoted) n.Add(UIK.Col("진급! 스킬 1점", UIK.GOLD));
                if (x.downed) n.Add(UIK.Col("쓰러졌다 살아남음", UIK.NEG));
                if (x.severed.Count > 0) n.Add(UIK.Col(string.Join(" · ", x.severed.ConvertAll(s => Battle.PART_NAME[s])) + " 잃음 → 의수 · 의족 필요", UIK.NEG));
                if (!string.IsNullOrEmpty(x.broke)) n.Add(UIK.Col("정신 이상: " + x.broke, UIK.PSY));
                n.Add("스트레스 " + (x.r.stress >= CampRules.STRESS.shaky ? UIK.Col(x.r.stress.ToString(), UIK.PSY) : x.r.stress.ToString()));
                notes = string.Join(" · ", n);
            }
            UIK.T(row, "Text").text = $"<b>{CampRules.RANKS[CampRules.RankOf(x.r.xp)].name} {x.r.name}</b>\n{notes}";
        }
        UIK.OnClick(UIK.B(p, "ToHQ"), () => { gameObject.SetActive(false); Lobby.I.Show(); });
    }
}

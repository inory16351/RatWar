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
        UIK.T(p, "Title").text = Loc.T(win ? "end.title.win" : "end.title.lose");
        UIK.T(p, "Title").color = win ? UIK.GOLD : UIK.CAT;
        UIK.T(p, "Body").text = Loc.F("end.body", Loc.T(Battle.mission.name), Loc.T(win ? "end.body.win" : "end.body.lose"), o.reward)
            + (o.drafted > 0 ? "\n" + UIK.Dim(Loc.F("end.drafted", o.drafted)) : "");
        var box = UIK.F(p, "Reps");
        Tpl.Clear(box);
        foreach (var x in o.report)
        {
            var row = Tpl.Spawn("RepRow", box).transform;
            UIK.Portrait((RectTransform)UIK.F(row, "Portrait"), x.r, dead: x.dead);
            string notes;
            if (x.dead) notes = UIK.Col(Loc.F("end.dead", Loc.T(x.how)), UIK.NEG) + "  " + UIK.Dim($"“{x.r.Story}”");
            else
            {
                var n = new System.Collections.Generic.List<string> { Loc.F("end.kills", x.kills) };
                if (x.promoted) n.Add(UIK.Col(Loc.T("end.promoted"), UIK.GOLD));
                if (x.downed) n.Add(UIK.Col(Loc.T("end.downed"), UIK.NEG));
                if (x.severed.Count > 0) n.Add(UIK.Col(Loc.F("end.severed", string.Join(" · ", x.severed.ConvertAll(s => Loc.T(Battle.PART_NAME[s])))), UIK.NEG));
                if (!string.IsNullOrEmpty(x.broke)) n.Add(UIK.Col(Loc.F("end.broke", Loc.T(x.broke)), UIK.PSY));
                foreach (var t in x.gained) n.Add(UIK.Col(Loc.F("end.trait", Loc.T(CampRules.TRAITS[t].name)), UIK.GOLD));
                n.Add(Loc.F("end.stress", x.r.stress >= CampRules.STRESS.shaky ? UIK.Col(x.r.stress.ToString(), UIK.PSY) : x.r.stress.ToString()));
                notes = string.Join(" · ", n);
            }
            UIK.T(row, "Text").text = $"<b>{Loc.T(CampRules.RANKS[CampRules.RankOf(x.r.xp)].name)} {x.r.Name}</b>\n{notes}";
        }
        UIK.OnClick(UIK.B(p, "ToHQ"), () => { gameObject.SetActive(false); Lobby.I.Show(); });
    }
}

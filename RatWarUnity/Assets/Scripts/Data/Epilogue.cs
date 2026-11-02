// 전쟁이 끝난 뒤: 살아남은 쥐 하나하나의 '그 뒤의 이야기' (밴드 오브 브라더스 마지막 화처럼)
// 사연 · 고향 · 잃은 팔다리 · 형제 · 전우 · 앙숙 · 스트레스 · 계급 · 처치 수로 문장을 고름. 같은 쥐면 늘 같은 이야기
// 글자 = Tables/<언어>/epilogue.json
using System.Collections.Generic;
using System.Linq;

public static class Epilogue
{
    const int MAX_RATS = 8;

    public static Slide[] Build(CampData c, bool win)
    {
        var list = new List<Slide>();
        var alive = c.squad.OrderByDescending(r => r.missions).ThenByDescending(r => r.xp).ToList();
        list.Add(new Slide { img = win ? "epilogue_2" : "epilogue_4", text = Loc.T(win ? "epi.intro.win" : "epi.intro.lose") });
        foreach (var r in alive.Take(MAX_RATS))
            list.Add(new Slide { rat = r, title = Loc.F("epi.name", Rank(r), Loc.T(r.name)), sub = Loc.F("epi.sub", Loc.T(r.home), r.missions, r.kills), text = Story(c, r, win) });
        // 전사자 이름
        var fallen = c.fallen.Select(r => Rank(r) + " " + Loc.T(r.name)).ToList();
        list.Add(new Slide
        {
            img = win ? "epilogue_1" : "epilogue_3",
            title = Loc.T("epi.roll.head"),
            text = fallen.Count == 0 ? Loc.T("epi.roll.none") : Loc.F("epi.roll", alive.Count, fallen.Count, string.Join(" · ", fallen)),
        });
        // 가장 오래 싸운 쥐의 한마디
        var top = alive.OrderByDescending(r => Camp.RankIdx(r)).ThenByDescending(r => r.missions).FirstOrDefault();
        if (top != null) list.Add(new Slide { rat = top, text = Loc.F("epi.quote", Rank(top), Loc.T(top.name)) });
        return list.ToArray();
    }

    static string Rank(RatRecord r) => Loc.T(CampRules.RANKS[Camp.RankIdx(r)].name);

    static string Story(CampData c, RatRecord r, bool win)
    {
        int seed = (r.id ?? r.name ?? "").Aggregate(17, (h, ch) => h * 31 + ch);
        string home = Loc.T(r.home);
        var s = new List<string>();
        if (!win) s.Add(Loc.T("epi.pow"));
        int si = StoryIndex(r);
        s.Add(Loc.F(si >= 0 && Loc.Has("epi.story." + si) ? "epi.story." + si : "epi.story.none", home));

        // 가운데: 몸 · 관계 · 마음 중 두 개까지
        var mid = new List<string>();
        if (r.lost.Contains("leg")) mid.Add(Loc.T("epi.leg"));
        else if (r.lost.Contains("arm")) mid.Add(Loc.T("epi.arm"));
        if (!string.IsNullOrEmpty(r.sibling))
        {
            var sib = Find(c, r.sibling, out bool dead);
            if (sib != null) mid.Add(Loc.F(dead ? "epi.sib.dead" : "epi.sib.alive", Loc.T(sib.name)));
        }
        foreach (var (kind, key) in new[] { ("comrade", "epi.comrade"), ("mentor", "epi.mentor"), ("pupil", "epi.pupil"), ("feud", "epi.feud") })
        {
            if (mid.Count >= 2) break;
            var id = Camp.Partners(r, kind).FirstOrDefault();
            if (id == null) continue;
            var p = Find(c, id, out bool dead);
            if (p == null) continue;
            mid.Add(Loc.F(kind == "comrade" ? key + (dead ? ".dead" : ".alive") : key, Loc.T(p.name)));
        }
        if (mid.Count < 2 && r.traits.Contains("psychic")) mid.Add(Loc.T("epi.psychic"));
        if (mid.Count < 2 && (r.breaks > 0 || !string.IsNullOrEmpty(r.afflicted) || r.stress >= CampRules.STRESS.shaky)) mid.Add(Loc.T("epi.scarred"));
        s.AddRange(mid.Take(2));

        // 마지막 한 줄
        if (r.kills >= 15) s.Add(Loc.T("epi.end.medals"));
        else if (Camp.RankIdx(r) >= 5) s.Add(Loc.T("epi.end.reunion"));
        else s.Add(Loc.F("epi.end." + ((seed & 0x7fffffff) % 5), home));
        return string.Join(" ", s);
    }

    // 사연은 키(story.N)로 저장되거나, 예전 기록은 한국어 문장 그대로
    static int StoryIndex(RatRecord r)
    {
        for (int i = 0; i < CampRules.STORIES.Length; i++)
            if (CampRules.STORIES[i] == r.story || Loc.T(CampRules.STORIES[i]) == r.story) return i;
        return -1;
    }

    static RatRecord Find(CampData c, string id, out bool dead)
    {
        var r = c.squad.FirstOrDefault(q => q.id == id);
        dead = r == null;
        return r ?? c.fallen.FirstOrDefault(q => q.id == id);
    }
}

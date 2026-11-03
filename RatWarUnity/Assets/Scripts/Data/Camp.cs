// 쥐 전쟁 시뮬 — 아웃게임: 분대 기록 · 모집 · 특성 · 스트레스 · 임무 진행 (웹 camp.js)
// 쥐 하나하나 = 이름 · 얼굴 · 고향 · 사연 · 특성. 죽으면 추모비로. 스트레스는 판을 넘어 남고, 쉬어야 풀림.
// 저장 = persistentDataPath/war_camp_v2.json (웹은 localStorage)
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

[Serializable]
public class RatRecord
{
    public string id, name, home, story;
    public List<string> traits = new List<string>();
    public int face, fur, gear, xp, kills, missions, stress, rested;
    public List<string> nodes = new List<string>(), lost = new List<string>(), prosthetic = new List<string>();
    public string afflicted = "", sibling = "", grief = "";
    public bool drafted;
    public int wounded; // 수비대에서 다쳐 쉬는 남은 턴 (0 = 출전 가능)
    // 획득 특성 누적 · 관계 (같이 출전 횟수 · 전우 · 멘토 · 앙숙)
    public int knifeKills, breaks;
    public List<Bond> bonds = new List<Bond>();
    // 추모비에만
    public string rank = "", cls = "", died = "", how = "";
    public int battle;
    public RatRecord Clone() => JsonUtility.FromJson<RatRecord>(JsonUtility.ToJson(this));
    // 화면에 쓸 글자. 저장값은 테이블 키 (예전 기록은 한국어 문장 그대로 → Loc.T 가 그대로 돌려줌)
    public string Name => Loc.T(name);
    public string Home => Loc.T(home);
    public string Story => Loc.T(story);
}

// 다른 쥐와의 관계. kind: with(같이 출전 n판) · comrade(전우) · mentor(내 멘토) · pupil(내 제자) · feud(앙숙)
[Serializable]
public class Bond { public string kind, id; public int n; }

[Serializable]
public class CampData
{
    public int v = 2, supplies = 60, battles, made, recruited, rerolls, limbs, diff = RULES.defaultDiff; // recruited = 이번 전쟁에서 모집한 수 (모집 값이 오름) · rerolls = 지원자 새로 받은 수 · limbs = 단 의수 · 의족 수 (값이 오름)
    public List<string> done = new List<string>(), deploy = new List<string>();
    public List<RatRecord> squad = new List<RatRecord>(), fallen = new List<RatRecord>(), recruits = new List<RatRecord>();
    public List<int> hired = new List<int>(); // 이번 턴에 뽑아서 잠긴 지원자 칸 (턴이 넘어가면 새 지원자로 열림)
    public WarState war = new WarState();
}

public class TraitDef
{
    public string name, desc; public bool rare, acq; // acq = 모집 때는 안 붙음 (전투 · 관계로 생김)
    public Dictionary<string, float> n = new Dictionary<string, float>();      // 능력치 더하기 · 기타 수치
    public Dictionary<string, float> stressBy;                                 // 이유별 스트레스 배율
    public float Get(string k) => n.TryGetValue(k, out var v) ? v : 0;
}

public class RatStats
{
    public string clsId; public ClassDef cls;
    public int hp, aim, mob, sight, crit, def, dmg, clip, heal, boom, grenades, medkits, charges, psi, whisker;
    public string weapon;
    public float blast; public bool climb, sibling, shaky;
    public List<string> perks, abil;
}

public static class CampRules
{
    public static readonly (string name, int xp, int aim, int hp)[] RANKS =
    {
        // 다음 계급까지 2 · 3 · 5 · 6 · 8 · 10 · 12 — 전투 한 번에 보통 2~5
        ("camp.rank.0", 0, 0, 0), ("camp.rank.1", 2, 3, 0), ("camp.rank.2", 5, 5, 1), ("camp.rank.3", 10, 7, 1),
        ("camp.rank.4", 16, 9, 2), ("camp.rank.5", 24, 11, 2), ("camp.rank.6", 34, 13, 3), ("camp.rank.7", 46, 15, 3),
    };
    public static int RankOf(int xp) { int r = 0; for (int i = 0; i < RANKS.Length; i++) if (xp >= RANKS[i].xp) r = i; return r; }

    public const int RAT_FACES = 7;
    // 털 · 이름 · 고향 · 사연 = 글자 테이블 키 (Tables/ko/camp.json). 쥐 기록엔 키를 저장
    static string[] Keys(string pre, int n) => Enumerable.Range(0, n).Select(i => pre + i).ToArray();
    public static readonly string[] FURS = Keys("camp.fur.", 6);
    public static readonly string[] NAMES = Keys("camp.name.", 80);
    public static readonly string[] HOMES = Keys("camp.home.", 10);
    public static readonly string[] STORIES = Keys("camp.story.", 12);

    static TraitDef T(params (string k, float v)[] n) => new TraitDef { n = n.ToDictionary(x => x.k, x => x.v) };
    // 이름 · 설명 = 글자 테이블 키 camp.trait.<id>.name / .desc
    static Dictionary<string, TraitDef> Keyed(Dictionary<string, TraitDef> d)
    {
        foreach (var kv in d) { kv.Value.name = $"camp.trait.{kv.Key}.name"; kv.Value.desc = $"camp.trait.{kv.Key}.desc"; }
        return d;
    }
    static TraitDef By(this TraitDef t, params (string k, float v)[] s) { t.stressBy = s.ToDictionary(x => x.k, x => x.v); return t; }

    // 특성: 모집 때 1~2개. stress = 스트레스 받는 배율, 나머지는 능력치 더하기
    public static readonly Dictionary<string, TraitDef> TRAITS = Keyed(new Dictionary<string, TraitDef>
    {
        ["brave"] = T(("stress", 0.7f)),
        ["coward"] = T(("stress", 1.3f), ("def", 5)),
        ["veteran"] = T(("aim", 5), ("startStress", 25)),
        ["tough"] = T(("hp", 1)),
        ["marksman"] = T(("aim", 5)),
        ["nimble"] = T(("mob", 1)),
        ["drinker"] = T(("aim", -3), ("unwind", 10)),
        ["homesick"] = T(("homesick", 8)),
        ["pious"] = T(("rest", 2)),
        ["sibling"] = T(),
        ["psychic"] = new TraitDef { rare = true },
        // ── 림월드식 성격 ──
        ["bloodlust"] = T(("killRelief", 15)).By(("gore", 0)),
        ["squeamish"] = T().By(("gore", 2), ("dead", 1.3f)),
        ["pyrophobe"] = T().By(("fire", 3)),
        ["psychopath"] = T().By(("dead", 0), ("gore", 0), ("downed", 0.3f)),
        ["ironwill"] = T(("breakAt", 20)),
        ["nervous"] = T(("breakAt", -20)),
        ["optimist"] = T(("unwind", 10)),
        ["pessimist"] = T(("unwind", -8)),
        ["quick"] = T(("xp", 1.5f)),
        ["slowwit"] = T(("xp", 0.7f)),
        ["lucky"] = T(),
        ["careful"] = T(),
        ["clumsy"] = T(("grenades", 1)),
        ["leader"] = T(),
        ["loner"] = T(),
        ["smoker"] = T(("smoke", 3), ("hp", -1)),
        ["sluggish"] = T(("mob", -1), ("hp", 1)),
        ["sharpears"] = T(("sight", 1)),
        // ── 2026-10-30 확정안 (docs/SKILLS_TRAITS_PLAN.md 3-0) ──
        ["cautious"] = T(),
        ["desperate"] = T(),
        ["brawler"] = T(),
        ["longshot"] = T(),
        ["coverpro"] = T(),
        ["reckless"] = T(),
        ["rage"] = T(),
        ["calm"] = T(),
        ["catphobia"] = T(),
        ["nightowl"] = T(),
        ["bigears"] = T(),
        ["irontooth"] = T(),
        // 전투가 만드는 특성 (치료 불가)
        ["knifer"] = new TraitDef { acq = true },
        ["shellshock"] = new TraitDef { acq = true, stressBy = new Dictionary<string, float> { ["blast"] = 2 } },
        ["avenger"] = new TraitDef { acq = true, n = new Dictionary<string, float> { ["aim"] = 5, ["killRelief"] = 10 } },
        ["broken"] = new TraitDef { acq = true, n = new Dictionary<string, float> { ["breakAt"] = -15 } },
        ["notail"] = new TraitDef { acq = true, n = new Dictionary<string, float> { ["mob"] = -1, ["def"] = 5 } },
        // 관계 (상대는 bonds 에. 같이 출전해야 발동)
        ["comrade"] = new TraitDef { acq = true },
        ["mentor"] = new TraitDef { acq = true },
        ["pupil"] = new TraitDef { acq = true },
        ["feud"] = new TraitDef { acq = true },
    });
    // 같이 가질 수 없는 특성
    static readonly string[][] TRAIT_CLASH =
    {
        new[] { "brave", "coward" }, new[] { "ironwill", "nervous" }, new[] { "optimist", "pessimist" }, new[] { "quick", "slowwit" },
        new[] { "bloodlust", "squeamish" }, new[] { "leader", "loner" }, new[] { "nimble", "sluggish" }, new[] { "careful", "clumsy" },
        new[] { "psychopath", "sibling" }, new[] { "brave", "nervous" }, new[] { "coward", "ironwill" },
        new[] { "brawler", "longshot" }, new[] { "coverpro", "reckless" }, new[] { "rage", "calm" }, new[] { "catphobia", "bloodlust" },
    };
    public static bool Clashes(string a, string b) => TRAIT_CLASH.Any(p => (p[0] == a && p[1] == b) || (p[0] == b && p[1] == a));

    public static class STRESS
    {
        public const int hit = 8, crit = 12, downed = 20, allyDead = 22, siblingDead = 45, gore = 8, suppressed = 6, panicNear = 6;
        public const int kill = -6, win = -12, rest = -35, warcry = -10, blast = 6;
        public const int breakAt = 100, after = 70; // 무너지면 이 값으로 내려감
        public const int shaky = 60;                // 이 위로 출전하면 '불안' (명중 −10)
    }

    // 스트레스 상태 글자 · 색
    public static (string t, Color c) StressState(int s, int cap = STRESS.breakAt)
    {
        if (s >= cap - 15) return (Loc.T("camp.stress.3"), SkillsData.Hex("#ff7aa8"));
        if (s >= STRESS.shaky) return (Loc.T("camp.stress.2"), SkillsData.Hex("#c9a3ff"));
        if (s >= 35) return (Loc.T("camp.stress.1"), SkillsData.Hex("#e8c66a"));
        return (Loc.T("camp.stress.0"), SkillsData.Hex("#9fd6a0"));
    }

    public const int RECRUIT_COST = 35, REFRESH_COST = 8, LIMB_COST = 50, LIMB_PER_FIT = 15, SQUAD_MAX = 12, DEPLOY_MAX = 6;
    public const int KILL_XP_MAX = 3; // 한 판 처치 경험치 상한 (한 마리가 처치를 몰아 먹어 급성장하는 것 막기)
    // 모집 값 = 기본 + 위협 단계마다 + 모집할 때마다 누적 + 지원자 계급마다
    public const int RECRUIT_PER_TIER = 10, RECRUIT_PER_HIRE = 3, RECRUIT_PER_RANK = 15;
    public const int REFRESH_PER_ROLL = 4, REFRESH_PER_TIER = 5; // 지원자 새로 받기 값 = 기본 + 이번 전쟁에서 받은 횟수마다 + 위협 단계마다
    public const float VET_PER_TIER = 0.15f; // 위협 2단계부터 단계마다 지원자가 계급을 달고 올 확률 +15%
}

public static class Camp
{
    static string PathOf => SaveSlots.CampPath(SaveSlots.Cur); // 지금 슬롯
    static int seq = 1;
    static T Pick<T>(IList<T> a) => a[UnityEngine.Random.Range(0, a.Count)];
    static float Rnd => UnityEngine.Random.value;

    // 새 쥐 한 마리 (모집 후보 · 처음 분대)
    public static RatRecord NewRat(CampData c, bool psychic = false)
    {
        // 이름 겹침 검사는 키 · 글자 둘 다 (예전 저장은 한국어 이름)
        var used = new HashSet<string>(c.squad.Concat(c.fallen).Concat(c.recruits).SelectMany(r => new[] { r.name, r.Name }));
        bool Free(string n) => !used.Contains(n) && !used.Contains(Loc.T(n));
        // 분대 · 추모비 · 지원자와 겹치지 않는 이름 중 아무거나. 80개가 다 쓰이면 '신병 n'
        var free = CampRules.NAMES.Where(Free).ToList();
        var name = free.Count > 0 ? Pick(free) : Loc.F("camp.name.recruit", c.made + 1);
        var traits = new List<string>();
        if (psychic || Rnd < 0.15f) traits.Add("psychic");
        var pool = CampRules.TRAITS.Keys.Where(t => !CampRules.TRAITS[t].rare && !CampRules.TRAITS[t].acq && t != "sibling").ToList();
        int want = (Rnd < 0.35f ? 3 : 2) + (traits.Contains("psychic") ? 1 : 0);
        for (int guard = 0; traits.Count < want && guard < 50; guard++)
        {
            var t = Pick(pool);
            if (!traits.Contains(t) && !traits.Any(o => CampRules.Clashes(o, t))) traits.Add(t);
        }
        c.made++;
        return new RatRecord
        {
            id = "r" + DateTime.Now.Ticks.ToString("x") + (seq++),
            name = name, home = Pick(CampRules.HOMES), story = Pick(CampRules.STORIES), traits = traits,
            face = UnityEngine.Random.Range(0, CampRules.RAT_FACES), fur = UnityEngine.Random.Range(0, CampRules.FURS.Length), gear = UnityEngine.Random.Range(0, 3),
            stress = traits.Contains("veteran") ? 25 : UnityEngine.Random.Range(0, 10),
        };
    }

    // 새 전쟁: 난이도는 여기서 정해지고 전쟁 동안 고정 (시작 보급품도 난이도 따라)
    public static CampData Fresh(int diff = RULES.defaultDiff)
    {
        var c = new CampData { diff = diff, supplies = RULES.DiffOf(diff).supplies };
        for (int i = 0; i < 6; i++) c.squad.Add(NewRat(c, i == 5));
        // 처음 분대에 형제 한 쌍
        foreach (var i in new[] { 1, 2 })
            c.squad[i].traits = new[] { "sibling" }.Concat(c.squad[i].traits.Where(t => !CampRules.Clashes(t, "sibling") && t != "sibling")).Take(3).ToList();
        c.squad[1].sibling = c.squad[2].id; c.squad[2].sibling = c.squad[1].id;
        c.squad[2].home = c.squad[1].home;
        c.deploy = c.squad.Take(CampRules.DEPLOY_MAX).Select(r => r.id).ToList();
        RollRecruits(c);
        War.Ensure(c);
        return c;
    }

    public static CampData Load()
    {
        try { if (File.Exists(PathOf)) { var c = JsonUtility.FromJson<CampData>(File.ReadAllText(PathOf)); if (c != null && c.v == 2) return Migrate(c); } }
        catch (Exception e) { Debug.LogWarning("저장 읽기 실패 " + e.Message); }
        return null;
    }
    // 예전 저장: 없어진 특성(치즈 귀신 등) 빼고, 새 필드 채움
    public static CampData Migrate(CampData c)
    {
        foreach (var r in c.squad.Concat(c.fallen).Concat(c.recruits))
        {
            r.traits.RemoveAll(t => !CampRules.TRAITS.ContainsKey(t));
            if (r.bonds == null) r.bonds = new List<Bond>();
        }
        if (c.hired == null) c.hired = new List<int>();
        War.Ensure(c); // 예전 저장 = 전략 지도 새로 시작
        return c;
    }
    public static void Save(CampData c) { try { File.WriteAllText(PathOf, JsonUtility.ToJson(c)); } catch (Exception e) { Debug.LogWarning("저장 실패 " + e.Message); } }
    public static void Reset() { try { if (File.Exists(PathOf)) File.Delete(PathOf); } catch { /* 무시 */ } }
    public static void RollRecruits(CampData c) { c.recruits = new List<RatRecord> { NewRecruit(c), NewRecruit(c), NewRecruit(c) }; c.hired = new List<int>(); }
    // 턴 넘김: 뽑아서 잠긴 칸만 새 지원자로 열림 (모병소를 쥐고 있으면 전부 새로)
    public static void Unlock(CampData c)
    {
        foreach (var i in c.hired) if (i < c.recruits.Count) c.recruits[i] = NewRecruit(c);
        c.hired = new List<int>();
    }
    public static bool Locked(CampData c, int i) => c.hired != null && c.hired.Contains(i);
    // 모집 지원자: 위협 단계가 높을수록 계급을 단 고참이 옴 (단계 t → 일병 ~ 최대 t 계급, 하사까지). 대신 더 비쌈
    static RatRecord NewRecruit(CampData c)
    {
        var r = NewRat(c);
        int tier = c.war != null && c.war.own.Count == War.N ? War.Threat(c) : 1;
        if (tier > 1 && Rnd < (tier - 1) * CampRules.VET_PER_TIER) r.xp = CampRules.RANKS[UnityEngine.Random.Range(1, Mathf.Min(tier, 4) + 1)].xp;
        return r;
    }
    public static int RecruitCost(CampData c, RatRecord r)
    {
        int tier = c.war != null && c.war.own.Count == War.N ? War.Threat(c) : 1;
        return CampRules.RECRUIT_COST + (tier - 1) * CampRules.RECRUIT_PER_TIER + c.recruited * CampRules.RECRUIT_PER_HIRE + RankIdx(r) * CampRules.RECRUIT_PER_RANK;
    }

    // ── 한 쥐의 계산된 정보 ──
    public static int RankIdx(RatRecord r) => CampRules.RankOf(r.xp);
    public static int Points(RatRecord r) => 1 + RankIdx(r) - r.nodes.Count; // 신병 1점 + 진급마다 1점
    public static string ClassOf(RatRecord r)
    {
        var cls = SkillsData.ClassOf(r.nodes);
        // 주술사는 '영매'만. 아니면 가장 많이 찍은 단일 갈래로
        if (cls == "shaman" && !r.traits.Contains("psychic"))
        {
            var c = SkillsData.Counts(r.nodes);
            int top = Array.IndexOf(c, c.Max());
            cls = SkillsData.RAT_CLASSES.Values.FirstOrDefault(C => C.from.Length == 1 && C.from[0] == top)?.id ?? "recruit";
        }
        return cls;
    }
    public static bool Fit(RatRecord r) => r.lost == null || r.lost.Count == 0;   // 팔다리 멀쩡
    public static bool Ready(RatRecord r) => Fit(r) && r.wounded <= 0;           // 출전 가능 (수비대 부상 아님)
    // 추모비로 (전투 · 수비대 공통). 형제를 잃은 쥐는 스트레스 +30
    //   rank · how · grief 는 키로 저장 (화면에서 Loc.T)
    public static RatRecord Bury(CampData c, RatRecord r, string died, string how, int kills = 0)
    {
        c.squad.Remove(r); c.deploy.Remove(r.id);
        var f = r.Clone();
        f.rank = CampRules.RANKS[RankIdx(r)].name; f.cls = SkillsData.RAT_CLASSES[ClassOf(r)].name; f.died = died; f.battle = c.battles; f.how = how ?? "camp.how.kia"; f.kills = r.kills + kills;
        c.fallen.Add(f);
        var sib = c.squad.FirstOrDefault(q => q.sibling == r.id);
        if (sib != null) { sib.stress = Mathf.Min(99, sib.stress + 30); sib.grief = r.name; }
        return f;
    }
    public static float TraitSum(RatRecord r, string k) => r == null ? 0 : r.traits.Sum(t => CampRules.TRAITS[t].Get(k));
    public static float StressMul(RatRecord r, string why)
    {
        float m = 1;
        foreach (var t in r.traits)
        {
            var T = CampRules.TRAITS[t];
            m *= T.n.TryGetValue("stress", out var s) ? s : 1;
            if (why != null && T.stressBy != null && T.stressBy.TryGetValue(why, out var b)) m *= b;
        }
        return m * (string.IsNullOrEmpty(r.afflicted) ? 1 : 1.25f);
    }
    public static int BreakAt(RatRecord r) => CampRules.STRESS.breakAt + (int)TraitSum(r, "breakAt");
    public static bool Has(RatRecord r, string t) => r != null && r.traits.Contains(t);
    static float XpMul(RatRecord r) => r.traits.Aggregate(1f, (m, t) => m * (CampRules.TRAITS[t].n.TryGetValue("xp", out var v) ? v : 1));

    // 전투용 능력치: 병종 기본 + 계급 + 스킬판 + 특성
    public static RatStats Stats(RatRecord r, List<string> squadIds = null)
    {
        var clsId = ClassOf(r); var C = SkillsData.RAT_CLASSES[clsId]; var R = CampRules.RANKS[RankIdx(r)]; var T = SkillsData.TotalsOf(r.nodes);
        int sibling = !string.IsNullOrEmpty(r.sibling) && squadIds != null && squadIds.Contains(r.sibling) ? 8 : 0;
        int shaky = r.stress >= CampRules.STRESS.shaky ? -10 : 0;
        var abil = C.abil.Concat(T.abil).Distinct().ToList();
        var perks = T.perks.ToList();
        if (clsId == "engineer" || r.traits.Contains("irontooth")) perks.Add("gnawer");
        // 순서: 사격 먼저, 경계 끝
        abil = abil.Select((a, i) => (a, i)).OrderBy(x => x.a == "shoot" ? 0 : x.a == "overwatch" ? 2 : 1).ThenBy(x => x.i).Select(x => x.a).ToList();
        int ts(string k) => (int)TraitSum(r, k);
        int tt(string k) => (int)T.Get(k);
        return new RatStats
        {
            clsId = clsId, cls = C,
            hp = C.hp + R.hp + tt("hp") + ts("hp"),
            aim = C.aim + R.aim + tt("aim") + ts("aim") + sibling + shaky,
            mob = C.mob + tt("mob") + ts("mob") - (r.lost != null && r.lost.Contains("leg") ? 1 : 0),
            sight = C.sight + tt("sight") + ts("sight"),
            crit = C.crit + tt("crit") + ts("crit"), def = C.def + tt("def") + ts("def"),
            dmg = tt("dmg"), clip = tt("clip"), heal = tt("heal"), blast = C.blast + T.Get("blast"), boom = tt("boom"),
            grenades = C.grenades + tt("grenades") + (C.grenades > 0 || T.Get("grenades") > 0 ? ts("grenades") : 0), medkits = C.medkits + tt("medkits"),
            charges = C.charges + tt("charges"),
            psi = C.psi, climb = C.climb, whisker = C.whisker,
            weapon = perks.Contains("flamer") ? "flamer" : C.weapon,
            perks = perks, abil = abil, sibling = sibling > 0, shaky = shaky < 0,
        };
    }

    // ── 모집 · 치료 ──
    public static bool Recruit(CampData c, int i)
    {
        if (i >= c.recruits.Count || Locked(c, i) || c.squad.Count >= CampRules.SQUAD_MAX) return false;
        int cost = RecruitCost(c, c.recruits[i]);
        if (c.supplies < cost) return false;
        c.supplies -= cost; c.recruited++;
        c.squad.Add(c.recruits[i]);
        c.recruits[i] = NewRecruit(c); // 자리 채움 (잠겨 있어 이번 턴엔 못 뽑음)
        c.hired.Add(i);
        Save(c);
        return true;
    }
    public static int RefreshCost(CampData c)
    {
        int tier = c.war != null && c.war.own.Count == War.N ? War.Threat(c) : 1;
        return CampRules.REFRESH_COST + c.rerolls * CampRules.REFRESH_PER_ROLL + (tier - 1) * CampRules.REFRESH_PER_TIER;
    }
    // 지원자 새로 받기: 잠긴 칸도 다시 열림
    public static bool Refresh(CampData c)
    {
        int cost = RefreshCost(c);
        if (c.supplies < cost) return false;
        c.supplies -= cost; c.rerolls++; RollRecruits(c); Save(c); return true;
    }
    // 의수 · 의족 값: 이번 전쟁에서 달 때마다 +15 (전략 지도에서 공방을 쥐고 있으면 절반)
    public static int LimbCost(CampData c)
    {
        int cost = CampRules.LIMB_COST + c.limbs * CampRules.LIMB_PER_FIT;
        return War.Holds(c, "workshop") ? cost / 2 : cost;
    }
    public static bool FitLimb(CampData c, RatRecord r)
    {
        if (c.supplies < LimbCost(c) || r.lost.Count == 0) return false;
        c.supplies -= LimbCost(c); c.limbs++;
        r.prosthetic = r.prosthetic.Concat(r.lost).Distinct().ToList();
        r.lost.Clear();
        Save(c);
        return true;
    }
    public static bool TakeNode(CampData c, RatRecord r, string k)
    {
        if (Points(r) <= 0 || !SkillsData.CanTake(r.nodes, k)) return false;
        r.nodes.Add(k); Save(c); return true;
    }

    public class BattleResult
    {
        public string id, broke, how, hitByFrenzy; public bool alive, downed, legBlast, painkiller; public int kills, stress, knifeKills, nearBlasts;
        public List<string> hurt, severed;
    }
    public class ReportRow { public RatRecord r; public bool dead, promoted, downed; public string how, broke; public int kills; public List<string> hurt = new List<string>(), severed = new List<string>(), gained = new List<string>(); }

    // ── 관계 ──
    public static Bond BondOf(RatRecord r, string kind, string id) => r?.bonds?.FirstOrDefault(b => b.kind == kind && b.id == id);
    public static bool Bonded(RatRecord r, string id, string kind) => !string.IsNullOrEmpty(id) && BondOf(r, kind, id) != null;
    public static IEnumerable<string> Partners(RatRecord r, params string[] kinds) => r?.bonds == null ? Enumerable.Empty<string>() : r.bonds.Where(b => kinds.Contains(b.kind)).Select(b => b.id);
    static void Bind(RatRecord a, string kind, RatRecord b) { if (BondOf(a, kind, b.id) == null) a.bonds.Add(new Bond { kind = kind, id = b.id }); }
    static void AddTrait(RatRecord r, string t) { if (!r.traits.Contains(t)) r.traits.Add(t); }
    // 획득 특성 굴림: 붙으면 보고서에 남김
    static bool Gain(RatRecord r, string t, float chance, Dictionary<string, ReportRow> rows)
    {
        if (r.traits.Contains(t) || UnityEngine.Random.value >= chance) return false;
        r.traits.Add(t);
        if (rows.TryGetValue(r.id, out var row)) row.gained.Add(t);
        return true;
    }

    // 판 끝: 전투 경험 · 관계로 생기는 특성 (docs/SKILLS_TRAITS_PLAN.md 3-0 확률)
    static void Acquire(CampData c, List<BattleResult> res, HashSet<string> deadIds, Dictionary<string, ReportRow> rows, Dictionary<string, int> rankBefore)
    {
        var alive = new List<(RatRecord r, BattleResult x)>();
        foreach (var x in res) { var r = c.squad.FirstOrDefault(q => q.id == x.id); if (r != null) alive.Add((r, x)); }
        foreach (var (r, x) in alive)
        {
            int before = r.knifeKills; r.knifeKills += x.knifeKills;
            for (int k = Mathf.Max(before + 1, 3); k <= r.knifeKills; k++) if (Gain(r, "knifer", 0.6f, rows)) break;
            if (x.nearBlasts >= 2) Gain(r, "shellshock", 0.35f, rows);
            if (!string.IsNullOrEmpty(x.broke)) { r.breaks++; if (r.breaks >= 3) Gain(r, "broken", 0.5f, rows); }
            if (x.legBlast) Gain(r, "notail", 0.15f, rows);
            if (deadIds.Contains(r.sibling ?? "") || Partners(r, "comrade", "mentor", "pupil").Any(deadIds.Contains)) Gain(r, "avenger", 0.7f, rows);
            if (x.painkiller) r.stress = Mathf.Min(99, r.stress + 10);
            var foe = alive.FirstOrDefault(o => o.r.id == x.hitByFrenzy).r;
            if (foe != null && foe != r && !Bonded(r, foe.id, "feud") && UnityEngine.Random.value < 0.5f)
            {
                Bind(r, "feud", foe); Bind(foe, "feud", r); AddTrait(r, "feud"); AddTrait(foe, "feud");
                if (rows.TryGetValue(r.id, out var ra)) ra.gained.Add("feud");
                if (rows.TryGetValue(foe.id, out var rb)) rb.gained.Add("feud");
            }
        }
        // 같이 출전한 쌍
        for (int i = 0; i < alive.Count; i++)
            for (int j = i + 1; j < alive.Count; j++)
            {
                var a = alive[i].r; var b = alive[j].r;
                var wa = BondOf(a, "with", b.id) ?? new Bond { kind = "with", id = b.id };
                if (!a.bonds.Contains(wa)) a.bonds.Add(wa);
                var wb = BondOf(b, "with", a.id) ?? new Bond { kind = "with", id = a.id };
                if (!b.bonds.Contains(wb)) b.bonds.Add(wb);
                wa.n++; wb.n = wa.n;
                // x 가 y 를 kx 로 보고 tx 특성을, y 는 x 를 ky 로 보고 ty 특성을 가짐
                void Pair(RatRecord x, string kx, string tx, RatRecord y, string ky, string ty)
                {
                    Bind(x, kx, y); Bind(y, ky, x); AddTrait(x, tx); AddTrait(y, ty);
                    if (rows.TryGetValue(x.id, out var rx)) rx.gained.Add(tx);
                    if (rows.TryGetValue(y.id, out var ry)) ry.gained.Add(ty);
                }
                if (wa.n >= 5 && !Bonded(a, b.id, "comrade") && UnityEngine.Random.value < 0.2f) Pair(a, "comrade", "comrade", b, "comrade", "comrade");
                // 노병 + 이병 3판 → 멘토 / 제자
                RatRecord mentor = a.traits.Contains("veteran") && rankBefore[b.id] == 0 ? a : b.traits.Contains("veteran") && rankBefore[a.id] == 0 ? b : null;
                if (mentor != null && wa.n >= 3)
                {
                    var pupil = mentor == a ? b : a;
                    if (!Bonded(pupil, mentor.id, "mentor") && UnityEngine.Random.value < 0.25f) Pair(pupil, "mentor", "pupil", mentor, "pupil", "mentor");
                }
            }
    }
    public class SettleOut { public int reward, drafted; public List<ReportRow> report; }

    // ── 판 끝: 결과 반영 ──
    public static SettleOut Settle(CampData c, MissionDef mission, bool win, List<BattleResult> res)
    {
        c.battles++;
        var rankBefore = c.squad.ToDictionary(q => q.id, RankIdx); // 멘토 · 제자는 출전 전 계급으로
        int reward = win ? mission.reward : Mathf.FloorToInt(mission.reward * 0.2f);
        c.supplies += reward;
        if (win && !c.done.Contains(mission.id) && !War.IsWar(mission)) c.done.Add(mission.id);
        var report = new List<ReportRow>();
        var deployed = new HashSet<string>(res.Select(x => x.id));
        foreach (var x in res)
        {
            var r = c.squad.FirstOrDefault(q => q.id == x.id);
            if (r == null) continue;
            bool survived = x.alive || (x.downed && win);
            if (!survived)
            {
                var f = Bury(c, r, mission.name, x.how, x.kills);
                report.Add(new ReportRow { r = f, dead = true, how = x.how });
                continue;
            }
            int before = RankIdx(r);
            float pupilMul = Partners(r, "mentor").Any(deployed.Contains) ? 1.5f : 1;
            r.xp += Mathf.RoundToInt((Mathf.Min(x.kills, CampRules.KILL_XP_MAX) + 1 + (win ? 1 : 0)) * XpMul(r) * pupilMul);
            r.kills += x.kills; r.missions++;
            r.stress = Mathf.Clamp(x.stress + (win ? CampRules.STRESS.win : 0) - (int)TraitSum(r, "unwind"), 0, 99);
            if (!string.IsNullOrEmpty(x.broke)) r.afflicted = x.broke;
            r.rested = 0;
            if (x.severed != null && x.severed.Count > 0) r.lost = r.lost.Concat(x.severed).Distinct().ToList();
            report.Add(new ReportRow { r = r, promoted = RankIdx(r) > before, kills = x.kills, downed = x.downed, hurt = x.hurt ?? new List<string>(), severed = x.severed ?? new List<string>(), broke = x.broke });
        }
        var rows = report.Where(x => !x.dead).GroupBy(x => x.r.id).ToDictionary(g => g.Key, g => g.First());
        Acquire(c, res.Where(x => rows.ContainsKey(x.id)).ToList(), new HashSet<string>(report.Where(x => x.dead).Select(x => x.r.id)), rows, rankBefore);
        // 출전 안 한 쥐는 쉼: 스트레스 풀림, 두 판 쉬면 정신 이상도 나음 (수비대로 나간 쥐는 못 쉼)
        foreach (var r in c.squad)
        {
            if (deployed.Contains(r.id) || War.Posted(c, r.id) >= 0) continue;
            float rest = TraitSum(r, "rest");
            r.stress = Mathf.Max(0, r.stress + (int)(CampRules.STRESS.rest * (rest != 0 ? rest : 1)));
            r.rested++;
            if (r.rested >= 2) r.afflicted = "";
        }
        c.deploy = c.deploy.Where(id => c.squad.Any(r => r.id == id)).ToList();
        // 출전할 쥐가 4마리도 안 남으면 징집병이 공짜로 옴 (전멸해도 캠페인은 이어짐)
        int drafted = 0;
        while (c.squad.Count(Fit) < 4 && c.squad.Count < CampRules.SQUAD_MAX) { var r = NewRat(c); r.drafted = true; c.squad.Add(r); drafted++; }
        foreach (var r in c.squad) if (c.deploy.Count < CampRules.DEPLOY_MAX && Ready(r) && !c.deploy.Contains(r.id) && r.drafted) c.deploy.Add(r.id);
        // 전략 지도: 칸 주인 바꾸고 턴 넘김
        if (War.IsWar(mission)) War.AfterBattle(c, mission, win);
        Save(c);
        return new SettleOut { reward = reward, report = report, drafted = drafted };
    }

    // 지금 열린 임무 (깬 것 + 다음 하나)
    public static List<MissionDef> Unlocked(CampData c) => GameData.MISSIONS.Where((m, i) => i == 0 || c.done.Contains(GameData.MISSIONS[i - 1].id)).ToList();
    public static MissionDef Current(CampData c) => GameData.MISSIONS.FirstOrDefault(m => !c.done.Contains(m.id)) ?? GameData.MISSIONS[GameData.MISSIONS.Length - 1];
}

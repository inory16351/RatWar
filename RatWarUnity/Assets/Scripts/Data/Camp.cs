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
    // 획득 특성 누적 · 관계 (같이 출전 횟수 · 전우 · 멘토 · 앙숙)
    public int knifeKills, breaks;
    public List<Bond> bonds = new List<Bond>();
    // 추모비에만
    public string rank = "", cls = "", died = "", how = "";
    public int battle;
    public RatRecord Clone() => JsonUtility.FromJson<RatRecord>(JsonUtility.ToJson(this));
}

// 다른 쥐와의 관계. kind: with(같이 출전 n판) · comrade(전우) · mentor(내 멘토) · pupil(내 제자) · feud(앙숙)
[Serializable]
public class Bond { public string kind, id; public int n; }

[Serializable]
public class CampData
{
    public int v = 2, supplies = 60, battles, made, diff = RULES.defaultDiff;
    public List<string> done = new List<string>(), deploy = new List<string>();
    public List<RatRecord> squad = new List<RatRecord>(), fallen = new List<RatRecord>(), recruits = new List<RatRecord>();
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
        ("이병", 0, 0, 0), ("일병", 2, 3, 0), ("상병", 5, 5, 1), ("병장", 9, 7, 1),
        ("하사", 14, 9, 2), ("중사", 20, 11, 2), ("상사", 27, 13, 3), ("원사", 35, 15, 3),
    };
    public static int RankOf(int xp) { int r = 0; for (int i = 0; i < RANKS.Length; i++) if (xp >= RANKS[i].xp) r = i; return r; }

    public const int RAT_FACES = 7;
    public static readonly string[] FURS = { "갈색", "검은 털", "흰 털", "잿빛", "붉은 털", "누런 털" };
    public static readonly string[] NAMES = { "쥬드", "미르코", "레나", "토비", "아벨", "사샤", "니코", "이다", "파벨", "루카", "엘사", "보리스", "야나", "오토", "미샤", "한스", "마리", "코스챠", "일리야", "베라", "폴", "안나", "그레타", "빅토르", "루드비크", "조피아", "타냐", "에밀", "유리", "카밀" };
    public static readonly string[] HOMES = { "방앗간 마을", "항구 창고", "치즈 공장 뒷골목", "밀밭 둑", "성당 지하", "기차역 하수구", "빵집 다락", "포도밭 돌담", "광산촌", "강가 물레방아" };
    public static readonly string[] STORIES =
    {
        "딸에게 매일 편지를 쓴다. 아직 한 통도 못 부쳤다.",
        "전쟁 전엔 빵을 구웠다. 지금도 주머니에 밀가루가 묻어 있다.",
        "형을 찾으러 입대했다.",
        "고양이에게 가족을 잃었다. 말이 없다.",
        "노래를 잘한다. 밤마다 참호에서 흥얼거린다.",
        "첫 월급으로 어머니에게 치즈 한 덩이를 보냈다.",
        "약혼녀의 사진을 철모 안에 붙여 다닌다.",
        "열여섯이라고 속이고 입대했다. 사실 열넷이다.",
        "전쟁이 끝나면 바다를 보러 가겠다고 한다.",
        "시계 수리공이었다. 손이 아직 떨리지 않는다.",
        "도박 빚을 갚으러 왔다. 운이 좋다고 믿는다.",
        "교회 성가대 출신. 기도 없이는 출전하지 않는다.",
    };

    static TraitDef T(string name, string desc, params (string k, float v)[] n) => new TraitDef { name = name, desc = desc, n = n.ToDictionary(x => x.k, x => x.v) };
    static TraitDef By(this TraitDef t, params (string k, float v)[] s) { t.stressBy = s.ToDictionary(x => x.k, x => x.v); return t; }

    // 특성: 모집 때 1~2개. stress = 스트레스 받는 배율, 나머지는 능력치 더하기
    public static readonly Dictionary<string, TraitDef> TRAITS = new Dictionary<string, TraitDef>
    {
        ["brave"] = T("용감함", "스트레스 −30%", ("stress", 0.7f)),
        ["coward"] = T("겁쟁이", "스트레스 +30% · 회피 +5", ("stress", 1.3f), ("def", 5)),
        ["veteran"] = T("노병", "명중 +5 · 처음 스트레스 25 · 경계 사격 감점 절반", ("aim", 5), ("startStress", 25)),
        ["tough"] = T("튼튼함", "체력 +1", ("hp", 1)),
        ["marksman"] = T("사격 재능", "명중 +5", ("aim", 5)),
        ["nimble"] = T("날쌤", "이동 +1", ("mob", 1)),
        ["drinker"] = T("술꾼", "판 끝나면 스트레스 −10 · 명중 −3", ("aim", -3), ("unwind", 10)),
        ["homesick"] = T("향수병", "출전할 때마다 스트레스 +8", ("homesick", 8)),
        ["pious"] = T("신앙심", "쉴 때 스트레스 두 배로 풀림 · 차례 시작에 스트레스 60 넘으면 기도 (−5)", ("rest", 2)),
        ["sibling"] = T("형제", "같이 출전하면 명중 +8. 한쪽이 죽으면…"),
        ["psychic"] = new TraitDef { name = "영매", desc = "정신의 목소리를 들음 — 주술사가 될 수 있음", rare = true },
        // ── 림월드식 성격 ──
        ["bloodlust"] = T("피에 굶주림", "처치하면 스트레스 −15 · 피 · 절단을 봐도 아무렇지 않음", ("killRelief", 15)).By(("gore", 0)),
        ["squeamish"] = T("비위 약함", "피 · 절단을 보면 스트레스 두 배").By(("gore", 2), ("dead", 1.3f)),
        ["pyrophobe"] = T("불 공포증", "불길 근처에서 스트레스 세 배").By(("fire", 3)),
        ["psychopath"] = T("사이코패스", "동료가 죽어도 흔들리지 않음 · 아무도 이 쥐를 좋아하지 않음").By(("dead", 0), ("gore", 0), ("downed", 0.3f)),
        ["ironwill"] = T("강철 의지", "스트레스 120 에서야 무너짐", ("breakAt", 20)),
        ["nervous"] = T("예민함", "스트레스 80 에서 무너짐", ("breakAt", -20)),
        ["optimist"] = T("낙천가", "판 끝나면 스트레스 −10", ("unwind", 10)),
        ["pessimist"] = T("비관론자", "판 끝나면 스트레스 +8", ("unwind", -8)),
        ["quick"] = T("빠른 학습", "경험치 1.5배", ("xp", 1.5f)),
        ["slowwit"] = T("굼뜬 머리", "경험치 0.7배", ("xp", 0.7f)),
        ["lucky"] = T("행운아", "전투당 1번, 죽을 피해를 체력 1로 버팀"),
        ["careful"] = T("꼼꼼함", "팔을 다쳐도 재장전을 실수하지 않음"),
        ["clumsy"] = T("덤벙댐", "재장전 10% 실패 · 수류탄 하나 더 · 수류탄이 15% 확률로 1칸 빗나감", ("grenades", 1)),
        ["leader"] = T("타고난 지휘관", "3칸 안 아군 명중 +5 · 스트레스 −20%"),
        ["loner"] = T("외톨이", "3칸 안에 아군이 없으면 명중 +10"),
        ["smoker"] = T("골초", "매 턴 스트레스 −3 · 체력 −1", ("smoke", 3), ("hp", -1)),
        ["sluggish"] = T("느림보", "이동 −1 · 체력 +1", ("mob", -1), ("hp", 1)),
        ["sharpears"] = T("밝은 귀", "시야 +1", ("sight", 1)),
        // ── 2026-10-30 확정안 (docs/SKILLS_TRAITS_PLAN.md 3-0) ──
        ["cautious"] = T("신중함", "이동 안 한 턴 명중 +10, 이동한 턴 −5"),
        ["desperate"] = T("절박함", "체력 절반 이하면 명중 +10 · 치명 +10"),
        ["brawler"] = T("근접 광", "4칸 안 명중 +10, 9칸 밖 −10"),
        ["longshot"] = T("원거리 본능", "9칸 밖 명중 +10, 3칸 안 −10"),
        ["coverpro"] = T("엄폐 장인", "엄폐 중 회피 +10"),
        ["reckless"] = T("무모함", "엄폐 없을 때 명중 +10 · 받는 스트레스 −20%"),
        ["rage"] = T("분노 조절 장애", "맞으면 다음 사격 명중 +15 · 무너지면 광란 확률 두 배"),
        ["calm"] = T("평온함", "무너져도 공황 · 광란 · 도주 대신 멍함 (행동력 1만 잃음)"),
        ["catphobia"] = T("고양이 공포증", "3칸 안에 고양이가 있으면 차례마다 스트레스 +5"),
        ["nightowl"] = T("어둠을 좋아함", "고양이 눈에 안 띌 때 (안개 속 · 매복) 스트레스가 안 쌓임"),
        ["bigears"] = T("큰 귀", "경계 범위 +1 · 폭발에 휘말리면 다음 차례 행동력 −1"),
        ["irontooth"] = T("쇠 이빨", "철조망을 갉으며 지나감 (공병처럼)"),
        // 전투가 만드는 특성 (치료 불가)
        ["knifer"] = new TraitDef { name = "칼잡이", desc = "칼 명중 +10 · 치명 +10", acq = true },
        ["shellshock"] = new TraitDef { name = "포탄 충격", desc = "폭발 근처에서 스트레스 두 배", acq = true, stressBy = new Dictionary<string, float> { ["blast"] = 2 } },
        ["avenger"] = new TraitDef { name = "복수자", desc = "명중 +5 · 처치하면 스트레스 −10", acq = true, n = new Dictionary<string, float> { ["aim"] = 5, ["killRelief"] = 10 } },
        ["broken"] = new TraitDef { name = "망가진", desc = "무너지는 기준 −15", acq = true, n = new Dictionary<string, float> { ["breakAt"] = -15 } },
        ["notail"] = new TraitDef { name = "꼬리 잘린 쥐", desc = "이동 −1 · 회피 +5", acq = true, n = new Dictionary<string, float> { ["mob"] = -1, ["def"] = 5 } },
        // 관계 (상대는 bonds 에. 같이 출전해야 발동)
        ["comrade"] = new TraitDef { name = "전우애", desc = "전우가 3칸 안이면 명중 +5 · 받는 스트레스 −20%. 전우가 죽으면 크게 흔들림", acq = true },
        ["mentor"] = new TraitDef { name = "멘토", desc = "제자를 이끔 — 제자가 3칸 안이면 제자 명중 +8 · 경험치 1.5배", acq = true },
        ["pupil"] = new TraitDef { name = "제자", desc = "멘토가 3칸 안이면 명중 +8 · 같이 출전하면 경험치 1.5배", acq = true },
        ["feud"] = new TraitDef { name = "앙숙", desc = "앙숙과 3칸 안이면 차례마다 스트레스 +3", acq = true },
    };
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
        if (s >= cap - 15) return ("붕괴 직전", SkillsData.Hex("#ff7aa8"));
        if (s >= STRESS.shaky) return ("불안", SkillsData.Hex("#c9a3ff"));
        if (s >= 35) return ("긴장", SkillsData.Hex("#e8c66a"));
        return ("안정", SkillsData.Hex("#9fd6a0"));
    }

    public const int RECRUIT_COST = 35, REFRESH_COST = 8, LIMB_COST = 50, SQUAD_MAX = 12, DEPLOY_MAX = 6;
}

public static class Camp
{
    static string PathOf => System.IO.Path.Combine(Application.persistentDataPath, "war_camp_v2.json");
    static int seq = 1;
    static T Pick<T>(IList<T> a) => a[UnityEngine.Random.Range(0, a.Count)];
    static float Rnd => UnityEngine.Random.value;

    // 새 쥐 한 마리 (모집 후보 · 처음 분대)
    public static RatRecord NewRat(CampData c, bool psychic = false)
    {
        var used = new HashSet<string>(c.squad.Concat(c.fallen).Concat(c.recruits).Select(r => r.name));
        var name = CampRules.NAMES.FirstOrDefault(n => !used.Contains(n) && Rnd < 0.5f) ?? CampRules.NAMES.FirstOrDefault(n => !used.Contains(n)) ?? "신병 " + (c.made + 1);
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

    public static CampData Fresh()
    {
        var c = new CampData();
        for (int i = 0; i < 6; i++) c.squad.Add(NewRat(c, i == 5));
        // 처음 분대에 형제 한 쌍
        foreach (var i in new[] { 1, 2 })
            c.squad[i].traits = new[] { "sibling" }.Concat(c.squad[i].traits.Where(t => !CampRules.Clashes(t, "sibling") && t != "sibling")).Take(3).ToList();
        c.squad[1].sibling = c.squad[2].id; c.squad[2].sibling = c.squad[1].id;
        c.squad[2].home = c.squad[1].home;
        c.deploy = c.squad.Take(CampRules.DEPLOY_MAX).Select(r => r.id).ToList();
        RollRecruits(c);
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
        return c;
    }
    public static void Save(CampData c) { try { File.WriteAllText(PathOf, JsonUtility.ToJson(c)); } catch (Exception e) { Debug.LogWarning("저장 실패 " + e.Message); } }
    public static void Reset() { try { if (File.Exists(PathOf)) File.Delete(PathOf); } catch { /* 무시 */ } }
    static void RollRecruits(CampData c) => c.recruits = new List<RatRecord> { NewRat(c), NewRat(c), NewRat(c) };

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
    public static bool Ready(RatRecord r) => r.lost == null || r.lost.Count == 0;
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
        if (c.supplies < CampRules.RECRUIT_COST || c.squad.Count >= CampRules.SQUAD_MAX || i >= c.recruits.Count) return false;
        c.supplies -= CampRules.RECRUIT_COST;
        c.squad.Add(c.recruits[i]);
        c.recruits[i] = NewRat(c);
        Save(c);
        return true;
    }
    public static bool Refresh(CampData c)
    {
        if (c.supplies < CampRules.REFRESH_COST) return false;
        c.supplies -= CampRules.REFRESH_COST; RollRecruits(c); Save(c); return true;
    }
    public static bool FitLimb(CampData c, RatRecord r)
    {
        if (c.supplies < CampRules.LIMB_COST || r.lost.Count == 0) return false;
        c.supplies -= CampRules.LIMB_COST;
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
        if (win && !c.done.Contains(mission.id)) c.done.Add(mission.id);
        var report = new List<ReportRow>();
        var deployed = new HashSet<string>(res.Select(x => x.id));
        foreach (var x in res)
        {
            var r = c.squad.FirstOrDefault(q => q.id == x.id);
            if (r == null) continue;
            bool survived = x.alive || (x.downed && win);
            if (!survived)
            {
                c.squad.Remove(r);
                var f = r.Clone();
                f.rank = CampRules.RANKS[RankIdx(r)].name; f.cls = SkillsData.RAT_CLASSES[ClassOf(r)].name; f.died = mission.name; f.battle = c.battles; f.how = x.how ?? "전사"; f.kills = r.kills + x.kills;
                c.fallen.Add(f);
                report.Add(new ReportRow { r = f, dead = true, how = x.how });
                // 형제를 잃은 쥐
                var sib = c.squad.FirstOrDefault(q => q.sibling == r.id);
                if (sib != null) { sib.stress = Mathf.Min(99, sib.stress + 30); sib.grief = r.name; }
                continue;
            }
            int before = RankIdx(r);
            float pupilMul = Partners(r, "mentor").Any(deployed.Contains) ? 1.5f : 1;
            r.xp += Mathf.RoundToInt((x.kills + 1 + (win ? 1 : 0)) * XpMul(r) * pupilMul);
            r.kills += x.kills; r.missions++;
            r.stress = Mathf.Clamp(x.stress + (win ? CampRules.STRESS.win : 0) - (int)TraitSum(r, "unwind"), 0, 99);
            if (!string.IsNullOrEmpty(x.broke)) r.afflicted = x.broke;
            r.rested = 0;
            if (x.severed != null && x.severed.Count > 0) r.lost = r.lost.Concat(x.severed).Distinct().ToList();
            report.Add(new ReportRow { r = r, promoted = RankIdx(r) > before, kills = x.kills, downed = x.downed, hurt = x.hurt ?? new List<string>(), severed = x.severed ?? new List<string>(), broke = x.broke });
        }
        var rows = report.Where(x => !x.dead).GroupBy(x => x.r.id).ToDictionary(g => g.Key, g => g.First());
        Acquire(c, res.Where(x => rows.ContainsKey(x.id)).ToList(), new HashSet<string>(report.Where(x => x.dead).Select(x => x.r.id)), rows, rankBefore);
        // 출전 안 한 쥐는 쉼: 스트레스 풀림, 두 판 쉬면 정신 이상도 나음
        foreach (var r in c.squad)
        {
            if (deployed.Contains(r.id)) continue;
            float rest = TraitSum(r, "rest");
            r.stress = Mathf.Max(0, r.stress + (int)(CampRules.STRESS.rest * (rest != 0 ? rest : 1)));
            r.rested++;
            if (r.rested >= 2) r.afflicted = "";
        }
        c.deploy = c.deploy.Where(id => c.squad.Any(r => r.id == id)).ToList();
        // 출전할 쥐가 4마리도 안 남으면 징집병이 공짜로 옴 (전멸해도 캠페인은 이어짐)
        int drafted = 0;
        while (c.squad.Count(Ready) < 4 && c.squad.Count < CampRules.SQUAD_MAX) { var r = NewRat(c); r.drafted = true; c.squad.Add(r); drafted++; }
        foreach (var r in c.squad) if (c.deploy.Count < CampRules.DEPLOY_MAX && Ready(r) && !c.deploy.Contains(r.id) && r.drafted) c.deploy.Add(r.id);
        Save(c);
        return new SettleOut { reward = reward, report = report, drafted = drafted };
    }

    // 지금 열린 임무 (깬 것 + 다음 하나)
    public static List<MissionDef> Unlocked(CampData c) => GameData.MISSIONS.Where((m, i) => i == 0 || c.done.Contains(GameData.MISSIONS[i - 1].id)).ToList();
    public static MissionDef Current(CampData c) => GameData.MISSIONS.FirstOrDefault(m => !c.done.Contains(m.id)) ?? GameData.MISSIONS[GameData.MISSIONS.Length - 1];
}

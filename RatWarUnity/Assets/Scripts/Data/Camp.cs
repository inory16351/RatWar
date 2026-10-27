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
    // 추모비에만
    public string rank = "", cls = "", died = "", how = "";
    public int battle;
    public RatRecord Clone() => JsonUtility.FromJson<RatRecord>(JsonUtility.ToJson(this));
}

[Serializable]
public class CampData
{
    public int v = 2, supplies = 60, battles, made;
    public List<string> done = new List<string>(), deploy = new List<string>();
    public List<RatRecord> squad = new List<RatRecord>(), fallen = new List<RatRecord>(), recruits = new List<RatRecord>();
}

public class TraitDef
{
    public string name, desc; public bool rare;
    public Dictionary<string, float> n = new Dictionary<string, float>();      // 능력치 더하기 · 기타 수치
    public Dictionary<string, float> stressBy;                                 // 이유별 스트레스 배율
    public float Get(string k) => n.TryGetValue(k, out var v) ? v : 0;
}

public class RatStats
{
    public string clsId; public ClassDef cls;
    public int hp, aim, mob, sight, crit, def, dmg, clip, heal, boom, grenades, medkits, charges, cheese, psi, whisker;
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
        ["veteran"] = T("노병", "명중 +5 · 처음 스트레스 25", ("aim", 5), ("startStress", 25)),
        ["tough"] = T("튼튼함", "체력 +1", ("hp", 1)),
        ["marksman"] = T("사격 재능", "명중 +5", ("aim", 5)),
        ["nimble"] = T("날쌤", "이동 +1", ("mob", 1)),
        ["drinker"] = T("술꾼", "판 끝나면 스트레스 −10 · 명중 −3", ("aim", -3), ("unwind", 10)),
        ["homesick"] = T("향수병", "출전할 때마다 스트레스 +8", ("homesick", 8)),
        ["pious"] = T("신앙심", "쉴 때 스트레스 두 배로 풀림", ("rest", 2)),
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
        ["lucky"] = T("행운아", "치명 +5 · 회피 +5", ("crit", 5), ("def", 5)),
        ["careful"] = T("꼼꼼함", "팔을 다쳐도 재장전을 실수하지 않음"),
        ["clumsy"] = T("덤벙댐", "재장전 10% 실패 · 수류탄 하나 더", ("grenades", 1)),
        ["leader"] = T("타고난 지휘관", "3칸 안 아군 명중 +5 · 스트레스 −20%"),
        ["loner"] = T("외톨이", "3칸 안에 아군이 없으면 명중 +10"),
        ["smoker"] = T("골초", "매 턴 스트레스 −3 · 체력 −1", ("smoke", 3), ("hp", -1)),
        ["glutton"] = T("치즈 귀신", "치즈 배급을 받으면 두 배로 풀림"),
        ["sluggish"] = T("느림보", "이동 −1 · 체력 +1", ("mob", -1), ("hp", 1)),
        ["sharpears"] = T("밝은 귀", "시야 +1", ("sight", 1)),
    };
    // 같이 가질 수 없는 특성
    static readonly string[][] TRAIT_CLASH =
    {
        new[] { "brave", "coward" }, new[] { "ironwill", "nervous" }, new[] { "optimist", "pessimist" }, new[] { "quick", "slowwit" },
        new[] { "bloodlust", "squeamish" }, new[] { "leader", "loner" }, new[] { "nimble", "sluggish" }, new[] { "careful", "clumsy" },
        new[] { "psychopath", "sibling" }, new[] { "brave", "nervous" }, new[] { "coward", "ironwill" },
    };
    public static bool Clashes(string a, string b) => TRAIT_CLASH.Any(p => (p[0] == a && p[1] == b) || (p[0] == b && p[1] == a));

    public static class STRESS
    {
        public const int hit = 8, crit = 12, downed = 20, allyDead = 22, siblingDead = 45, gore = 8, suppressed = 6, panicNear = 6;
        public const int kill = -6, win = -12, cheese = -25, rest = -35, warcry = -10;
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
        var pool = CampRules.TRAITS.Keys.Where(t => !CampRules.TRAITS[t].rare && t != "sibling").ToList();
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
        try { if (File.Exists(PathOf)) { var c = JsonUtility.FromJson<CampData>(File.ReadAllText(PathOf)); if (c != null && c.v == 2) return c; } }
        catch (Exception e) { Debug.LogWarning("저장 읽기 실패 " + e.Message); }
        return null;
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
            charges = C.charges + tt("charges"), cheese = C.cheese > 0 ? C.cheese : (abil.Contains("cheese") ? 2 : 0),
            psi = C.psi, climb = C.climb, whisker = C.whisker,
            perks = T.perks, abil = abil, sibling = sibling > 0, shaky = shaky < 0,
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

    public class BattleResult { public string id, broke, how; public bool alive, downed; public int kills, stress; public List<string> hurt, severed; }
    public class ReportRow { public RatRecord r; public bool dead, promoted, downed; public string how, broke; public int kills; public List<string> hurt = new List<string>(), severed = new List<string>(); }
    public class SettleOut { public int reward, drafted; public List<ReportRow> report; }

    // ── 판 끝: 결과 반영 ──
    public static SettleOut Settle(CampData c, MissionDef mission, bool win, List<BattleResult> res)
    {
        c.battles++;
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
            r.xp += Mathf.RoundToInt((x.kills + 1 + (win ? 1 : 0)) * XpMul(r));
            r.kills += x.kills; r.missions++;
            r.stress = Mathf.Clamp(x.stress + (win ? CampRules.STRESS.win : 0) - (int)TraitSum(r, "unwind"), 0, 99);
            if (!string.IsNullOrEmpty(x.broke)) r.afflicted = x.broke;
            r.rested = 0;
            if (x.severed != null && x.severed.Count > 0) r.lost = r.lost.Concat(x.severed).Distinct().ToList();
            report.Add(new ReportRow { r = r, promoted = RankIdx(r) > before, kills = x.kills, downed = x.downed, hurt = x.hurt ?? new List<string>(), severed = x.severed ?? new List<string>(), broke = x.broke });
        }
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

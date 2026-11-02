// 전략 지도 (땅따먹기): 육각 40칸을 쥐 · 고양이가 나눠 가짐. 데이터 = Resources/Data/campaign.json
// 한 턴 = 고양이가 내 땅 몇 곳을 침공 → 나는 한 곳만 직접 싸움(침공 막기 또는 붙은 고양이 땅 공격) → 나머지 침공은 자동 판정
// 땅마다 턴 수입(보급품) · 특수 효과(병원 · 모병소 · 공방 · 포병 진지), 지형 따라 전투 맵이 바뀜
// 주요 전투 7개(2차 대전 모티브)는 컷씬 + 정해진 큰 전투. 고양이 수도를 먹으면 승리, 하수구 사령부를 잃으면 패배
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[Serializable]
public class WarState
{
    public int turn;                                     // 0 = 아직 시작 안 함
    public List<string> own = new List<string>();        // 칸별 주인 "rat" / "cat"
    public List<int> threats = new List<int>();          // 이번 턴 고양이가 침공한 내 칸
    public List<string> seen = new List<string>();       // 본 컷씬 · 끝난 주요 전투
    public List<string> news = new List<string>();       // 지난 턴 소식
    public string over = "";                             // "win" / "lose"
    public int battleHex = -1; public string battleKind = ""; // 지금 출격한 전투
    public List<Post> posts = new List<Post>();          // 이번 턴 수비대 (침공당한 칸에 보낸 대기 쥐)
    public List<int> forts = new List<int>();            // 요새화한 내 칸 (잃으면 사라짐)
}
[Serializable] public class Post { public int hex; public string rat; }

public class WarKind { public string name, map; public int income; }
public class WarHex { public string name, kind, fx, special; }
// 컷씬 한 장. text 는 테이블 키여도 됨. rat 이 있으면 그림 대신 검은 바탕에 초상 (에필로그)
public class Slide { public string img, text, title, sub; [Newtonsoft.Json.JsonIgnore] public RatRecord rat; }
public class WarSpecial
{
    public string name, kind, tpl, obj, brief; public int turn, cats, reward, shells; public bool mirror;
    public Slide[] slides;
}
public class CampaignDef
{
    public int cols, rows; public float x0, dx, y0, dy;
    public int[] start;
    public Dictionary<string, WarKind> kinds; public Dictionary<string, string> fx;
    public WarHex[] hexes; public Dictionary<string, WarSpecial> specials;
    public Slide[] prologue, victory, defeat;
}

public static class War
{
    public static readonly CampaignDef D = Newtonsoft.Json.JsonConvert.DeserializeObject<CampaignDef>(Resources.Load<TextAsset>("Data/campaign").text);
    // 지형 → 기본 틀 임무 (고양이 배치를 빌려 옴)
    static readonly Dictionary<string, string> TPL_OF_MAP = new Dictionary<string, string> { ["sewer"] = "m1", ["hill"] = "m2", ["station"] = "m22", ["trench"] = "mb1" };

    public static int N => D.hexes.Length;
    public static int Col(int i) => i % D.cols;
    public static int Row(int i) => i / D.cols;
    public static WarKind KindOf(int i) => D.kinds[D.hexes[i].kind];
    public static WarSpecial SpecialOf(int i) => D.hexes[i].special != null && D.specials.TryGetValue(D.hexes[i].special, out var s) ? s : null;
    // 지도 그림 위 위치 (0~1, 위가 0)
    public static Vector2 Pos(int i) { int c = Col(i), r = Row(i); return new Vector2(D.x0 + c * D.dx, D.y0 + (r + (c % 2 == 1 ? 0.5f : 0)) * D.dy); }

    // 이웃 (평평한 윗변, 홀수 열이 반 칸 아래)
    public static IEnumerable<int> Neighbors(int i)
    {
        int c = Col(i), r = Row(i);
        var d = c % 2 == 0 ? new[] { (0, -1), (0, 1), (-1, -1), (-1, 0), (1, -1), (1, 0) } : new[] { (0, -1), (0, 1), (-1, 0), (-1, 1), (1, 0), (1, 1) };
        foreach (var (dc, dr) in d)
        {
            int nc = c + dc, nr = r + dr;
            if (nc >= 0 && nr >= 0 && nc < D.cols && nr < D.rows) yield return nr * D.cols + nc;
        }
    }

    public static bool Rat(CampData c, int i) => c.war.own.Count == N && c.war.own[i] == "rat";
    public static bool Holds(CampData c, string fx) => c?.war != null && c.war.own.Count == N && Enumerable.Range(0, N).Any(i => D.hexes[i].fx == fx && Rat(c, i));
    public static int HoldCount(CampData c, string fx) => c?.war == null || c.war.own.Count != N ? 0 : Enumerable.Range(0, N).Count(i => D.hexes[i].fx == fx && Rat(c, i));
    public static int Income(CampData c) => Enumerable.Range(0, N).Where(i => Rat(c, i)).Sum(i => KindOf(i).income);
    public static int Count(CampData c, string side) => c.war.own.Count(o => o == side);

    // 공격할 수 있는 칸 = 내 땅에 붙은 고양이 땅
    public static bool CanAttack(CampData c, int i) => !Rat(c, i) && Neighbors(i).Any(n => Rat(c, n));
    public static bool CanDefend(CampData c, int i) => Rat(c, i) && c.war.threats.Contains(i);

    // ── 시작 ──
    public static void Ensure(CampData c)
    {
        if (c.war == null) c.war = new WarState();
        if (c.war.posts == null) c.war.posts = new List<Post>();
        if (c.war.forts == null) c.war.forts = new List<int>();
        if (c.war.turn > 0 && c.war.own.Count == N) return;
        var w = c.war;
        w.turn = 1; w.over = ""; w.battleHex = -1; w.battleKind = ""; w.posts = new List<Post>(); w.forts = new List<int>();
        w.own = Enumerable.Range(0, N).Select(i => D.start.Contains(i) ? "rat" : "cat").ToList();
        w.threats = new List<int>(); w.news = new List<string>();
        // 시작한 다음 사령부가 컷씬을 봤는지만 남김 (새 전쟁이면 다시 봄)
        w.seen = new List<string>();
        Invade(c);
    }

    // ── 출격할 임무 만들기 (같은 입력이면 늘 같은 임무 — 이어하기에서 다시 만들 수 있게) ──
    public static string MissionId(CampData c, int hex, string kind) => $"war:{c.war.turn}:{hex}:{kind}";
    public static MissionDef Mission(CampData c, int hex, string kind)
    {
        var h = D.hexes[hex]; var K = D.kinds[h.kind];
        var sp = SpecialOf(hex);
        // 주요 전투: 방어전은 방어할 때만, 공격전은 공격할 때만 (끝난 방어전은 평범한 전투로)
        bool special = sp != null && sp.kind == kind && !(kind == "defend" && c.war.seen.Contains(h.special + ":done"));
        string tplId = special ? sp.tpl : TPL_OF_MAP[K.map];
        var tpl = GameData.MISSIONS.First(m => m.id == tplId);
        string obj = special ? sp.obj : "elim";
        int turn = c.war.turn;
        int nCats = special ? sp.cats : Mathf.Clamp(4 + turn / 3 + (kind == "attack" ? 1 : 0) + (h.kind == "capital" ? 4 : 0), 4, tpl.cats.Length);
        // 장교 임무면 표적을 먼저 넣고 나머지
        var pool = tpl.cats.OrderBy(x => obj == "officer" && x.target ? 0 : 1).ToList();
        var cats = pool.Take(Mathf.Min(nCats, pool.Count)).Select(x => new CatSpawn { cls = x.cls, head = x.head, pod = x.pod, x = x.x, z = x.z, target = obj == "officer" && x.target }).ToArray();
        if (obj == "officer" && !cats.Any(x => x.target)) obj = "elim";
        int tier = Threat(c, hex);
        CatMix(cats, tier, special, K.map != "sewer", h.kind == "capital", new System.Random(turn * 7919 + hex * 131 + (kind == "attack" ? 1 : 2)));
        int reward = special ? sp.reward : (kind == "attack" ? 30 + turn * 4 : 25 + turn * 3) + K.income * 2;
        string verb = kind == "attack" ? "공격" : "방어";
        string brief = (special ? sp.brief
            : kind == "attack" ? $"{K.name} '{h.name}'을(를) 고양이에게서 빼앗는다. 이기면 이 땅이 우리 것이 된다."
            : $"고양이가 우리 {K.name} '{h.name}'을(를) 침공했다. 지면 이 땅을 잃는다.") + "\n" + ThreatText(cats, tier);
        return new MissionDef
        {
            id = MissionId(c, hex, kind),
            name = special ? sp.name : $"{h.name} {verb}",
            map = tpl.map, obj = obj, brief = brief, reward = reward,
            mirror = special ? sp.mirror : hex % 2 == 1,
            shells = (special ? sp.shells : tpl.shells) + HoldCount(c, "artillery"),
            act = tpl.act, goal = tpl.goal, cats = cats,
        };
    }
    // ── 위협 단계 (1~5): 턴이 지날수록 + 처음보다 땅을 많이 먹을수록 고양이가 세짐. 수도 전투는 +1 ──
    //   진척 = (턴 − 1) + 늘어난 내 땅, 난이도 threatStep 마다 한 단계 (병장 3)
    public static int Threat(CampData c, int hex = -1)
    {
        int gained = Mathf.Max(0, Count(c, "rat") - D.start.Length);
        float prog = (c.war.turn - 1) + gained;
        int t = 1 + Mathf.FloorToInt(prog / RULES.DiffOf(c.diff).threatStep);
        if (hex >= 0 && D.hexes[hex].kind == "capital") t++;
        return Mathf.Clamp(t, 1, 5);
    }
    // 단계별 나오는 병과 · 가중치 (뒤 단계는 앞 단계 것을 다 가짐)
    static readonly (string cls, int tier, int w, int max)[] CAT_POOL =
    {
        ("trooper", 1, 6, 99), ("mgcat", 2, 3, 3), ("catsniper", 3, 2, 2), ("catassault", 3, 3, 3),
        ("catflame", 4, 2, 2), ("catgren", 4, 2, 2), ("officer", 5, 1, 1), ("catheavy", 5, 2, 2),
    };
    public static readonly string[] TIER_NAME = { "", "선봉대", "정규군", "증원군", "기갑 사단", "근위 정예" };
    public const float ELITE_PER_TIER = 0.12f; // 3단계부터 단계마다 정예 확률 +12%
    // 템플릿 배치 자리(위치 · 무리)는 그대로, 병과만 단계에 맞게 다시 굴림. 주요 전투는 소총병 절반만 바꿈 (짜 둔 구성 유지)
    static void CatMix(CatSpawn[] cats, int tier, bool special, bool tanks, bool capital, System.Random rng)
    {
        var count = new Dictionary<string, int>();
        foreach (var x in cats) count[x.cls] = count.TryGetValue(x.cls, out var n) ? n + 1 : 1;
        int Has(string k) => count.TryGetValue(k, out var n) ? n : 0;
        // 전차: 4단계 30% 1대 · 5단계 1대 (수도 2대). 하수구엔 못 들어옴. 뒤쪽(z 작은) 무리 자리에
        int nTank = !tanks ? 0 : tier >= 5 ? (capital ? 2 : 1) : tier == 4 && rng.NextDouble() < 0.3 ? 1 : 0;
        foreach (var x in cats.Where(x => !x.target).OrderBy(x => x.z).Take(nTank)) { count[x.cls]--; x.cls = "cattank"; }
        var opts = CAT_POOL.Where(p => p.tier <= tier).ToArray();
        foreach (var x in cats)
        {
            if (x.target || x.cls == "cattank" || x.cls == "officer" && (special || tier >= 5)) continue; // 장교는 5단계부터 (주요 전투는 짜 둔 대로)
            if (special && (x.cls != "trooper" || rng.NextDouble() < 0.5)) continue;
            var ok = opts.Where(p => Has(p.cls) < p.max || p.cls == x.cls).ToArray();
            int roll = rng.Next(ok.Sum(p => p.w));
            var pick = ok.First(p => (roll -= p.w) < 0).cls;
            count[x.cls]--; x.cls = pick; count[pick] = Has(pick) + 1;
        }
        float elite = Mathf.Max(0, tier - 2) * ELITE_PER_TIER;
        foreach (var x in cats) x.elite = x.cls != "cattank" && rng.NextDouble() < elite;
    }
    static string ThreatText(CatSpawn[] cats, int tier)
    {
        var kinds = cats.GroupBy(x => x.cls).OrderByDescending(g => g.Count()).Select(g => $"{GameData.CLASSES[g.Key].name} {g.Count()}");
        int el = cats.Count(x => x.elite);
        return UIK.Col($"위협 {tier}단계 · {TIER_NAME[tier]}", UIK.CAT) + UIK.Dim($" — {string.Join(" · ", kinds)}{(el > 0 ? $" (정예 {el})" : "")}");
    }

    // 이어하기: 저장한 전투의 임무 id → 같은 임무 다시
    public static MissionDef FromId(CampData c, string id)
    {
        if (c?.war == null || string.IsNullOrEmpty(id) || !id.StartsWith("war:")) return null;
        var p = id.Split(':');
        if (p.Length < 4 || !int.TryParse(p[2], out int hex) || hex < 0 || hex >= N) return null;
        return Mission(c, hex, p[3]);
    }
    public static bool IsWar(MissionDef m) => m != null && m.id != null && m.id.StartsWith("war:");
    public static bool IsSpecial(CampData c, int hex, string kind) { var sp = SpecialOf(hex); return sp != null && sp.kind == kind && !c.war.seen.Contains(D.hexes[hex].special + ":done"); }
    public static string SpecialId(int hex) => D.hexes[hex].special;

    // 출격 전에 고른 칸을 적어 둠 (정산 · 이어하기용)
    // ── 자동 방어 확률 (직접 싸우지 않은 침공) ──
    //   기본 = 50% + 붙은 내 땅마다 8% − 턴마다 1.5% + 난이도 보정 → 15~85% 로 자름
    //   그 위에 수비대(쥐마다 12% + 계급마다 1%) · 요새(+20%) → 최대 95%
    public const float GARRISON_PER = 0.12f, GARRISON_RANK = 0.01f, FORT_BONUS = 0.20f, HOLD_MAX = 0.95f;
    public const int GARRISON_MAX = 3, FORT_COST = 45;
    public const float GARRISON_HURT = 0.25f, GARRISON_DIE = 0.2f; // 뚫리면 쥐마다 25% 로 화 입음, 그중 1/5 은 전사 (나머지는 한 턴 부상)
    public const int GARRISON_STRESS = 10;
    public static float BaseHold(CampData c, int hex)
    {
        int friends = Neighbors(hex).Count(n => Rat(c, n));
        return Mathf.Clamp(0.5f + 0.08f * friends - 0.015f * c.war.turn + RULES.DiffOf(c.diff).hold, 0.15f, 0.85f);
    }
    public static IEnumerable<RatRecord> Garrison(CampData c, int hex) =>
        c.war.posts.Where(p => p.hex == hex).Select(p => c.squad.FirstOrDefault(r => r.id == p.rat)).Where(r => r != null);
    public static float GarrisonBonus(CampData c, int hex) => Garrison(c, hex).Sum(r => GARRISON_PER + GARRISON_RANK * Camp.RankIdx(r));
    public static float HoldChance(CampData c, int hex) =>
        Mathf.Min(HOLD_MAX, BaseHold(c, hex) + GarrisonBonus(c, hex) + (c.war.forts.Contains(hex) ? FORT_BONUS : 0));

    // 쥐가 수비대로 나간 칸 (없으면 −1)
    public static int Posted(CampData c, string ratId) => c?.war?.posts?.FirstOrDefault(p => p.rat == ratId)?.hex ?? -1;
    // 수비대 넣기 · 빼기 (같은 칸이면 뺌). 출전 명단에서는 빠짐
    public static bool TogglePost(CampData c, int hex, RatRecord r)
    {
        var w = c.war;
        int at = Posted(c, r.id);
        w.posts.RemoveAll(p => p.rat == r.id);
        if (at == hex) { Camp.Save(c); return true; }
        if (!CanDefend(c, hex) || !Camp.Ready(r) || Garrison(c, hex).Count() >= GARRISON_MAX) { Camp.Save(c); return at >= 0; }
        w.posts.Add(new Post { hex = hex, rat = r.id });
        c.deploy.Remove(r.id);
        Camp.Save(c);
        return true;
    }
    public static bool CanFortify(CampData c, int hex) => Rat(c, hex) && !c.war.forts.Contains(hex) && c.war.over == "";
    public static bool Fortify(CampData c, int hex)
    {
        if (!CanFortify(c, hex) || c.supplies < FORT_COST) return false;
        c.supplies -= FORT_COST; c.war.forts.Add(hex);
        Camp.Save(c);
        return true;
    }
    static void Lose(CampData c, int hex) { c.war.own[hex] = "cat"; c.war.forts.Remove(hex); }

    public static void Begin(CampData c, int hex, string kind)
    {
        c.war.battleHex = hex; c.war.battleKind = kind;
        c.war.posts.RemoveAll(p => p.hex == hex || c.deploy.Contains(p.rat)); // 직접 싸우는 칸 · 출전하는 쥐는 수비대에서 빠짐
        Camp.Save(c);
    }

    // ── 판 끝 (Camp.Settle 안에서, 저장 전에) ──
    public static void AfterBattle(CampData c, MissionDef m, bool win)
    {
        var w = c.war; int hex = w.battleHex;
        if (hex < 0) return;
        var name = D.hexes[hex].name;
        if (IsSpecial(c, hex, w.battleKind) && (win || w.battleKind == "defend")) w.seen.Add(D.hexes[hex].special + ":done");
        w.news.Clear();
        if (w.battleKind == "attack")
        {
            if (win) { w.own[hex] = "rat"; w.news.Add(UIK.Col($"▲ {name} 점령!", UIK.OK)); }
            else w.news.Add(UIK.Col($"✖ {name} 공격 실패", UIK.NEG));
        }
        else
        {
            if (win) w.news.Add(UIK.Col($"■ {name} 방어 성공", UIK.OK));
            else { Lose(c, hex); w.news.Add(UIK.Col($"▼ {name} 함락", UIK.NEG)); }
        }
        w.threats.Remove(hex);
        EndTurn(c);
    }

    // 싸우지 않고 턴 넘기기 (분대 전원 휴식)
    public static void Wait(CampData c)
    {
        c.war.news.Clear();
        c.war.news.Add(UIK.Dim("이번 턴은 출격하지 않았다 — 분대가 쉰다"));
        foreach (var r in c.squad.Where(q => Posted(c, q.id) < 0)) { r.stress = Mathf.Max(0, r.stress + CampRules.STRESS.rest); r.rested++; if (r.rested >= 2) r.afflicted = ""; }
        EndTurn(c);
        Camp.Save(c);
    }

    // ── 턴 넘김: 남은 침공 자동 판정 → 수입 · 효과 → 승패 → 다음 침공 ──
    static void EndTurn(CampData c)
    {
        var w = c.war;
        // 지난 턴 수비대에서 다친 쥐는 한 턴 쉬고 돌아옴
        foreach (var r in c.squad) if (r.wounded > 0) r.wounded--;
        foreach (var hex in w.threats.ToList())
        {
            if (!Rat(c, hex)) continue;
            // 직접 안 막은 주요 방어전은 그걸로 끝 (다음 턴에 또 오지 않음)
            var sp = SpecialOf(hex);
            if (sp != null && sp.kind == "defend") w.seen.Add(D.hexes[hex].special + ":done");
            var name = D.hexes[hex].name;
            var gar = Garrison(c, hex).ToList();
            float hold = HoldChance(c, hex);
            string pct = $" ({Mathf.RoundToInt(hold * 100)}%)";
            if (UnityEngine.Random.value < hold)
            {
                w.news.Add(gar.Count > 0 ? UIK.Col($"■ {name} 수비대({string.Join(", ", gar.Select(r => r.name))})가 침공을 막아냈다{pct}", UIK.OK)
                    : UIK.Dim($"■ {name} 수비대가 침공을 막아냈다 (자동){pct}"));
                foreach (var r in gar) { r.xp++; r.stress = Mathf.Min(99, r.stress + GARRISON_STRESS); r.rested = 0; }
            }
            else
            {
                Lose(c, hex);
                w.news.Add(UIK.Col($"▼ {name} 수비대가 무너졌다{(gar.Count > 0 ? "" : " (자동)")}{pct}", UIK.NEG));
                foreach (var r in gar)
                {
                    r.stress = Mathf.Min(99, r.stress + GARRISON_STRESS * 2); r.rested = 0;
                    if (UnityEngine.Random.value >= GARRISON_HURT) continue;
                    if (UnityEngine.Random.value < GARRISON_DIE) { Camp.Bury(c, r, name + " 수비", "수비 중 전사"); w.news.Add(UIK.Col($"  ✝ {r.name} 전사", UIK.NEG)); }
                    else { r.wounded = 1; c.deploy.Remove(r.id); w.news.Add(UIK.Col($"  ✚ {r.name} 부상 — 한 턴 쉼", UIK.NEG)); }
                }
            }
        }
        w.threats.Clear();
        w.posts.Clear();
        w.battleHex = -1; w.battleKind = "";
        // 승패
        int hq = Array.FindIndex(D.hexes, h => h.fx == "hq"), cap = Array.FindIndex(D.hexes, h => h.fx == "capital");
        if (cap >= 0 && Rat(c, cap)) { w.over = "win"; w.news.Add(UIK.Col("펠린부르크 함락 — 전쟁에서 이겼다!", UIK.GOLD)); return; }
        if (hq >= 0 && !Rat(c, hq) || Count(c, "rat") == 0) { w.over = "lose"; w.news.Add(UIK.Col("하수구 사령부 함락 — 전쟁에서 졌다", UIK.NEG)); return; }
        // 수입 · 땅 효과
        int inc = Mathf.RoundToInt(Income(c) * RULES.DiffOf(c.diff).income);
        c.supplies += inc;
        w.news.Add(UIK.Col($"보급품 +{inc} (땅 {Count(c, "rat")}칸)", UIK.GOLD));
        if (Holds(c, "hospital")) foreach (var r in c.squad) r.stress = Mathf.Max(0, r.stress - 10);
        if (Holds(c, "recruit")) Camp.RollRecruits(c);
        w.turn++;
        Invade(c);
    }

    // 고양이 침공: 정해진 주요 방어전 먼저, 나머지는 붙은 내 땅 중 값진 곳
    static void Invade(CampData c)
    {
        var w = c.war;
        w.threats.Clear();
        foreach (var kv in D.specials)
        {
            var sp = kv.Value;
            if (sp.kind != "defend" || w.turn < sp.turn || w.seen.Contains(kv.Key + ":done")) continue;
            int hex = Array.FindIndex(D.hexes, h => h.special == kv.Key);
            if (hex >= 0 && Rat(c, hex) && !w.threats.Contains(hex)) w.threats.Add(hex);
        }
        int want = w.turn <= 2 ? 1 : w.turn < 8 ? (UnityEngine.Random.value < 0.5f ? 2 : 1) : (UnityEngine.Random.value < 0.4f ? 3 : 2);
        int inv = RULES.DiffOf(c.diff).invade; // 난이도: 쉬우면 −1 (최소 1), 어려우면 8턴부터 +1
        want = Mathf.Max(1, want + (inv < 0 || w.turn >= 8 ? inv : 0));
        var front = Enumerable.Range(0, N).Where(i => Rat(c, i) && !w.threats.Contains(i) && Neighbors(i).Any(n => !Rat(c, n))).ToList();
        while (w.threats.Count < want && front.Count > 0)
        {
            // 수입이 큰 땅 · 고양이 땅에 많이 둘러싸인 땅이 잘 뽑힘
            var weights = front.Select(i => KindOf(i).income + 3f * Neighbors(i).Count(n => !Rat(c, n))).ToList();
            float roll = UnityEngine.Random.value * weights.Sum(); int k = 0;
            while (k < front.Count - 1 && (roll -= weights[k]) > 0) k++;
            w.threats.Add(front[k]); front.RemoveAt(k);
        }
        if (w.threats.Count > 0) w.news.Add(UIK.Col($"고양이 침공: {string.Join(", ", w.threats.Select(i => D.hexes[i].name))}", UIK.CAT));
    }
}

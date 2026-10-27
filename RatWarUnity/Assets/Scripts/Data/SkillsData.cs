// 쥐 전쟁 시뮬 — 육각 벌집 스킬판 · 병종 결정 (웹 skills.js)
// 가운데 '신병' 칸에서 시작해 이웃 칸으로만 넓혀 감. 판은 여섯 갈래(위부터 시계 방향):
//   0 사격 · 1 화력 · 2 폭파 · 3 기동 · 4 지원 · 5 정찰
// 많이 찍은 갈래가 병종을 정함 (2칸 이상). 사격+정찰 · 지원+정찰을 비슷하게 찍으면 혼합 병종(통신병 · 주술사).
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// 칸 효과: 숫자 = 능력치 더하기, abil = 능력 추가, perk = 특수 효과
public class NodeFx
{
    public Dictionary<string, float> n = new Dictionary<string, float>();
    public string abil, perk, name;
    public NodeFx Set(string k, float v) { n[k] = v; return this; }
    public static NodeFx Of(string name = null, string abil = null, string perk = null) => new NodeFx { name = name, abil = abil, perk = perk };
}

public class HexCell { public int q, r, ring; public int[] sec; public NodeFx fx; public string title; }

public static class SkillsData
{
    public static readonly (string name, Color col)[] SECTORS =
    {
        ("사격", Hex("#d9c36a")), ("화력", Hex("#d9824a")), ("폭파", Hex("#c8563f")),
        ("기동", Hex("#6fa8d8")), ("지원", Hex("#7fbf8a")), ("정찰", Hex("#9b86c9")),
    };
    public static Color Hex(string h) { ColorUtility.TryParseHtmlString(h, out var c); return c; }

    // 병종: 무기 · 기본치 · 기본 능력. 판의 갈래로 정해짐 (from = 갈래 번호, 혼합은 두 개)
    public static readonly Dictionary<string, ClassDef> RAT_CLASSES = new[]
    {
        new ClassDef { id = "recruit", name = "소총수", hp = 6, aim = 65, mob = 6, weapon = "rifle", grenades = 1, abil = new[] { "shoot", "grenade", "scurry", "overwatch" }, desc = "아직 길이 정해지지 않은 신병. 잽싼 발로 위기를 피함" },
        new ClassDef { id = "sniper", name = "저격수", from = new[] { 0 }, hp = 5, aim = 72, mob = 6, weapon = "sniper", sight = 2, abil = new[] { "shoot", "ambush", "overwatch" }, desc = "쥐구멍에 숨어 먼 거리 한 발. 가까우면 약함" },
        new ClassDef { id = "gunner", name = "기관총 사수", from = new[] { 1 }, hp = 7, aim = 60, mob = 5, weapon = "lmg", abil = new[] { "shoot", "suppress", "warcry", "overwatch" }, desc = "연사 · 제압. 찍찍 함성으로 분대 사기를 올림" },
        new ClassDef { id = "engineer", name = "공병", from = new[] { 2 }, hp = 6, aim = 64, mob = 6, weapon = "rifle", grenades = 2, charges = 2, abil = new[] { "shoot", "grenade", "charge", "gnaw", "overwatch" }, desc = "폭약으로 벽까지 날리고, 철조망을 갉아 끊고, 땅굴을 팜" },
        new ClassDef { id = "commando", name = "코만도", from = new[] { 3 }, hp = 6, aim = 66, mob = 7, weapon = "smg", grenades = 1, def = 5, climb = true, abil = new[] { "shoot", "knife", "grenade", "overwatch" }, desc = "벽을 타고 넘어 파고들어 칼로 끝냄. 칼은 소리가 안 남" },
        new ClassDef { id = "medic", name = "의무병", from = new[] { 4 }, hp = 6, aim = 62, mob = 6, weapon = "smg", medkits = 2, cheese = 2, abil = new[] { "shoot", "firstaid", "cheese", "overwatch" }, desc = "동료를 살려 내고 치즈로 정신을 차리게 함" },
        new ClassDef { id = "scout", name = "정찰병", from = new[] { 5 }, hp = 5, aim = 66, mob = 7, weapon = "rifle", sight = 2, whisker = 3, abil = new[] { "shoot", "mark", "overwatch" }, desc = "수염으로 벽 너머 적을 느끼고 표적을 찍어 줌" },
        new ClassDef { id = "radio", name = "통신병", from = new[] { 0, 5 }, hp = 6, aim = 63, mob = 6, weapon = "smg", abil = new[] { "shoot", "command", "radioscan", "overwatch" }, desc = "사격 + 정찰: 쥐떼 전령으로 멀리 있는 아군까지 지휘 · 무전 탐지" },
        new ClassDef { id = "shaman", name = "주술사", from = new[] { 4, 5 }, hp = 5, aim = 60, mob = 6, weapon = "smg", psi = 3, abil = new[] { "shoot", "dominate", "ward", "overwatch" }, desc = "지원 + 정찰: 적의 정신을 붙잡아 조종하고, 아군에게 마법 방어막" },
    }.ToDictionary(c => c.id);

    // 갈래마다: r1 = 첫 칸, r2 = 둘째 줄 꼭짓점(핵심), r3 = 셋째 줄 꼭짓점(정점), side = 셋째 줄 옆 칸 두 개, small = 경계 칸에 섞이는 작은 값
    class SectorFx { public NodeFx r1, r2, r3, small; public NodeFx[] side; }
    static readonly SectorFx[] NODE_FX =
    {
        new SectorFx { r1 = new NodeFx().Set("aim", 5), r2 = NodeFx.Of("정조준", abil: "aimed"), r3 = NodeFx.Of("명사수").Set("crit", 15), side = new[] { new NodeFx().Set("aim", 5), new NodeFx().Set("sight", 1) }, small = new NodeFx().Set("aim", 3) },
        new SectorFx { r1 = new NodeFx().Set("hp", 1), r2 = NodeFx.Of("제압 사격", abil: "suppress"), r3 = NodeFx.Of("긴 탄띠").Set("clip", 2), side = new[] { new NodeFx().Set("hp", 1), new NodeFx().Set("dmg", 1) }, small = new NodeFx().Set("hp", 1) },
        new SectorFx { r1 = new NodeFx().Set("grenades", 1), r2 = NodeFx.Of("땅굴 파기", abil: "tunnel"), r3 = NodeFx.Of("화염방사기 장착", abil: "flame"), side = new[] { NodeFx.Of("고폭 수류탄").Set("blast", 0.5f), NodeFx.Of("폭파 전문").Set("boom", 2).Set("charges", 1) }, small = new NodeFx().Set("grenades", 1) },
        new SectorFx { r1 = new NodeFx().Set("mob", 1), r2 = NodeFx.Of("근접 격투", abil: "knife"), r3 = NodeFx.Of("날쌘 발").Set("def", 10).Set("mob", 1), side = new[] { NodeFx.Of("근접 사격", perk: "closeq"), new NodeFx().Set("def", 5) }, small = new NodeFx().Set("def", 3) },
        new SectorFx { r1 = NodeFx.Of("응급 처치", abil: "firstaid").Set("medkits", 1), r2 = NodeFx.Of("구출", perk: "revive"), r3 = NodeFx.Of("야전 의무").Set("heal", 2), side = new[] { new NodeFx().Set("medkits", 1), new NodeFx().Set("hp", 1) }, small = new NodeFx().Set("hp", 1) },
        new SectorFx { r1 = new NodeFx().Set("sight", 1), r2 = NodeFx.Of("노련한 경계", perk: "sentry"), r3 = NodeFx.Of("조용한 발걸음", perk: "ghost"), side = new[] { new NodeFx().Set("sight", 1), NodeFx.Of("표적 지정", abil: "mark") }, small = new NodeFx().Set("aim", 2) },
    };
    public static readonly Dictionary<string, string> PERK_TEXT = new Dictionary<string, string>
    {
        ["revive"] = "쓰러진 아군을 응급 처치로 일으킴 (체력 2)",
        ["closeq"] = "4칸 안 명중 +15",
        ["sentry"] = "경계 사격 명중 감점 없음",
        ["ghost"] = "자는 고양이 무리가 3칸 안에서만 깸",
    };
    public static readonly Dictionary<string, string> STAT_TEXT = new Dictionary<string, string>
    {
        ["hp"] = "체력", ["aim"] = "명중", ["mob"] = "이동", ["sight"] = "시야", ["crit"] = "치명", ["def"] = "회피", ["dmg"] = "피해", ["clip"] = "탄창",
        ["grenades"] = "수류탄", ["medkits"] = "구급상자", ["heal"] = "치료량", ["blast"] = "폭발 범위", ["boom"] = "폭발 피해", ["burn"] = "화상 피해", ["charges"] = "폭약", ["psi"] = "정신력",
    };

    public const int R = 3;
    // 육각(평평한 윗변) 축 좌표 (q, r). 0번 방향 = 위, 시계 방향
    static readonly int[][] DIR = { new[] { 0, -1 }, new[] { 1, -1 }, new[] { 1, 0 }, new[] { 0, 1 }, new[] { -1, 1 }, new[] { -1, 0 } };
    public static string Key(int q, int r) => q + "," + r;
    public static readonly Dictionary<string, HexCell> Cells = BuildCells();

    static Dictionary<string, HexCell> BuildCells()
    {
        var cells = new Dictionary<string, HexCell>();
        cells[Key(0, 0)] = new HexCell { q = 0, r = 0, ring = 0, sec = new int[0], fx = new NodeFx(), title = "신병" };
        for (int k = 1; k <= R; k++)
            for (int i = 0; i < 6; i++)
                for (int j = 0; j < k; j++)
                {
                    int q = DIR[i][0] * k + DIR[(i + 2) % 6][0] * j, r = DIR[i][1] * k + DIR[(i + 2) % 6][1] * j;
                    float f = (float)j / k; int n = (i + 1) % 6;
                    int[] sec = f == 0 ? new[] { i } : f < 0.5f ? new[] { i } : f > 0.5f ? new[] { n } : new[] { i, n };
                    var S = NODE_FX[sec[0]];
                    NodeFx fx;
                    if (sec.Length == 2) fx = Merge(NODE_FX[i].small, NODE_FX[n].small);
                    else if (k == 1) fx = S.r1;
                    else if (k == 2) fx = S.r2;
                    else if (j == 0) fx = S.r3;
                    else fx = S.side[f < 0.5f ? 0 : 1];
                    cells[Key(q, r)] = new HexCell { q = q, r = r, ring = k, sec = sec, fx = fx };
                }
        return cells;
    }

    static NodeFx Merge(NodeFx a, NodeFx b)
    {
        var o = new NodeFx { name = a.name, abil = a.abil, perk = a.perk };
        foreach (var kv in a.n) o.n[kv.Key] = kv.Value;
        foreach (var kv in b.n) o.n[kv.Key] = (o.n.TryGetValue(kv.Key, out var v) ? v : 0) + kv.Value;
        return o;
    }

    public static IEnumerable<HexCell> Neighbors(int q, int r)
    {
        foreach (var d in DIR) if (Cells.TryGetValue(Key(q + d[0], r + d[1]), out var c)) yield return c;
    }

    // 칸 설명 한 줄
    public static string FxText(NodeFx fx)
    {
        var t = new List<string>();
        if (fx.name != null) t.Add(fx.name);
        if (fx.abil != null) t.Add("능력: " + GameData.ABILITIES[fx.abil].name);
        if (fx.perk != null) t.Add(PERK_TEXT[fx.perk]);
        foreach (var kv in fx.n) if (STAT_TEXT.ContainsKey(kv.Key)) t.Add($"{STAT_TEXT[kv.Key]} +{kv.Value}");
        return string.Join(" · ", t);
    }

    // 찍은 칸 목록 → 갈래별 개수 (경계 칸은 반씩)
    public static float[] Counts(List<string> nodes)
    {
        var c = new float[6];
        foreach (var k in nodes)
        {
            if (!Cells.TryGetValue(k, out var cell)) continue;
            foreach (var s in cell.sec) c[s] += 1f / cell.sec.Length;
        }
        return c;
    }

    // 병종 정하기
    public static string ClassOf(List<string> nodes)
    {
        var c = Counts(nodes);
        var order = c.Select((v, i) => (v, i)).OrderByDescending(x => x.v).ToArray(); // 안정 정렬
        var top = order[0]; var second = order[1];
        if (top.v < 2) return "recruit";
        foreach (var C in RAT_CLASSES.Values)
        {
            if (C.from.Length != 2) continue;
            var pair = new[] { top.i, second.i }.OrderBy(x => x);
            if (pair.SequenceEqual(C.from.OrderBy(x => x)) && second.v >= 2 && second.v >= top.v - 1) return C.id;
        }
        return RAT_CLASSES.Values.First(C => C.from.Length == 1 && C.from[0] == top.i).id;
    }

    // 찍을 수 있는 칸 = 아직 안 찍었고, 찍은 칸(또는 가운데)과 붙어 있음
    public static bool CanTake(List<string> nodes, string k)
    {
        if (nodes.Contains(k) || !Cells.TryGetValue(k, out var cell)) return false;
        return Neighbors(cell.q, cell.r).Any(n => n.ring == 0 || nodes.Contains(Key(n.q, n.r)));
    }

    public class Totals { public Dictionary<string, float> n = new Dictionary<string, float>(); public List<string> abil = new List<string>(), perks = new List<string>(); public float Get(string k) => n.TryGetValue(k, out var v) ? v : 0; }

    // 찍은 칸 효과 전부 합치기
    public static Totals TotalsOf(List<string> nodes)
    {
        var t = new Totals();
        foreach (var k in nodes)
        {
            if (!Cells.TryGetValue(k, out var cell)) continue;
            if (cell.fx.abil != null) t.abil.Add(cell.fx.abil);
            if (cell.fx.perk != null) t.perks.Add(cell.fx.perk);
            foreach (var kv in cell.fx.n) t.n[kv.Key] = t.Get(kv.Key) + kv.Value;
        }
        return t;
    }
}

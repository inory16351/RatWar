// 쥐 전쟁 시뮬 — 데이터 (무기 · 능력 · 병과 · 맵 · 임무 · 규칙). 웹 data.js 를 옮김
// 수치는 여기서만 고친다. 거리 = 칸, 명중 = %.
using System;
using System.Collections.Generic;
using System.Linq;

public class WeaponDef
{
    public string name; public int dmgMin, dmgMax, clip, crit;
    public Func<int, int> range; // 거리별 명중 보정 (d = 칸 거리)
}

public class AbilityDef
{
    public string id, name, key = "", target, desc = "";
    public int ap; public bool end, once, melee, far;
    public float range, radius; public int dmgMin, dmgMax, psi, uses, heal;
    public bool HasDmg => dmgMax > 0;
}

// 쥐 병종 · 고양이 병과 같이 씀
public class ClassDef
{
    public string id, name, weapon, desc = "";
    public int hp, aim, mob, grenades, medkits, charges, cheese, psi, sight, def, crit, aura, whisker;
    public float blast;
    public bool flame, climb;
    public string[] abil;
    public int[] from = new int[0];
}

public class PropDef
{
    public string name, emplacement; public int cover, selfCover; public bool block, breakable, sightBlock;
}

public class HillDef { public float cx, cz, rx, rz; }
public class MapDef { public string[] rows; public HillDef[] hills; public int[][] spawn; }
public class CatSpawn { public string cls, head, pod; public int x, z; public bool target; }
public class GoalDef { public int x, z; public float r; }

public class MissionDef
{
    public string id, name, map, obj, brief; public bool mirror; public int reward, shells, act = 1;
    public GoalDef goal; public CatSpawn[] cats;
}

// 임무 하나를 전투용으로 펼친 것 (좌우 뒤집기 포함)
public class BattleMapDef
{
    public string name, obj; public GoalDef goal; public string[] rows; public HillDef[] hills;
    public int[][] spawn; public CatSpawn[] cats; public int mortarShells;
}

public static class GameData
{
    public static readonly Dictionary<string, WeaponDef> WEAPONS = new Dictionary<string, WeaponDef>
    {
        ["rifle"] = new WeaponDef { name = "소총", dmgMin = 3, dmgMax = 5, clip = 4, crit = 10, range = d => d <= 2 ? 10 : d <= 8 ? 0 : -(d - 8) * 3 },
        ["lmg"] = new WeaponDef { name = "경기관총", dmgMin = 3, dmgMax = 5, clip = 3, crit = 5, range = d => -5 + (d <= 8 ? 0 : -(d - 8) * 3) },
        ["sniper"] = new WeaponDef { name = "저격소총", dmgMin = 4, dmgMax = 6, clip = 3, crit = 25, range = d => d <= 3 ? -25 : d <= 6 ? -5 : 5 },
        ["shotgun"] = new WeaponDef { name = "산탄총", dmgMin = 4, dmgMax = 7, clip = 2, crit = 20, range = d => d <= 2 ? 30 : d <= 4 ? 15 : d <= 7 ? 0 : -(d - 7) * 8 },
        ["smg"] = new WeaponDef { name = "기관단총", dmgMin = 2, dmgMax = 4, clip = 3, crit = 10, range = d => d <= 3 ? 15 : d <= 6 ? 5 : d <= 9 ? 0 : -(d - 9) * 5 },
        ["hmg"] = new WeaponDef { name = "중기관총", dmgMin = 4, dmgMax = 6, clip = 99, crit = 10, range = d => 15 + (d <= 10 ? 0 : -(d - 10) * 3) },
    };

    // 능력: ap = 필요한 행동력, end = 쓰면 턴 끝
    public static readonly Dictionary<string, AbilityDef> ABILITIES = Build(new[]
    {
        new AbilityDef { id = "shoot", name = "사격", key = "1", ap = 1, end = true, target = "enemy" },
        new AbilityDef { id = "aimed", name = "정조준", key = "2", ap = 2, end = true, target = "enemy", desc = "명중 +20, 피해 +1 (이동하지 않았을 때만)" },
        new AbilityDef { id = "suppress", name = "제압 사격", key = "2", ap = 1, end = true, target = "enemy", desc = "탄 2발. 대상 명중 −30, 움직이면 반응 사격" },
        new AbilityDef { id = "grenade", name = "수류탄", key = "3", ap = 1, end = true, target = "tile", range = 9, radius = 1.5f, dmgMin = 3, dmgMax = 4, desc = "낮은 엄폐물을 부숨" },
        new AbilityDef { id = "firstaid", name = "응급 처치", key = "4", ap = 1, end = false, target = "ally", heal = 4, desc = "붙어 있는 아군 체력 +4" },
        new AbilityDef { id = "overwatch", name = "경계", key = "5", ap = 1, end = true, target = "self", desc = "적이 시야 안에서 움직이면 사격 (명중 −10)" },
        new AbilityDef { id = "hunker", name = "엎드리기", key = "6", ap = 1, end = true, target = "self", desc = "다음 턴까지 방어 +25" },
        new AbilityDef { id = "reload", name = "재장전", key = "r", ap = 1, end = false, target = "self" },
        new AbilityDef { id = "mortar", name = "박격포", key = "m", ap = 2, end = true, target = "tile", range = 16, radius = 1.5f, dmgMin = 4, dmgMax = 5, desc = "분대가 보는 곳 어디든. 포탄 공유" },
        new AbilityDef { id = "heavy", name = "중기관총", key = "h", ap = 1, end = true, target = "enemy", desc = "진지의 중기관총 (명중 +15)" },
        // ── 병종 · 스킬판 능력 (쥐 테마) ──
        new AbilityDef { id = "scurry", name = "잽싼 발", ap = 1, end = false, target = "tile", range = 3, desc = "3칸 안으로 쪼르르. 경계 사격을 안 맞음" },
        new AbilityDef { id = "ambush", name = "쥐구멍 매복", ap = 1, end = true, target = "self", desc = "엄폐물 옆에서 숨음: 쏘기 전까지 고양이에게 안 보임" },
        new AbilityDef { id = "warcry", name = "찍찍 함성", ap = 0, end = false, target = "self", once = true, desc = "4칸 안 아군 이번 턴 명중 +10, 스트레스 −10 (전투당 1번)" },
        new AbilityDef { id = "charge", name = "폭약", ap = 1, end = true, target = "tile", range = 1.5f, desc = "붙은 엄폐물 · 벽 · 트럭까지 날려 버림" },
        new AbilityDef { id = "gnaw", name = "철조망 갉기", ap = 1, end = false, target = "tile", range = 1.5f, desc = "붙은 철조망을 갉아 끊음" },
        new AbilityDef { id = "tunnel", name = "땅굴 파기", ap = 2, end = true, target = "tile", range = 7, once = true, desc = "지금 칸 ↔ 고른 칸 땅굴. 아군은 입구에서 출구로 1걸음 (전투당 1번)" },
        new AbilityDef { id = "flame", name = "화염방사기", ap = 1, end = true, target = "tile", range = 3.2f, radius = 1.1f, dmgMin = 3, dmgMax = 4, uses = 2, desc = "엄폐 무시 불길 + 다음 턴 화상. 낮은 엄폐물도 태움" },
        new AbilityDef { id = "knife", name = "칼", ap = 1, end = true, target = "enemy", melee = true, desc = "붙은 적을 칼로 (명중 +30). 소리 안 남" },
        new AbilityDef { id = "cheese", name = "치즈 배급", ap = 1, end = false, target = "ally", desc = "붙은 아군 스트레스 −25 · 제압 · 공황 풀림" },
        new AbilityDef { id = "mark", name = "표적 지정", ap = 1, end = false, target = "enemy", desc = "이번 판 동안 그 적에게 아군 명중 +15" },
        new AbilityDef { id = "command", name = "쥐떼 전령", ap = 1, end = true, target = "ally", far = true, desc = "시야 안 아군 하나 행동력 +1" },
        new AbilityDef { id = "radioscan", name = "무전 탐지", ap = 1, end = false, target = "self", once = true, desc = "맵의 모든 고양이 위치를 이번 턴 동안 드러냄 (전투당 1번)" },
        new AbilityDef { id = "dominate", name = "정신 지배", ap = 2, end = true, target = "enemy", psi = 2, desc = "적 하나를 2턴 동안 아군처럼 조종 (정신력 2)" },
        new AbilityDef { id = "ward", name = "마법 방어막", ap = 1, end = false, target = "ally", far = true, psi = 1, desc = "아군 하나에 피해 4 막는 방어막 (정신력 1)" },
    });

    static Dictionary<string, AbilityDef> Build(AbilityDef[] a) => a.ToDictionary(x => x.id);

    // 고양이 군 (쥐 병종은 SkillsData.RAT_CLASSES)
    public static readonly Dictionary<string, ClassDef> CLASSES = new[]
    {
        new ClassDef { id = "trooper", name = "고양이 소총병", hp = 5, aim = 60, mob = 6, weapon = "smg", grenades = 1, abil = new[] { "shoot", "grenade", "overwatch", "hunker" } },
        new ClassDef { id = "mgcat", name = "고양이 기관총병", hp = 6, aim = 55, mob = 5, weapon = "lmg", abil = new[] { "shoot", "suppress", "overwatch", "hunker" } },
        new ClassDef { id = "officer", name = "고양이 장교", hp = 8, aim = 65, mob = 6, weapon = "smg", aura = 10, abil = new[] { "shoot", "overwatch", "hunker" } },
        new ClassDef { id = "catsniper", name = "고양이 저격병", hp = 4, aim = 72, mob = 5, weapon = "sniper", sight = 2, abil = new[] { "shoot", "overwatch", "hunker" } },
        new ClassDef { id = "catflame", name = "고양이 화염병", hp = 6, aim = 55, mob = 6, weapon = "smg", flame = true, abil = new[] { "shoot", "flame", "hunker" } },
    }.ToDictionary(c => c.id);

    // 맵 · 임무는 데이터: Resources/Data/maps.json · missions.json (tools/maps/build_maps.js 가 만들고 검사함)
    // 맵 기호: . 풀밭  , 진흙  = 길  o 포탄 구덩이
    //   낮은 엄폐 S 모래주머니  C 상자     높은 엄폐 W 벽돌 벽  T 죽은 나무  R 트럭 잔해  H 돌집 벽
    //   X 철조망 (못 지나감, 엄폐 아님)   M 박격포 진지   G 기관총 진지 (올라선 칸 = 낮은 엄폐)
    // z = 0 이 맨 뒤(북쪽, 고양이 후방), 쥐는 맨 앞 줄에서 시작. 크기는 맵마다 다름 (S 20×26 · M 24×34 · L 30×44)
    public static readonly Dictionary<string, MapDef> MAPS = LoadJson<Dictionary<string, MapDef>>("Data/maps");

    // 임무: 보상 = 보급품. 하나 끝내면 다음이 열림. mirror = 맵 좌우 뒤집기, act = 막 번호
    public static readonly MissionDef[] MISSIONS = LoadJson<MissionDef[]>("Data/missions");

    static T LoadJson<T>(string path)
    {
        var t = UnityEngine.Resources.Load<UnityEngine.TextAsset>(path);
        if (!t) throw new Exception("데이터 없음: Resources/" + path);
        return Newtonsoft.Json.JsonConvert.DeserializeObject<T>(t.text);
    }

    // 임무 하나를 전투용 맵 정의로 펼침 (좌우 뒤집기 포함)
    public static BattleMapDef MissionMap(MissionDef m)
    {
        var b = MAPS[m.map];
        int W = b.rows[0].Length;
        Func<int, int> fx = x => m.mirror ? W - 1 - x : x;
        return new BattleMapDef
        {
            name = m.name, obj = m.obj,
            goal = m.goal != null ? new GoalDef { x = fx(m.goal.x), z = m.goal.z, r = m.goal.r } : null,
            rows = m.mirror ? b.rows.Select(r => new string(r.Reverse().ToArray())).ToArray() : b.rows,
            hills = b.hills.Select(h => new HillDef { cx = m.mirror ? W - 1 - h.cx : h.cx, cz = h.cz, rx = h.rx, rz = h.rz }).ToArray(),
            spawn = b.spawn.Select(s => new[] { fx(s[0]), s[1] }).ToArray(),
            cats = m.cats.Select(c => new CatSpawn { cls = c.cls, head = c.head, pod = c.pod, target = c.target, x = fx(c.x), z = c.z }).ToArray(),
            mortarShells = m.shells,
        };
    }

    public static readonly Dictionary<char, PropDef> PROPS = new Dictionary<char, PropDef>
    {
        ['S'] = new PropDef { name = "모래주머니", cover = 1, block = true, breakable = true },
        ['C'] = new PropDef { name = "보급 상자", cover = 1, block = true, breakable = true },
        ['W'] = new PropDef { name = "벽돌 벽", cover = 2, block = true, sightBlock = true },
        ['T'] = new PropDef { name = "죽은 나무", cover = 2, block = true, sightBlock = true },
        ['R'] = new PropDef { name = "트럭 잔해", cover = 2, block = true, sightBlock = true },
        ['H'] = new PropDef { name = "돌집 벽", cover = 2, block = true, sightBlock = true },
        ['X'] = new PropDef { name = "철조망", cover = 0, block = true },
        ['M'] = new PropDef { name = "박격포 진지", cover = 0, block = false, emplacement = "mortar" },
        ['G'] = new PropDef { name = "기관총 진지", cover = 0, block = false, emplacement = "hmg", selfCover = 1 },
        // ── 소품 시트 A (2026-10-27, ChatGPT) ── 낮은 엄폐 8
        ['D'] = new PropDef { name = "드럼통 더미", cover = 1, block = true, breakable = true },
        ['B'] = new PropDef { name = "건초 묶음", cover = 1, block = true, breakable = true },
        ['U'] = new PropDef { name = "잔해 더미", cover = 1, block = true },
        ['K'] = new PropDef { name = "뒤집힌 수레", cover = 1, block = true, breakable = true },
        ['F'] = new PropDef { name = "돌담", cover = 1, block = true },
        ['L'] = new PropDef { name = "통나무 더미", cover = 1, block = true },
        ['Q'] = new PropDef { name = "치즈 보급 상자", cover = 1, block = true, breakable = true },
        ['J'] = new PropDef { name = "모래주머니 모서리", cover = 1, block = true, breakable = true },
        //                                   높은 엄폐 8
        ['Y'] = new PropDef { name = "무너진 벽 모서리", cover = 2, block = true, sightBlock = true },
        ['V'] = new PropDef { name = "불탄 자동차", cover = 2, block = true, sightBlock = true },
        ['P'] = new PropDef { name = "헛간", cover = 2, block = true, sightBlock = true },
        ['A'] = new PropDef { name = "건초 더미", cover = 2, block = true, sightBlock = true },
        ['E'] = new PropDef { name = "우물", cover = 2, block = true, sightBlock = true },
        ['Z'] = new PropDef { name = "큰 그루터기", cover = 2, block = true, sightBlock = true },
        ['N'] = new PropDef { name = "성당 벽 조각", cover = 2, block = true, sightBlock = true },
        ['I'] = new PropDef { name = "광고 기둥", cover = 2, block = true, sightBlock = true },
    };
}

public static class RULES
{
    public const int ap = 2;
    public const int sight = 8;
    // 맞은 부위 확률 (가중치). 치명타면 머리가 잘 맞음. 머리 = 즉사
    public static readonly (string part, int w)[] hitParts = { ("head", 10), ("torso", 50), ("arm", 20), ("leg", 20) };
    public static readonly (string part, int w)[] hitPartsCrit = { ("head", 30), ("torso", 40), ("arm", 15), ("leg", 15) };
    public const float severOnCrit = 0.7f;   // 팔 · 다리 치명타면 이 확률로 잘림 → 쓰러짐, 의수 · 의족 전엔 출전 못 함
    public const int armWoundAim = 30;       // 팔 부상: 그 판 명중 감소
    public const float armReloadFail = 0.35f; // 팔 부상: 재장전 실패 확률 (행동력은 씀)
    public const float legWoundMob = 0.5f;   // 다리 부상: 이동 범위 배율
    public const float executeRange = 7;     // 고양이가 쓰러진 쥐를 확인사살하러 가는 거리
    public const float wakeRange = 6;        // 자고 있는 무리는 이 거리 안에서 서로 보여야 깸
    public const int highGroundAim = 20;
    public static readonly int[] coverDef = { 0, 20, 40 };
    public const int hunkerDef = 25;
    public const int flankCrit = 40;
    public const int critDmg = 2;
    public const int overwatchAim = -10;
    public const int suppressAim = -30;
    public const float climbCost = 1;
    public const float diagCost = 1.5f;
}

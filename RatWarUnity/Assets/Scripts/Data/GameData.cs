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
    public int ap, cd; public bool end, once, melee, far; // cd = 쓰고 나서 내 차례 cd 번 동안 못 씀
    public float range, radius; public int dmgMin, dmgMax, psi, uses, heal;
    public bool HasDmg => dmgMax > 0;
}

// 쥐 병종 · 고양이 병과 같이 씀
public class ClassDef
{
    public string id, name, weapon, desc = "";
    public int hp, aim, mob, grenades, medkits, charges, psi, sight, def, crit, aura, whisker;
    public float blast;
    public bool flame, climb, vehicle; // vehicle = 전차 (리그 대신 차체 그림, 엄폐 · 부위 부상 없음)
    public int armor;                  // 장갑: 폭발 아닌 피해를 이만큼 줄임 (최소 1)
    public string[] abil;
    public int[] from = new int[0];
}

public class PropDef
{
    public string name, emplacement; public int cover, selfCover; public bool block, breakable, sightBlock;
}

public class HillDef { public float cx, cz, rx, rz; }
public class MapDef { public string[] rows; public HillDef[] hills; public int[][] spawn; }
public class CatSpawn { public string cls, head, pod; public int x, z; public bool target, elite; }
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
        ["rifle"] = new WeaponDef { name = "game.weapon.rifle.name", dmgMin = 3, dmgMax = 5, clip = 4, crit = 10, range = d => d <= 2 ? 10 : d <= 8 ? 0 : -(d - 8) * 3 },
        ["lmg"] = new WeaponDef { name = "game.weapon.lmg.name", dmgMin = 3, dmgMax = 5, clip = 3, crit = 5, range = d => -5 + (d <= 8 ? 0 : -(d - 8) * 3) },
        ["sniper"] = new WeaponDef { name = "game.weapon.sniper.name", dmgMin = 4, dmgMax = 6, clip = 3, crit = 25, range = d => d <= 3 ? -25 : d <= 6 ? -5 : 5 },
        ["shotgun"] = new WeaponDef { name = "game.weapon.shotgun.name", dmgMin = 4, dmgMax = 7, clip = 2, crit = 20, range = d => d <= 2 ? 30 : d <= 4 ? 15 : d <= 7 ? 0 : -(d - 7) * 8 },
        ["smg"] = new WeaponDef { name = "game.weapon.smg.name", dmgMin = 2, dmgMax = 4, clip = 3, crit = 10, range = d => d <= 3 ? 15 : d <= 6 ? 5 : d <= 9 ? 0 : -(d - 9) * 5 },
        ["hmg"] = new WeaponDef { name = "game.weapon.hmg.name", dmgMin = 4, dmgMax = 6, clip = 99, crit = 10, range = d => 15 + (d <= 10 ? 0 : -(d - 10) * 3) },
        // 화염방사기: 스킬판 '화염방사기 장착'을 찍으면 총 대신 듦. 사격 = 앞쪽 부채꼴 불길 (엄폐 무시), 연료 = 탄창
        ["flamer"] = new WeaponDef { name = "game.weapon.flamer.name", dmgMin = 3, dmgMax = 4, clip = 3, crit = 0, range = d => d <= 4 ? 15 : -200 },
    };
    // 화염방사기 수치
    public const float flamerLen = 4.5f, flamerHalfW = 1.5f, flamerBoomChance = 0.4f, flamerBoomR = 1.5f;
    public const int flamerBoomMin = 3, flamerBoomMax = 5;

    // 능력: ap = 필요한 행동력, end = 쓰면 턴 끝
    public static readonly Dictionary<string, AbilityDef> ABILITIES = Build(new[]
    {
        new AbilityDef { id = "shoot", name = "game.abil.shoot.name", key = "1", ap = 1, end = true, target = "enemy" },
        new AbilityDef { id = "aimed", name = "game.abil.aimed.name", key = "2", ap = 2, end = true, target = "enemy", desc = "game.abil.aimed.desc" },
        new AbilityDef { id = "suppress", name = "game.abil.suppress.name", key = "2", ap = 1, end = true, target = "enemy", desc = "game.abil.suppress.desc" },
        new AbilityDef { id = "grenade", name = "game.abil.grenade.name", key = "3", ap = 1, end = true, target = "tile", range = 9, radius = 1.5f, dmgMin = 3, dmgMax = 4, desc = "game.abil.grenade.desc" },
        new AbilityDef { id = "firstaid", name = "game.abil.firstaid.name", key = "4", ap = 1, end = false, target = "ally", heal = 4, desc = "game.abil.firstaid.desc" },
        new AbilityDef { id = "overwatch", name = "game.abil.overwatch.name", key = "5", ap = 1, end = true, target = "self", desc = "game.abil.overwatch.desc" },
        new AbilityDef { id = "hunker", name = "game.abil.hunker.name", key = "6", ap = 1, end = true, target = "self", desc = "game.abil.hunker.desc" },
        new AbilityDef { id = "reload", name = "game.abil.reload.name", key = "r", ap = 1, end = false, target = "self" },
        new AbilityDef { id = "mortar", name = "game.abil.mortar.name", key = "m", ap = 2, end = true, target = "tile", range = 16, radius = 1.5f, dmgMin = 4, dmgMax = 5, desc = "game.abil.mortar.desc" },
        new AbilityDef { id = "heavy", name = "game.abil.heavy.name", key = "h", ap = 1, end = true, target = "enemy", desc = "game.abil.heavy.desc" },
        // ── 병종 · 스킬판 능력 (쥐 테마) ──
        new AbilityDef { id = "ambush", name = "game.abil.ambush.name", ap = 1, end = true, target = "self", desc = "game.abil.ambush.desc" },
        new AbilityDef { id = "warcry", name = "game.abil.warcry.name", ap = 0, end = false, target = "self", once = true, desc = "game.abil.warcry.desc" },
        new AbilityDef { id = "charge", name = "game.abil.charge.name", ap = 1, end = true, target = "tile", range = 1.5f, desc = "game.abil.charge.desc" },
        new AbilityDef { id = "tunnel", name = "game.abil.tunnel.name", ap = 2, end = true, target = "tile", range = 7, once = true, desc = "game.abil.tunnel.desc" },
        new AbilityDef { id = "flame", name = "game.abil.flame.name", ap = 1, end = true, target = "tile", range = 3.2f, radius = 1.1f, dmgMin = 3, dmgMax = 4, uses = 2, desc = "game.abil.flame.desc" },
        new AbilityDef { id = "knife", name = "game.abil.knife.name", ap = 1, end = true, target = "enemy", melee = true, desc = "game.abil.knife.desc" },
        new AbilityDef { id = "mark", name = "game.abil.mark.name", ap = 1, end = false, target = "enemy", desc = "game.abil.mark.desc" },
        new AbilityDef { id = "command", name = "game.abil.command.name", ap = 1, end = true, target = "ally", far = true, desc = "game.abil.command.desc" },
        new AbilityDef { id = "dominate", name = "game.abil.dominate.name", ap = 2, end = true, target = "enemy", psi = 2, desc = "game.abil.dominate.desc" },
        // ── 2026-10-30 병종 액티브 확정안 (docs/SKILLS_TRAITS_PLAN.md 2-0) ──
        new AbilityDef { id = "barrage", name = "game.abil.barrage.name", ap = 2, end = true, target = "enemy", cd = 3, range = 7, desc = "game.abil.barrage.desc" },
        new AbilityDef { id = "trap", name = "game.abil.trap.name", ap = 1, end = true, target = "tile", range = 3, radius = 1, dmgMin = 3, dmgMax = 4, uses = 2, desc = "game.abil.trap.desc" },
        new AbilityDef { id = "smoke", name = "game.abil.smoke.name", ap = 1, end = false, target = "tile", range = 6, radius = 1.5f, cd = 3, desc = "game.abil.smoke.desc" },
        new AbilityDef { id = "dash", name = "game.abil.dash.name", ap = 1, end = true, target = "enemy", range = 5, cd = 3, desc = "game.abil.dash.desc" },
        new AbilityDef { id = "painkiller", name = "game.abil.painkiller.name", ap = 1, end = false, target = "ally", desc = "game.abil.painkiller.desc" },
        new AbilityDef { id = "supply", name = "game.abil.supply.name", ap = 2, end = true, target = "self", radius = 4, heal = 3, cd = 3, desc = "game.abil.supply.desc" },
        new AbilityDef { id = "flare", name = "game.abil.flare.name", ap = 1, end = false, target = "tile", range = 10, radius = 4, cd = 3, desc = "game.abil.flare.desc" },
        new AbilityDef { id = "artillery", name = "game.abil.artillery.name", ap = 1, end = true, target = "tile", range = 16, radius = 2, dmgMin = 4, dmgMax = 5, desc = "game.abil.artillery.desc" },
        new AbilityDef { id = "terror", name = "game.abil.terror.name", ap = 1, end = true, target = "enemy", psi = 1, desc = "game.abil.terror.desc" },
        // ── 고양이 전용 ──
        new AbilityDef { id = "cannon", name = "game.abil.cannon.name", ap = 1, end = true, target = "tile", range = 12, radius = 1.5f, dmgMin = 4, dmgMax = 5, cd = 2, desc = "game.abil.cannon.desc" },
        new AbilityDef { id = "cleanse", name = "game.abil.cleanse.name", ap = 1, end = false, target = "ally", far = true, psi = 1, desc = "game.abil.cleanse.desc" },
    });

    static Dictionary<string, AbilityDef> Build(AbilityDef[] a) => a.ToDictionary(x => x.id);

    // 고양이 군 (쥐 병종은 SkillsData.RAT_CLASSES)
    public static readonly Dictionary<string, ClassDef> CLASSES = new[]
    {
        new ClassDef { id = "trooper", name = "game.class.trooper.name", hp = 5, aim = 60, mob = 6, weapon = "smg", grenades = 1, abil = new[] { "shoot", "grenade", "overwatch", "hunker" } },
        new ClassDef { id = "mgcat", name = "game.class.mgcat.name", hp = 6, aim = 55, mob = 5, weapon = "lmg", abil = new[] { "shoot", "suppress", "overwatch", "hunker" } },
        new ClassDef { id = "officer", name = "game.class.officer.name", hp = 8, aim = 65, mob = 6, weapon = "smg", aura = 10, abil = new[] { "shoot", "overwatch", "hunker" } },
        new ClassDef { id = "catsniper", name = "game.class.catsniper.name", hp = 4, aim = 72, mob = 5, weapon = "sniper", sight = 2, abil = new[] { "shoot", "overwatch", "hunker" } },
        new ClassDef { id = "catflame", name = "game.class.catflame.name", hp = 6, aim = 55, mob = 6, weapon = "smg", flame = true, abil = new[] { "shoot", "flame", "hunker" } },
        // ── 2026-11-02 진행도 병과 (War.CatMix 가 위협 단계에 따라 섞음) ──
        new ClassDef { id = "catassault", name = "game.class.catassault.name", hp = 6, aim = 58, mob = 7, weapon = "shotgun", abil = new[] { "shoot", "overwatch", "hunker" } },
        new ClassDef { id = "catgren", name = "game.class.catgren.name", hp = 6, aim = 55, mob = 6, weapon = "smg", grenades = 3, abil = new[] { "shoot", "grenade", "overwatch", "hunker" } },
        new ClassDef { id = "catheavy", name = "game.class.catheavy.name", hp = 10, aim = 55, mob = 4, weapon = "lmg", def = 15, abil = new[] { "shoot", "suppress", "overwatch" } },
        new ClassDef { id = "cattank", name = "game.class.cattank.name", hp = 16, aim = 60, mob = 5, weapon = "lmg", vehicle = true, armor = 2, abil = new[] { "shoot", "cannon", "overwatch" } },
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
            cats = m.cats.Select(c => new CatSpawn { cls = c.cls, head = c.head, pod = c.pod, target = c.target, elite = c.elite, x = fx(c.x), z = c.z }).ToArray(),
            mortarShells = m.shells,
        };
    }

    public static readonly Dictionary<char, PropDef> PROPS = new Dictionary<char, PropDef>
    {
        ['S'] = new PropDef { name = "game.prop.sandbag", cover = 1, block = true, breakable = true },
        ['C'] = new PropDef { name = "game.prop.crate", cover = 1, block = true, breakable = true },
        ['W'] = new PropDef { name = "game.prop.brickwall", cover = 2, block = true, sightBlock = true },
        ['T'] = new PropDef { name = "game.prop.deadtree", cover = 2, block = true, sightBlock = true },
        ['R'] = new PropDef { name = "game.prop.truck", cover = 2, block = true, sightBlock = true },
        ['H'] = new PropDef { name = "game.prop.stonewall", cover = 2, block = true, sightBlock = true },
        ['X'] = new PropDef { name = "game.prop.wire", cover = 0, block = true },
        ['M'] = new PropDef { name = "game.prop.mortar", cover = 0, block = false, emplacement = "mortar" },
        ['G'] = new PropDef { name = "game.prop.mgnest", cover = 0, block = false, emplacement = "hmg", selfCover = 1 },
        // ── 소품 시트 A (2026-10-27, ChatGPT) ── 낮은 엄폐 8
        ['D'] = new PropDef { name = "game.prop.drums", cover = 1, block = true, breakable = true },
        ['B'] = new PropDef { name = "game.prop.haybale", cover = 1, block = true, breakable = true },
        ['U'] = new PropDef { name = "game.prop.rubble", cover = 1, block = true },
        ['K'] = new PropDef { name = "game.prop.cart", cover = 1, block = true, breakable = true },
        ['F'] = new PropDef { name = "game.prop.drywall", cover = 1, block = true },
        ['L'] = new PropDef { name = "game.prop.logs", cover = 1, block = true },
        ['Q'] = new PropDef { name = "game.prop.cheese", cover = 1, block = true, breakable = true },
        ['J'] = new PropDef { name = "game.prop.sandcorner", cover = 1, block = true, breakable = true },
        //                                   높은 엄폐 8
        ['Y'] = new PropDef { name = "game.prop.ruincorner", cover = 2, block = true, sightBlock = true },
        ['V'] = new PropDef { name = "game.prop.burntcar", cover = 2, block = true, sightBlock = true },
        ['P'] = new PropDef { name = "game.prop.barn", cover = 2, block = true, sightBlock = true },
        ['A'] = new PropDef { name = "game.prop.haystack", cover = 2, block = true, sightBlock = true },
        ['E'] = new PropDef { name = "game.prop.well", cover = 2, block = true, sightBlock = true },
        ['Z'] = new PropDef { name = "game.prop.stump", cover = 2, block = true, sightBlock = true },
        ['N'] = new PropDef { name = "game.prop.chapel", cover = 2, block = true, sightBlock = true },
        ['I'] = new PropDef { name = "game.prop.billboard", cover = 2, block = true, sightBlock = true },
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

    // 난이도 (엑스컴식): 쥐 · 고양이 명중 보정 + 쉬울수록 운이 덜 가혹함
    //   streak = 쥐가 빗나갈 때마다 다음 사격 명중 +streak (맞히면 0, 최대 streakMax)
    //   chain  = 고양이가 이번 턴 쥐를 맞힐 때마다 다음 고양이 사격 명중 −chain
    // 전쟁(아웃게임) 쪽 — 새 전쟁 시작 때 고르고 그 전쟁 동안 고정:
    //   threatStep = 위협 단계 하나 오르는 데 드는 진척 (클수록 고양이가 느리게 세짐) · supplies = 시작 보급품
    //   income = 턴 수입 배율 · invade = 한 턴 침공 수 보정 (+ 는 8턴부터) · hold = 자동 방어 성공률 보정
    //   land = 처음 가진 땅 칸 수 (사령부에서 가까운 순, campaign.json start 앞에서부터)
    public class Diff
    {
        public string name, tag, desc; public int ratAim, catAim, catCrit, streak, streakMax, chain;
        public float threatStep = 3, income = 1, hold; public int supplies = 60, invade, land = 12;
    }
    public static readonly Diff[] DIFFS =
    {
        new Diff { name = "game.diff.easy.name", tag = "game.diff.easy.tag", desc = "game.diff.easy.desc", ratAim = 10, catAim = -10, catCrit = -10, streak = 10, streakMax = 30, chain = 10,
            threatStep = 4, supplies = 90, income = 1.25f, invade = -1, hold = 0.10f, land = 16 },
        new Diff { name = "game.diff.normal.name", tag = "game.diff.normal.tag", desc = "game.diff.normal.desc", streak = 5, streakMax = 15, chain = 5 },
        new Diff { name = "game.diff.hard.name", tag = "game.diff.hard.tag", desc = "game.diff.hard.desc", catAim = 5, catCrit = 5,
            threatStep = 2.5f, supplies = 50, income = 0.9f, hold = -0.05f, land = 9 },
        new Diff { name = "game.diff.nightmare.name", tag = "game.diff.nightmare.tag", desc = "game.diff.nightmare.desc", catAim = 10, catCrit = 10,
            threatStep = 2, supplies = 40, income = 0.75f, invade = 1, hold = -0.10f, land = 6 },
    };
    public static Diff DiffOf(int i) => DIFFS[UnityEngine.Mathf.Clamp(i, 0, DIFFS.Length - 1)];
    public const int defaultDiff = 1;
    // 참호의 악몽(마지막 난이도)은 병장 이상으로 전쟁을 이겨야 열림 (슬롯과 상관없이 이 컴퓨터에 남김)
    public static bool Unlocked(int i) => i < DIFFS.Length - 1 || UnityEngine.PlayerPrefs.GetInt("diffClear", -1) >= defaultDiff;
    public static void Cleared(int i) { if (i > UnityEngine.PlayerPrefs.GetInt("diffClear", -1)) { UnityEngine.PlayerPrefs.SetInt("diffClear", i); UnityEngine.PlayerPrefs.Save(); } }
}

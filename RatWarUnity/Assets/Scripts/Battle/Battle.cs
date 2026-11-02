// 쥐 전쟁 시뮬 — 전투 규칙 · 턴 흐름 · 고양이 AI (웹 game.js)
// 화면 그리기는 View(BoardView · Hud). 둘이 같이 쓰는 상태는 여기 static 필드 (웹의 G).
// 기다림(sleep)은 Awaitable.WaitForSecondsAsync — Time.timeScale 을 따름 (슬로 모션 중엔 길어짐)
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static CampRules;

public static class Battle
{
    // ── 상태 (웹 G) ──
    public static WorldMap world;
    public static List<Unit> units = new List<Unit>();
    public static string turn = "rat";
    public static int round = 1;
    public static Unit sel;
    public static string mode = "idle"; // idle | move | target | tile | busy | over
    public static string ability;
    public static int targetIdx;
    public static int shells;
    public static List<(string msg, string kind)> log = new List<(string, string)>();
    public static HashSet<int> vis = new HashSet<int>();
    public static string over;
    public static CampData camp;
    public static MissionDef mission;
    public static List<(int x, int z, int turns)> fires = new List<(int, int, int)>();
    public static int scan;
    public static HashSet<string> alerted = new HashSet<string>();
    public static int statKills, statLost;
    public static int version; // 상태가 바뀔 때마다 올라감 (HUD 다시 그리기 신호)
    public static int catHits; // 이번 턴 고양이가 쥐를 맞힌 횟수 (난이도 연속 명중 완화)
    // 연막 · 조명탄 칸(남은 아군 차례 수) · 부비트랩 · 포격 예정 자리 (owner = 놓은 쥐 id)
    public static List<(int x, int z, int turns)> smokes = new List<(int, int, int)>(), flares = new List<(int, int, int)>();
    public static List<(int x, int z, int owner)> traps = new List<(int, int, int)>(), strikes = new List<(int, int, int)>();
    public static bool SmokeAt(int x, int z) => smokes.Any(s => s.x == x && s.z == z);
    static Unit ById(int id) => units.FirstOrDefault(o => o.id == id);
    static bool Near(Unit u, Func<Unit, bool> pred, float r = 3) => units.Any(o => o != u && o.alive && pred(o) && World.Dist(o, u) <= r);
    // 같이 출전한 관계 상대가 곁에 있나 (kind = comrade · mentor · pupil · feud)
    static bool PartnerNear(Unit u, string kind, int x, int z, float r = 3) => u.r != null && Rats().Any(o => o != u && o.rosterId != null && Camp.Bonded(u.r, o.rosterId, kind) && World.Dist(o.x, o.z, x, z) <= r);
    public static RULES.Diff Diff => RULES.DIFFS[Mathf.Clamp(camp != null ? camp.diff : RULES.defaultDiff, 0, RULES.DIFFS.Length - 1)];

    static int UID;
    static System.Random rng = new System.Random();
    public static int Rnd(int a, int b) => a + rng.Next(b - a + 1);
    static float R100 => (float)rng.NextDouble() * 100;
    static float R01 => (float)rng.NextDouble();
    static Awaitable Sleep(float ms) => Awaitable.WaitForSecondsAsync(ms / 1000f);
    public static void Say(string msg, string kind = "") { log.Insert(0, (msg, kind)); if (log.Count > 7) log.RemoveRange(7, log.Count - 7); version++; }
    static Dictionary<string, AbilityDef> AB => GameData.ABILITIES;
    static WeaponDef WP(string w) => GameData.WEAPONS[w];

    // ── 유닛 ──
    static Unit MakeUnit(string side, string cls, int x, int z, RatRecord r = null, RatStats st = null, string pod = null, bool target = false, int faceIdx = -1)
    {
        var def = st != null ? st.cls : GameData.CLASSES[cls];
        var u = new Unit
        {
            id = ++UID, side = side, cls = st != null ? st.clsId : cls, def = def, x = x, z = z, px = x, pz = z,
            name = r != null ? r.name : def.name,
            maxHp = st != null ? st.hp : def.hp, hp = st != null ? st.hp : def.hp, aim = st != null ? st.aim : def.aim,
            weapon = st?.weapon != null && st.weapon != def.weapon ? st.weapon : null,
            clip = WP(st?.weapon ?? def.weapon).clip + (st != null ? st.clip : 0),
            grenades = st != null ? st.grenades : def.grenades, medkits = st != null ? st.medkits : def.medkits,
            charges = st != null ? st.charges : 0, psi = st != null ? st.psi : 0,
            flame = def.flame ? AB["flame"].uses : 0,
            critB = st != null ? st.crit : 0, defB = st != null ? st.def : 0, dmgB = st != null ? st.dmg : 0, healB = st != null ? st.heal : 0,
            blast = st != null ? st.blast : 0, boom = st != null ? st.boom : 0,
            sight = st != null ? st.sight : def.sight, climb = st != null && st.climb, whisker = st != null ? st.whisker : 0,
            perks = st != null ? st.perks : new List<string>(), abil = st != null ? st.abil : def.abil.ToList(),
            mob = st != null ? st.mob : def.mob,
            active = side == "rat", pod = pod, seen = side == "rat", target = target,
            stress = r != null ? r.stress : 0, r = r, rosterId = r?.id, xp = r != null ? r.xp : 0,
            fdz = side == "rat" ? -1 : 1, faceIdx = faceIdx,
        };
        if (st == null) u.defB = def.def; // 고양이 병과 회피 (중장갑)
        u.ammo = u.clip; u.baseMob = u.mob;
        foreach (var a in u.abil) if (AB[a].uses > 0 && a != "flame") u.left[a] = AB[a].uses;
        return u;
    }

    public static IEnumerable<Unit> Rats() => units.Where(u => u.side == "rat" && u.alive);
    public static IEnumerable<Unit> Cats() => units.Where(u => u.side == "cat" && u.alive);
    public static IEnumerable<Unit> EnemiesOf(Unit u) => units.Where(o => o.alive && o.side != u.side);
    public static IEnumerable<Unit> FriendsOf(Unit u) => units.Where(o => o.alive && o.side == u.side);

    // 고양이 얼굴: 정해 둔 머리 이름 → 개체 얼굴 번호 (얼룩 · 턱시도 · 뚱보 주황 · 샴 · 흉터 흰 수컷)
    static readonly Dictionary<string, int> CAT_FACE = new Dictionary<string, int> { ["mackerel"] = 0, ["tuxedo"] = 1, ["chonk"] = 2, ["loaf"] = 2, ["boss_ryan"] = 3, ["boss_turret"] = 4, ["boss_dictator"] = 4 };

    // 정예 고양이 (위협 단계가 높을수록 자주): 체력 · 명중 보너스
    public const int ELITE_HP = 2, ELITE_AIM = 10;

    // 전투 시작: m = MISSIONS 하나, deploy = 출전할 쥐 id (최대 6)
    public static void StartBattle(MissionDef m, List<string> deploy)
    {
        UID = 0;
        BattleSave.Delete(); // 새 출격 = 예전 중간 저장은 버림
        camp = Camp.Load() ?? Camp.Fresh();
        mission = m ?? Camp.Current(camp);
        var def = GameData.MissionMap(mission);
        world = World.Create(def);
        units = new List<Unit>();
        world.units = units;
        fires.Clear(); scan = 0; smokes.Clear(); flares.Clear(); traps.Clear(); strikes.Clear();
        var ids = (deploy ?? camp.deploy).Where(id => camp.squad.Any(r => r.id == id && Camp.Ready(r))).Take(DEPLOY_MAX).ToList();
        if (ids.Count == 0) ids.AddRange(camp.squad.Where(Camp.Ready).Take(DEPLOY_MAX).Select(r => r.id));
        for (int i = 0; i < ids.Count; i++)
        {
            var r = camp.squad.First(q => q.id == ids[i]);
            var st = Camp.Stats(r, ids);
            foreach (var t in r.traits) { var hs = TRAITS[t].Get("homesick"); if (hs > 0) r.stress = Mathf.Min(99, r.stress + (int)hs); }
            var u = MakeUnit("rat", st.clsId, def.spawn[i][0], def.spawn[i][1], r, st, faceIdx: r.face);
            u.stress = r.stress;
            units.Add(u);
        }
        foreach (var c in def.cats)
        {
            var u = MakeUnit("cat", c.cls, c.x, c.z, pod: c.pod, target: c.target, faceIdx: CAT_FACE.TryGetValue(c.head ?? "", out var f) ? f : -1);
            if (c.elite) { u.maxHp += ELITE_HP; u.hp += ELITE_HP; u.aim += ELITE_AIM; u.name = "정예 " + u.name; }
            if (c.target) u.name = "표적 · " + u.name;
            units.Add(u);
        }
        shells = def.mortarShells;
        turn = "rat"; round = 1; over = null; log.Clear(); alerted.Clear();
        statKills = statLost = 0;
        BeginTurn("rat");
        UpdateVision();
        Say(mission.name, "title");
    }

    // 저장한 전투 이어하기 (BattleSave). 실행 중에만 쓰는 참조(병과 표 · 쥐 기록 · 제압 상대)를 다시 이어 붙임
    public static bool Restore(BattleSaveData d)
    {
        var m = GameData.MISSIONS.FirstOrDefault(x => x.id == d.missionId) ?? War.FromId(d.camp, d.missionId);
        if (m == null || d.units == null) return false;
        // 사령부에 다녀왔으면 그동안 바뀐 기록(모집 · 스킬 · 보급품)을 씀. 같은 전투 · 출전 쥐가 다 있을 때만, 아니면 저장 때 기록
        var now = Camp.Load();
        bool same = now?.war != null && d.camp?.war != null && now.war.battleHex == d.camp.war.battleHex && now.war.turn == d.camp.war.turn
            && d.units.Where(u => u.rosterId != null).All(u => now.squad.Any(q => q.id == u.rosterId));
        camp = same ? now : Camp.Migrate(d.camp); mission = m;
        world = World.Create(GameData.MissionMap(m));
        for (int i = 0; i < world.tiles.Length && i < d.props.Length; i++)
        {
            world.tiles[i].prop = string.IsNullOrEmpty(d.props[i]) ? '\0' : d.props[i][0];
            world.tiles[i].ground = d.grounds[i];
        }
        world.tunnels = d.tunnels.Select(t => (new Pt(t[0], t[1]), new Pt(t[2], t[3]))).ToList();
        units = d.units; world.units = units;
        foreach (var u in units)
        {
            u.r = u.rosterId != null ? camp.squad.FirstOrDefault(q => q.id == u.rosterId) : null;
            u.def = u.r != null ? SkillsData.RAT_CLASSES[u.cls] : GameData.CLASSES[u.cls];
            u.st = new AnimState();
            u.abil.RemoveAll(a => !AB.ContainsKey(a)); // 없어진 능력 (예전 저장)
            if (u.baseMob == 0) u.baseMob = u.mob;
            if (!u.alive) { u.st.death = 1; u.forceSide = true; }   // 시체 · 쓰러진 몸은 누운 채로
            u.tint = u.controlled > 0 ? SkillsData.Hex("#b9a0ff") : Color.white;
        }
        foreach (var u in units)
        {
            u.suppressedBy = units.FirstOrDefault(o => o.id == u.suppressedById);
            u.suppressing = units.FirstOrDefault(o => o.id == u.suppressingId);
        }
        fires = d.fires.Select(f => (f[0], f[1], f[2])).ToList();
        (int, int, int) T3(int[] f) => (f[0], f[1], f[2]);
        smokes = (d.smokes ?? new List<int[]>()).Select(T3).ToList(); flares = (d.flares ?? new List<int[]>()).Select(T3).ToList();
        traps = (d.traps ?? new List<int[]>()).Select(T3).ToList(); strikes = (d.strikes ?? new List<int[]>()).Select(T3).ToList();
        shells = d.shells; scan = d.scan; round = d.round; UID = d.uid;
        alerted = new HashSet<string>(d.alerted);
        statKills = d.statKills; statLost = d.statLost;
        log = d.log.Select(l => (l[0], l[1])).ToList();
        turn = "rat"; over = null; mode = "move"; sel = null; ability = null;
        UpdateVision();
        return true;
    }

    // ── 시야 ──
    public static HashSet<string> UpdateVision()
    {
        var w = world;
        vis = new HashSet<int>();
        foreach (var r in Rats())
        {
            float s = World.SightOf(w, r);
            foreach (var t in w.tiles)
                if (World.Dist(t.x, t.z, r.x, r.z) <= s && World.Los(w, r.x, r.z, t.x, t.z)) vis.Add(t.z * w.W + t.x);
        }
        // 조명탄: 반경 안은 벽 · 안개 상관없이 보임
        foreach (var f in flares)
            foreach (var t in w.tiles) if (World.Dist(t.x, t.z, f.x, f.z) <= AB["flare"].radius) vis.Add(t.z * w.W + t.x);
        // 무리 깨우기: 가까이서(wakeRange) 서로 보이거나, 맞았으면 그 무리 전부
        var wake = new HashSet<string>(alerted);
        var rats = Rats().ToList();
        foreach (var c in Cats())
        {
            bool whisk = rats.Any(r => r.whisker > 0 && World.Dist(r, c) <= r.whisker);
            bool lit = flares.Any(f => World.Dist(c.x, c.z, f.x, f.z) <= AB["flare"].radius);
            // 연막 속 고양이는 3칸 안에서만 보임 (조명탄이 비추면 보임)
            bool hid = SmokeAt(c.x, c.z) && !lit && !rats.Any(r => World.Dist(r, c) <= 3);
            bool seenNow = (vis.Contains(c.z * w.W + c.x) && !hid) || whisk || scan > 0;
            if (seenNow && !c.seen) c.seen = true;
            c.visible = seenNow || c.revealed;
            // 조용한 발걸음(ghost): 그 쥐 곁에서는 3칸 안에서만 깸
            if (!c.active && rats.Any(r => World.Dist(r, c) <= (r.perks.Contains("ghost") ? 3 : RULES.wakeRange) && World.CanSee(w, c, r))) wake.Add(c.pod);
        }
        version++;
        return wake;
    }

    // 무리가 깨면 엑스컴처럼 엄폐물로 흩어짐 (공짜 이동)
    static async Awaitable WakePods(HashSet<string> pods)
    {
        foreach (var pod in pods)
        {
            alerted.Remove(pod);
            var group = Cats().Where(c => c.pod == pod && !c.active).ToList();
            if (group.Count == 0) continue;
            foreach (var c in group) c.active = true;
            Say($"고양이 무리 발견! ({group.Count})", "warn");
            View.Focus(group[0]);
            await Sleep(350);
            foreach (var c in group)
            {
                var spot = BestTile(c, c.mob, true);
                if (spot != null && (spot.x != c.x || spot.z != c.z)) await MoveUnit(c, spot.path, scamper: true);
            }
        }
    }

    // ── 명중률 ──
    public class HitInfo { public int hit, crit, cover; public bool flanked; public List<(string n, int v)> parts; public WeaponDef wp; }
    public static HitInfo GetHitInfo(Unit a, Unit b, bool overwatchShot = false, bool aimed = false, bool heavy = false, bool knife = false, int ax = int.MinValue, int az = 0, int extra = 0, string extraName = null)
    {
        var w = world;
        int x = ax == int.MinValue ? a.x : ax, z = ax == int.MinValue ? a.z : az; // 고양이 AI: 가상의 자리에서 계산
        var wp = WP(heavy ? "hmg" : a.Weapon);
        float d = World.Dist(x, z, b.x, b.z);
        var parts = new List<(string, int)> { ("조준", a.aim), ("거리", wp.range(World.JsRound(d))) };
        bool flamer = !heavy && !knife && a.Weapon == "flamer";
        var cv = knife || flamer || b.def.vehicle ? (0, false) : World.CoverAgainst(w, b.x, b.z, x, z); // 전차는 엄폐 못 함
        if (flamer) parts.Add(("불길 (엄폐 무시)", 0));
        if (cv.Item1 > 0) parts.Add((cv.Item1 == 2 ? "높은 엄폐" : "낮은 엄폐", -RULES.coverDef[cv.Item1]));
        if (b.hunker && cv.Item1 > 0) parts.Add(("엎드림", -RULES.hunkerDef));
        int ha = World.TileAt(w, x, z).h, hb = World.TileAt(w, b.x, b.z).h;
        if (ha > hb) parts.Add(("고지", RULES.highGroundAim));
        if (a.suppressedBy != null) parts.Add(("제압당함", RULES.suppressAim));
        if (a.wounds.ContainsKey("arm") && !a.painkiller) parts.Add(("팔 부상", -RULES.armWoundAim));
        if (overwatchShot) parts.Add(("경계 사격", RULES.overwatchAim));
        if (overwatchShot && Camp.Has(a.r, "veteran") && !a.perks.Contains("sentry")) parts.Add(("노병", -RULES.overwatchAim / 2));
        if (extra != 0) parts.Add((extraName ?? "보정", extra));
        // 연막: 쏘는 쪽 · 맞는 쪽이 연막 안이거나 연막을 지나감
        if (!knife && (SmokeAt(x, z) || SmokeAt(b.x, b.z) || World.LineAny(w, x, z, b.x, b.z, t => SmokeAt(t.x, t.z)))) parts.Add(("연막", -30));
        if (aimed) parts.Add(("정조준", 20));
        if (knife) parts.Add(("칼", 30));
        if (a.buffAim != 0) parts.Add(("함성", a.buffAim));
        if (b.marked && a.side == "rat") parts.Add(("표적 지정", 15));
        if (a.perks.Contains("closeq") && d <= 4) parts.Add(("근접 사격", 15));
        if (overwatchShot && a.perks.Contains("sentry")) parts.Add(("노련한 경계", -RULES.overwatchAim));
        bool weak = b.marked && b.weakMarked && a.side == "rat";
        if (b.defB != 0 && !weak) parts.Add(("회피", -b.defB));
        if (weak) parts.Add(("약점 간파", 0));
        if (Camp.Has(b.r, "coverpro") && cv.Item1 > 0) parts.Add(("엄폐 장인", -10));
        if (a.side == "rat" && Rats().Any(o => o != a && Camp.Has(o.r, "leader") && World.Dist(o.x, o.z, x, z) <= 3)) parts.Add(("지휘관 곁", 5));
        if (Camp.Has(a.r, "loner") && !Rats().Any(o => o != a && World.Dist(o.x, o.z, x, z) <= 3)) parts.Add(("외톨이", 10));
        if (a.r != null && a.r.traits.Contains("sibling") && !string.IsNullOrEmpty(a.r.sibling) && units.Any(o => o.rosterId == a.r.sibling && !o.alive && !o.downed)) parts.Add(("복수심", 15));
        if (a.side == "cat" && Cats().Any(o => o.def.aura > 0 && o != a && World.Dist(o.x, o.z, x, z) <= 4)) parts.Add(("장교 지휘", 10));
        // 특성 (쥐)
        var r = a.r;
        bool half = a.hp * 2 <= a.maxHp;
        if (r != null)
        {
            if (Camp.Has(r, "cautious")) parts.Add(("신중함", a.moved ? -5 : 10));
            if (Camp.Has(r, "desperate") && half) parts.Add(("절박함", 10));
            if (Camp.Has(r, "brawler") && d <= 4) parts.Add(("근접 광", 10));
            if (Camp.Has(r, "brawler") && d > 9) parts.Add(("근접 광", -10));
            if (Camp.Has(r, "longshot") && d > 9) parts.Add(("원거리 본능", 10));
            if (Camp.Has(r, "longshot") && d <= 3) parts.Add(("원거리 본능", -10));
            if (Camp.Has(r, "reckless") && World.CoverSides(w, x, z).Count == 0) parts.Add(("무모함", 10));
            if (a.rageAim) parts.Add(("분노", 15));
            if (Camp.Has(r, "comrade") && PartnerNear(a, "comrade", x, z)) parts.Add(("전우 곁", 5));
            if (Camp.Has(r, "pupil") && PartnerNear(a, "mentor", x, z)) parts.Add(("멘토 곁", 8));
            if (knife && Camp.Has(r, "knifer")) parts.Add(("칼잡이", 10));
        }
        // 난이도
        var D = Diff;
        if (a.side == "rat")
        {
            if (D.ratAim != 0) parts.Add(("난이도", D.ratAim));
            if (D.streak > 0 && a.missStreak > 0) parts.Add(("끈기", Mathf.Min(D.streakMax, a.missStreak * D.streak)));
        }
        else
        {
            if (D.catAim != 0) parts.Add(("난이도", D.catAim));
            if (D.chain > 0 && catHits > 0 && b.side == "rat") parts.Add(("연속 명중 완화", -catHits * D.chain));
        }
        int hit = Mathf.Clamp(parts.Sum(p => p.Item2), 1, 99);
        bool open = cv.Item1 == 0 && World.CoverSides(w, b.x, b.z).Count == 0;
        int crit = wp.crit + (cv.Item2 || open ? RULES.flankCrit : 0);
        if (open) crit = wp.crit + 15;
        if (cv.Item2) crit = wp.crit + RULES.flankCrit;
        crit += a.critB;
        if (r != null && Camp.Has(r, "desperate") && half) crit += 10;
        if (knife && Camp.Has(r, "knifer")) crit += 10;
        if (weak) crit += 15;
        if (flamer) crit = 0;
        if (a.side == "cat") crit = Mathf.Max(0, crit + D.catCrit);
        if (b.def.vehicle) crit = 0; // 장갑엔 급소가 없음
        return new HitInfo { hit = hit, crit = Mathf.Min(100, crit), parts = parts, cover = cv.Item1, flanked = cv.Item2, wp = wp };
    }

    static bool CanTarget(Unit a, Unit b)
    {
        if (b.hidden && a.side == "cat") return false;
        return b.alive && (b.side == "rat" || b.visible || a.side == "cat") && World.CanSee(world, a, b);
    }
    public static List<Unit> VisibleEnemies(Unit u) => EnemiesOf(u).Where(e => CanTarget(u, e)).ToList();

    // ── 턴 ──
    static void BeginTurn(string side)
    {
        turn = side;
        catHits = 0;
        if (side == "rat")
        {
            scan = 0;
            // 지배 끝난 고양이는 돌아감
            foreach (var u in units) if (u.controlled > 0 && --u.controlled <= 0) { u.side = "cat"; u.active = true; u.tint = Color.white; View.Popup(u, "정신 돌아옴", "#ff9f8f"); Say($"{u.name} 지배 풀림", "warn"); }
        }
        if (side == "cat") foreach (var c in units) c.revealed = false;
        if (side == "rat")
        {
            smokes = smokes.Select(f => (f.x, f.z, f.turns - 1)).Where(f => f.Item3 > 0).ToList();
            flares = flares.Select(f => (f.x, f.z, f.turns - 1)).Where(f => f.Item3 > 0).ToList();
            View.MarksChanged();
        }
        foreach (var u in units.Where(u => u.alive && u.side == side).ToList())
        {
            u.ap = RULES.ap; u.moved = false;
            u.overwatch = false; u.hunker = false; u.buffAim = 0; u.fired = false;
            if (u.suppressing != null) { u.suppressing.suppressedBy = null; u.suppressing = null; }
            if (u.panic > 0) { u.panic--; u.ap = 0; View.Popup(u, "공황 — 움직이지 못함", "#c9a3ff"); }
            foreach (var k in u.cd.Keys.ToList()) if (--u.cd[k] <= 0) u.cd.Remove(k);
            if (u.dazed > 0) { u.dazed = 0; u.ap = Mathf.Max(0, u.ap - 1); View.Popup(u, "멍함 — 행동력 −1", "#c9a3ff"); }
            if (side == "rat" && Camp.Has(u.r, "smoker") && round > 1) AddStress(u, -(int)TRAITS["smoker"].Get("smoke"));
            if (side != "rat" || u.r == null || round <= 1) continue;
            if (Camp.Has(u.r, "pious") && u.stress > 60) { AddStress(u, -5); View.Popup(u, "기도", "#c9a3ff"); }
            if (Camp.Has(u.r, "catphobia") && Cats().Any(c => c.visible && World.Dist(c, u) <= 3)) AddStress(u, 5, "phobia");
            if (Camp.Has(u.r, "feud") && PartnerNear(u, "feud", u.x, u.z)) AddStress(u, 3, "feud");
        }
        version++;
    }

    // 불 · 화상: 턴이 바뀔 때 (불 난 칸 위 · 화상 입은 유닛)
    static async Awaitable BurnTick()
    {
        fires = fires.Select(f => (f.x, f.z, f.turns - 1)).Where(f => f.Item3 > 0).ToList();
        View.FiresChanged();
        foreach (var u in units.Where(u => u.alive && (u.burn > 0 || fires.Any(f => f.x == u.x && f.z == u.z))).ToList())
        {
            u.burn = Mathf.Max(0, u.burn - 1);
            View.Popup(u, "화상", "#ff9a3c");
            await Damage(u, 1 + u.burnB, kind: "fire", part: "torso");
        }
    }

    // ── 스트레스 (쥐만). 100 이 넘으면 무너짐: 공황 · 광란 · 도주 ──
    public static void AddStress(Unit u, int n, string why = null)
    {
        if (u == null || u.side != "rat" || u.r == null || !u.alive || u.controlled > 0) return;
        int before = u.stress, cap = Camp.BreakAt(u.r);
        float lead = n > 0 && Rats().Any(o => o != u && Camp.Has(o.r, "leader") && World.Dist(o, u) <= 3) ? 0.8f : 1;
        if (n > 0)
        {
            // 어둠을 좋아함: 고양이 눈에 안 띌 때는 안 쌓임
            if (Camp.Has(u.r, "nightowl") && (u.hidden || !Cats().Any(c => c.active && World.CanSee(world, c, u)))) return;
            if (Camp.Has(u.r, "reckless") && World.CoverSides(world, u.x, u.z).Count == 0) lead *= 0.8f;
            if (Camp.Has(u.r, "comrade") && PartnerNear(u, "comrade", u.x, u.z)) lead *= 0.8f;
        }
        u.stress = Mathf.Clamp(u.stress + (n > 0 ? World.JsRound(n * Camp.StressMul(u.r, why) * lead) : n), 0, cap);
        if (n > 0 && u.stress - before >= 6) View.Popup(u, $"스트레스 +{u.stress - before}", "#c9a3ff");
        if (u.stress >= cap && u.broke == null) u.broke = why ?? "true"; // 실제로 무너지는 건 그 쥐 차례
        version++;
    }
    // 아군이 당한 걸 본 쥐들
    static void Witness(Unit victim, int n, string why)
    {
        foreach (var r in Rats().ToList())
        {
            if (r == victim) continue;
            bool sib = victim.r != null && r.r != null && (r.r.sibling == victim.rosterId || Camp.Bonded(r.r, victim.rosterId, "comrade"));
            if (sib || World.Dist(r, victim) <= 7) AddStress(r, sib && why == "dead" ? STRESS.siblingDead : n, sib && why == "dead" ? "sibling" : why);
        }
    }
    static readonly (string id, string name, int w, string desc)[] BREAKS =
    {
        ("panic", "공황", 50, "몸이 굳어 버림"), ("frenzy", "광란", 25, "눈에 보이는 아무나 쏨"), ("flee", "도주", 25, "적에게서 달아남"),
    };
    static async Awaitable Breakdowns()
    {
        foreach (var u in Rats().Where(u => u.broke != null && u.r != null).ToList())
        {
            var B = BREAKS[0];
            if (Camp.Has(u.r, "calm")) B = ("daze", "멍함", 0, "잠깐 넋이 나감");
            else
            {
                var table = BREAKS.Select(b => b.id == "frenzy" && Camp.Has(u.r, "rage") ? (id: b.id, name: b.name, w: b.w * 2, desc: b.desc) : b).ToArray();
                float roll = R01 * table.Sum(b => b.w), acc = 0;
                foreach (var b in table) { acc += b.w; if (roll < acc) { B = b; break; } }
            }
            u.broke = null; u.brokeName = B.name;
            u.stress = STRESS.after;
            View.Focus(u); View.Slowmo(0.6f, 0.35f);
            View.Popup(u, $"{B.name}!", "#c9a3ff");
            Say($"{u.name} 정신이 무너졌다 — {B.name} ({B.desc})", "bad");
            u.face = "hit"; u.faceT = 2;
            Witness(u, STRESS.panicNear, "panic");
            await Sleep(900);
            if (B.id == "daze") u.ap = Mathf.Max(0, u.ap - 1);
            else if (B.id == "panic") { u.panic = 1; u.ap = 0; }
            else if (B.id == "frenzy")
            {
                var t = units.Where(o => o != u && o.alive && World.CanSee(world, u, o) && (o.side == "rat" || o.visible)).OrderBy(o => World.Dist(u, o)).FirstOrDefault();
                if (t != null && u.ammo > 0) { Say($"{u.name}이(가) {t.name}에게 총을 겨눈다!", "bad"); if (await Shoot(u, t) && t.side == "rat") t.hitByFrenzy = u.rosterId; }
                u.ap = 0;
            }
            else
            {
                var R = World.ReachOf(world, u, u.mob);
                (int x, int z, float d)? best = null;
                foreach (var kv in R.Stops)
                {
                    int x = kv.Key % world.W, z = kv.Key / world.W;
                    float d = Cats().Select(c => World.Dist(c.x, c.z, x, z)).DefaultIfEmpty(99).Min();
                    if (best == null || d > best.Value.d) best = (x, z, d);
                }
                if (best != null) await MoveUnit(u, R.Path(best.Value.x, best.Value.z));
                u.ap = 0;
            }
        }
    }

    // UI 에서 부르는 진입점 (기다리지 않음 · 오류는 콘솔로)
    public static async void EndRatTurnUI() { try { await EndRatTurn(); } catch (Exception e) { Debug.LogException(e); } }
    public static async void DoAbilityUI(Unit u, string ab, Unit tu = null, Tile tt = null) { try { await DoAbility(u, ab, tu, tt); } catch (Exception e) { Debug.LogException(e); mode = "move"; } }
    public static async void DoMoveUI(Unit u, int x, int z) { try { await DoMove(u, x, z); } catch (Exception e) { Debug.LogException(e); mode = "move"; } }

    static async Awaitable EndRatTurn()
    {
        if (turn != "rat" || mode == "busy" || over != null) return;
        mode = "busy"; sel = null;
        foreach (var r in Rats())
            if (!r.fired && !r.overwatch && !r.hidden && World.CoverSides(world, r.x, r.z).Count > 0) { r.hunker = true; View.Popup(r, "엎드림", "#8fd0ff"); }
        View.Banner("고양이 군 차례", "cat");
        BeginTurn("cat");
        await Sleep(900);
        await CatTurn();
        if (over != null) return;
        await BurnTick();
        if (over != null) return;
        round++;
        BeginTurn("rat");
        View.Banner($"아군 차례 · {round}턴", "rat");
        await Strikes();
        if (over != null) return;
        await Breakdowns();
        CheckOver();
        if (over != null) return;
        mode = "idle";
        SelectNext();
    }

    // 포격 유도: 지난 차례에 찍어 둔 자리에 포탄이 떨어짐
    static async Awaitable Strikes()
    {
        var list = strikes.ToList(); strikes.Clear(); View.MarksChanged();
        foreach (var s in list)
        {
            if (over != null) return;
            View.FocusPoint(BoardView.TilePos(s.x, s.z));
            Say("포격 도착!", "warn");
            Sfx.Mortar();
            await Sleep(700);
            var A = AB["artillery"];
            await Explode(s.x, s.z, A.radius, A.dmgMin, A.dmgMax, ById(s.owner));
        }
    }

    public static void SelectNext(Unit from = null)
    {
        var list = Rats().Where(u => u.ap > 0).ToList();
        if (list.Count == 0) { sel = null; version++; return; }
        int i = from != null ? (list.IndexOf(from) + 1) % list.Count : 0;
        sel = list[Mathf.Max(0, i)];
        mode = "move"; ability = null;
        View.Focus(sel);
        version++;
    }

    static async Awaitable AfterRatAction(Unit u)
    {
        CheckOver();
        if (over != null) return;
        if (u.ap <= 0 || !u.alive)
        {
            if (!Rats().Any(r => r.ap > 0)) { await Sleep(450); await EndRatTurn(); return; }
            SelectNext(u);
        }
        else { mode = "move"; ability = null; }
        version++;
    }

    // 목표: elim 전멸 · officer 표적 처치 · reach 깃발 칸
    static bool ObjectiveDone()
    {
        if (!Cats().Any()) return true;
        if (mission.obj == "officer") return !units.Any(u => u.target && u.alive);
        if (mission.obj == "reach" && world.goal != null) { var g = world.goal; return Rats().Any(r => r.controlled == 0 && World.Dist(r.x, r.z, g.x, g.z) <= g.r); }
        return false;
    }
    public static List<(string t, bool ok)> ObjectiveText()
    {
        int left = Cats().Count();
        if (mission.obj == "officer") return new List<(string, bool)> { ("표적 장교 사살", !units.Any(u => u.target && u.alive)), ($"남은 고양이 {left}", false) };
        if (mission.obj == "reach") return new List<(string, bool)> { ("깃발까지 쥐 한 마리", ObjectiveDone()), ($"남은 고양이 {left}", left == 0) };
        return new List<(string, bool)> { ($"고양이 전멸 (남은 {left})", left == 0) };
    }
    static void CheckOver()
    {
        if (over != null) return;
        if (ObjectiveDone()) Finish("win");
        else if (!units.Any(u => u.side == "rat" && u.alive && u.controlled == 0)) Finish("lose");
    }

    static void Finish(string result)
    {
        over = result; mode = "over";
        BattleSave.Delete(); // 끝난 전투는 이어할 것 없음
        // 지배당한 고양이는 원래대로
        foreach (var u in units) if (u.controlled > 0) { u.side = "cat"; u.controlled = 0; }
        var res = units.Where(u => u.side == "rat" && u.r != null).Select(u => new Camp.BattleResult
        {
            id = u.rosterId, alive = u.alive, downed = u.downed, kills = u.kills, stress = u.stress, broke = u.brokeName,
            hurt = u.wounds.Where(kv => kv.Value == 1).Select(kv => kv.Key).ToList(), severed = u.wounds.Where(kv => kv.Value > 1).Select(kv => kv.Key).ToList(),
            how = u.deathHow, knifeKills = u.knifeKills, nearBlasts = u.nearBlasts, legBlast = u.legBlast, hitByFrenzy = u.hitByFrenzy, painkiller = u.painkiller,
        }).ToList();
        var o = Camp.Settle(camp, mission, result == "win", res);
        version++;
        View.ShowEnd(result, o);
    }

    // ── 행동 ──
    public static void FaceToward(Unit u, float x, float z)
    {
        float dx = x - u.px, dz = z - u.pz, L = Mathf.Sqrt(dx * dx + dz * dz);
        if (L > 1e-3f) { u.fdx = dx / L; u.fdz = dz / L; }
    }

    static async Awaitable MoveUnit(Unit u, List<Pt> path, bool scamper = false, bool quiet = false)
    {
        if (path == null || path.Count == 0) return;
        var w = world;
        u.overwatch = false; u.hunker = false;
        foreach (var step in path)
        {
            FaceToward(u, step.x, step.z);
            float sx = u.px, sz = u.pz;
            float dur = Mathf.Sqrt((step.x - sx) * (step.x - sx) + (step.z - sz) * (step.z - sz)) / (scamper ? 5.2f : 4.2f) * 1000;
            await View.Tween(dur, k => { u.px = sx + (step.x - sx) * k; u.pz = sz + (step.z - sz) * k; u.st.walk = (u.st.walk ?? 0) + 0.035f; });
            u.x = step.x; u.z = step.z; u.px = u.x; u.pz = u.z;
            // 갉기: 철조망 칸을 지나가며 끊음
            var here = World.TileAt(w, u.x, u.z);
            if (here.prop == 'X') { View.Debris(u.x, u.z, 'X'); here.prop = '\0'; View.GroundDirty(); View.Popup(u, "갉작!", "#d8d0b8"); }
            // 부비트랩: 고양이가 밟으면 터짐
            if (u.side == "cat" && traps.Any(t => t.x == u.x && t.z == u.z))
            {
                var tr = traps.First(t => t.x == u.x && t.z == u.z);
                traps.Remove(tr); View.MarksChanged();
                Say($"{u.name} 부비트랩을 밟았다!", "good");
                var A = AB["trap"];
                await Explode(u.x, u.z, A.radius, A.dmgMin, A.dmgMax, ById(tr.owner));
                if (!u.alive) break;
            }
            if (u.side == "cat") foreach (var r in Rats()) if (r.hidden && World.Dist(r, u) < 1.6f) { r.hidden = false; View.Popup(r, "들킴!", "#ff6b5b"); }
            var wake = UpdateVision();
            // 경계 사격 (제압당한 채 움직여도)
            foreach (var e in EnemiesOf(u).ToList())
            {
                if (!u.alive) break;
                bool ears = Camp.Has(e.r, "bigears");
                bool react = !quiet && (e.overwatch || e.suppressing == u) && e.ammo > 0 && (World.CanSee(w, e, u) || (ears && World.Dist(e, u) <= World.SightOf(w, e) + 1 && World.Los(w, e.x, e.z, u.x, u.z)))
                    && World.Dist(e, u) <= (e.Weapon == "flamer" ? GameData.flamerLen : 12 + (ears ? 1 : 0));
                if (react)
                {
                    e.overwatch = false;
                    Say($"{e.name} 반응 사격!", "warn");
                    await Shoot(e, u, overwatchShot: true);
                }
            }
            if (!u.alive) break;
            if (u.side == "rat" && wake.Count > 0) { u.st.walk = null; await WakePods(wake); UpdateVision(); break; }
        }
        u.st.walk = null;
        u.moved = true;
        IdleFacing(u);
    }

    // 서 있을 때 방향: 가장 가까운 보이는 적 쪽, 엄폐물 옆이면 웅크림
    public static void IdleFacing(Unit u)
    {
        if (!u.alive) return;
        Unit tgt = null; float best = 1e9f;
        foreach (var e in EnemiesOf(u).Where(e => u.side == "cat" || e.seen)) { float d = World.Dist(u, e); if (d < best) { best = d; tgt = e; } }
        if (tgt != null) FaceToward(u, tgt.x, tgt.z);
        else { u.fdx = 0; u.fdz = u.side == "rat" ? -1 : 1; }
        u.inCover = tgt != null ? World.CoverAgainst(world, u.x, u.z, tgt.x, tgt.z).cover > 0 : World.CoverSides(world, u.x, u.z).Count > 0;
    }

    // 팔 부상이면 탄창을 떨어뜨려 실패할 때가 있음 (행동력은 이미 씀)
    static bool Reload(Unit u)
    {
        bool show = u.side == "rat" || u.visible;
        if ((u.wounds.ContainsKey("arm") && !Camp.Has(u.r, "careful") && R01 < RULES.armReloadFail) || (Camp.Has(u.r, "clumsy") && R01 < 0.1f))
        {
            if (show) { View.Popup(u, "재장전 실패", "#ff6b5b"); Say($"{u.name} 재장전 실패 (팔 부상)", u.side == "rat" ? "bad" : "good"); }
            return false;
        }
        u.ammo = WP(u.Weapon).clip;
        if (show) View.Popup(u, "재장전", "#d8d0b8");
        return true;
    }

    static void SpendAP(Unit u, string ab) { if (AB[ab].end) u.ap = 0; else u.ap -= AB[ab].ap; }

    static async Awaitable<bool> Shoot(Unit a, Unit b, bool overwatchShot = false, bool aimed = false, bool heavy = false, int extra = 0, string extraName = null)
    {
        if (!heavy && a.Weapon == "flamer") return await FlamerShot(a, b, overwatchShot);
        var info = GetHitInfo(a, b, overwatchShot, aimed, heavy, extra: extra, extraName: extraName);
        var wp = WP(heavy ? "hmg" : a.Weapon);
        FaceToward(a, b.x, b.z);
        if (b.side == "cat" && !b.active) alerted.Add(b.pod); // 빗나가도 총소리에 깸
        if (!overwatchShot) View.FocusPair(a, b.x, b.z);
        a.face = "attack"; a.faceT = 1.2f;
        await View.Aim(a, 380);
        if (!heavy) a.ammo = Mathf.Max(0, a.ammo - 1);
        bool hit = R100 < info.hit;
        bool crit = hit && R100 < info.crit;
        if (a.side == "rat") { a.missStreak = hit ? 0 : a.missStreak + 1; a.rageAim = false; }
        else if (hit && b.side == "rat") catHits++;
        int shots = a.Weapon == "lmg" || a.Weapon == "smg" || heavy ? 3 : 1;
        int dmg = Rnd(wp.dmgMin, wp.dmgMax) + (crit ? RULES.critDmg : 0) + (aimed ? 1 : 0) + a.dmgB;
        bool lethal = hit && (crit || dmg >= b.hp + b.ward);
        if (a.hidden) { a.hidden = false; View.Popup(a, "매복!", "#8fd0ff"); }
        a.fired = true;
        // 안개 속 고양이가 쏘면 그 자리가 드러남 (다음 고양이 차례까지)
        if (a.side == "cat" && !a.visible) { a.revealed = true; a.visible = true; a.seen = true; View.FocusPair(a, b.x, b.z); View.Popup(a, "발견!", "#ff9f8f"); }
        if (lethal) await View.Cinematic(a, b, crit);
        for (int i = 0; i < shots; i++)
        {
            View.Muzzle(a, b, hit, crit, i == shots - 1, heavy ? "hmg" : a.Weapon);
            Sfx.Shot(heavy ? "lmg" : a.Weapon);
            a.st.recoil = 1;
            await Sleep(shots > 1 ? 90 : 160);
        }
        if (lethal) await View.BulletTime(a, b, crit);
        if (hit) await Damage(b, dmg, crit, a, a.Weapon, RollHitPart(crit));
        else
        {
            View.Popup(b, "빗나감", "#d8d0b8");
            Say($"{a.name} → {b.name} 빗나감 ({info.hit}%)");
            if (b.side == "rat") AddStress(b, 3, "shot");
        }
        if (lethal) View.EndCinematic();
        await Sleep(350);
        return hit;
    }

    // 화염방사기: 대상 쪽 부채꼴(길이 4.5 · 반폭 1.5) 안 적을 하나하나 굴림. 엄폐 무시 · 불 · 화상 · 낮은 엄폐물 태움
    static bool InCone(Unit a, float dx, float dz, int x, int z, float len, float halfW)
    {
        float vx = x - a.x, vz = z - a.z, along = vx * dx + vz * dz;
        return along > 0.1f && along <= len && Mathf.Abs(vx * dz - vz * dx) <= halfW;
    }
    static async Awaitable<bool> FlamerShot(Unit a, Unit b, bool overwatchShot)
    {
        float L = Mathf.Max(0.01f, World.Dist(a, b)), dx = (b.x - a.x) / L, dz = (b.z - a.z) / L;
        FaceToward(a, b.x, b.z);
        if (!overwatchShot) View.FocusPair(a, b.x, b.z);
        a.face = "attack"; a.faceT = 1.5f;
        a.ammo = Mathf.Max(0, a.ammo - 1); a.fired = true;
        if (a.hidden) a.hidden = false;
        await View.Flame(a, b.x, b.z);
        foreach (var t in world.tiles)
        {
            if (!InCone(a, dx, dz, t.x, t.z, GameData.flamerLen, GameData.flamerHalfW)) continue;
            fires.Add((t.x, t.z, 2));
            if (t.HasProp && GameData.PROPS[t.prop].breakable) { View.Debris(t.x, t.z, t.prop); t.prop = '\0'; t.ground = "crater"; View.GroundDirty(); }
        }
        View.FiresChanged();
        bool any = false;
        var hits = EnemiesOf(a).Where(e => e == b || InCone(a, dx, dz, e.x, e.z, GameData.flamerLen, GameData.flamerHalfW)).ToList();
        foreach (var e in hits)
        {
            if (e.side == "cat" && !e.active) alerted.Add(e.pod);
            var info = GetHitInfo(a, e, overwatchShot);
            bool hit = R100 < info.hit;
            if (!hit) { View.Popup(e, "빗나감", "#d8d0b8"); continue; }
            any = true; e.burn = Mathf.Max(e.burn, 1);
            await Damage(e, Rnd(GameData.WEAPONS["flamer"].dmgMin, GameData.WEAPONS["flamer"].dmgMax) + a.dmgB, false, a, "fire", "torso");
        }
        if (a.side == "rat") { a.missStreak = any ? 0 : a.missStreak + 1; a.rageAim = false; }
        UpdateVision();
        await Sleep(300);
        return any;
    }
    // 화염방사기를 든 쥐가 쓰러지면 40% 로 연료통이 터짐 → 그 쥐는 확정 사망
    static bool FuelBoom(Unit u)
    {
        if (u.side != "rat" || u.Weapon != "flamer" || u.used.Contains("fuelboom") || R01 >= GameData.flamerBoomChance) return false;
        u.used.Add("fuelboom");
        return true;
    }
    static async Awaitable FuelExplode(Unit u)
    {
        View.Popup(u, "연료통 폭발!", "#ff9a3c");
        Say($"{u.name}의 연료통이 터졌다!", "bad");
        await Sleep(300);
        await Explode(u.x, u.z, GameData.flamerBoomR, GameData.flamerBoomMin, GameData.flamerBoomMax, null);
    }

    // ── 맞은 부위: 머리 = 즉사, 몸통 = 체력, 팔 · 다리 = 부상 ──
    public static readonly Dictionary<string, string> PART_NAME = new Dictionary<string, string> { ["head"] = "머리", ["torso"] = "몸통", ["arm"] = "팔", ["leg"] = "다리" };
    static string RollHitPart(bool crit)
    {
        var w = crit ? RULES.hitPartsCrit : RULES.hitParts;
        float r = R01 * w.Sum(p => p.w);
        foreach (var p in w) { if ((r -= p.w) < 0) return p.part; }
        return "torso";
    }

    static async Awaitable Damage(Unit u, int dmg, bool crit = false, Unit from = null, string kind = null, string part = null, bool quiet = false)
    {
        if (u.downed) { await Kill(u, from, kind, false, crit); return; } // 쓰러진 채 또 맞으면 끝
        if (!u.alive) return;
        if (u.ward > 0)
        {
            int absorbed = Mathf.Min(u.ward, dmg);
            u.ward -= absorbed; dmg -= absorbed;
            View.Popup(u, $"방어막 −{absorbed}", "#c9a3ff");
            if (dmg <= 0) return;
        }
        // 행운아: 전투당 1번 죽을 피해를 체력 1로
        if (Camp.Has(u.r, "lucky") && !u.used.Contains("lucky") && (dmg >= u.hp || part == "head"))
        {
            u.used.Add("lucky"); dmg = Mathf.Max(0, u.hp - 1); part = "torso";
            View.Popup(u, "행운!", "#ffcf4a"); Say($"{u.name} 아슬아슬하게 살았다 (행운아)", "good");
        }
        // 전차: 폭발 아닌 피해는 장갑만큼 깎임 (최소 1), 부위 부상 없음
        if (u.def.vehicle)
        {
            part = "torso";
            if (u.def.armor > 0 && kind != "explosion" && dmg > 0) { int a0 = dmg; dmg = Mathf.Max(1, dmg - u.def.armor); if (dmg < a0) View.Popup(u, $"장갑 −{a0 - dmg}", "#c8ccd4"); }
        }
        if (u.side == "rat" && part == "leg" && (crit || kind == "explosion")) u.legBlast = true;
        if (u.side == "rat" && kind == "explosion" && R01 < 0.3f) u.legBlast = true;
        if (Camp.Has(u.r, "rage") && dmg > 0) u.rageAim = true;
        u.hp -= dmg;
        AddStress(u, crit ? STRESS.crit : STRESS.hit, "hit");
        if (u.side == "cat" && !u.active && !quiet) alerted.Add(u.pod);
        u.face = "hit"; u.faceT = 1.0f;
        u.st.hit = 1;
        if (!u.def.vehicle) View.Blood(u, crit || part == "head" ? 14 : 8);
        string where = part != null && part != "torso" ? PART_NAME[part] + " " : "";
        View.Popup(u, where + (crit ? "치명타! " : "") + "−" + dmg, crit || part == "head" ? "#ffcf4a" : "#ff6b5b");
        Sfx.Hit();
        if (from != null) Say($"{from.name} → {u.name} {where}{dmg} 피해{(crit ? " (치명타)" : "")}", u.side == "rat" ? "bad" : "good");
        version++;
        if (part == "head") { await Kill(u, from, kind, true, crit); return; }
        if (part == "arm" || part == "leg")
        {
            bool severed = crit && R01 < RULES.severOnCrit;
            u.wounds[part] = Mathf.Max(u.wounds.TryGetValue(part, out var wv) ? wv : 0, severed ? 2 : 1);
            if (part == "leg" && !u.painkiller) u.mob = Mathf.Max(2, Mathf.FloorToInt(u.baseMob * RULES.legWoundMob));
            if (severed || u.hp <= 0) { await DownUnit(u, from, part, severed); return; }
            Say($"{u.name} {PART_NAME[part]} 부상", u.side == "rat" ? "bad" : "good");
            return;
        }
        if (u.hp <= 0) await Kill(u, from, kind, false, crit);
    }

    // 쓰러짐: 행동 불가 · 누움. 적이 확인사살하면 사망, 이긴 채 끝나면 부상자로 살아남음
    static async Awaitable DownUnit(Unit u, Unit from, string part, bool severed)
    {
        if (FuelBoom(u)) { await Kill(u, from, "explosion"); u.deathHow = "연료통 폭발"; await FuelExplode(u); return; }
        u.alive = false; u.downed = true; u.hp = 0;
        u.overwatch = false; u.hunker = false;
        if (u.suppressing != null) { u.suppressing.suppressedBy = null; u.suppressing = null; }
        if (from != null) from.kills++;
        if (u.side == "cat") statKills++; else statLost++;
        if (u.side == "rat") { AddStress(u, STRESS.downed, "downed"); Witness(u, 12, "downed"); }
        else if (from != null) AddStress(from, STRESS.kill);
        Say($"{u.name} 쓰러짐!{(severed ? $" ({PART_NAME[part]} 잘림)" : "")}", u.side == "rat" ? "bad" : "good");
        if (from != null) FaceToward(u, from.x, from.z);
        u.forceSide = true; u.face = "hit";
        Sfx.Death();
        await View.Die(u, severed ? part : null);
        UpdateVision();
        CheckOver();
    }

    static async Awaitable Kill(Unit u, Unit from = null, string kind = null, bool headshot = false, bool crit = false)
    {
        bool wasDown = u.downed;
        u.alive = false; u.downed = false; u.hp = 0;
        u.overwatch = false;
        if (u.suppressing != null) { u.suppressing.suppressedBy = null; u.suppressing = null; }
        u.face = "dead";
        version++;
        if (wasDown)
        {
            // 이미 누워 있음: 넘어지는 동작 없이 피만
            View.Blood(u, 10);
            Say(u.side == "rat" ? $"{u.name} 전사…" : $"{u.name} 사살", u.side == "rat" ? "bad" : "good");
            if (u.side == "rat") { u.deathHow = "쓰러진 채 확인사살"; Witness(u, STRESS.allyDead + 8, "dead"); }
            Sfx.Death();
            u.corpse = true;
            CheckOver();
            return;
        }
        if (from != null) { from.kills++; if (kind == "knife") from.knifeKills++; }
        if (u.side == "cat") statKills++; else statLost++;
        if (u.side == "rat") Witness(u, STRESS.allyDead, "dead");
        else if (from != null) AddStress(from, STRESS.kill - (int)Camp.TraitSum(from.r, "killRelief"));
        u.deathHow = headshot ? "머리에 총탄" : kind == "explosion" ? "폭발" : kind == "fire" ? "불길" : from != null && from.side == "rat" ? "아군의 총에" : "총탄";
        Say(u.side == "rat" ? $"{u.name} 전사…" : $"{u.name} {(u.def.vehicle ? "격파!" : "사살")}", u.side == "rat" ? "bad" : "good");
        // 쓰러지는 방향: 쏜 쪽 반대. 옆모습으로 돌려서 리그로 쓰러뜨림
        if (from != null) FaceToward(u, from.x, from.z);
        u.forceSide = true;
        // 신체 절단: 머리 명중 · 폭발 · 치명타 · 산탄총 근접
        string gore = null;
        if (headshot) gore = crit || R01 < 0.4f ? "decap" : null;
        else if (kind == "explosion") gore = R01 < 0.6f ? "burst" : "sever";
        else if (crit && R01 < 0.65f) gore = R01 < 0.5f ? "decap" : "sever";
        else if (kind == "shotgun" && R01 < 0.4f) gore = "sever";
        if (u.def.vehicle) { gore = null; u.deathHow = "격파"; View.Explosion(u.x, u.z, 1.2f); Sfx.Boom(); } // 전차: 피 대신 폭발 → 잔해
        u.gore = gore;
        if (gore != null && u.side == "rat") Witness(u, STRESS.gore, "gore");
        Sfx.Death();
        await View.Die(u, gore);
        u.corpse = true;
        if (kind != "explosion" || !u.used.Contains("fuelboom")) if (FuelBoom(u)) { u.deathHow = "연료통 폭발"; await FuelExplode(u); }
        UpdateVision();
        CheckOver();
    }

    // 확인사살: 쓰러진 적에게 다가가 쏨 (반드시 맞음)
    static async Awaitable Execute(Unit a, Unit b)
    {
        FaceToward(a, b.x, b.z);
        View.FocusPair(a, b.x, b.z);
        a.face = "attack"; a.faceT = 1.2f;
        await View.Aim(a, 450);
        View.Muzzle(a, b, true, false, true, a.Weapon);
        Sfx.Shot(a.Weapon);
        a.st.recoil = 1;
        a.ammo = Mathf.Max(0, a.ammo - 1);
        Say($"{a.name}: {b.name} 확인사살", b.side == "rat" ? "bad" : "good");
        await Sleep(200);
        await Kill(b, a, a.Weapon);
        await Sleep(350);
    }

    static async Awaitable ThrowGrenade(Unit a, int tx, int tz, bool mortar = false)
    {
        FaceToward(a, tx, tz);
        a.ap = 0;
        if (!mortar) a.grenades--;
        if (!mortar && Camp.Has(a.r, "clumsy") && R01 < 0.15f)
        {
            var d = World.DIRS8[rng.Next(8)]; tx += d[0]; tz += d[1];
            View.Popup(a, "앗, 빗나갔다", "#ff9f8f");
        }
        a.face = "attack"; a.faceT = 1.6f;
        View.FocusPair(a, tx, tz);
        if (mortar)
        {
            a.mortarT = true;
            await Sleep(500);
            Sfx.Mortar();
            a.st.recoil = 1;
            await View.Shell(a, tx, tz, 1100);
        }
        else
        {
            await Sleep(120);
            await View.Lob(a, tx, tz, 700);
        }
        a.mortarT = false;
        var ab = AB[mortar ? "mortar" : "grenade"];
        await Explode(tx, tz, ab.radius, ab.dmgMin, ab.dmgMax, a);
    }

    static async Awaitable Explode(int tx, int tz, float rad, int dmin, int dmax, Unit from, bool high = false)
    {
        var w = world;
        float radius = rad + (from != null ? from.blast : 0);
        View.Explosion(tx, tz, radius);
        Sfx.Boom();
        // 낮은 엄폐물 부서짐 (폭약은 높은 엄폐 · 트럭까지)
        foreach (var t in w.tiles)
        {
            float d = World.Dist(t.x, t.z, tx, tz);
            if (d <= radius && t.HasProp && (GameData.PROPS[t.prop].breakable || (high && GameData.PROPS[t.prop].emplacement == null)))
            {
                if (t.prop == 'R') foreach (var dx in new[] { -1, 1 }) { var o = World.TileAt(w, t.x + dx, t.z); if (o != null && o.prop == 'R') { View.Debris(o.x, o.z, 'R'); o.prop = '\0'; o.ground = "crater"; } }
                View.Debris(t.x, t.z, t.prop);
                t.prop = '\0'; t.ground = "crater";
                View.GroundDirty();
            }
            else if (d <= 0.6f && !t.HasProp) { t.ground = "crater"; View.GroundDirty(); }
        }
        var hits = units.Where(u => (u.alive || u.downed) && World.Dist(u.x, u.z, tx, tz) <= radius).ToList();
        foreach (var r in Rats().ToList())
        {
            float d = World.Dist(r.x, r.z, tx, tz);
            if (d <= Mathf.Max(1.5f, radius)) r.nearBlasts++;
            if (d <= radius + 1.5f) AddStress(r, STRESS.blast, "blast");
            if (d <= radius && Camp.Has(r.r, "bigears")) r.dazed = 1;
        }
        await Sleep(200);
        var jobs = hits.Select(u => Damage(u, Rnd(dmin, dmax) + (from != null ? from.boom : 0), false, from, "explosion")).ToList();
        foreach (var j in jobs) await j;
        UpdateVision();
        await Sleep(400);
    }

    // 쥐 능력 실행 (UI 에서 부름)
    static async Awaitable DoAbility(Unit u, string ab, Unit target, Tile tile)
    {
        if (mode == "busy") return;
        mode = "busy"; version++;
        try
        {
            var AD = AB[ab];
            if (AD.cd > 0) u.cd[ab] = AD.cd;
            if (u.left.ContainsKey(ab)) u.left[ab]--;
            switch (ab)
            {
                case "shoot": SpendAP(u, ab); await Shoot(u, target); break;
                case "aimed": SpendAP(u, ab); await Shoot(u, target, aimed: true); break;
                case "heavy": SpendAP(u, ab); await Shoot(u, target, heavy: true); break;
                case "suppress":
                    SpendAP(u, ab);
                    u.ammo = Mathf.Max(0, u.ammo - 1);
                    target.suppressedBy = u; u.suppressing = target;
                    FaceToward(u, target.x, target.z);
                    View.FocusPair(u, target.x, target.z);
                    for (int i = 0; i < 6; i++) { View.Muzzle(u, target, false, false, false, "lmg"); Sfx.Shot("lmg"); u.st.recoil = 1; await Sleep(80); }
                    View.Popup(target, "제압됨", "#ffb84a");
                    Say($"{u.name}: {target.name} 제압 사격");
                    await Sleep(400);
                    break;
                case "grenade": await ThrowGrenade(u, tile.x, tile.z); break;
                case "mortar": shells--; await ThrowGrenade(u, tile.x, tile.z, true); break;
                case "overwatch": SpendAP(u, ab); u.overwatch = true; View.Popup(u, "경계", "#8fd0ff"); Say($"{u.name} 경계"); break;
                case "hunker": SpendAP(u, ab); u.hunker = true; View.Popup(u, "엎드림", "#8fd0ff"); break;
                case "reload": SpendAP(u, ab); Reload(u); Sfx.Reload(); await Sleep(350); break;
                case "firstaid":
                    SpendAP(u, ab); u.medkits--;
                    FaceToward(u, target.x, target.z);
                    if (target.downed)
                    {
                        // 구출: 쓰러진 아군을 일으킴
                        target.downed = false; target.alive = true; target.hp = 2; target.st.death = null; target.forceSide = false; target.face = null; target.ap = 0;
                        View.Revive(target);
                        View.Popup(target, "일어남!", "#7fdc8a");
                        Say($"{u.name} → {target.name} 구출!", "good");
                        AddStress(target, -15);
                    }
                    else
                    {
                        int heal = AB["firstaid"].heal + u.healB;
                        target.hp = Mathf.Min(target.maxHp, target.hp + heal);
                        View.Popup(target, "+" + heal, "#7fdc8a");
                        Say($"{u.name} → {target.name} 응급 처치", "good");
                    }
                    await Sleep(350);
                    break;
                case "knife": SpendAP(u, ab); await KnifeAttack(u, target); break;
                case "ambush": SpendAP(u, ab); u.hidden = true; u.hunker = false; View.Popup(u, "쥐구멍 매복", "#8fd0ff"); Say($"{u.name} 매복 — 쏘기 전까지 안 보임"); break;
                case "warcry":
                    u.used.Add("warcry");
                    u.face = "attack"; u.faceT = 1.5f;
                    View.Shout(u, "찍찍!!");
                    foreach (var f in FriendsOf(u).ToList()) if (World.Dist(f, u) <= 4) { f.buffAim = 10; AddStress(f, STRESS.warcry); View.Popup(f, "명중 +10", "#ffcf4a"); }
                    Say($"{u.name} 찍찍 함성! 분대 사기가 오른다", "good");
                    await Sleep(500);
                    break;
                case "charge":
                    SpendAP(u, ab); u.charges--;
                    FaceToward(u, tile.x, tile.z);
                    View.Popup(u, "폭약 설치!", "#ffb84a");
                    await Sleep(600);
                    // 짧은 도화선: 붙은 칸에 폭발
                    await Explode(tile.x, tile.z, 0.8f, 3, 4, u, high: true);
                    break;
                case "tunnel":
                    SpendAP(u, ab); u.used.Add("tunnel");
                    View.Popup(u, "땅굴 파는 중…", "#d8d0b8");
                    View.Dig(u.x, u.z); await Sleep(700);
                    world.tunnels.Add((new Pt(u.x, u.z), new Pt(tile.x, tile.z)));
                    View.Dig(tile.x, tile.z); View.GroundDirty();
                    Say($"{u.name} 땅굴 완성 — 입구에서 출구까지 한 걸음", "good");
                    await Sleep(400);
                    break;
                case "flame":
                    SpendAP(u, ab); u.flame--;
                    await FlameAt(u, tile.x, tile.z);
                    break;
                case "mark":
                    SpendAP(u, ab);
                    target.marked = true;
                    if (u.perks.Contains("weakspot")) target.weakMarked = true;
                    View.Popup(target, "표적 지정", "#ffcf4a");
                    Say($"{u.name}: {target.name} 표적 지정 (아군 명중 +15)");
                    await Sleep(300);
                    break;
                case "command":
                    SpendAP(u, ab);
                    target.ap += 1;
                    View.RatsRun(u, target);
                    View.Popup(target, "행동력 +1", "#8fd0ff");
                    Say($"{u.name} → {target.name}: 쥐떼 전령이 명령을 전함", "good");
                    await Sleep(400);
                    break;
                case "dominate":
                    SpendAP(u, ab); u.psi -= AB["dominate"].psi;
                    FaceToward(u, target.x, target.z);
                    await View.Psi(u, target, "#b07cff");
                    if (target.target || R01 < 0.15f) { View.Popup(target, "저항!", "#ff9f8f"); Say($"{target.name}이(가) 정신 지배를 버텼다", "warn"); }
                    else
                    {
                        target.side = "rat"; target.controlled = 2; target.active = true; target.ap = 0; target.overwatch = false;
                        target.tint = SkillsData.Hex("#b9a0ff");
                        View.Popup(target, "지배됨", "#c9a3ff");
                        Say($"{u.name}: {target.name}의 정신을 붙잡았다 (2턴)", "good");
                    }
                    await Sleep(400);
                    break;
                // ── 2026-10-30 병종 액티브 ──
                case "barrage": SpendAP(u, ab); await Barrage(u, target); break;
                case "trap":
                    SpendAP(u, ab);
                    traps.Add((tile.x, tile.z, u.id)); View.MarksChanged();
                    View.Dig(tile.x, tile.z);
                    View.Popup(u, "덫 설치", "#ffb84a"); Say($"{u.name} 부비트랩 설치");
                    await Sleep(350);
                    break;
                case "smoke":
                    SpendAP(u, ab);
                    FaceToward(u, tile.x, tile.z);
                    await View.Lob(u, tile.x, tile.z, 600);
                    foreach (var t in world.tiles)
                        if (World.Dist(t.x, t.z, tile.x, tile.z) <= AD.radius) { smokes.RemoveAll(m => m.x == t.x && m.z == t.z); smokes.Add((t.x, t.z, 2)); }
                    View.SmokePuff(tile.x, tile.z, AD.radius); View.MarksChanged();
                    Say($"{u.name} 연막탄!");
                    UpdateVision();
                    await Sleep(400);
                    break;
                case "dash":
                {
                    SpendAP(u, ab);
                    var spot = DashSpot(u, target);
                    if (spot != null) { View.Popup(u, "돌진!", "#b07cff"); await MoveUnit(u, spot, scamper: true); }
                    if (u.alive && target.alive && World.Dist(u, target) < 1.6f) await KnifeAttack(u, target);
                    break;
                }
                case "painkiller":
                    SpendAP(u, ab); u.medkits--;
                    target.painkiller = true;
                    if (target.wounds.ContainsKey("leg")) target.mob = target.baseMob;
                    AddStress(target, -10);
                    View.Popup(target, "진통제", "#7fdc8a"); Say($"{u.name} → {target.name} 진통제 (부상 무시)", "good");
                    await Sleep(300);
                    break;
                case "supply":
                    SpendAP(u, ab); u.medkits--;
                    View.Shout(u, "보급!");
                    foreach (var f in FriendsOf(u).Where(f => f.controlled == 0 && World.Dist(f, u) <= AD.radius).ToList())
                    {
                        int heal = Mathf.Min(f.maxHp - f.hp, AD.heal + u.healB);
                        if (heal <= 0) continue;
                        f.hp += heal; View.Popup(f, "+" + heal, "#7fdc8a");
                    }
                    Say($"{u.name} 대량 보급품 — 주변 아군 체력 +{AD.heal + u.healB}", "good");
                    await Sleep(450);
                    break;
                case "flare":
                    SpendAP(u, ab);
                    FaceToward(u, tile.x, tile.z);
                    await View.Lob(u, tile.x, tile.z, 650);
                    flares.Add((tile.x, tile.z, 2)); View.MarksChanged();
                    View.FlareBurst(tile.x, tile.z);
                    Say($"{u.name} 조명탄!", "good");
                    UpdateVision();
                    await Sleep(350);
                    break;
                case "artillery":
                    SpendAP(u, ab); shells--;
                    strikes.Add((tile.x, tile.z, u.id)); View.MarksChanged();
                    View.Popup(u, "포격 요청", "#ffb84a"); Say($"{u.name}: 포격 요청 — 다음 차례에 떨어짐", "good");
                    await Sleep(350);
                    break;
                case "terror":
                    SpendAP(u, ab); u.psi -= AD.psi;
                    FaceToward(u, target.x, target.z);
                    await View.Psi(u, target, "#9b4dff");
                    if (target.target && R01 < 0.5f) { View.Popup(target, "저항!", "#ff9f8f"); Say($"{target.name}이(가) 공포를 버텼다", "warn"); }
                    else { target.panic = 1; target.overwatch = false; View.Popup(target, "공포!", "#c9a3ff"); Say($"{u.name}: {target.name} 겁에 질림 (다음 턴 못 움직임)", "good"); }
                    await Sleep(350);
                    break;
                case "cleanse":
                    SpendAP(u, ab); u.psi -= AD.psi;
                    await View.Psi(u, target, "#8fd0ff");
                    AddStress(target, -30); target.panic = 0; target.broke = null;
                    View.Popup(target, "정신 정화", "#8fd0ff"); Say($"{u.name} → {target.name} 정신 정화", "good");
                    await Sleep(300);
                    break;
            }
            // 자는 무리를 쐈으면 깸
            var wake = UpdateVision();
            if (wake.Count > 0 && over == null) { await WakePods(wake); UpdateVision(); }
        }
        finally
        {
            if (mode == "busy") mode = "move";
        }
        IdleFacing(u);
        await AfterRatAction(u);
    }

    // 칼: 바로 옆으로 붙어 찌름 (소리 안 남 → 무리를 안 깨움)
    static async Awaitable KnifeAttack(Unit u, Unit target)
    {
        FaceToward(u, target.x, target.z);
        View.FocusPair(u, target.x, target.z);
        u.face = "attack"; u.faceT = 1.2f;
        u.st.recoil = 1;
        var info = GetHitInfo(u, target, knife: true);
        bool hit = R100 < info.hit;
        bool crit = hit && R100 < info.crit + 20;
        bool cine = hit && (crit || 5 >= target.hp);
        if (cine) await View.Cinematic(u, target, true);
        View.Slash(u, target);
        await Sleep(220);
        if (hit) await Damage(target, Rnd(4, 6) + (crit ? 2 : 0), crit, u, "knife", crit ? "head" : "torso", quiet: true);
        else View.Popup(target, "빗나감", "#d8d0b8");
        if (cine) View.EndCinematic();
        if (target.side == "cat" && !target.active) alerted.Remove(target.pod);
        await Sleep(300);
    }

    // 그림자 돌진: 대상 옆 칸 중 5 이동력 안에 닿는 가장 가까운 칸까지 가는 길 (이미 붙어 있으면 빈 길)
    public static List<Pt> DashSpot(Unit u, Unit target)
    {
        if (World.Dist(u, target) < 1.6f) return new List<Pt>();
        var R = World.ReachOf(world, u, AB["dash"].range);
        List<Pt> best = null; float bc = 1e9f;
        foreach (var d in World.DIRS8)
        {
            int x = target.x + d[0], z = target.z + d[1];
            var c = R.Cost(x, z);
            if (c == null || c <= 0 || c >= bc) continue;
            bc = c.Value; best = R.Path(x, z);
        }
        return best;
    }

    // 난사: 고른 적 쪽 부채꼴(길이 7 · 반폭 1.5) 안 보이는 적을 하나하나 (명중 −15). 탄창을 다 씀 · 낮은 엄폐물 부숨
    static async Awaitable Barrage(Unit u, Unit target)
    {
        float L = Mathf.Max(0.01f, World.Dist(u, target)), dx = (target.x - u.x) / L, dz = (target.z - u.z) / L;
        var A = AB["barrage"];
        var list = VisibleEnemies(u).Where(e => e == target || InCone(u, dx, dz, e.x, e.z, A.range, 1.5f)).OrderBy(e => World.Dist(u, e)).ToList();
        Say($"{u.name} 난사!", "warn");
        foreach (var e in list)
        {
            if (!u.alive || !e.alive) continue;
            u.ammo = Mathf.Max(1, u.ammo);
            await Shoot(u, e, extra: -15, extraName: "난사");
        }
        u.ammo = 0;
        foreach (var t in world.tiles)
            if (InCone(u, dx, dz, t.x, t.z, A.range, 1.5f) && t.HasProp && GameData.PROPS[t.prop].breakable && GameData.PROPS[t.prop].cover == 1)
            { View.Debris(t.x, t.z, t.prop); t.prop = '\0'; t.ground = "crater"; View.GroundDirty(); }
        UpdateVision();
    }

    // 화염 방사 (쥐 · 고양이 같이 씀)
    static async Awaitable FlameAt(Unit u, int x, int z)
    {
        FaceToward(u, x, z);
        View.FocusPair(u, x, z);
        u.face = "attack"; u.faceT = 1.5f;
        await View.Flame(u, x, z);
        float r = AB["flame"].radius;
        foreach (var t in world.tiles)
        {
            if (World.Dist(t.x, t.z, x, z) > r) continue;
            fires.Add((t.x, t.z, 2));
            if (t.HasProp && GameData.PROPS[t.prop].breakable) { View.Debris(t.x, t.z, t.prop); t.prop = '\0'; t.ground = "crater"; View.GroundDirty(); }
        }
        View.FiresChanged();
        var hits = units.Where(o => (o.alive || o.downed) && World.Dist(o.x, o.z, x, z) <= r).ToList();
        foreach (var o in hits) { o.burn = 2; o.burnB = u.burnB; }
        var jobs = hits.Select(o => Damage(o, Rnd(3, 4), false, u, "fire", "torso")).ToList();
        foreach (var j in jobs) await j;
        if (u.side == "cat") foreach (var rt in Rats().ToList()) if (World.Dist(rt.x, rt.z, x, z) <= 3) AddStress(rt, 8, "fire");
        UpdateVision();
        await Sleep(300);
    }

    static async Awaitable DoMove(Unit u, int targetX, int targetZ)
    {
        if (mode == "busy") return;
        var w = world;
        var R = World.ReachOf(w, u, u.mob * u.ap);
        int x = targetX, z = targetZ;
        float? c = R.Cost(x, z);
        // 클릭한 칸이 이동 불가 칸이면 인접 칸(4방향) 중 비용이 가장 적은 칸
        if (c == null || c == 0)
        {
            Pt? bestAdj = null; float minCost = 999;
            foreach (var d in World.DIRS4)
            {
                float? ac = R.Cost(targetX + d[0], targetZ + d[1]);
                if (ac != null && ac > 0 && ac < minCost) { minCost = ac.Value; bestAdj = new Pt(targetX + d[0], targetZ + d[1]); }
            }
            if (bestAdj != null) { x = bestAdj.Value.x; z = bestAdj.Value.z; c = minCost; }
        }
        if (c == null || c == 0) { View.Popup(u, "이동 불가", "#ff6b5b"); return; }
        mode = "busy"; version++;
        u.ap -= c <= u.mob ? 1 : 2;
        await MoveUnit(u, R.Path(x, z));
        mode = "move";
        foreach (var r in Rats()) IdleFacing(r);
        await AfterRatAction(u);
    }

    // ── 이 유닛이 지금 쓸 수 있는 능력 ──
    public class AbilityState { public string ab, why = ""; public bool ok; public int? count; }
    static List<Tile> Adjacent(Unit u, Func<Tile, bool> pred) => World.DIRS8.Select(d => World.TileAt(world, u.x + d[0], u.z + d[1])).Where(t => t != null && pred(t)).ToList();
    public static List<AbilityState> AbilitiesOf(Unit u)
    {
        var list = new List<AbilityState>();
        var emp = World.PropOf(World.TileAt(world, u.x, u.z));
        var vis = VisibleEnemies(u);
        foreach (var ab in u.abil)
        {
            var A = AB[ab];
            bool ok = u.ap >= A.ap; string why = ""; int? count = null;
            if ((ab == "shoot" || ab == "aimed" || ab == "suppress" || ab == "overwatch" || ab == "barrage") && u.ammo <= 0) { ok = false; why = u.Weapon == "flamer" ? "연료 없음" : "탄 없음"; }
            if (u.left.TryGetValue(ab, out var lf)) { count = lf; if (lf <= 0) { ok = false; why = "다 씀"; } }
            if (ab == "painkiller" || ab == "supply") { count = u.medkits; if (u.medkits <= 0) { ok = false; why = "구급상자 없음"; } }
            if (ab == "artillery") { count = shells; if (shells <= 0) { ok = false; why = "포탄 없음"; } }
            if (ab == "aimed" && u.moved) { ok = false; why = "이동함"; }
            if (ab == "suppress" && u.ammo < 2) { ok = false; why = "탄 2발 필요"; }
            if (ab == "grenade") { count = u.grenades; if (u.grenades <= 0) { ok = false; why = "없음"; } }
            if (ab == "firstaid") { count = u.medkits; if (u.medkits <= 0) { ok = false; why = "없음"; } }
            if (ab == "charge") { count = u.charges; if (u.charges <= 0) { ok = false; why = "없음"; } else if (Adjacent(u, t => t.HasProp && GameData.PROPS[t.prop].emplacement == null).Count == 0) { ok = false; why = "붙은 엄폐물 없음"; } }
            if (ab == "flame") { count = u.flame; if (u.flame <= 0) { ok = false; why = "연료 없음"; } }
            if (A.psi > 0) { count = u.psi; if (u.psi < A.psi) { ok = false; why = "정신력 부족"; } }
            if (A.once && u.used.Contains(ab)) { ok = false; why = "이번 판엔 씀"; }
            if (u.cd.TryGetValue(ab, out var cdl) && cdl > 0) { ok = false; why = $"{cdl}턴 뒤"; }
            if (ab == "ambush" && World.CoverSides(world, u.x, u.z).Count == 0) { ok = false; why = "엄폐물 옆에서만"; }
            if (ab == "knife" && !vis.Any(e => World.Dist(e, u) < 1.6f)) { ok = false; why = "붙은 적 없음"; }
            else if (A.target == "enemy" && AbilityTargets(u, ab).Count == 0) { ok = false; why = why != "" ? why : ab == "dash" ? "닿는 적 없음" : u.Weapon == "flamer" && ab == "shoot" ? "4칸 안 적 없음" : "대상 없음"; }
            if ((A.target == "ally") && ab != "firstaid" && ab != "command" && AbilityTargets(u, ab).Count == 0) { ok = false; why = why != "" ? why : "대상 없음"; }
            if (ab == "firstaid" && !FriendsOf(u).Concat(units.Where(o => o.downed && o.side == u.side)).Any(f => f != u && World.Dist(f, u) < 1.6f && (f.hp < f.maxHp || (f.downed && u.perks.Contains("revive"))))) { ok = false; why = why != "" ? why : "붙은 부상자 없음"; }
            if (ab == "command" && !FriendsOf(u).Any(f => f != u && f.controlled == 0 && World.CanSee(world, u, f))) { ok = false; why = why != "" ? why : "보이는 아군 없음"; }
            list.Add(new AbilityState { ab = ab, ok = ok, why = why, count = count });
        }
        list.Add(new AbilityState { ab = "reload", ok = u.ap >= 1 && u.ammo < u.clip });
        if (emp != null && emp.emplacement == "mortar") list.Add(new AbilityState { ab = "mortar", ok = u.ap >= 2 && shells > 0, why = shells > 0 ? (u.ap < 2 ? "행동력 2 필요" : "") : "포탄 없음", count = shells });
        if (emp != null && emp.emplacement == "hmg") list.Add(new AbilityState { ab = "heavy", ok = u.ap >= 1 && vis.Count > 0 });
        return list;
    }
    // 능력 대상 고르기: 적 목록 · 아군 목록
    public static List<Unit> AbilityTargets(Unit u, string ab)
    {
        var A = AB[ab];
        if (A.target == "enemy")
        {
            var es = VisibleEnemies(u).Where(e => !A.melee || World.Dist(e, u) < 1.6f);
            if (ab == "dash") es = es.Where(e => World.Dist(e, u) <= A.range + 1.5f && DashSpot(u, e) != null);
            if (ab == "barrage") es = es.Where(e => World.Dist(e, u) <= A.range);
            if (ab == "dominate") es = es.Where(e => !e.def.vehicle); // 전차는 정신 지배 안 됨 (안에 여럿)
            if ((ab == "shoot" || ab == "overwatch") && u.Weapon == "flamer") es = es.Where(e => World.Dist(e, u) <= GameData.flamerLen);
            return es.ToList();
        }
        if (A.target == "ally")
        {
            var pool = FriendsOf(u).Concat(ab == "firstaid" ? units.Where(o => o.downed && o.side == u.side) : Enumerable.Empty<Unit>());
            if (ab == "painkiller") return pool.Where(f => f.controlled == 0 && !f.painkiller && World.Dist(f, u) < 1.6f).ToList(); // 자신 포함
            if (ab == "cleanse") return pool.Where(f => f.controlled == 0 && f.r != null && (f.stress > 0 || f.panic > 0 || f.broke != null) && (f == u || World.CanSee(world, u, f))).ToList();
            return pool.Where(f => f != u && f.controlled == 0 && (A.far ? World.CanSee(world, u, f) : World.Dist(f, u) < 1.6f)).ToList();
        }
        return new List<Unit>();
    }
    public static bool TileTargetOk(Unit u, string ab, Tile t)
    {
        if (t == null) return false;
        var A = AB[ab];
        if (World.Dist(u, t) > A.range) return false;
        if (ab == "mortar") return vis.Contains(t.z * world.W + t.x);
        if (ab == "charge") return t.HasProp && GameData.PROPS[t.prop].emplacement == null;
        if (ab == "trap") return World.Walkable(world, t.x, t.z) && World.UnitAt(world, t.x, t.z) == null && !traps.Any(o => o.x == t.x && o.z == t.z);
        if (ab == "artillery") return vis.Contains(t.z * world.W + t.x);
        if (ab == "tunnel") return World.Walkable(world, t.x, t.z) && World.UnitAt(world, t.x, t.z) == null && World.Dist(u, t) >= 2;
        return true;
    }

    // ── 고양이 AI ──
    // 칸 점수: 엄폐(보이는 쥐 기준) + 쏠 수 있는지 + 측면 + 진지 − 노출 − 거리
    static float ScoreTile(Unit c, int x, int z)
    {
        var w = world;
        float s = 0, bestShot = 0, nearest = 1e9f; int exposed = 0;
        foreach (var r in Rats())
        {
            float d = World.Dist(r.x, r.z, x, z);
            nearest = Mathf.Min(nearest, d);
            bool sees = d <= RULES.sight + 1 && World.Los(w, r.x, r.z, x, z);
            if (!sees) continue;
            var cv = World.CoverAgainst(w, x, z, r.x, r.z);
            s += cv.cover * 14;
            if (cv.cover == 0) exposed++;
            if (cv.flanked) exposed++;
            var hi = GetHitInfo(c, r, ax: x, az: z);
            bestShot = Mathf.Max(bestShot, hi.hit + (hi.flanked ? 20 : 0));
        }
        s += bestShot * 0.45f;
        s -= exposed * 22;
        // 너무 붙거나 너무 멀지 않게
        float want = c.def.flame ? 2.8f : c.Weapon == "sniper" ? 10 : c.Weapon == "smg" ? 4.5f : c.Weapon == "shotgun" ? 2.5f : 7;
        s -= Mathf.Abs(nearest - want) * 3;
        var pr = World.PropOf(World.TileAt(w, x, z));
        if (pr != null && pr.emplacement == "mortar" && shells > 0) s += 45;
        if (pr != null && pr.emplacement == "hmg") s += 22;
        if (World.TileAt(w, x, z).h > 0) s += 12;
        return s;
    }

    class Spot { public int x, z; public float s, cost; public List<Pt> path; }
    static Spot BestTile(Unit c, float budget, bool wantCover)
    {
        var R = World.ReachOf(world, c, budget);
        Spot best = null;
        foreach (var kv in R.Stops.ToList())
        {
            int x = kv.Key % world.W, z = kv.Key / world.W;
            float s = ScoreTile(c, x, z) - kv.Value * 0.6f + R01 * 4;
            if (wantCover && World.CoverSides(world, x, z).Count == 0) s -= 15;
            if (best == null || s > best.s) best = new Spot { x = x, z = z, s = s, cost = kv.Value, path = R.Path(x, z) };
        }
        return best;
    }

    // 확인사살할 쓰러진 쥐 (보이고 가까운 것)
    static Unit DownedInReach(Unit c) => units.Where(r => r.side == "rat" && r.downed && World.Dist(c, r) <= RULES.executeRange && World.CanSee(world, c, r)).OrderBy(r => World.Dist(c, r)).FirstOrDefault();

    static async Awaitable CatTurn()
    {
        var w = world;
        foreach (var c in Cats().ToList())
        {
            if (!c.active || !c.alive || over != null) continue;
            if (c.ap <= 0) continue; // 공포 · 공황
            if (!Rats().Any()) break;
            if (c.visible) View.Focus(c);
            await Sleep(c.visible ? 300 : 60);
            // 1) 진지에 있으면 박격포 · 중기관총
            var emp = World.PropOf(World.TileAt(w, c.x, c.z));
            var shots = VisibleEnemies(c).Select(r => (r, h: GetHitInfo(c, r))).OrderByDescending(s => s.h.hit + (s.h.flanked ? 25 : 0)).ToList();
            if (emp != null && emp.emplacement == "mortar" && shells > 0)
            {
                var tgt = ClusterTarget(c);
                if (tgt != null) { shells--; Say($"{c.name} 박격포 사격!", "warn"); await ThrowGrenade(c, tgt.Value.x, tgt.Value.z, true); continue; }
            }
            if (emp != null && emp.emplacement == "hmg" && shots.Count > 0) { await Shoot(c, shots[0].r, heavy: true); continue; }
            // 1.5) 전차포: 쥐가 보이면 (엄폐한 쥐 우선) 포탄 — 쓰고 나면 2턴 쉼
            if (c.abil.Contains("cannon") && !c.cd.ContainsKey("cannon"))
            {
                var tgt = ClusterTarget(c, AB["cannon"].range, 1);
                if (tgt != null) { await TankCannon(c, tgt.Value.x, tgt.Value.z); continue; }
            }
            // 2) 지금 자리에서 좋은 사격이면 그대로 쏨
            bool flankedHere = shots.Any(s => World.CoverAgainst(w, c.x, c.z, s.r.x, s.r.z).flanked);
            if (shots.Count > 0 && shots[0].h.hit >= 55 && !flankedHere) { await CatAttack(c, shots); continue; }
            // 2.5) 좋은 사격이 없으면 쓰러진 쥐 확인사살
            var finishOff = DownedInReach(c);
            if (finishOff != null && c.ammo > 0) { await Execute(c, finishOff); continue; }
            // 2.7) 화염병: 불 닿는 거리에 쥐가 있으면 태움
            if (c.flame > 0)
            {
                var f = ClusterTarget(c, AB["flame"].range, 1);
                if (f != null) { Say($"{c.name} 화염 방사!", "warn"); await CatFlame(c, f.Value.x, f.Value.z); continue; }
            }
            // 3) 수류탄: 두 마리 이상 모였으면
            if (c.grenades > 0)
            {
                var g = ClusterTarget(c, 9, c.def.grenades >= 3 ? 1 : 2); // 척탄병은 한 마리에게도 던짐
                if (g != null) { Say($"{c.name} 수류탄!", "warn"); await ThrowGrenade(c, g.Value.x, g.Value.z); continue; }
            }
            // 4) 더 나은 자리로 이동 후 사격
            var spot = BestTile(c, c.mob, false);
            if (spot != null && spot.cost > 0)
            {
                await MoveUnit(c, spot.path);
                if (!c.alive) continue;
                if (over != null) return;
            }
            var after = VisibleEnemies(c).Select(r => (r, h: GetHitInfo(c, r))).OrderByDescending(s => s.h.hit).ToList();
            var emp2 = World.PropOf(World.TileAt(w, c.x, c.z));
            if (emp2 != null && emp2.emplacement == "mortar" && shells > 0)
            {
                var tgt = ClusterTarget(c);
                if (tgt != null) { shells--; Say($"{c.name} 박격포 사격!", "warn"); await ThrowGrenade(c, tgt.Value.x, tgt.Value.z, true); continue; }
            }
            if (c.flame > 0 && c.alive) { var f = ClusterTarget(c, AB["flame"].range, 1); if (f != null) { Say($"{c.name} 화염 방사!", "warn"); await CatFlame(c, f.Value.x, f.Value.z); continue; } }
            var finishAfter = DownedInReach(c);
            if (after.Count > 0 && after[0].h.hit >= 25) await CatAttack(c, after);
            else if (finishAfter != null && c.ammo > 0) await Execute(c, finishAfter);
            else { c.overwatch = true; if (c.visible) View.Popup(c, "경계", "#ff9f8f"); }
            await Sleep(120);
        }
        UpdateVision();
    }

    static async Awaitable TankCannon(Unit c, int x, int z)
    {
        var ab = AB["cannon"];
        c.cd["cannon"] = ab.cd + 1; // 고양이 차례 시작에 하나 줄어드니 +1
        c.ap = 0;
        FaceToward(c, x, z);
        Say($"{c.name} 전차포 발사!", "warn");
        View.FocusPair(c, x, z);
        c.face = "attack"; c.faceT = 1.4f;
        await Sleep(350);
        Sfx.Mortar();
        c.st.recoil = 1;
        await View.Shell(c, x, z, 450);
        await Explode(x, z, ab.radius, ab.dmgMin, ab.dmgMax, c, high: true);
    }

    static async Awaitable CatFlame(Unit c, int x, int z)
    {
        var prev = mode;
        mode = "busy";
        c.ap = 1;
        c.flame--;
        await FlameAt(c, x, z);
        mode = prev;
    }

    static async Awaitable CatAttack(Unit c, List<(Unit r, HitInfo h)> shots)
    {
        if (c.ammo <= 0) { Reload(c); Sfx.Reload(); await Sleep(300); return; }
        var best = shots[0];
        if (c.Weapon == "lmg" && best.h.hit < 45 && c.ammo >= 2 && best.r.suppressedBy == null)
        {
            best.r.suppressedBy = c; c.suppressing = best.r; c.ammo--;
            View.FocusPair(c, best.r.x, best.r.z);
            for (int i = 0; i < 6; i++) { View.Muzzle(c, best.r, false, false, false, "lmg"); Sfx.Shot("lmg"); c.st.recoil = 1; await Sleep(80); }
            View.Popup(best.r, "제압됨", "#ffb84a");
            Say($"{c.name}: {best.r.name} 제압 사격", "bad");
            await Sleep(300);
            return;
        }
        await Shoot(c, best.r);
    }

    // 폭발 대상: 보이는 쥐가 가장 많이 들어가는 칸 (min 마리 이상)
    static (int x, int z, float s)? ClusterTarget(Unit c, float range = 16, int min = 1)
    {
        var w = world;
        var cats = Cats().ToList();
        var seenRats = Rats().Where(r => cats.Any(o => World.CanSee(w, o, r))).ToList();
        (int x, int z, float s)? best = null;
        foreach (var r in seenRats)
        {
            if (World.Dist(c, r) > range) continue;
            foreach (var d in new[] { new[] { 0, 0 } }.Concat(World.DIRS8))
            {
                int x = r.x + d[0], z = r.z + d[1];
                if (World.TileAt(w, x, z) == null) continue;
                int n = seenRats.Count(o => World.Dist(o.x, o.z, x, z) <= 1.5f);
                if (cats.Any(o => World.Dist(o.x, o.z, x, z) <= 1.6f)) continue;
                int covered = seenRats.Count(o => World.Dist(o.x, o.z, x, z) <= 1.5f && World.CoverSides(w, o.x, o.z).Count > 0);
                float s = n * 10 + covered * 3;
                if (n >= min && (best == null || s > best.Value.s)) best = (x, z, s);
            }
        }
        return best;
    }
}

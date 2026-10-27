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
            clip = WP(def.weapon).clip + (st != null ? st.clip : 0),
            grenades = st != null ? st.grenades : def.grenades, medkits = st != null ? st.medkits : def.medkits,
            charges = st != null ? st.charges : 0, cheese = st != null ? st.cheese : 0, psi = st != null ? st.psi : 0,
            flame = (st != null && st.abil.Contains("flame")) || def.flame ? AB["flame"].uses : 0,
            critB = st != null ? st.crit : 0, defB = st != null ? st.def : 0, dmgB = st != null ? st.dmg : 0, healB = st != null ? st.heal : 0,
            blast = st != null ? st.blast : 0, boom = st != null ? st.boom : 0,
            sight = st != null ? st.sight : def.sight, climb = st != null && st.climb, whisker = st != null ? st.whisker : 0,
            perks = st != null ? st.perks : new List<string>(), abil = st != null ? st.abil : def.abil.ToList(),
            mob = st != null ? st.mob : def.mob,
            active = side == "rat", pod = pod, seen = side == "rat", target = target,
            stress = r != null ? r.stress : 0, r = r, rosterId = r?.id, xp = r != null ? r.xp : 0,
            fdz = side == "rat" ? -1 : 1, faceIdx = faceIdx,
        };
        u.ammo = u.clip;
        return u;
    }

    public static IEnumerable<Unit> Rats() => units.Where(u => u.side == "rat" && u.alive);
    public static IEnumerable<Unit> Cats() => units.Where(u => u.side == "cat" && u.alive);
    public static IEnumerable<Unit> EnemiesOf(Unit u) => units.Where(o => o.alive && o.side != u.side);
    public static IEnumerable<Unit> FriendsOf(Unit u) => units.Where(o => o.alive && o.side == u.side);

    // 고양이 얼굴: 정해 둔 머리 이름 → 개체 얼굴 번호 (얼룩 · 턱시도 · 뚱보 주황 · 샴 · 흉터 흰 수컷)
    static readonly Dictionary<string, int> CAT_FACE = new Dictionary<string, int> { ["mackerel"] = 0, ["tuxedo"] = 1, ["chonk"] = 2, ["loaf"] = 2, ["boss_ryan"] = 3, ["boss_turret"] = 4, ["boss_dictator"] = 4 };

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
        fires.Clear(); scan = 0;
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
        var m = GameData.MISSIONS.FirstOrDefault(x => x.id == d.missionId);
        if (m == null || d.units == null) return false;
        camp = d.camp; mission = m;
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
            if (!u.alive) { u.st.death = 1; u.forceSide = true; }   // 시체 · 쓰러진 몸은 누운 채로
            u.tint = u.controlled > 0 ? SkillsData.Hex("#b9a0ff") : Color.white;
        }
        foreach (var u in units)
        {
            u.suppressedBy = units.FirstOrDefault(o => o.id == u.suppressedById);
            u.suppressing = units.FirstOrDefault(o => o.id == u.suppressingId);
        }
        fires = d.fires.Select(f => (f[0], f[1], f[2])).ToList();
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
        // 무리 깨우기: 가까이서(wakeRange) 서로 보이거나, 맞았으면 그 무리 전부
        var wake = new HashSet<string>(alerted);
        var rats = Rats().ToList();
        foreach (var c in Cats())
        {
            bool whisk = rats.Any(r => r.whisker > 0 && World.Dist(r, c) <= r.whisker);
            bool seenNow = vis.Contains(c.z * w.W + c.x) || whisk || scan > 0;
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
    public static HitInfo GetHitInfo(Unit a, Unit b, bool overwatchShot = false, bool aimed = false, bool heavy = false, bool knife = false, int ax = int.MinValue, int az = 0)
    {
        var w = world;
        int x = ax == int.MinValue ? a.x : ax, z = ax == int.MinValue ? a.z : az; // 고양이 AI: 가상의 자리에서 계산
        var wp = WP(heavy ? "hmg" : a.Weapon);
        float d = World.Dist(x, z, b.x, b.z);
        var parts = new List<(string, int)> { ("조준", a.aim), ("거리", wp.range(World.JsRound(d))) };
        var cv = knife ? (0, false) : World.CoverAgainst(w, b.x, b.z, x, z);
        if (cv.Item1 > 0) parts.Add((cv.Item1 == 2 ? "높은 엄폐" : "낮은 엄폐", -RULES.coverDef[cv.Item1]));
        if (b.hunker && cv.Item1 > 0) parts.Add(("엎드림", -RULES.hunkerDef));
        int ha = World.TileAt(w, x, z).h, hb = World.TileAt(w, b.x, b.z).h;
        if (ha > hb) parts.Add(("고지", RULES.highGroundAim));
        if (a.suppressedBy != null) parts.Add(("제압당함", RULES.suppressAim));
        if (a.wounds.ContainsKey("arm")) parts.Add(("팔 부상", -RULES.armWoundAim));
        if (overwatchShot) parts.Add(("경계 사격", RULES.overwatchAim));
        if (aimed) parts.Add(("정조준", 20));
        if (knife) parts.Add(("칼", 30));
        if (a.buffAim != 0) parts.Add(("함성", a.buffAim));
        if (b.marked && a.side == "rat") parts.Add(("표적 지정", 15));
        if (a.perks.Contains("closeq") && d <= 4) parts.Add(("근접 사격", 15));
        if (overwatchShot && a.perks.Contains("sentry")) parts.Add(("노련한 경계", -RULES.overwatchAim));
        if (b.defB != 0) parts.Add(("회피", -b.defB));
        if (a.side == "rat" && Rats().Any(o => o != a && Camp.Has(o.r, "leader") && World.Dist(o.x, o.z, x, z) <= 3)) parts.Add(("지휘관 곁", 5));
        if (Camp.Has(a.r, "loner") && !Rats().Any(o => o != a && World.Dist(o.x, o.z, x, z) <= 3)) parts.Add(("외톨이", 10));
        if (a.r != null && a.r.traits.Contains("sibling") && !string.IsNullOrEmpty(a.r.sibling) && units.Any(o => o.rosterId == a.r.sibling && !o.alive && !o.downed)) parts.Add(("복수심", 15));
        if (a.side == "cat" && Cats().Any(o => o.def.aura > 0 && o != a && World.Dist(o.x, o.z, x, z) <= 4)) parts.Add(("장교 지휘", 10));
        int hit = Mathf.Clamp(parts.Sum(p => p.Item2), 1, 99);
        bool open = cv.Item1 == 0 && World.CoverSides(w, b.x, b.z).Count == 0;
        int crit = wp.crit + (cv.Item2 || open ? RULES.flankCrit : 0);
        if (open) crit = wp.crit + 15;
        if (cv.Item2) crit = wp.crit + RULES.flankCrit;
        crit += a.critB;
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
        if (side == "rat")
        {
            scan = 0;
            // 지배 끝난 고양이는 돌아감
            foreach (var u in units) if (u.controlled > 0 && --u.controlled <= 0) { u.side = "cat"; u.active = true; u.tint = Color.white; View.Popup(u, "정신 돌아옴", "#ff9f8f"); Say($"{u.name} 지배 풀림", "warn"); }
        }
        if (side == "cat") foreach (var c in units) c.revealed = false;
        foreach (var u in units.Where(u => u.alive && u.side == side).ToList())
        {
            u.ap = RULES.ap; u.moved = false;
            u.overwatch = false; u.hunker = false; u.buffAim = 0; u.fired = false;
            if (u.suppressing != null) { u.suppressing.suppressedBy = null; u.suppressing = null; }
            if (u.panic > 0) { u.panic--; u.ap = 0; View.Popup(u, "공황 — 움직이지 못함", "#c9a3ff"); }
            if (side == "rat" && Camp.Has(u.r, "smoker") && round > 1) AddStress(u, -(int)TRAITS["smoker"].Get("smoke"));
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
            bool sib = victim.r != null && r.r != null && r.r.sibling == victim.rosterId;
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
            float roll = R100, acc = 0; var B = BREAKS[0];
            foreach (var b in BREAKS) { acc += b.w; if (roll < acc) { B = b; break; } }
            u.broke = null; u.brokeName = B.name;
            u.stress = STRESS.after;
            View.Focus(u); View.Slowmo(0.6f, 0.35f);
            View.Popup(u, $"{B.name}!", "#c9a3ff");
            Say($"{u.name} 정신이 무너졌다 — {B.name} ({B.desc})", "bad");
            u.face = "hit"; u.faceT = 2;
            Witness(u, STRESS.panicNear, "panic");
            await Sleep(900);
            if (B.id == "panic") { u.panic = 1; u.ap = 0; }
            else if (B.id == "frenzy")
            {
                var t = units.Where(o => o != u && o.alive && World.CanSee(world, u, o) && (o.side == "rat" || o.visible)).OrderBy(o => World.Dist(u, o)).FirstOrDefault();
                if (t != null && u.ammo > 0) { Say($"{u.name}이(가) {t.name}에게 총을 겨눈다!", "bad"); await Shoot(u, t); }
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
        await Breakdowns();
        CheckOver();
        if (over != null) return;
        mode = "idle";
        SelectNext();
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
            how = u.deathHow,
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
            if (u.side == "cat") foreach (var r in Rats()) if (r.hidden && World.Dist(r, u) < 1.6f) { r.hidden = false; View.Popup(r, "들킴!", "#ff6b5b"); }
            var wake = UpdateVision();
            // 경계 사격 (제압당한 채 움직여도)
            foreach (var e in EnemiesOf(u).ToList())
            {
                if (!u.alive) break;
                bool react = !quiet && (e.overwatch || e.suppressing == u) && e.ammo > 0 && World.CanSee(w, e, u) && World.Dist(e, u) <= 12;
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

    static async Awaitable<bool> Shoot(Unit a, Unit b, bool overwatchShot = false, bool aimed = false, bool heavy = false)
    {
        var info = GetHitInfo(a, b, overwatchShot, aimed, heavy);
        var wp = WP(heavy ? "hmg" : a.Weapon);
        FaceToward(a, b.x, b.z);
        if (b.side == "cat" && !b.active) alerted.Add(b.pod); // 빗나가도 총소리에 깸
        if (!overwatchShot) View.FocusPair(a, b.x, b.z);
        a.face = "attack"; a.faceT = 1.2f;
        await View.Aim(a, 380);
        if (!heavy) a.ammo = Mathf.Max(0, a.ammo - 1);
        bool hit = R100 < info.hit;
        bool crit = hit && R100 < info.crit;
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
        u.hp -= dmg;
        AddStress(u, crit ? STRESS.crit : STRESS.hit, "hit");
        if (u.side == "cat" && !u.active && !quiet) alerted.Add(u.pod);
        u.face = "hit"; u.faceT = 1.0f;
        u.st.hit = 1;
        View.Blood(u, crit || part == "head" ? 14 : 8);
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
            if (part == "leg") u.mob = Mathf.Max(2, Mathf.FloorToInt(u.def.mob * RULES.legWoundMob));
            if (severed || u.hp <= 0) { await DownUnit(u, from, part, severed); return; }
            Say($"{u.name} {PART_NAME[part]} 부상", u.side == "rat" ? "bad" : "good");
            return;
        }
        if (u.hp <= 0) await Kill(u, from, kind, false, crit);
    }

    // 쓰러짐: 행동 불가 · 누움. 적이 확인사살하면 사망, 이긴 채 끝나면 부상자로 살아남음
    static async Awaitable DownUnit(Unit u, Unit from, string part, bool severed)
    {
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
        if (from != null) from.kills++;
        if (u.side == "cat") statKills++; else statLost++;
        if (u.side == "rat") Witness(u, STRESS.allyDead, "dead");
        else if (from != null) AddStress(from, STRESS.kill - (int)Camp.TraitSum(from.r, "killRelief"));
        u.deathHow = headshot ? "머리에 총탄" : kind == "explosion" ? "폭발" : kind == "fire" ? "불길" : from != null && from.side == "rat" ? "아군의 총에" : "총탄";
        Say(u.side == "rat" ? $"{u.name} 전사…" : $"{u.name} 사살", u.side == "rat" ? "bad" : "good");
        // 쓰러지는 방향: 쏜 쪽 반대. 옆모습으로 돌려서 리그로 쓰러뜨림
        if (from != null) FaceToward(u, from.x, from.z);
        u.forceSide = true;
        // 신체 절단: 머리 명중 · 폭발 · 치명타 · 산탄총 근접
        string gore = null;
        if (headshot) gore = crit || R01 < 0.4f ? "decap" : null;
        else if (kind == "explosion") gore = R01 < 0.6f ? "burst" : "sever";
        else if (crit && R01 < 0.65f) gore = R01 < 0.5f ? "decap" : "sever";
        else if (kind == "shotgun" && R01 < 0.4f) gore = "sever";
        u.gore = gore;
        if (gore != null && u.side == "rat") Witness(u, STRESS.gore, "gore");
        Sfx.Death();
        await View.Die(u, gore);
        u.corpse = true;
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
                case "knife":
                {
                    SpendAP(u, ab);
                    // 칼: 바로 옆으로 붙어 찌름 (소리 안 남 → 무리를 안 깨움)
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
                    break;
                }
                case "scurry":
                {
                    SpendAP(u, ab);
                    var R = World.ReachOf(world, u, 3);
                    View.Popup(u, "쪼르르", "#8fd0ff");
                    await MoveUnit(u, R.Path(tile.x, tile.z), scamper: true, quiet: true);
                    break;
                }
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
                case "gnaw":
                    SpendAP(u, ab);
                    FaceToward(u, tile.x, tile.z);
                    u.face = "attack"; u.faceT = 1.2f;
                    View.Popup(u, "갉작갉작…", "#d8d0b8");
                    for (int i = 0; i < 4; i++) { u.st.recoil = 0.5f; await Sleep(150); }
                    View.Debris(tile.x, tile.z, 'X');
                    tile.prop = '\0'; View.GroundDirty();
                    Say($"{u.name} 철조망을 갉아 끊음", "good");
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
                case "cheese":
                    SpendAP(u, ab); u.cheese--;
                    AddStress(target, STRESS.cheese * (Camp.Has(target.r, "glutton") ? 2 : 1));
                    target.suppressedBy = null; target.panic = 0;
                    View.Popup(target, "치즈! 스트레스 −25", "#ffcf4a");
                    Say($"{u.name} → {target.name} 치즈 배급", "good");
                    await Sleep(350);
                    break;
                case "mark":
                    SpendAP(u, ab);
                    target.marked = true;
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
                case "radioscan":
                    SpendAP(u, ab); u.used.Add("radioscan"); scan = 1;
                    View.Popup(u, "무전 탐지", "#8fd0ff");
                    Say($"{u.name}: 무전으로 고양이 위치를 잡았다", "good");
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
                case "ward":
                    SpendAP(u, ab); u.psi -= AB["ward"].psi;
                    await View.Psi(u, target, "#8fd0ff");
                    target.ward = 4;
                    View.Popup(target, "방어막", "#c9a3ff");
                    Say($"{u.name} → {target.name} 마법 방어막", "good");
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
            if ((ab == "shoot" || ab == "aimed" || ab == "suppress" || ab == "overwatch") && u.ammo <= 0) { ok = false; why = "탄 없음"; }
            if (ab == "aimed" && u.moved) { ok = false; why = "이동함"; }
            if (ab == "suppress" && u.ammo < 2) { ok = false; why = "탄 2발 필요"; }
            if (ab == "grenade") { count = u.grenades; if (u.grenades <= 0) { ok = false; why = "없음"; } }
            if (ab == "firstaid") { count = u.medkits; if (u.medkits <= 0) { ok = false; why = "없음"; } }
            if (ab == "charge") { count = u.charges; if (u.charges <= 0) { ok = false; why = "없음"; } else if (Adjacent(u, t => t.HasProp && GameData.PROPS[t.prop].emplacement == null).Count == 0) { ok = false; why = "붙은 엄폐물 없음"; } }
            if (ab == "gnaw" && Adjacent(u, t => t.prop == 'X').Count == 0) { ok = false; why = "붙은 철조망 없음"; }
            if (ab == "flame") { count = u.flame; if (u.flame <= 0) { ok = false; why = "연료 없음"; } }
            if (ab == "cheese") { count = u.cheese; if (u.cheese <= 0) { ok = false; why = "없음"; } else if (!FriendsOf(u).Any(f => f != u && World.Dist(f, u) < 1.6f)) { ok = false; why = "붙은 아군 없음"; } }
            if (A.psi > 0) { count = u.psi; if (u.psi < A.psi) { ok = false; why = "정신력 부족"; } }
            if (A.once && u.used.Contains(ab)) { ok = false; why = "이번 판엔 씀"; }
            if (ab == "ambush" && World.CoverSides(world, u.x, u.z).Count == 0) { ok = false; why = "엄폐물 옆에서만"; }
            if (ab == "knife" && !vis.Any(e => World.Dist(e, u) < 1.6f)) { ok = false; why = "붙은 적 없음"; }
            else if (A.target == "enemy" && vis.Count == 0) { ok = false; why = why != "" ? why : "대상 없음"; }
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
        if (A.target == "enemy") return VisibleEnemies(u).Where(e => !A.melee || World.Dist(e, u) < 1.6f).ToList();
        if (A.target == "ally")
        {
            var pool = FriendsOf(u).Concat(ab == "firstaid" ? units.Where(o => o.downed && o.side == u.side) : Enumerable.Empty<Unit>());
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
        if (ab == "gnaw") return t.prop == 'X';
        if (ab == "scurry") { var c = World.ReachOf(world, u, 3).Cost(t.x, t.z); return c != null && c > 0; }
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
        float want = c.def.flame ? 2.8f : c.Weapon == "sniper" ? 10 : c.Weapon == "smg" ? 4.5f : 7;
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
                var g = ClusterTarget(c, 9, 2);
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

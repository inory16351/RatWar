// 튜토리얼 (기초 훈련): 하수구 출구 앞 작은 맵(maps.json 'drill')에서 그레이 상사가 무전으로 지시하며 기본 조작을 가르침
// 참고: NKK 튜토리얼 (단계마다 발동 조건 → 대사 → 행동 기다리기, 하단 대사 상자 + 초상 + 강조)
// 화면은 하이라키 Hud/Tutorial (Blocker · Marker · Goal/Text · Box/Portrait · Name · Body · Hint · Skip), 여기선 글 채우기 · 진행만
// 단계: 대사(클릭 · Space · Enter 로 넘김, 그동안 전장 입력 막음) → 할 일(위 목표 줄 + 칸 강조 + ▼ 표시) → 해내면 다음
// 훈련 쥐는 죽지 않음(Battle.Spared), 결과는 사령부 기록에 안 남음(Battle.Finish), 중간 저장 안 됨(BattleSave.CanSave)
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class Tutorial : MonoBehaviour
{
    public static Tutorial I;
    public const string ID = "tut";
    public const int SQUAD = 3;

    // 훈련 임무 (tools/maps/build_maps.js 의 extraMissions 와 같게)
    public static readonly MissionDef MISSION = new MissionDef
    {
        id = ID, act = 0, name = "tut.mission", map = "drill", obj = "elim", reward = 0, shells = 0, brief = "tut.mission",
        cats = new[]
        {
            new CatSpawn { cls = "trooper", head = "tuxedo", x = 7, z = 7, pod = "A" },
            new CatSpawn { cls = "trooper", head = "chonk", x = 9, z = 7, pod = "A" },
        },
    };
    // 쥐 쪽 엄폐 줄(z 13) 바로 뒤 칸
    static readonly Pt[] COVER_SPOTS = { new Pt(4, 14), new Pt(5, 14), new Pt(8, 14), new Pt(11, 14), new Pt(12, 14) };

    public static bool On => Battle.mission != null && Battle.mission.id == ID && Battle.world != null;
    // 대사 중 (또는 대사를 방금 넘긴 프레임) → 전장 클릭 · 키 막기
    public static bool Blocking => I && I.isActiveAndEnabled && On && (I.talking || Time.frameCount <= I.eatFrame + 1);
    // 강조할 칸 (BoardView 가 금색으로 칠함)
    public static readonly List<Pt> Marks = new List<Pt>();

    class Line { public string who, key; public Line(string w, string k) { who = w; key = k; } }
    class Step
    {
        public string id;
        public Line[] talk = new Line[0];
        public string goal;                         // 할 일 (없으면 대사만)
        public Func<bool> ready = () => true;       // 시작 조건 (아군 차례 · 한가함은 따로 봄)
        public Func<bool> skip = () => false;       // 해당 없으면 건너뜀
        public Func<bool> done;                     // 해냈나
        public Action start;                        // 시작할 때 (쥐 고르기 · 표시)
        public Func<Unit> actor = () => null;       // 지시받는 쥐 (목표 글 {5})
        public Func<Vector3?> point = () => null;   // ▼ 표시 자리
        public bool anyTurn;                        // 고양이 차례에도 (끝 대사)
    }

    RectTransform root, marker, portrait; Transform box, goalBox, blocker;
    Text nameT, bodyT, hintT, goalT, markerT;
    List<Step> steps; int idx = -1; bool acting, talking; int eatFrame = -10;
    Line[] lines; int li; float typed, readyT, camT; string full;
    Unit actorU; int grenadesAtStart;
    RatRecord sgt;
    Action afterTalk;

    void Awake()
    {
        I = this;
        root = (RectTransform)transform;
        blocker = UIK.F(transform, "Blocker");
        marker = (RectTransform)UIK.F(transform, "Marker"); markerT = marker.GetComponent<Text>();
        goalBox = UIK.F(transform, "Goal"); goalT = UIK.T(goalBox, "Text");
        box = UIK.F(transform, "Box");
        portrait = (RectTransform)UIK.F(box, "Portrait");
        nameT = UIK.T(box, "Name"); bodyT = UIK.T(box, "Body"); hintT = UIK.T(box, "Hint");
        UIK.OnClick(UIK.B(box, "Skip"), AskSkip);
        UIK.OnClick(blocker.GetComponent<Button>(), Advance);   // 대사 중 아무 데나 클릭 = 다음
        HideAll();
    }

    void HideAll()
    {
        UIK.Show(blocker, false); UIK.Show(box, false); UIK.Show(goalBox, false); UIK.Show(marker, false);
        talking = false; Marks.Clear();
    }

    // ── 사령부: 새 전쟁 프롤로그 뒤 (전쟁마다 한 번) ──
    // 이 PC 에서 처음이면 묻지 않고 바로 훈련, 한 번 본 뒤로는(다른 슬롯 · 새 전쟁) 예/아니오로 물어 건너뛸 수 있음
    const string SEEN_PREF = "tutSeen";
    public static bool SeenOnce => PlayerPrefs.GetInt(SEEN_PREF, 0) == 1;

    public static void Offer(CampData c)
    {
        if (c.war.seen.Contains("tutorial")) return;
        c.war.seen.Add("tutorial"); Camp.Save(c);
        if (!SeenOnce) { Begin(c); return; }
        Menus.Ask(Loc.T("tut.ask"), () => Begin(c), Loc.T("ui.common.yes"), Loc.T("ui.common.no"));
    }

    public static void Begin(CampData c)
    {
        PlayerPrefs.SetInt(SEEN_PREF, 1); PlayerPrefs.Save();
        var ids = c.deploy.Where(id => c.squad.Any(r => r.id == id && Camp.Ready(r))).Concat(c.squad.Where(Camp.Ready).Select(r => r.id)).Distinct().Take(SQUAD).ToList();
        GameRoot.I.Deploy(MISSION, ids);   // Hud 가 켜지며 Awake
        if (I) I.Run();
    }

    void Run()
    {
        sgt = new RatRecord { id = "tut_sgt", name = "tut.sgt.name", face = 3, fur = 0, gear = 2, xp = 60, traits = new List<string> { "veteran" } };
        steps = Build();
        idx = -1; acting = false; readyT = 0; afterTalk = null;
        HideAll();
        Next();
    }

    // ── 단계 ──
    static List<Unit> Rats => Battle.units.Where(u => u.side == "rat" && u.r != null).ToList();
    static Unit R(int i) { var l = Rats; return i < l.Count ? l[i] : null; }
    static List<Unit> SeenCats => Battle.Cats().Where(c => c.visible).ToList();
    static bool CanUse(Unit u, string ab) => u != null && u.alive && u.ap > 0 && Battle.AbilitiesOf(u).Any(a => a.ab == ab && a.ok);
    static Unit FirstCan(string ab) => Battle.Rats().FirstOrDefault(u => CanUse(u, ab));
    static void Select(Unit u) { if (u == null || u.ap <= 0) return; Battle.sel = u; Battle.mode = "move"; Battle.ability = null; View.Focus(u); Battle.version++; }
    static Vector3? Above(Unit u) => u?.view ? u.view.Top + Vector3.up * 0.35f : (Vector3?)null;
    static string KeyOf(Unit u, string ab) { if (u == null) return "?"; var l = Battle.AbilitiesOf(u); int i = l.FindIndex(a => a.ab == ab); return ab == "reload" ? "R" : i >= 0 ? (i + 1).ToString() : "?"; }

    List<Step> Build()
    {
        var L = new List<Step>();
        L.Add(new Step { id = "intro", talk = new[] { new Line("sgt", "tut.intro.1"), new Line("r0", "tut.intro.2"), new Line("sgt", "tut.intro.3") } });
        L.Add(new Step
        {
            id = "camera", talk = new[] { new Line("sgt", "tut.camera.1") }, goal = "tut.goal.camera",
            start = () => camT = 0,
            done = () => camT > 0.9f,
        });
        L.Add(new Step
        {
            id = "move", talk = new[] { new Line("sgt", "tut.move.1"), new Line("sgt", "tut.move.2"), new Line("r0", "tut.move.3") }, goal = "tut.goal.move",
            start = () => { Select(R(0)); SetMarks(COVER_SPOTS); },
            actor = () => R(0),
            done = () => Battle.Rats().Any(u => u.moved && World.CoverSides(Battle.world, u.x, u.z).Count > 0) || Battle.Rats().All(u => u.moved),
            point = () => { var p = Marks.FirstOrDefault(); return Marks.Count > 0 ? BoardView.TilePos(p.x, p.z) + Vector3.up * 0.4f : (Vector3?)null; },
        });
        L.Add(new Step
        {
            id = "cover", talk = new[] { new Line("sgt", "tut.cover.1"), new Line("sgt", "tut.cover.2"), new Line("r0", "tut.cover.3") },
            skip = () => SeenCats.Count == 0 && Battle.Cats().All(c => c.active),   // 이미 깨서 싸우는 중이면 '자는 놈' 얘기는 뺌
            start = () => { var c = SeenCats.FirstOrDefault(); if (c != null) View.Focus(c); },
        });
        L.Add(new Step
        {
            id = "others", talk = new[] { new Line("sgt", "tut.others.1"), new Line("r1", "tut.others.2") }, goal = "tut.goal.others",
            skip = () => Rats.Count <= 1 || Battle.Rats().All(u => u.moved),
            start = () => { Select(Battle.Rats().FirstOrDefault(u => !u.moved && u.ap > 0)); SetMarks(COVER_SPOTS); },
            done = () => Battle.Rats().All(u => u.moved || u.ap <= 0),
            point = () => Above(Battle.Rats().FirstOrDefault(u => !u.moved)),
        });
        // 엄폐로 갔는데도 고양이가 안 보이면: 앞으로 더 (보일 때까지)
        L.Add(new Step
        {
            id = "scout", talk = new[] { new Line("sgt", "tut.scout.1") }, goal = "tut.goal.scout",
            skip = () => FirstCan("shoot") != null || Battle.Cats().Any(c => c.active),
            start = () => Select(Battle.Rats().FirstOrDefault(u => u.ap > 0)),
            done = () => FirstCan("shoot") != null || Battle.Cats().Any(c => c.active),
        });
        L.Add(new Step
        {
            id = "shoot", talk = new[] { new Line("sgt", "tut.shoot.1"), new Line("sgt", "tut.shoot.2"), new Line("act", "tut.shoot.3") }, goal = "tut.goal.shoot",
            ready = () => FirstCan("shoot") != null,
            start = () => { actorU = FirstCan("shoot"); Select(actorU); },
            actor = () => actorU,
            done = () => Battle.Rats().Any(u => u.fired) || Battle.Cats().Count() < 2,
            point = () => Above(actorU != null ? Battle.VisibleEnemies(actorU).OrderBy(e => World.Dist(e, actorU)).FirstOrDefault() : null),
        });
        L.Add(new Step
        {
            id = "awake", talk = new[] { new Line("sgt", "tut.awake.1"), new Line("r1", "tut.awake.2") },
            ready = () => Battle.Cats().Any(c => c.active),
            start = () => { var c = Battle.Cats().FirstOrDefault(x => x.visible); if (c != null) View.Focus(c); },
        });
        L.Add(new Step
        {
            id = "overwatch", talk = new[] { new Line("sgt", "tut.overwatch.1"), new Line("act", "tut.overwatch.2") }, goal = "tut.goal.overwatch",
            skip = () => Battle.turn != "rat" || FirstCan("overwatch") == null || Battle.round > 2,
            start = () => { actorU = FirstCan("overwatch"); Select(actorU); },
            actor = () => actorU,
            done = () => Battle.Rats().Any(u => u.overwatch) || Battle.turn != "rat",
            point = () => Above(actorU),
        });
        L.Add(new Step
        {
            id = "endturn", talk = new[] { new Line("sgt", "tut.end.1"), new Line("r2", "tut.end.2") }, goal = "tut.goal.end",
            skip = () => Battle.turn != "rat" || Battle.round > 2,
            done = () => Battle.turn == "cat" || Battle.round > roundAt,
            start = () => roundAt = Battle.round,
        });
        L.Add(new Step
        {
            id = "after", talk = new[] { new Line("sgt", "tut.after.1"), new Line("sgt", "tut.after.2"), new Line("r2", "tut.after.3"), new Line("sgt", "tut.after.4") },
            ready = () => Battle.round >= 2,
            start = () => Select(Battle.Rats().FirstOrDefault(u => u.hp < u.maxHp) ?? Battle.Rats().FirstOrDefault()),
        });
        L.Add(new Step
        {
            id = "grenade", talk = new[] { new Line("sgt", "tut.grenade.1"), new Line("sgt", "tut.grenade.2"), new Line("act", "tut.grenade.3") }, goal = "tut.goal.grenade",
            skip = () => FirstCan("grenade") == null || SeenCats.Count == 0,
            start = () =>
            {
                actorU = FirstCan("grenade"); Select(actorU);
                grenadesAtStart = Battle.Rats().Sum(u => u.grenades);
                var cs = SeenCats; float cx = (float)cs.Average(c => c.x), cz = (float)cs.Average(c => c.z);
                var spot = new Pt(Mathf.RoundToInt(cx), Mathf.RoundToInt(cz));
                if (actorU != null && !Battle.TileTargetOk(actorU, "grenade", World.TileAt(Battle.world, spot.x, spot.z))) { var c0 = cs.OrderBy(c => World.Dist(c, actorU)).First(); spot = new Pt(c0.x, c0.z); }
                SetMarks(new[] { spot }, false);
            },
            actor = () => actorU,
            done = () => Battle.Rats().Sum(u => u.grenades) < grenadesAtStart || Battle.turn != "rat",
            point = () => { var p = Marks.FirstOrDefault(); return Marks.Count > 0 ? BoardView.TilePos(p.x, p.z) + Vector3.up * 0.6f : (Vector3?)null; },
        });
        L.Add(new Step
        {
            id = "finish", talk = new[] { new Line("sgt", "tut.finish.1"), new Line("sgt", "tut.finish.2") }, goal = "tut.goal.finish",
            done = () => false,   // Finished() 가 끝냄
        });
        return L;
    }
    int roundAt;

    // free = 빈 칸만 (이동할 자리). 수류탄 자리는 고양이가 선 칸이어도 됨
    void SetMarks(IEnumerable<Pt> pts, bool free = true) { Marks.Clear(); Marks.AddRange(pts.Where(p => !free || World.UnitAt(Battle.world, p.x, p.z) == null && World.Walkable(Battle.world, p.x, p.z))); }

    void Next()
    {
        acting = false; Marks.Clear(); UIK.Show(goalBox, false); UIK.Show(marker, false);
        idx++; readyT = 0;
    }

    // 지금 단계 시작: 대사 → (할 일이 있으면) 목표 띄우기
    void Begin(Step s)
    {
        s.start?.Invoke();
        Talk(s.talk, () =>
        {
            if (s.done == null) { Next(); return; }
            acting = true;
            var a = s.actor();
            goalT.text = "▶ " + Loc.F(s.goal, Args(a));
            UIK.Show(goalBox, true);
            if (s.id == "move" || s.id == "others") SetMarks(COVER_SPOTS);
        });
    }

    object[] Args(Unit actor)
    {
        string N(Unit u) => u != null ? Loc.T(u.name) : Loc.T("tut.rookie");
        var a = actor ?? R(0);
        return new object[] { N(R(0)), N(R(1)), N(R(2)), KeyOf(a, "overwatch"), KeyOf(a, "grenade"), N(a), KeyOf(a, "shoot") };
    }

    // ── 대사 상자 ──
    void Talk(Line[] l, Action then)
    {
        if (l == null || l.Length == 0) { then?.Invoke(); return; }
        lines = l; li = -1; afterTalk = then; talking = true;
        UIK.Show(blocker, true); UIK.Show(box, true); UIK.Show(goalBox, false); UIK.Show(marker, false);
        ShowLine(0);
    }

    void ShowLine(int i)
    {
        li = i;
        var ln = lines[i];
        Unit u = ln.who == "r0" ? R(0) : ln.who == "r1" ? R(1) : ln.who == "r2" ? R(2) : ln.who == "act" ? (actorU ?? R(0)) : null;
        if (u != null && u.r != null)
        {
            UIK.Portrait(portrait, u.r);
            nameT.text = UIK.Col($"{Loc.T(CampRules.RANKS[CampRules.RankOf(u.xp)].name)} {Loc.T(u.name)}", UIK.RAT);
        }
        else
        {
            UIK.Portrait(portrait, sgt);
            nameT.text = UIK.Col(Loc.T("tut.sgt.name"), UIK.GOLD) + "  " + UIK.Dim(Loc.T("tut.sgt.radio"));
        }
        full = Loc.F(ln.key, Args(actorU));
        typed = 0; bodyT.text = "";
        hintT.text = UIK.Dim(Loc.F("tut.hint", li + 1, lines.Length));
    }

    void Advance()
    {
        if (!talking) return;
        eatFrame = Time.frameCount;
        if (typed < full.Length) { typed = full.Length; return; }
        if (li + 1 < lines.Length) { ShowLine(li + 1); return; }
        talking = false;
        UIK.Show(blocker, false); UIK.Show(box, false);
        var a = afterTalk; afterTalk = null; a?.Invoke();
    }

    void AskSkip() => Menus.Ask(Loc.T("tut.skip.ask"), Leave, Loc.T("tut.skip.yes"), Loc.T("ui.common.cancel"));

    // 사령부로 (건너뛰기 · 다 끝남). 전투 루프가 돌고 있으면 over 로 멈춤
    void Leave()
    {
        if (Battle.over == null) Battle.over = "skip";
        Battle.mode = "over";
        HideAll(); steps = null;
        Time.timeScale = 1;
        var lobby = Lobby.I ? Lobby.I : FindAnyObjectByType<Lobby>(FindObjectsInactive.Include);
        lobby.Show();
    }

    // Battle.Finish 가 부름 (사령부 정산 대신)
    public static void Finished(string result)
    {
        if (!I) return;
        I.acting = false; I.Marks_Clear();
        I.steps = null;
        var end = result == "win"
            ? new[] { new Line("sgt", "tut.win.1"), new Line("r0", "tut.win.2"), new Line("sgt", "tut.win.3"), new Line("sgt", "tut.win.4"), new Line("sgt", "tut.win.5") }
            : new[] { new Line("sgt", "tut.lose.1") };
        I.Delay(1.4f, () => I.Talk(end, I.Leave));
    }
    void Marks_Clear() { Marks.Clear(); UIK.Show(goalBox, false); UIK.Show(marker, false); }

    float delayT = -1; Action delayed;
    void Delay(float s, Action a) { delayT = s; delayed = a; }

    void Update()
    {
        if (!On) return;
        float dt = Time.unscaledDeltaTime;
        if (delayT >= 0) { delayT -= dt; if (delayT < 0) { var d = delayed; delayed = null; d?.Invoke(); } }

        // 대사: 타자 + 넘기기 (클릭은 Blocker 버튼)
        if (talking)
        {
            typed = Mathf.Min(full.Length, typed + dt * 40);
            bodyT.text = full.Substring(0, (int)typed);
            var kb = Keyboard.current;
            if (kb != null && !Menus.Blocking && (kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame)) Advance();
            return;
        }
        if (steps == null || idx >= steps.Count) return;
        if (idx < 0) { Next(); return; }
        var s = steps[idx];
        bool calm = Battle.over == null && (s.anyTurn || (Battle.turn == "rat" && Battle.mode != "busy")) && !Menus.Blocking && !Cutscene.Playing;

        if (!acting)
        {
            // 연출이 끝나고 잠깐 뒤에 시작
            readyT = calm && s.ready() ? readyT + dt : 0;
            if (readyT < 0.5f) return;
            if (s.skip()) { Next(); return; }
            Begin(s);
            return;
        }

        // 할 일 기다리기
        if (s.id == "camera")
        {
            var kb = Keyboard.current; var ms = Mouse.current;
            bool cam = (kb != null && (kb.wKey.isPressed || kb.aKey.isPressed || kb.sKey.isPressed || kb.dKey.isPressed))
                || (ms != null && (Mathf.Abs(ms.scroll.ReadValue().y) > 0.01f || ms.rightButton.isPressed && CameraRig.I.rightDragged));
            if (cam) camT += Mathf.Max(dt, 0.25f * (ms != null && Mathf.Abs(ms.scroll.ReadValue().y) > 0.01f ? 1 : 0));
        }
        if (s.done != null && s.done() && Battle.mode != "busy") { Next(); return; }
        if (s.skip() && s.id != "move" && s.id != "camera") { Next(); return; }
        // 목표 글은 지시받는 쥐가 바뀌면 다시
        goalT.text = "▶ " + Loc.F(s.goal, Args(s.actor()));
        // ▼ 표시
        var p = Battle.turn == "rat" ? s.point() : null;
        UIK.Show(marker, p != null);
        if (p != null)
        {
            var sp = CameraRig.I.cam.WorldToScreenPoint(p.Value);
            if (sp.z < 0) UIK.Show(marker, false);
            else
            {
                RectTransformUtility.ScreenPointToLocalPointInRectangle(root, sp, null, out var lp);
                marker.anchoredPosition = lp + Vector2.up * (10 + Mathf.Abs(Mathf.Sin(Time.unscaledTime * 4)) * 14);
            }
        }
    }
}

// 전투 HUD (웹 ui.js): 위 차례 · 임무 · 기록 · 명중 툴팁 · 배너 · 아래 분대 카드 + 행동 패널 · 병사 머리 위 표 · 팝업 · 레터박스
// 아래 줄 배치 (사용자 요청 10-27): 왼쪽 = 간단한 분대 카드, 바로 옆 = 넓힌 행동 패널(큰 버튼 · 이름 · 단축키 · 남은 횟수 · 못 쓰는 이유)
// 입력: 클릭 · 키 (웹 UI.click · UI.key)
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class Hud : MonoBehaviour
{
    public static Hud I;
    Text turn, shells, mission, log, tip, banner;
    RectTransform tipRt, squad, abil, tags, popups, lbTop, lbBot;
    Transform selInfo, actionPanel;
    Image flash;
    Button endTurn;
    List<Unit> targets = new List<Unit>();
    public Unit CurrentTarget => targets.Count > 0 ? targets[Battle.targetIdx % targets.Count] : null;
    int lastVer = -1, selPid; string lastSig;
    float bannerT, flashK, lbK, lbT;
    readonly Dictionary<Unit, RectTransform> tagOf = new Dictionary<Unit, RectTransform>();
    class Pop { public RectTransform rt; public Text t; public Vector3 world; public float age; public bool big; }
    readonly List<Pop> pops = new List<Pop>();
    RectTransform canvasRt;

    void Awake()
    {
        I = this;
        var t = transform;
        turn = UIK.T(t, "Top/Turn"); shells = UIK.T(t, "Top/Shells");
        mission = UIK.T(t, "Mission/Text"); log = UIK.T(t, "Log");
        tip = UIK.T(t, "Tip/Text"); tipRt = (RectTransform)UIK.F(t, "Tip");
        banner = UIK.T(t, "Banner");
        squad = (RectTransform)UIK.F(t, "Bottom/Squad");
        actionPanel = UIK.F(t, "Bottom/Action");
        selInfo = UIK.F(t, "Bottom/Action/Sel");
        abil = (RectTransform)UIK.F(t, "Bottom/Action/Abil");
        endTurn = UIK.B(t, "Bottom/Action/EndTurn");
        tags = (RectTransform)UIK.F(t, "Tags"); popups = (RectTransform)UIK.F(t, "Popups");
        lbTop = (RectTransform)UIK.F(t, "LetterTop"); lbBot = (RectTransform)UIK.F(t, "LetterBot");
        flash = UIK.I(t, "Flash");
        canvasRt = (RectTransform)GetComponentInParent<Canvas>().transform;
        UIK.OnClick(endTurn, () => Battle.EndRatTurnUI());
        // 전투 중간 저장 (아군 차례에만)
        saveBtn = UIK.B(t, "SaveBar/Save"); exitBtn = UIK.B(t, "SaveBar/Exit");
        UIK.OnClick(saveBtn, () => Banner(BattleSave.Save() ? "저장했습니다" : "지금은 저장할 수 없습니다", "rat"));
        UIK.OnClick(exitBtn, () => { if (BattleSave.Save()) Lobby.I.Show(); else Banner("지금은 저장할 수 없습니다", "cat"); });
    }
    Button saveBtn, exitBtn;

    // 전투 시작 직후
    public void Restart()
    {
        lastSig = null; lastVer = -1; selPid = 0;
        Tpl.Clear(squad);
        foreach (var kv in tagOf) if (kv.Value) Destroy(kv.Value.gameObject);
        tagOf.Clear();
        foreach (var p in pops) if (p.rt) Destroy(p.rt.gameObject);
        pops.Clear();
        Battle.SelectNext();
        Banner($"아군 차례 · {Battle.round}턴", "rat");
    }

    public void Banner(string text, string side)
    {
        banner.text = text;
        banner.color = side == "cat" ? UIK.CAT : UIK.RAT;
        bannerT = 1.4f;
    }
    public void Flash(float k) => flashK = k;
    public void Letterbox(bool on) => lbT = on ? 1 : 0;

    public void Popup(Vector3 world, string txt, Color col, bool big)
    {
        var t = Tpl.Spawn<Text>("PopupText", popups);
        t.text = txt; t.color = col;
        t.fontSize = big ? 40 : 24;
        pops.Add(new Pop { rt = t.rectTransform, t = t, world = world, big = big });
    }

    // ── 능력 쓰기 (웹 useAbility) ──
    public void UseAbility(string ab)
    {
        var u = Battle.sel;
        if (u == null || Battle.turn != "rat" || Battle.mode == "busy") return;
        var it = Battle.AbilitiesOf(u).FirstOrDefault(a => a.ab == ab);
        if (it == null || !it.ok) return;
        var A = GameData.ABILITIES[ab];
        if (A.target == "self") { Battle.DoAbilityUI(u, ab); return; }
        if (A.target == "enemy" || A.target == "ally")
        {
            targets = Battle.AbilityTargets(u, ab);
            if (A.target == "enemy") targets = targets.OrderByDescending(e => Battle.GetHitInfo(u, e).hit).ToList();
            else targets = targets.OrderBy(f => f.downed ? 0 : 1).ThenBy(f => (float)f.hp / f.maxHp).ToList();
            if (targets.Count == 0) return;
            Battle.targetIdx = 0; Battle.mode = "target"; Battle.ability = ab;
            View.FocusPair(u, targets[0].x, targets[0].z);
            Battle.version++;
            return;
        }
        if (A.target == "tile") { Battle.mode = "tile"; Battle.ability = ab; Battle.version++; }
    }
    void Confirm() { var t = CurrentTarget; if (t != null && Battle.sel != null) Battle.DoAbilityUI(Battle.sel, Battle.ability, t); }
    void Cancel()
    {
        if (Battle.mode == "target" || Battle.mode == "tile") { Battle.mode = "move"; Battle.ability = null; Battle.version++; if (Battle.sel != null) View.Focus(Battle.sel); }
    }

    void Click(Vector2 screen)
    {
        if (Battle.over != null || Battle.turn != "rat" || Battle.mode == "busy") return;
        var u = Battle.sel;
        var tile = BoardView.I.PickTile(screen);
        var hitU = BoardView.I.UnitAtScreen(screen);
        if (Battle.mode == "target")
        {
            if (hitU != null && targets.Contains(hitU)) { if (CurrentTarget == hitU) Confirm(); else { Battle.targetIdx = targets.IndexOf(hitU); Battle.version++; } }
            else Cancel();
            return;
        }
        if (Battle.mode == "tile")
        {
            if (Battle.TileTargetOk(u, Battle.ability, tile)) Battle.DoAbilityUI(u, Battle.ability, null, tile);
            else Cancel();
            return;
        }
        if (hitU != null && hitU.side == "rat" && hitU.ap > 0 && hitU.panic <= 0 && hitU.alive) { Battle.sel = hitU; Battle.mode = "move"; View.Focus(hitU); Battle.version++; return; }
        if (hitU != null && hitU.side == "cat" && u != null && Battle.VisibleEnemies(u).Contains(hitU))
        {
            UseAbility("shoot");
            if (Battle.mode == "target") Battle.targetIdx = Mathf.Max(0, targets.IndexOf(hitU));
            return;
        }
        if (u != null && tile != null && Battle.mode == "move") Battle.DoMoveUI(u, tile.x, tile.z);
    }

    void Keys()
    {
        var kb = Keyboard.current; if (kb == null) return;
        if (kb.tabKey.wasPressedThisFrame)
        {
            if (Battle.mode == "target") { Battle.targetIdx++; Battle.version++; }
            else if (Battle.turn == "rat" && Battle.mode != "busy") Battle.SelectNext(Battle.sel);
            return;
        }
        if (kb.escapeKey.wasPressedThisFrame) { Cancel(); return; }
        if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame) { if (Battle.mode == "target") Confirm(); else Battle.EndRatTurnUI(); return; }
        if (kb.spaceKey.wasPressedThisFrame && Battle.sel != null) { View.Focus(Battle.sel); return; }
        if (Battle.sel == null || Battle.turn != "rat") return;
        var list = Battle.AbilitiesOf(Battle.sel);
        if (kb.rKey.wasPressedThisFrame) { UseAbility("reload"); return; }
        Key[] digits = { Key.Digit1, Key.Digit2, Key.Digit3, Key.Digit4, Key.Digit5, Key.Digit6, Key.Digit7, Key.Digit8, Key.Digit9 };
        for (int i = 0; i < digits.Length && i < list.Count; i++) if (kb[digits[i]].wasPressedThisFrame) UseAbility(list[i].ab);
    }

    void Update()
    {
        if (Battle.world == null) return;
        float dt = Time.unscaledDeltaTime;
        // 입력 (UI 위가 아닐 때만 전장 클릭)
        var ms = Mouse.current;
        if (ms != null && Battle.over == null)
        {
            var mp = ms.position.ReadValue();
            BoardView.I.hoverTile = GameRoot.PointerOnBoard ? BoardView.I.PickTile(mp) : null;
            if (ms.leftButton.wasPressedThisFrame && GameRoot.PointerOnBoard) Click(mp);
            if (ms.rightButton.wasReleasedThisFrame && !CameraRig.I.rightDragged) Cancel();
            Keys();
        }
        // 배너 · 번쩍 · 레터박스
        bannerT -= dt; UIK.Show(banner.transform, bannerT > 0);
        if (bannerT > 0) banner.color = new Color(banner.color.r, banner.color.g, banner.color.b, Mathf.Clamp01(bannerT * 3));
        flashK = Mathf.Max(0, flashK - dt * 5); flash.color = new Color(1, 1, 1, flashK * 0.7f); flash.enabled = flashK > 0.01f;
        lbK += (lbT - lbK) * Mathf.Min(1, dt * 8);
        lbTop.sizeDelta = new Vector2(0, 110 * lbK); lbBot.sizeDelta = new Vector2(0, 110 * lbK);
        UpdateTags(); UpdatePops(dt);

        saveBtn.interactable = exitBtn.interactable = BattleSave.CanSave;
        turn.text = Battle.over != null ? "" : Battle.turn == "rat" ? $"아군 차례 · {Battle.round}턴" : "고양이 군 차례…";
        turn.color = Battle.turn == "rat" ? UIK.RAT : UIK.CAT;
        shells.text = Battle.shells > 0 ? $"박격포탄 {Battle.shells}" : "";
        RenderTip();
        if (lastVer == Battle.version) return;
        lastVer = Battle.version;
        mission.text = $"<b>{Battle.mission.name}</b>\n" + string.Join("\n", Battle.ObjectiveText().Select(o => (o.ok ? "☑ " : "☐ ") + (o.ok ? UIK.Col(o.t, UIK.OK) : o.t))) + "\n" + UIK.Dim($"{Battle.round}턴 · 보상 보급품 {Battle.mission.reward}");
        log.text = string.Join("\n", Battle.log.Select(l => UIK.Col(l.msg, l.kind == "bad" ? UIK.NEG : l.kind == "good" ? UIK.OK : l.kind == "warn" ? UIK.GOLD : l.kind == "title" ? UIK.INK : UIK.DIM)));
        RenderBottom();
    }

    // ── 아래 줄: 분대 카드(간단히) + 행동 패널(넓게) ──
    void RenderBottom()
    {
        var u = Battle.sel;
        var ab = u != null && Battle.turn == "rat" ? Battle.AbilitiesOf(u) : new List<Battle.AbilityState>();
        var rats = Battle.units.Where(x => x.side == "rat" && x.r != null).ToList();
        string sig = string.Join("|", rats.Select(x => $"{x.hp},{x.ap},{x.alive},{x.downed},{x.ammo},{x.stress},{x.overwatch},{x.hunker},{x.hidden},{x.ward},{x.wounds.Count}"))
            + (u?.id ?? 0) + string.Join(",", ab.Select(a => a.ab + a.ok + a.count)) + Battle.mode + Battle.ability + Battle.turn;
        if (sig == lastSig) return;
        lastSig = sig;

        // 분대 카드: 초상 · 이름 · 체력 칸 · 행동력 점 · 얇은 스트레스 줄
        if (squad.childCount != rats.Count)
        {
            Tpl.Clear(squad);
            foreach (var x in rats)
            {
                var c = Tpl.Spawn("SquadCard", squad).transform;
                c.name = x.id.ToString();
                var unit = x;
                UIK.OnClick(c.GetComponent<Button>(), () => { if (unit.alive && unit.ap > 0 && Battle.turn == "rat" && Battle.mode != "busy") { Battle.sel = unit; Battle.mode = "move"; View.Focus(unit); Battle.version++; } });
                UIK.Portrait((RectTransform)UIK.F(c, "Portrait"), x.r);
            }
        }
        for (int i = 0; i < rats.Count; i++)
        {
            var x = rats[i]; var c = squad.GetChild(i);
            bool done = x.alive && Battle.turn == "rat" && x.ap <= 0;
            var ss = CampRules.StressState(x.stress, Camp.BreakAt(x.r));
            UIK.T(c, "Name").text = x.name;
            string hp = new string('■', Mathf.Max(0, x.hp)) + UIK.Dim(new string('□', Mathf.Max(0, x.maxHp - x.hp)));
            UIK.T(c, "Info").text = x.alive ? $"{UIK.Col(hp, UIK.RAT)}\n{UIK.Col(new string('●', x.ap), UIK.GOLD)}{UIK.Dim(new string('○', Mathf.Max(0, 2 - x.ap)))} 탄{x.ammo}" + (x.wounds.Count > 0 ? UIK.Col(" 부상", UIK.NEG) : "")
                : x.downed ? UIK.Col("쓰러짐", UIK.NEG) : UIK.Dim("전사");
            UIK.Bar(UIK.F(c, "SBar"), x.stress / 100f, ss.c);
            c.GetComponent<Image>().color = x == u ? UIK.GOLD : done ? new Color(0.55f, 0.55f, 0.55f) : x.alive ? Color.white : new Color(0.4f, 0.35f, 0.35f);
            UIK.Tip(c.GetComponent<Button>(), $"<b>{CampRules.RANKS[CampRules.RankOf(x.xp)].name} {x.name}</b> · {x.def.name}\n체력 {x.hp}/{x.maxHp} · 탄 {x.ammo}/{x.clip} · 행동력 {x.ap}\n스트레스 {UIK.Col($"{x.stress} · {ss.t}", ss.c)}" + (x.wounds.Count > 0 ? "\n" + UIK.Col(string.Join(" · ", x.wounds.Select(w => Battle.PART_NAME[w.Key] + (w.Value > 1 ? " 잘림" : " 부상"))), UIK.NEG) : ""));
        }

        // 행동 패널
        bool show = u != null && Battle.turn == "rat";
        UIK.Show(actionPanel, Battle.over == null);
        UIK.Show(selInfo, show);
        if (show)
        {
            var ss = u.r != null ? CampRules.StressState(u.stress, Camp.BreakAt(u.r)) : ("", Color.white);
            UIK.T(selInfo, "Text").text = u.r != null
                ? $"<b>{CampRules.RANKS[CampRules.RankOf(u.xp)].name} {u.name}</b>  {UIK.Dim(u.def.name)}   체력 {u.hp}/{u.maxHp} · 탄 {u.ammo}/{u.clip}{(u.psi > 0 ? $" · 정신력 {u.psi}" : "")} · 행동력 {u.ap}   스트레스 {UIK.Col($"{u.stress} {ss.Item1}", ss.Item2)}\n"
                  + string.Join(" ", u.r.traits.Select(tr => UIK.Col("[" + CampRules.TRAITS[tr].name + "]", CampRules.TRAITS[tr].rare ? UIK.PSY : UIK.DIM)))
                : $"<b>{u.name}</b>  {UIK.Col($"정신 지배 중 · {u.controlled}턴", UIK.PSY)}";
            var pslot = (RectTransform)UIK.F(selInfo, "Portrait");
            if (u.r != null && selPid != u.id) { selPid = u.id; UIK.Portrait(pslot, u.r); }
            if (u.r == null && selPid != -1) { selPid = -1; Tpl.Clear(pslot); }
        }
        // 능력 버튼 (큰 아이콘 + 이름 + 단축키 + 남은 횟수 + 못 쓰는 이유)
        Tpl.Clear(abil);
        for (int i = 0; i < ab.Count; i++)
        {
            var a = ab[i]; var A = GameData.ABILITIES[a.ab];
            var b = Tpl.Spawn("AbilButton", abil).transform;
            bool on = (Battle.mode == "target" || Battle.mode == "tile") && Battle.ability == a.ab;
            UIK.I(b, "Icon").sprite = ArtLib.Icon(a.ab);
            UIK.I(b, "Icon").color = a.ok ? Color.white : new Color(1, 1, 1, 0.35f);
            UIK.T(b, "Label").text = A.name;
            UIK.T(b, "Label").color = a.ok ? UIK.INK : UIK.DIM;
            UIK.T(b, "Key").text = (i + 1).ToString();
            UIK.T(b, "Count").text = a.count != null ? "×" + a.count : "";
            UIK.T(b, "Why").text = a.ok ? "" : a.why;
            var img = b.GetComponent<Image>();
            img.sprite = ArtLib.Sprite(on ? "out/ui/btn_hi.png" : a.ok ? "out/ui/btn.png" : "out/ui/btn_off.png");
            img.color = A.psi > 0 && a.ok ? new Color(0.85f, 0.75f, 1f) : Color.white;
            string id = a.ab;
            UIK.OnClick(b.GetComponent<Button>(), () => UseAbility(id));
            UIK.Tip(b.GetComponent<Button>(), AbilityTip(a.ab, a.why, a.count));
        }
        endTurn.interactable = Battle.turn == "rat" && Battle.mode != "busy";
    }

    static readonly Dictionary<string, string> FLAVOR = new Dictionary<string, string>
    {
        ["scurry"] = "쥐는 도망칠 줄 안다. 그래서 오래 산다.", ["ambush"] = "고양이는 구멍 안을 보지 못한다.", ["warcry"] = "찍! 찍찍! 찍찍찍!", ["gnaw"] = "이빨은 쇠보다 끈질기다.",
        ["tunnel"] = "땅 밑은 언제나 쥐의 영토였다.", ["cheese"] = "전쟁 중에도 치즈는 치즈다.", ["command"] = "전령은 작고 빠르고, 셀 수 없이 많다.", ["dominate"] = "눈을 마주치는 순간, 그 고양이는 더 이상 고양이가 아니다.",
        ["ward"] = "보이지 않는 막이 총알을 삼킨다.", ["knife"] = "총소리는 무리를 깨운다. 칼은 깨우지 않는다.", ["flame"] = "불은 엄폐물을 가리지 않는다.",
    };
    // 롤식 툴팁 내용
    public static string AbilityTip(string ab, string why = "", int? count = null)
    {
        if (!GameData.ABILITIES.TryGetValue(ab, out var A)) return "";
        var tags = new List<string> { A.ap > 0 ? $"행동력 {A.ap}" : "행동력 없이", A.end ? "쓰면 턴 끝" : "턴 계속" };
        if (A.psi > 0) tags.Add($"정신력 {A.psi}");
        if (A.once) tags.Add("전투당 1번");
        if (count != null) tags.Add($"남은 횟수 {count}");
        if (A.range > 0) tags.Add($"사거리 {A.range}칸");
        if (A.radius > 0) tags.Add($"범위 {A.radius}칸");
        if (A.HasDmg) tags.Add($"피해 {A.dmgMin}–{A.dmgMax}");
        return $"<size=18><b>{A.name}</b></size>{(A.key != "" ? UIK.Dim("   단축키 " + A.key.ToUpper()) : "")}\n{UIK.Col(string.Join(" · ", tags), UIK.GOLD)}\n{A.desc}"
            + (!string.IsNullOrEmpty(why) ? "\n" + UIK.Col("✖ " + why, UIK.NEG) : "") + (FLAVOR.TryGetValue(ab, out var f) ? "\n" + UIK.Dim($"<i>“{f}”</i>") : "");
    }

    // 명중 미리보기 (대상 고르는 중 · 적 위에 마우스)
    void RenderTip()
    {
        Unit a = Battle.sel, b = null;
        var ms = Mouse.current;
        if (Battle.mode == "target") b = CurrentTarget;
        else if (a != null && Battle.turn == "rat" && Battle.mode == "move" && ms != null && GameRoot.PointerOnBoard)
        {
            var h = BoardView.I.UnitAtScreen(ms.position.ReadValue());
            if (h != null && h.side == "cat" && Battle.VisibleEnemies(a).Contains(h)) b = h;
        }
        if (a == null || b == null || Battle.over != null) { UIK.Show(tipRt, false); return; }
        UIK.Show(tipRt, true);
        string ab = Battle.mode == "target" ? Battle.ability : "shoot";
        var A = GameData.ABILITIES[ab];
        if (Battle.mode == "target" && !new[] { "shoot", "aimed", "heavy", "knife" }.Contains(ab))
        {
            tip.text = $"<b>{A.name} → {b.name}</b>  {UIK.Dim($"체력 {b.hp}/{b.maxHp}")}\n{A.desc}\n{UIK.Dim("클릭 · Enter 사용 / Tab 다음 대상 / Esc 취소")}";
        }
        else
        {
            var info = Battle.GetHitInfo(a, b, aimed: ab == "aimed", heavy: ab == "heavy", knife: ab == "knife");
            int bonus = ab == "aimed" ? 1 : 0;
            tip.text = $"<b>{b.name}</b>  {UIK.Dim($"체력 {b.hp}/{b.maxHp}")}\n<size=34><b>{info.hit}%</b></size>{UIK.Dim(" 명중")}   <size=26>{info.crit}%</size>{UIK.Dim(" 치명")}\n"
                + $"피해 {info.wp.dmgMin + bonus}–{info.wp.dmgMax + bonus}{(info.flanked ? UIK.Col(" · 측면!", UIK.GOLD) : "")}{(b.active ? "" : UIK.Col(" · 눈치 못 챔", UIK.GOLD))}\n"
                + string.Join("\n", info.parts.Select(p => $"{p.n}  {UIK.Col((p.v >= 0 ? "+" : "") + p.v, p.v >= 0 ? UIK.OK : UIK.NEG)}"))
                + (Battle.mode == "target" ? "\n" + UIK.Dim("클릭 · Enter 발사 / Tab 다음 대상 / Esc 취소") : "");
        }
    }

    // ── 병사 머리 위 표 (이름 · 체력 · 스트레스 · 선택 화살표) ──
    void UpdateTags()
    {
        var cam = CameraRig.I.cam;
        foreach (var u in Battle.units)
        {
            if (!tagOf.TryGetValue(u, out var rt)) { rt = Tpl.Spawn<RectTransform>("UnitTag", tags); tagOf[u] = rt; }
            bool vis = u.view && u.view.gameObject.activeInHierarchy && (u.alive || u.downed) && Battle.over == null;
            UIK.Show(rt, vis);
            if (!vis) continue;
            var sp = cam.WorldToScreenPoint(u.view.Top);
            if (sp.z < 0) { UIK.Show(rt, false); continue; }
            RectTransformUtility.ScreenPointToLocalPointInRectangle(tags, sp, null, out var lp);
            rt.anchoredPosition = lp;
            bool selU = u == Battle.sel && Battle.turn == "rat";
            UIK.T(rt, "Name").text = selU || u.side == "cat" && u.target ? u.name : "";
            UIK.T(rt, "Hp").text = u.downed ? UIK.Col("✚ 쓰러짐", UIK.NEG) : UIK.Col(new string('■', Mathf.Max(0, u.hp)), u.side == "rat" ? UIK.RAT : UIK.CAT) + UIK.Dim(new string('□', Mathf.Max(0, u.maxHp - u.hp)))
                + (u.overwatch ? UIK.Col(" ◉", UIK.RAT) : "") + (u.hunker ? UIK.Col(" ▼", UIK.RAT) : "") + (u.ward > 0 ? UIK.Col(" ⛨", UIK.PSY) : "") + (u.marked ? UIK.Col(" ◎", UIK.GOLD) : "") + (u.suppressedBy != null ? UIK.Col(" 〰", UIK.GOLD) : "");
            var bar = UIK.F(rt, "SBar");
            UIK.Show(bar, u.side == "rat" && u.r != null);
            if (u.r != null) UIK.Bar(bar, u.stress / 100f, u.stress >= 60 ? UIK.PSY : SkillsData.Hex("#8a77b8"));
            var arrow = UIK.T(rt, "Arrow");
            arrow.enabled = selU;
            if (selU) arrow.rectTransform.anchoredPosition = new Vector2(0, 46 + Mathf.Abs(Mathf.Sin(Time.unscaledTime * 4)) * 8);
        }
    }

    void UpdatePops(float dt)
    {
        var cam = CameraRig.I.cam;
        for (int i = pops.Count - 1; i >= 0; i--)
        {
            var p = pops[i]; p.age += dt;
            if (p.age > 1.2f || !p.rt) { if (p.rt) Destroy(p.rt.gameObject); pops.RemoveAt(i); continue; }
            var sp = cam.WorldToScreenPoint(p.world);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(popups, sp, null, out var lp);
            p.rt.anchoredPosition = lp + Vector2.up * (p.age * 50);
            var c = p.t.color; c.a = 1 - Mathf.Max(0, p.age - 0.7f) / 0.5f; p.t.color = c;
        }
    }
}

// 지하 사령부 (웹 lobby.js): 작전(임무 · 출전 6자리) · 분대(카드 · 상세 · 육각 스킬판) · 모집 · 추모비
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using static CampRules;

public class Lobby : MonoBehaviour
{
    public static Lobby I;
    string tab = "ops", sel;
    CampData C;
    Transform ops, squad, recruit, memorial;
    Text stats, toast; float toastT;

    void Awake()
    {
        I = this;
        var t = transform;
        stats = UIK.T(t, "Header/Stats"); toast = UIK.T(t, "Toast");
        ops = UIK.F(t, "Body/Ops"); squad = UIK.F(t, "Body/Squad"); recruit = UIK.F(t, "Body/Recruit"); memorial = UIK.F(t, "Body/Memorial");
        foreach (var name in new[] { "ops", "squad", "recruit", "memorial" })
        {
            string n = name;
            UIK.OnClick(UIK.B(t, "Header/Tabs/" + n), () => { tab = n; Render(); });
        }
        UIK.OnClick(UIK.B(t, "Header/Reset"), () => { ConfirmReset(); });
        // 기록은 바뀔 때마다 자동 저장되지만, 확인용 버튼
        UIK.OnClick(UIK.B(t, "Header/Save"), () => { Camp.Save(C); Toast(Loc.T(BattleSave.Exists ? "ui.lobby.saved_battle" : "ui.lobby.saved")); });
        // 난이도: 저장 버튼을 복제해 옆에 둠. 표시만 (새 전쟁 때 정하고 전쟁 동안 고정)
        var save = UIK.F(t, "Header/Save") as RectTransform;
        diffBtn = Instantiate(save.gameObject, save.parent).GetComponent<Button>();
        diffBtn.name = "Diff";
        var drt = (RectTransform)diffBtn.transform;
        drt.SetSiblingIndex(save.GetSiblingIndex());
        if (!save.parent.GetComponent<LayoutGroup>()) drt.anchoredPosition = save.anchoredPosition - new Vector2(save.rect.width + 12, 0);
        UIK.OnClick(diffBtn, () => Toast(Loc.F("ui.lobby.diff.toast", Loc.T(RULES.DiffOf(C.diff).name))));
        // 수비대 배치 · 요새화: '이번 턴 쉬기' 버튼을 복제해 왼쪽에 나란히
        var wait = UIK.F(ops, "WarInfo/Wait") as RectTransform;
        garBtn = CloneLeft(wait, "Garrison", 1); fortBtn = CloneLeft(wait, "Fortify", 2);
        // 언어를 바꾸면 열린 사령부를 다시 그림
        Loc.Changed += () => { if (this && gameObject.activeInHierarchy) Render(); };
    }
    Button diffBtn, garBtn, fortBtn;
    bool garrisonMode; // 켜면 대기 쥐를 누를 때 출전 대신 고른 침공 칸의 수비대로

    static Button CloneLeft(RectTransform src, string name, int k)
    {
        var b = Instantiate(src.gameObject, src.parent).GetComponent<Button>();
        b.name = name;
        var rt = (RectTransform)b.transform;
        rt.SetSiblingIndex(src.GetSiblingIndex());
        if (!src.parent.GetComponent<LayoutGroup>()) rt.anchoredPosition = src.anchoredPosition - new Vector2((src.rect.width + 12) * k, 0);
        return b;
    }

    float resetArm;
    void ConfirmReset()
    {
        // 두 번 누르면 지움 (웹은 confirm 창)
        if (Time.unscaledTime - resetArm > 2.5f) { resetArm = Time.unscaledTime; Toast(Loc.T("ui.lobby.reset.arm")); return; }
        resetArm = 0;
        // 난이도를 고르고 '전쟁 시작'을 눌러야 실제로 지움 (뒤로 = 취소)
        DiffPicker.Open(C.diff, d =>
        {
            Camp.Reset(); BattleSave.Delete(); Camp.Save(Camp.Fresh(d)); sel = null; selHex = -1;
            Toast(Loc.F("ui.lobby.reset.done", Loc.T(RULES.DiffOf(d).name))); Render();
        }, null);
    }

    public void Show()
    {
        gameObject.SetActive(true);
        GameRoot.I.SetMode("lobby");
        Music.Play("lobby");
        Render();
    }

    void Update()
    {
        toastT -= Time.unscaledDeltaTime;
        UIK.Show(toast.transform, toastT > 0);
    }
    void Toast(string s) { toast.text = s; toastT = 2.2f; }

    public void Render()
    {
        C = Camp.Load() ?? Camp.Fresh();
        Camp.Save(C);
        stats.text = Loc.F("ui.lobby.stats", C.supplies, C.battles, C.squad.Count, SQUAD_MAX);
        var D = RULES.DIFFS[Mathf.Clamp(C.diff, 0, RULES.DIFFS.Length - 1)];
        diffBtn.GetComponentInChildren<Text>(true).text = Loc.F("ui.lobby.diff.btn", Loc.T(D.name));
        UIK.Tip(diffBtn, Loc.F("ui.lobby.diff.tip", Loc.T(D.name), Loc.T(D.desc), D.threatStep, D.income));
        foreach (var n in new[] { "ops", "squad", "recruit", "memorial" })
            UIK.B(transform, "Header/Tabs/" + n).GetComponent<Image>().sprite = ArtLib.Sprite(tab == n ? "out/ui/btn_hi.png" : "out/ui/btn.png");
        UIK.Show(ops, tab == "ops"); UIK.Show(squad, tab == "squad"); UIK.Show(recruit, tab == "recruit"); UIK.Show(memorial, tab == "memorial");
        Canvas.ForceUpdateCanvases();
        if (tab == "ops") Ops(); else if (tab == "squad") Squad(); else if (tab == "recruit") Recruit(); else Memorial();
    }

    static string StressLine(RatRecord r)
    {
        var ss = StressState(r.stress, Camp.BreakAt(r));
        return Loc.F("ui.lobby.stress", UIK.Col($"{r.stress}/{Camp.BreakAt(r)} · {Loc.T(ss.t)}", ss.c)); // 무너지는 기준(특성마다 다름)도 같이
    }
    static string TraitText(RatRecord r) => string.Join(" ", r.traits.Select(t => UIK.Col("[" + Loc.T(TRAITS[t].name) + "]", TRAITS[t].rare ? UIK.PSY : UIK.GOLD)));
    // 관계 특성은 상대 이름도 (전우 · 멘토 · 제자 · 앙숙)
    static readonly System.Collections.Generic.Dictionary<string, string> BOND_OF = new System.Collections.Generic.Dictionary<string, string> { ["comrade"] = "comrade", ["mentor"] = "pupil", ["pupil"] = "mentor", ["feud"] = "feud" };
    string Partners(RatRecord r, string t)
    {
        if (!BOND_OF.TryGetValue(t, out var kind)) return "";
        var names = Camp.Partners(r, kind).Select(id => C.squad.Concat(C.fallen).FirstOrDefault(q => q.id == id)?.Name).Where(n => n != null).ToList();
        return names.Count > 0 ? UIK.Dim($" ({string.Join(", ", names)})") : "";
    }
    string TraitTip(RatRecord r) => string.Join("\n", r.traits.Select(t => $"<b>{Loc.T(TRAITS[t].name)}</b>{Partners(r, t)} — {Loc.T(TRAITS[t].desc)}"));
    static string Status(RatRecord r)
    {
        var s = new System.Collections.Generic.List<string>();
        if (r.lost.Count > 0) s.Add(UIK.Col(Loc.F("ui.lobby.lost", string.Join(" · ", r.lost.Select(p => Loc.T(p == "arm" ? "ui.lobby.lost.arm" : "ui.lobby.lost.leg")))), UIK.NEG));
        if (!string.IsNullOrEmpty(r.afflicted)) s.Add(UIK.Col(Loc.F("ui.lobby.afflicted", Loc.T(r.afflicted)), UIK.PSY));
        if (r.stress >= STRESS.shaky) s.Add(UIK.Col(Loc.T("ui.lobby.shaky"), UIK.PSY));
        if (!string.IsNullOrEmpty(r.grief)) s.Add(UIK.Dim(Loc.F("ui.lobby.grief", Loc.T(r.grief))));
        return string.Join(" · ", s);
    }

    // ── 작전: 전략 지도(땅따먹기) · 고른 칸 브리핑 · 출전 6자리 · 대기 ──
    int selHex = -1;
    // 고른 칸에서 할 수 있는 일: 침공 막기 · 공격 · 없음
    string ActionOf(int i) => i < 0 || C.war.over != "" ? null : War.CanDefend(C, i) ? "defend" : War.CanAttack(C, i) ? "attack" : null;

    void Ops()
    {
        var w = C.war;
        // 처음 들어오면 프롤로그, 전쟁이 끝났으면 결말
        if (!w.seen.Contains("prologue")) { w.seen.Add("prologue"); Camp.Save(C); Cutscene.I.Play(Loc.T("ui.lobby.cut.prologue"), War.D.prologue, () => { Render(); Tutorial.Offer(C); }); }
        else if (w.over != "" && !w.seen.Contains("ending")) { w.seen.Add("ending"); Camp.Save(C); Cutscene.I.Play(Loc.T(w.over == "win" ? "ui.common.win" : "ui.common.lose"), w.over == "win" ? War.D.victory : War.D.defeat, () => Cutscene.I.Play(Loc.T("epi.head"), Epilogue.Build(C, w.over == "win"), Render)); }
        // 고른 칸이 없거나 할 일 없는 고양이 땅이면 첫 침공 칸으로 (내 땅은 요새화할 수 있으니 그대로)
        if (selHex < 0 || ActionOf(selHex) == null && !War.Rat(C, selHex)) selHex = w.threats.Count > 0 ? w.threats[0] : selHex;
        WarMap();
        WarInfo();
        var act = ActionOf(selHex);
        var m = act != null ? War.Mission(C, selHex, act) : null;
        var br = UIK.F(ops, "Brief");
        if (m == null)
        {
            string why = w.over == "win" ? UIK.Col(Loc.T("ui.ops.won"), UIK.GOLD)
                : w.over == "lose" ? UIK.Col(Loc.T("ui.ops.lost"), UIK.NEG)
                : selHex >= 0 ? HexText(selHex) + "\n" + UIK.Dim(Loc.T("ui.ops.fightable")
                    + (War.CanFortify(C, selHex) ? "\n" + Loc.F("ui.ops.fort_hint", War.FORT_COST, Mathf.RoundToInt(War.FORT_BONUS * 100)) : ""))
                : UIK.Dim(Loc.T("ui.ops.pick_hex"));
            UIK.T(br, "Head").text = $"<size=30><b>{Loc.F("ui.ops.turn", w.turn)}</b></size>\n{why}";
        }
        else
        {
            string obj = Loc.T(m.obj == "elim" ? "ui.ops.obj.elim" : m.obj == "officer" ? "ui.ops.obj.officer" : "ui.ops.obj.flag");
            bool sp = War.IsSpecial(C, selHex, act);
            UIK.T(br, "Head").text = $"{(sp ? UIK.Col(Loc.T("ui.ops.special") + "  ", UIK.GOLD) : "")}<size=30><b>{Loc.T(m.name)}</b></size>    {UIK.Col(Loc.F("ui.ops.reward", m.reward), UIK.GOLD)}\n{Loc.T(m.brief)}\n{UIK.Col(Loc.T("ui.ops.goal"), UIK.GOLD)} {obj} · {UIK.Col(Loc.T("ui.ops.enemy"), UIK.GOLD)} {Loc.F("ui.ops.cats", m.cats.Length)}{(m.shells > 0 ? $" · {UIK.Col(Loc.T("ui.ops.shells"), UIK.GOLD)} {m.shells}" : "")}"
                + "\n" + UIK.Dim(Loc.T(act == "attack" ? "ui.ops.attack_note" : "ui.ops.defend_note"))
                + (act == "defend" ? "\n" + HoldLine(selHex) : "");
        }
        UIK.T(br, "DeployH").text = $"{UIK.Col(Loc.T("ui.ops.deploy_h"), UIK.OK)} {C.deploy.Count} / {DEPLOY_MAX}   {UIK.Dim(Loc.T("ui.ops.deploy_hint"))}";
        bool posting = garrisonMode && War.CanDefend(C, selHex);
        UIK.T(br, "BenchH").text = $"{UIK.Col(Loc.T("ui.ops.bench_h"), UIK.RAT)} {C.squad.Count - C.deploy.Count}   "
            + (posting ? UIK.Col(Loc.F("ui.ops.post_hint", Loc.T(War.D.hexes[selHex].name), War.GARRISON_MAX), UIK.GOLD)
               : UIK.Dim(Loc.T(garrisonMode ? "ui.ops.garrison_pick" : "ui.ops.bench_hint")));
        var slots = UIK.F(br, "Slots");
        Tpl.Clear(slots);
        for (int i = 0; i < DEPLOY_MAX; i++)
        {
            var s = Tpl.Spawn("MiniCard", slots).transform;
            string id = i < C.deploy.Count ? C.deploy[i] : null;
            var r = id != null ? C.squad.FirstOrDefault(q => q.id == id) : null;
            FillMini(s, r, i + 1);
            if (r != null) UIK.OnClick(s.GetComponent<Button>(), () => { C.deploy.Remove(id); Camp.Save(C); Render(); });
        }
        var bench = UIK.F(br, "Bench");
        Tpl.Clear(bench);
        foreach (var r in C.squad.Where(q => !C.deploy.Contains(q.id)))
        {
            var s = Tpl.Spawn("MiniCard", bench).transform;
            FillMini(s, r, 0);
            var rr = r;
            UIK.OnClick(s.GetComponent<Button>(), () =>
            {
                int at = War.Posted(C, rr.id);
                // 수비대 모드: 고른 침공 칸에 넣기 / 빼기. 아니면 수비대에서 빼거나 출전으로
                if (posting) { if (!War.TogglePost(C, selHex, rr) && Camp.Ready(rr)) Toast(Loc.F("ui.ops.garrison_full", War.GARRISON_MAX)); }
                else if (at >= 0) War.TogglePost(C, at, rr);
                else { if (!Camp.Ready(rr) || C.deploy.Count >= DEPLOY_MAX) return; C.deploy.Add(rr.id); Camp.Save(C); }
                Render();
            });
        }
        var shaky = C.deploy.Select(id => C.squad.FirstOrDefault(q => q.id == id)).Where(r => r != null && r.stress >= STRESS.shaky).ToList();
        UIK.T(br, "Go/Warn").text = m == null ? UIK.Dim(Loc.T("ui.ops.warn.no_hex")) : C.deploy.Count == 0 ? Loc.T("ui.ops.warn.no_rat") : shaky.Count > 0 ? UIK.Col(Loc.F("ui.ops.warn.shaky", string.Join(", ", shaky.Select(r => r.Name))), UIK.PSY) : UIK.Dim(Loc.T("ui.ops.warn.rest"));
        var go = UIK.B(br, "Go/Btn");
        go.interactable = m != null && C.deploy.Count > 0;
        go.GetComponentInChildren<Text>(true).text = Loc.T("ui.ops.go");
        int hex = selHex;
        // 전투 중에 '저장 후 사령부로' 나왔으면: 그 전투를 이어하는 것만 됨 (새 출격 = 저장이 지워짐, 쉬기 = 턴이 넘어감)
        var saved = BattleSave.Exists ? BattleSave.Load() : null;
        if (saved != null)
        {
            var sm = War.FromId(C, saved.missionId) ?? GameData.MISSIONS.FirstOrDefault(x => x.id == saved.missionId);
            UIK.T(br, "Head").text = $"{UIK.Col(Loc.T("ui.ops.saved.head"), UIK.GOLD)}  <size=30><b>{Loc.T(sm?.name ?? "ui.ops.saved.battle")}</b></size>\n"
                + UIK.Dim(Loc.F("ui.ops.saved.note", saved.round));
            UIK.T(br, "Go/Warn").text = UIK.Dim(Loc.T("ui.ops.saved.warn"));
            go.GetComponentInChildren<Text>(true).text = Loc.T("ui.ops.saved.btn");
            go.interactable = true;
            UIK.OnClick(go, () => { if (!GameRoot.I.Resume()) { BattleSave.Delete(); Toast(Loc.T("ui.ops.saved.fail")); Render(); } });
            return;
        }
        UIK.OnClick(go, () =>
        {
            War.Begin(C, hex, act);
            var deploy = C.deploy.ToList();
            void Launch() => GameRoot.I.Deploy(m, deploy);
            // 주요 전투는 처음 한 번 컷씬
            string sid = War.SpecialId(hex);
            if (War.IsSpecial(C, hex, act) && !C.war.seen.Contains(sid)) { C.war.seen.Add(sid); Camp.Save(C); Cutscene.I.Play(Loc.T(m.name), War.D.specials[sid].slides, Launch); }
            else Launch();
        });
    }

    // 전략 지도: 그림 위에 육각 칸 40개 (내 땅 파랑 · 고양이 땅 빨강 · 침공 = 빨간 테두리 · 고른 칸 = 금색)
    void WarMap()
    {
        var map = (RectTransform)UIK.F(ops, "WarMap");
        var box = (RectTransform)UIK.F(map, "Hexes");
        Tpl.Clear(box);
        float W = map.rect.width, H = map.rect.height;
        float size = War.D.dx / 1.5f * 2 * W; // 육각 너비 = 2R
        for (int i = 0; i < War.N; i++)
        {
            var h = War.D.hexes[i];
            bool rat = War.Rat(C, i), threat = C.war.threats.Contains(i), can = ActionOf(i) != null, sp = War.IsSpecial(C, i, rat ? "defend" : "attack");
            var g = Tpl.Spawn("WarHex", box).transform;
            g.name = "Hex" + i;
            var rt = (RectTransform)g;
            var p = War.Pos(i);
            rt.anchorMin = rt.anchorMax = new Vector2(0, 1);
            rt.anchoredPosition = new Vector2(p.x * W, -p.y * H);
            rt.sizeDelta = new Vector2(size, size);
            var hex = g.GetComponent<UIHex>();
            hex.color = rat ? new Color(0.25f, 0.55f, 0.95f, 0.26f) : new Color(0.85f, 0.2f, 0.15f, can ? 0.22f : 0.12f);
            hex.edge = i == selHex ? UIK.GOLD : threat ? new Color(1, 0.25f, 0.15f, 0.95f) : can ? new Color(1, 0.9f, 0.7f, 0.85f) : new Color(0.1f, 0.07f, 0.05f, 0.55f);
            hex.SetVerticesDirty();
            string icon = h.fx == "hq" ? "command" : h.fx == "capital" ? "dominate" : h.fx == "hospital" ? "supply" : h.fx == "recruit" ? "warcry" : h.fx == "workshop" ? "trap" : h.fx == "artillery" ? "artillery" : null;
            var im = UIK.I(g, "Icon");
            im.sprite = icon != null ? ArtLib.Icon(icon) : null; im.enabled = im.sprite != null;
            int nGar = threat ? War.Garrison(C, i).Count() : 0;
            UIK.T(g, "Label").text = (sp ? UIK.Col("◆", UIK.GOLD) : "") + (C.war.forts.Contains(i) ? UIK.Col("▣", UIK.RAT) : "") + Loc.T(h.name)
                + (threat ? "\n" + UIK.Col(Loc.F("ui.map.invade", Mathf.RoundToInt(War.HoldChance(C, i) * 100)), UIK.CAT) + (nGar > 0 ? UIK.Col(" " + Loc.F("ui.map.garrison", nGar), UIK.OK) : "") : "");
            int idx = i;
            var btn = g.GetComponent<Button>();
            UIK.OnClick(btn, () => { selHex = idx; Render(); });
            UIK.Tip(btn, HexText(i));
        }
    }

    string HexText(int i)
    {
        var h = War.D.hexes[i]; var K = War.KindOf(i);
        bool rat = War.Rat(C, i);
        var sp = War.SpecialOf(i);
        var lines = new System.Collections.Generic.List<string>
        {
            $"<b>{Loc.T(h.name)}</b>  {UIK.Dim(Loc.T(K.name))}  {(rat ? UIK.Col(Loc.T("ui.hex.rat"), UIK.RAT) : UIK.Col(Loc.T("ui.hex.cat"), UIK.CAT))}",
            Loc.F("ui.hex.income", K.income)
        };
        if (h.fx != null) lines.Add(UIK.Col(Loc.T(War.D.fx[h.fx]), UIK.GOLD));
        if (sp != null && !C.war.seen.Contains(h.special + ":done")) lines.Add(UIK.Col(Loc.F("ui.hex.special", Loc.T(sp.name)), UIK.GOLD) + UIK.Dim(" " + (sp.kind == "defend" ? Loc.F("ui.hex.special_turn", sp.turn) : Loc.T("ui.hex.special_attack"))));
        if (C.war.forts.Contains(i)) lines.Add(UIK.Col(Loc.F("ui.hex.fort", Mathf.RoundToInt(War.FORT_BONUS * 100)), UIK.RAT));
        if (C.war.threats.Contains(i)) lines.Add(UIK.Col(Loc.T("ui.hex.threat"), UIK.CAT) + "\n" + HoldLine(i));
        else if (War.CanAttack(C, i)) lines.Add(UIK.Dim(Loc.T("ui.hex.attackable")));
        else if (h.fx == "capital" && !rat) lines.Add(UIK.Col(Loc.F("ui.hex.siege", War.Neighbors(i).Count(n => War.Rat(C, n)), War.Neighbors(i).Count()), UIK.CAT)); // 수도: 포위해야 공격
        return string.Join("\n", lines);
    }

    // 지도 아래: 턴 · 땅 · 수입 · 지난 턴 소식 + '이번 턴 쉬기'
    void WarInfo()
    {
        var w = C.war; var info = UIK.F(ops, "WarInfo");
        UIK.T(info, "Text").text = $"<size=26><b>{Loc.F("ui.ops.turn", w.turn)}</b></size>   {UIK.Col(Loc.F("ui.info.rat", War.Count(C, "rat")), UIK.RAT)} · {UIK.Col(Loc.F("ui.info.cat", War.Count(C, "cat")), UIK.CAT)} · {UIK.Col(Loc.F("ui.info.income", War.Income(C)), UIK.GOLD)}\n"
            + (w.news.Count > 0 ? string.Join("\n", w.news.Select(Loc.T)) : UIK.Dim(Loc.T("ui.info.no_news")));
        var wait = UIK.B(info, "Wait");
        wait.interactable = w.over == "" && !BattleSave.Exists;
        UIK.T(wait.transform, "Text").text = Loc.T("ui.info.wait");
        UIK.Tip(wait, Loc.T("ui.info.wait_tip"));
        UIK.OnClick(wait, () => { War.Wait(C); selHex = -1; garrisonMode = false; Toast(Loc.F("ui.info.wait_toast", w.turn)); Render(); });
        // 수비대 배치 모드
        garBtn.interactable = w.over == "" && w.threats.Count > 0 && !BattleSave.Exists;
        UIK.T(garBtn.transform, "Text").text = garrisonMode ? Loc.T("ui.info.garrison_end") : Loc.F("ui.info.garrison", w.posts.Count);
        UIK.Tip(garBtn, Loc.F("ui.info.garrison_tip", Mathf.RoundToInt(War.GARRISON_PER * 100), War.GARRISON_MAX, War.GARRISON_STRESS, Mathf.RoundToInt(War.GARRISON_HURT * 100)));
        UIK.OnClick(garBtn, () => { garrisonMode = !garrisonMode; if (garrisonMode && !War.CanDefend(C, selHex) && w.threats.Count > 0) selHex = w.threats[0]; Render(); });
        // 요새화: 고른 내 칸
        bool canFort = selHex >= 0 && War.CanFortify(C, selHex);
        fortBtn.interactable = canFort && C.supplies >= War.FORT_COST && !BattleSave.Exists;
        UIK.T(fortBtn.transform, "Text").text = Loc.F("ui.info.fort", War.FORT_COST);
        UIK.Tip(fortBtn, Loc.F("ui.info.fort_tip", Mathf.RoundToInt(War.FORT_BONUS * 100), War.FORT_COST)
            + (selHex >= 0 && C.war.forts.Contains(selHex) ? "\n" + UIK.Dim(Loc.T("ui.info.fort_already")) : !canFort ? "\n" + UIK.Dim(Loc.T("ui.info.fort_pick")) : ""));
        UIK.OnClick(fortBtn, () => { int h = selHex; if (h >= 0 && War.Fortify(C, h)) { Toast(Loc.F("ui.info.fort_toast", Loc.T(War.D.hexes[h].name))); Render(); } });
    }

    // 자동 방어 확률 풀이: 기본 + 수비대 + 요새
    string HoldLine(int hex)
    {
        int P(float f) => Mathf.RoundToInt(f * 100);
        var gar = War.Garrison(C, hex).ToList();
        string parts = Loc.F("ui.hold.base", P(War.BaseHold(C, hex))) + (gar.Count > 0 ? " + " + Loc.F("ui.hold.garrison", P(War.GarrisonBonus(C, hex))) : "") + (C.war.forts.Contains(hex) ? " + " + Loc.F("ui.hold.fort", P(War.FORT_BONUS)) : "");
        return UIK.Col(Loc.F("ui.hold.auto", P(War.HoldChance(C, hex))), UIK.GOLD) + UIK.Dim($" ({parts})")
            + (gar.Count > 0 ? "\n" + UIK.Col(Loc.F("ui.hold.list", string.Join(", ", gar.Select(r => r.Name))), UIK.OK) : "");
    }
    void FillMini(Transform s, RatRecord r, int num)
    {
        var slot = (RectTransform)UIK.F(s, "Portrait");
        if (r == null) { Tpl.Clear(slot); UIK.T(s, "Text").text = UIK.Dim(Loc.F("ui.mini.empty", num)); s.GetComponent<Image>().color = new Color(1, 1, 1, 0.45f); return; }
        var st = Camp.Stats(r);
        UIK.Portrait(slot, r);
        int post = War.Posted(C, r.id);
        UIK.T(s, "Text").text = $"<b>{r.Name}</b>\n{UIK.Dim(Loc.T(RANKS[Camp.RankIdx(r)].name) + " · " + Loc.T(st.cls.name))}\n{StressLine(r)}"
            + (!Camp.Fit(r) ? "\n" + UIK.Col(Loc.T("ui.mini.limb"), UIK.NEG) : r.wounded > 0 ? "\n" + UIK.Col(Loc.T("ui.mini.wounded"), UIK.NEG)
               : post >= 0 ? "\n" + UIK.Col(Loc.F("ui.mini.posted", Loc.T(War.D.hexes[post].name)), UIK.OK) : "");
        s.GetComponent<Image>().color = !Camp.Ready(r) ? new Color(0.6f, 0.5f, 0.5f) : post >= 0 ? new Color(0.75f, 0.95f, 0.78f) : r.stress >= STRESS.shaky ? new Color(0.9f, 0.82f, 1f) : Color.white;
        UIK.Tip(s.GetComponent<Button>(), $"<b>{r.Name}</b> — {r.Story}\n{TraitTip(r)}");
    }

    // ── 분대: 카드 목록 + 상세 (사연 · 능력치 · 육각 스킬판) ──
    void Squad()
    {
        var cards = UIK.F(squad, "Cards/Viewport/Content");
        Tpl.Clear(cards);
        if (sel == null || !C.squad.Any(r => r.id == sel)) sel = C.squad.FirstOrDefault()?.id;
        foreach (var r in C.squad)
        {
            var st = Camp.Stats(r);
            var c = Tpl.Spawn("RatCard", cards).transform;
            UIK.Portrait((RectTransform)UIK.F(c, "Portrait"), r);
            int pts = Camp.Points(r);
            UIK.T(c, "Text").text = $"<b>{r.Name}</b>  {UIK.Dim(Loc.T(RANKS[Camp.RankIdx(r)].name) + " · " + Loc.T(st.cls.name))}\n{StressLine(r)}\n{TraitText(r)}" + (pts > 0 ? "\n" + UIK.Col(Loc.F("ui.squad.points", pts), UIK.GOLD) : "");
            c.GetComponent<Image>().sprite = ArtLib.Sprite(r.id == sel ? "out/ui/panel_sq.png" : "out/ui/panel.png");
            c.GetComponent<Image>().color = r.id == sel ? UIK.GOLD : Color.white;
            var id = r.id; UIK.OnClick(c.GetComponent<Button>(), () => { sel = id; Render(); });
        }
        var R = C.squad.FirstOrDefault(q => q.id == sel);
        var d = UIK.F(squad, "Detail");
        UIK.Show(d, R != null);
        if (R != null) Detail(d, R);
    }
    void Detail(Transform d, RatRecord r)
    {
        var st = Camp.Stats(r);
        int ri = Camp.RankIdx(r); var rank = RANKS[ri]; var next = ri + 1 < RANKS.Length ? RANKS[ri + 1] : default;
        UIK.Portrait((RectTransform)UIK.F(d, "Portrait"), r, full: true);
        UIK.T(d, "Info").text = $"<size=30><b>{Loc.T(rank.name)} {r.Name}</b></size>\n{UIK.Col(Loc.T(st.cls.name), UIK.GOLD)} {UIK.Dim("— " + Loc.T(st.cls.desc))}\n"
            + UIK.Dim(Loc.F("ui.detail.sub", r.Home, r.missions, r.kills, r.xp) + (ri + 1 < RANKS.Length ? $" / {next.xp}" : "")) + $"\n<i>“{r.Story}”</i>\n{TraitText(r)}\n{StressLine(r)}  {Status(r)}";
        UIK.Tip(UIK.T(d, "Info"), TraitTip(r));
        var limb = UIK.B(d, "Limb");
        UIK.Show(limb.transform, r.lost.Count > 0);
        limb.interactable = C.supplies >= Camp.LimbCost(C);
        UIK.T(limb.transform, "Text").text = Loc.F("ui.detail.limb", Camp.LimbCost(C));
        UIK.OnClick(limb, () => { if (Camp.FitLimb(C, r)) Render(); });
        UIK.T(d, "Stats").text = string.Join("   ", new[] { ("ui.stat.hp", st.hp.ToString()), ("ui.stat.aim", st.aim.ToString()), ("ui.stat.mob", st.mob.ToString()), ("ui.stat.sight", "+" + st.sight), ("ui.stat.crit", "+" + st.crit), ("ui.stat.def", st.def.ToString()), ("ui.stat.weapon", Loc.T(GameData.WEAPONS[st.cls.weapon].name)) }.Select(p => $"{UIK.Dim(Loc.T(p.Item1))} <b>{p.Item2}</b>"))
            + "\n" + string.Join("  ", st.abil.Select(a => UIK.Col("◆" + Loc.T(GameData.ABILITIES[a].name), UIK.INK)));
        UIK.T(d, "HexHead").text = $"<b>{Loc.T("ui.detail.hex_head")}</b> {UIK.Dim("— " + Loc.F("ui.detail.hex_sub", Camp.Points(r)))}";
        HexBoard((RectTransform)UIK.F(d, "Hex"), r, UIK.T(d, "HexTip"));
    }

    // 육각 벌집판
    void HexBoard(RectTransform box, RatRecord r, Text tipText)
    {
        Tpl.Clear(box);
        float S = 34;
        Vector2 Pos(int q, int rr) => new Vector2(S * 1.5f * q, -S * Mathf.Sqrt(3) * (rr + q / 2f));
        float ext = S * 1.5f * SkillsData.R + S * 2.4f;
        // 갈래 이름
        for (int i = 0; i < 6; i++)
        {
            float a = -Mathf.PI / 2 + i * Mathf.PI / 3;
            var t = Tpl.Spawn<Text>("HexLabel", box);
            t.text = Loc.T(SkillsData.SECTORS[i].name); t.color = SkillsData.SECTORS[i].col;
            t.rectTransform.anchoredPosition = new Vector2(Mathf.Cos(a), -Mathf.Sin(a)) * (ext - 16);
        }
        var cls = Camp.ClassOf(r);
        foreach (var kv in SkillsData.Cells)
        {
            var cell = kv.Value; string key = kv.Key;
            bool owned = cell.ring == 0 || r.nodes.Contains(key);
            bool reach = !owned && SkillsData.CanTake(r.nodes, key);
            bool can = reach && Camp.Points(r) > 0;
            var g = Tpl.Spawn("HexCell", box).transform;
            var rt = (RectTransform)g; rt.anchoredPosition = Pos(cell.q, cell.r); rt.sizeDelta = new Vector2(S * 2, S * 2);
            var hex = g.GetComponent<UIHex>();
            Color col = cell.sec.Length > 0 ? SkillsData.SECTORS[cell.sec[0]].col : UIK.INK;
            // 찍음 = 갈래 색 꽉 채움 + 흰 굵은 테두리 + ✓ / 찍을 수 있음 = 어두운 갈래 색 + 금색 깜빡 테두리 / 못 찍음 = 거의 검정
            hex.color = owned ? col : can ? Color.Lerp(new Color(0.12f, 0.1f, 0.08f, 0.95f), col, 0.22f) : new Color(0.1f, 0.09f, 0.08f, 0.85f);
            hex.edge = owned ? new Color(1f, 0.97f, 0.88f, 1f) : can ? UIK.GOLD : reach ? new Color(UIK.GOLD.r, UIK.GOLD.g, UIK.GOLD.b, 0.35f) : new Color(col.r, col.g, col.b, 0.18f);
            hex.width = owned ? 5 : can ? 4 : 2;
            hex.pulse = can;
            hex.badge = owned && cell.ring > 0 ? UIK.OK : Color.clear;
            hex.SetVerticesDirty();
            string iconKey = cell.ring == 0 ? null : cell.fx.abil ?? cell.fx.perk ?? cell.fx.n.Keys.FirstOrDefault(k => ArtLib.Icon(k) != null);
            var icon = UIK.I(g, "Icon");
            icon.sprite = iconKey != null ? ArtLib.Icon(iconKey) : null;
            icon.enabled = icon.sprite != null;
            icon.color = owned ? Color.white : can ? new Color(1, 1, 1, 0.8f) : new Color(0.5f, 0.5f, 0.5f, 0.25f);
            UIK.T(g, "Label").text = cell.ring == 0 ? Loc.T(SkillsData.RAT_CLASSES[cls].name) : "";
            string sec = string.Join(" + ", cell.sec.Select(s => Loc.T(SkillsData.SECTORS[s].name)));
            string desc = cell.ring == 0 ? Loc.F("ui.board.cur_class", Loc.T(SkillsData.RAT_CLASSES[cls].name)) : $"{sec} — {Loc.T(SkillsData.FxText(cell.fx))}";
            string head = cell.ring == 0 ? $"<b>{Loc.T("ui.board.center")}</b>" : $"<b>{(cell.fx.name != null ? Loc.T(cell.fx.name) : sec)}</b>  {UIK.Dim(Loc.F("ui.board.ring", sec, cell.ring))}";
            string body = cell.fx.abil != null ? Hud.AbilityTip(cell.fx.abil) : desc;
            var src = g.GetComponent<TipSource>() ?? g.gameObject.AddComponent<TipSource>();
            src.text = (cell.fx.abil != null ? "" : head + "\n") + body + "\n" + (owned ? UIK.Col(Loc.T("ui.board.owned"), UIK.OK) : can ? UIK.Col(Loc.T("ui.board.click"), UIK.GOLD) : reach ? UIK.Dim(Loc.T("ui.board.no_points")) : UIK.Dim(Loc.T("ui.board.locked")));
            src.onEnter = () => tipText.text = desc + (owned ? " " + UIK.Col(Loc.T("ui.board.owned"), UIK.OK) : can ? " " + UIK.Col($"({Loc.T("ui.board.click")})", UIK.GOLD) : reach ? UIK.Dim($" ({Loc.T("ui.board.no_points_short")})") : UIK.Dim($" ({Loc.T("ui.board.locked_short")})"));
            var btn = g.GetComponent<Button>();
            btn.interactable = can;
            UIK.OnClick(btn, () =>
            {
                var before = Camp.ClassOf(r);
                if (Camp.TakeNode(C, r, key))
                {
                    var after = Camp.ClassOf(r);
                    if (after != before) Toast(Loc.F("ui.board.class_set", r.Name, Loc.T(SkillsData.RAT_CLASSES[after].name)));
                    Render();
                }
            });
        }
        tipText.text = UIK.Col(Loc.T("ui.board.legend.owned"), UIK.OK) + "  " + UIK.Col(Loc.T("ui.board.legend.can"), UIK.GOLD) + "  " + UIK.Dim(Loc.T("ui.board.legend.locked"));
    }

    // ── 모집 ──
    void Recruit()
    {
        int tier = War.Threat(C);
        UIK.T(recruit, "Head").text = Loc.F("ui.recruit.head", SQUAD_MAX, C.squad.Count) + "\n"
            + UIK.Dim(Loc.F("ui.recruit.cost", RECRUIT_COST, tier, (tier - 1) * RECRUIT_PER_TIER, C.recruited, C.recruited * RECRUIT_PER_HIRE, RECRUIT_PER_RANK))
            + (tier > 1 ? "\n" + UIK.Col(Loc.F("ui.recruit.vet", Mathf.RoundToInt((tier - 1) * VET_PER_TIER * 100)), UIK.GOLD) : "");
        var refresh = UIK.B(recruit, "Refresh");
        int rc = Camp.RefreshCost(C);
        refresh.interactable = C.supplies >= rc;
        UIK.T(refresh.transform, "Text").text = Loc.F("ui.recruit.refresh", rc);
        UIK.OnClick(refresh, () => { if (Camp.Refresh(C)) Render(); });
        var box = UIK.F(recruit, "Cands");
        Tpl.Clear(box);
        for (int i = 0; i < C.recruits.Count; i++)
        {
            var r = C.recruits[i]; int idx = i;
            var c = Tpl.Spawn("CandCard", box).transform;
            // 이번 턴에 뽑은 칸: 다음 턴까지 잠김
            if (Camp.Locked(C, i))
            {
                UIK.F(c, "Portrait").gameObject.SetActive(false);
                UIK.T(c, "Text").text = UIK.Dim(Loc.T("ui.recruit.locked"));
                var lb = UIK.B(c, "Btn"); lb.interactable = false;
                UIK.T(lb.transform, "Text").text = Loc.T("ui.recruit.locked_btn");
                continue;
            }
            UIK.Portrait((RectTransform)UIK.F(c, "Portrait"), r, full: true);
            int rk = Camp.RankIdx(r), cost = Camp.RecruitCost(C, r);
            UIK.T(c, "Text").text = $"<size=28><b>{(rk > 0 ? UIK.Col(Loc.T(RANKS[rk].name) + " ", UIK.GOLD) : "")}{r.Name}</b></size>\n{UIK.Dim(Loc.F("ui.common.from", r.Home))}{(rk > 0 ? UIK.Col(" · " + Loc.F("ui.recruit.vet_tag", Camp.Points(r)), UIK.GOLD) : "")}\n<i>“{r.Story}”</i>\n{TraitText(r)}\n{UIK.Dim(string.Join(" / ", r.traits.Select(t => Loc.T(TRAITS[t].desc))))}";
            var b = UIK.B(c, "Btn");
            b.interactable = C.supplies >= cost && C.squad.Count < SQUAD_MAX;
            UIK.T(b.transform, "Text").text = Loc.F("ui.recruit.btn", cost);
            UIK.OnClick(b, () => { if (Camp.Recruit(C, idx)) { Toast(Loc.F("ui.recruit.joined", r.Name)); Render(); } });
        }
    }

    // ── 추모비 ──
    void Memorial()
    {
        var box = UIK.F(memorial, "Stones/Viewport/Content");
        Tpl.Clear(box);
        UIK.T(memorial, "Head").text = C.fallen.Count == 0 ? UIK.Dim(Loc.T("ui.memorial.empty")) : $"<size=34><b>{Loc.T("ui.memorial.head")}</b></size>";
        foreach (var f in Enumerable.Reverse(C.fallen))
        {
            var s = Tpl.Spawn("Stone", box).transform;
            UIK.Portrait((RectTransform)UIK.F(s, "Portrait"), f, dead: true);
            UIK.T(s, "Text").text = $"<b>{Loc.T(f.rank)} {f.Name}</b>  {UIK.Dim(Loc.T(f.cls) + " · " + Loc.F("ui.common.from", f.Home))}\n{UIK.Dim(Loc.F("ui.memorial.line", Loc.T(f.died), Loc.T(f.how), f.missions + 1, f.kills))}\n<i>“{f.Story}”</i>";
        }
    }
}

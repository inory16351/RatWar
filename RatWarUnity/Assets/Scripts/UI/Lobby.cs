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
        UIK.OnClick(UIK.B(t, "Header/Save"), () => { Camp.Save(C); Toast(BattleSave.Exists ? "저장했습니다 — 진행 중인 전투는 타이틀의 '이어하기'로" : "저장했습니다"); });
        // 난이도: 저장 버튼을 복제해 옆에 둠. 표시만 (새 전쟁 때 정하고 전쟁 동안 고정)
        var save = UIK.F(t, "Header/Save") as RectTransform;
        diffBtn = Instantiate(save.gameObject, save.parent).GetComponent<Button>();
        diffBtn.name = "Diff";
        var drt = (RectTransform)diffBtn.transform;
        drt.SetSiblingIndex(save.GetSiblingIndex());
        if (!save.parent.GetComponent<LayoutGroup>()) drt.anchoredPosition = save.anchoredPosition - new Vector2(save.rect.width + 12, 0);
        UIK.OnClick(diffBtn, () => Toast($"난이도 {RULES.DiffOf(C.diff).name} — 전쟁 중엔 바꿀 수 없음 ('처음부터'로 새 전쟁)"));
    }
    Button diffBtn;

    float resetArm;
    void ConfirmReset()
    {
        // 두 번 누르면 지움 (웹은 confirm 창)
        if (Time.unscaledTime - resetArm > 2.5f) { resetArm = Time.unscaledTime; Toast("한 번 더 누르면 새 전쟁 — 난이도를 고르면 지금 분대 기록은 지워집니다"); return; }
        resetArm = 0;
        // 난이도를 고르고 '전쟁 시작'을 눌러야 실제로 지움 (뒤로 = 취소)
        DiffPicker.Open(GetComponentInParent<Canvas>().rootCanvas.transform, C.diff, d =>
        {
            Camp.Reset(); Camp.Save(Camp.Fresh(d)); sel = null; selHex = -1;
            Toast($"새 분대 · 새 전쟁 ({RULES.DiffOf(d).name})"); Render();
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
        stats.text = $"보급품 <b>{C.supplies}</b>    전투 {C.battles}    분대 {C.squad.Count}/{SQUAD_MAX}";
        var D = RULES.DIFFS[Mathf.Clamp(C.diff, 0, RULES.DIFFS.Length - 1)];
        diffBtn.GetComponentInChildren<Text>(true).text = "난이도: " + D.name;
        UIK.Tip(diffBtn, $"<b>난이도 {D.name}</b>\n{D.desc}\n위협 단계 {D.threatStep:0.#}턴마다 · 수입 ×{D.income:0.##}\n전쟁 시작 때 정함 · 바꿀 수 없음");
        foreach (var n in new[] { "ops", "squad", "recruit", "memorial" })
            UIK.B(transform, "Header/Tabs/" + n).GetComponent<Image>().sprite = ArtLib.Sprite(tab == n ? "out/ui/btn_hi.png" : "out/ui/btn.png");
        UIK.Show(ops, tab == "ops"); UIK.Show(squad, tab == "squad"); UIK.Show(recruit, tab == "recruit"); UIK.Show(memorial, tab == "memorial");
        Canvas.ForceUpdateCanvases();
        if (tab == "ops") Ops(); else if (tab == "squad") Squad(); else if (tab == "recruit") Recruit(); else Memorial();
    }

    static string StressLine(RatRecord r)
    {
        var ss = StressState(r.stress, Camp.BreakAt(r));
        return $"스트레스 {UIK.Col($"{r.stress} · {ss.t}", ss.c)}";
    }
    static string TraitText(RatRecord r) => string.Join(" ", r.traits.Select(t => UIK.Col("[" + TRAITS[t].name + "]", TRAITS[t].rare ? UIK.PSY : UIK.GOLD)));
    // 관계 특성은 상대 이름도 (전우 · 멘토 · 제자 · 앙숙)
    static readonly System.Collections.Generic.Dictionary<string, string> BOND_OF = new System.Collections.Generic.Dictionary<string, string> { ["comrade"] = "comrade", ["mentor"] = "pupil", ["pupil"] = "mentor", ["feud"] = "feud" };
    string Partners(RatRecord r, string t)
    {
        if (!BOND_OF.TryGetValue(t, out var kind)) return "";
        var names = Camp.Partners(r, kind).Select(id => C.squad.Concat(C.fallen).FirstOrDefault(q => q.id == id)?.name).Where(n => n != null).ToList();
        return names.Count > 0 ? UIK.Dim($" ({string.Join(", ", names)})") : "";
    }
    string TraitTip(RatRecord r) => string.Join("\n", r.traits.Select(t => $"<b>{TRAITS[t].name}</b>{Partners(r, t)} — {TRAITS[t].desc}"));
    static string Status(RatRecord r)
    {
        var s = new System.Collections.Generic.List<string>();
        if (r.lost.Count > 0) s.Add(UIK.Col(string.Join(" · ", r.lost.Select(p => p == "arm" ? "팔" : "다리")) + " 잃음", UIK.NEG));
        if (!string.IsNullOrEmpty(r.afflicted)) s.Add(UIK.Col("정신 이상: " + r.afflicted, UIK.PSY));
        if (r.stress >= STRESS.shaky) s.Add(UIK.Col("불안 (명중 −10)", UIK.PSY));
        if (!string.IsNullOrEmpty(r.grief)) s.Add(UIK.Dim(r.grief + "를 잃었다"));
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
        if (!w.seen.Contains("prologue")) { w.seen.Add("prologue"); Camp.Save(C); Cutscene.I.Play("라테리아 공화국 · 개전", War.D.prologue, Render); }
        else if (w.over != "" && !w.seen.Contains("ending")) { w.seen.Add("ending"); Camp.Save(C); Cutscene.I.Play(w.over == "win" ? "승리" : "패배", w.over == "win" ? War.D.victory : War.D.defeat, Render); }
        if (selHex < 0 || ActionOf(selHex) == null) selHex = w.threats.Count > 0 ? w.threats[0] : selHex;
        WarMap();
        WarInfo();
        var act = ActionOf(selHex);
        var m = act != null ? War.Mission(C, selHex, act) : null;
        var br = UIK.F(ops, "Brief");
        if (m == null)
        {
            string why = w.over == "win" ? UIK.Col("전쟁에서 이겼다. 머리 위 '처음부터'로 새 전쟁을 시작할 수 있다.", UIK.GOLD)
                : w.over == "lose" ? UIK.Col("전쟁에서 졌다. 머리 위 '처음부터'로 다시 시작.", UIK.NEG)
                : selHex >= 0 ? HexText(selHex) + "\n" + UIK.Dim("싸울 수 있는 칸: 침공당한 내 땅(빨간 테두리) · 내 땅에 붙은 고양이 땅")
                : UIK.Dim("지도에서 칸을 고르세요 — 침공당한 내 땅을 막거나, 붙어 있는 고양이 땅을 공격합니다. 한 턴에 한 번만 싸울 수 있습니다.");
            UIK.T(br, "Head").text = $"<size=30><b>{w.turn}턴</b></size>\n{why}";
        }
        else
        {
            string obj = m.obj == "elim" ? "고양이 전멸" : m.obj == "officer" ? "표적 장교 사살" : "깃발 칸에 쥐 한 마리 도달";
            bool sp = War.IsSpecial(C, selHex, act);
            UIK.T(br, "Head").text = $"{(sp ? UIK.Col("◆ 주요 전투  ", UIK.GOLD) : "")}<size=30><b>{m.name}</b></size>    {UIK.Col($"보급품 +{m.reward}", UIK.GOLD)}\n{m.brief}\n{UIK.Col("목표", UIK.GOLD)} {obj} · {UIK.Col("적", UIK.GOLD)} 고양이 {m.cats.Length}{(m.shells > 0 ? $" · {UIK.Col("박격포탄", UIK.GOLD)} {m.shells}" : "")}"
                + "\n" + UIK.Dim(act == "attack" ? "이기면 이 땅을 얻음 · 출격하지 않은 침공은 수비대가 알아서 막음(운)" : "지면 이 땅을 잃음 · 나머지 침공은 수비대가 알아서 막음(운)");
        }
        UIK.T(br, "DeployH").text = $"{UIK.Col("▶ 출전", UIK.OK)} {C.deploy.Count} / {DEPLOY_MAX}   {UIK.Dim("누르면 대기로")}";
        UIK.T(br, "BenchH").text = $"{UIK.Col("■ 대기", UIK.RAT)} {C.squad.Count - C.deploy.Count}   {UIK.Dim("쉬는 쥐는 다음 판 뒤 스트레스 −35 · 누르면 출전")}";
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
            UIK.OnClick(s.GetComponent<Button>(), () => { if (!Camp.Ready(rr) || C.deploy.Count >= DEPLOY_MAX) return; C.deploy.Add(rr.id); Camp.Save(C); Render(); });
        }
        var shaky = C.deploy.Select(id => C.squad.FirstOrDefault(q => q.id == id)).Where(r => r != null && r.stress >= STRESS.shaky).ToList();
        UIK.T(br, "Go/Warn").text = m == null ? UIK.Dim("싸울 칸을 고르면 출격할 수 있습니다") : C.deploy.Count == 0 ? "출전할 쥐를 고르세요" : shaky.Count > 0 ? UIK.Col($"스트레스 높음: {string.Join(", ", shaky.Select(r => r.name))} — 쉬게 하는 것도 방법", UIK.PSY) : UIK.Dim("쉬는 쥐는 스트레스가 풀립니다");
        var go = UIK.B(br, "Go/Btn");
        go.interactable = m != null && C.deploy.Count > 0;
        int hex = selHex;
        UIK.OnClick(go, () =>
        {
            War.Begin(C, hex, act);
            var deploy = C.deploy.ToList();
            void Launch() => GameRoot.I.Deploy(m, deploy);
            // 주요 전투는 처음 한 번 컷씬
            string sid = War.SpecialId(hex);
            if (War.IsSpecial(C, hex, act) && !C.war.seen.Contains(sid)) { C.war.seen.Add(sid); Camp.Save(C); Cutscene.I.Play(m.name, War.D.specials[sid].slides, Launch); }
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
            UIK.T(g, "Label").text = (sp ? UIK.Col("◆", UIK.GOLD) : "") + h.name + (threat ? "\n" + UIK.Col("침공!", UIK.CAT) : "");
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
            $"<b>{h.name}</b>  {UIK.Dim(K.name)}  {(rat ? UIK.Col("공화국 땅", UIK.RAT) : UIK.Col("제국 땅", UIK.CAT))}",
            $"수입 보급품 {K.income}/턴"
        };
        if (h.fx != null) lines.Add(UIK.Col(War.D.fx[h.fx], UIK.GOLD));
        if (sp != null && !C.war.seen.Contains(h.special + ":done")) lines.Add(UIK.Col($"◆ 주요 전투: {sp.name}", UIK.GOLD) + UIK.Dim(sp.kind == "defend" ? $" ({sp.turn}턴부터 고양이가 노림)" : " (공격하면)"));
        if (C.war.threats.Contains(i)) lines.Add(UIK.Col("고양이가 침공 중! 막지 않으면 수비대가 운에 맡김", UIK.CAT));
        else if (War.CanAttack(C, i)) lines.Add(UIK.Dim("우리 땅과 붙어 있음 — 공격 가능"));
        return string.Join("\n", lines);
    }

    // 지도 아래: 턴 · 땅 · 수입 · 지난 턴 소식 + '이번 턴 쉬기'
    void WarInfo()
    {
        var w = C.war; var info = UIK.F(ops, "WarInfo");
        UIK.T(info, "Text").text = $"<size=26><b>{w.turn}턴</b></size>   {UIK.Col($"공화국 {War.Count(C, "rat")}칸", UIK.RAT)} · {UIK.Col($"제국 {War.Count(C, "cat")}칸", UIK.CAT)} · {UIK.Col($"수입 +{War.Income(C)}/턴", UIK.GOLD)}\n"
            + (w.news.Count > 0 ? string.Join("\n", w.news) : UIK.Dim("아직 소식이 없다"));
        var wait = UIK.B(info, "Wait");
        wait.interactable = w.over == "";
        UIK.T(wait.transform, "Text").text = "이번 턴 쉬기";
        UIK.Tip(wait, "출격하지 않고 턴을 넘김 · 분대 전원 스트레스 −35\n침공은 모두 수비대가 운에 맡겨 막음");
        UIK.OnClick(wait, () => { War.Wait(C); selHex = -1; Toast($"{w.turn}턴 — 고양이가 움직였다"); Render(); });
    }
    void FillMini(Transform s, RatRecord r, int num)
    {
        var slot = (RectTransform)UIK.F(s, "Portrait");
        if (r == null) { Tpl.Clear(slot); UIK.T(s, "Text").text = UIK.Dim($"{num}\n빈자리"); s.GetComponent<Image>().color = new Color(1, 1, 1, 0.45f); return; }
        var st = Camp.Stats(r);
        UIK.Portrait(slot, r);
        UIK.T(s, "Text").text = $"<b>{r.name}</b>\n{UIK.Dim(RANKS[Camp.RankIdx(r)].name + " · " + st.cls.name)}\n{StressLine(r)}" + (Camp.Ready(r) ? "" : "\n" + UIK.Col("의수 · 의족 필요", UIK.NEG));
        s.GetComponent<Image>().color = !Camp.Ready(r) ? new Color(0.6f, 0.5f, 0.5f) : r.stress >= STRESS.shaky ? new Color(0.9f, 0.82f, 1f) : Color.white;
        UIK.Tip(s.GetComponent<Button>(), $"<b>{r.name}</b> — {r.story}\n{TraitTip(r)}");
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
            UIK.T(c, "Text").text = $"<b>{r.name}</b>  {UIK.Dim(RANKS[Camp.RankIdx(r)].name + " · " + st.cls.name)}\n{StressLine(r)}\n{TraitText(r)}" + (pts > 0 ? "\n" + UIK.Col($"스킬 {pts}점", UIK.GOLD) : "");
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
        UIK.T(d, "Info").text = $"<size=30><b>{rank.name} {r.name}</b></size>\n{UIK.Col(st.cls.name, UIK.GOLD)} {UIK.Dim("— " + st.cls.desc)}\n"
            + UIK.Dim($"{r.home} 출신 · 출전 {r.missions} · 처치 {r.kills} · 경험 {r.xp}{(ri + 1 < RANKS.Length ? $" / {next.xp}" : "")}") + $"\n<i>“{r.story}”</i>\n{TraitText(r)}\n{StressLine(r)}  {Status(r)}";
        UIK.Tip(UIK.T(d, "Info"), TraitTip(r));
        var limb = UIK.B(d, "Limb");
        UIK.Show(limb.transform, r.lost.Count > 0);
        limb.interactable = C.supplies >= Camp.LimbCost(C);
        UIK.T(limb.transform, "Text").text = $"의수 · 의족 달기 (보급품 {Camp.LimbCost(C)})";
        UIK.OnClick(limb, () => { if (Camp.FitLimb(C, r)) Render(); });
        UIK.T(d, "Stats").text = string.Join("   ", new[] { ("체력", st.hp.ToString()), ("명중", st.aim.ToString()), ("이동", st.mob.ToString()), ("시야", "+" + st.sight), ("치명", "+" + st.crit), ("회피", st.def.ToString()), ("무기", GameData.WEAPONS[st.cls.weapon].name) }.Select(p => $"{UIK.Dim(p.Item1)} <b>{p.Item2}</b>"))
            + "\n" + string.Join("  ", st.abil.Select(a => UIK.Col("◆" + GameData.ABILITIES[a].name, UIK.INK)));
        UIK.T(d, "HexHead").text = $"<b>스킬 벌집판</b> {UIK.Dim($"— 남은 점수 {Camp.Points(r)} · 많이 찍은 갈래가 병종이 됨")}{(r.traits.Contains("psychic") ? UIK.Col(" · 영매: 지원+정찰 = 주술사", UIK.PSY) : "")}";
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
            t.text = SkillsData.SECTORS[i].name; t.color = SkillsData.SECTORS[i].col;
            t.rectTransform.anchoredPosition = new Vector2(Mathf.Cos(a), -Mathf.Sin(a)) * (ext - 16);
        }
        var cls = Camp.ClassOf(r);
        foreach (var kv in SkillsData.Cells)
        {
            var cell = kv.Value; string key = kv.Key;
            bool owned = cell.ring == 0 || r.nodes.Contains(key);
            bool can = Camp.Points(r) > 0 && SkillsData.CanTake(r.nodes, key);
            var g = Tpl.Spawn("HexCell", box).transform;
            var rt = (RectTransform)g; rt.anchoredPosition = Pos(cell.q, cell.r); rt.sizeDelta = new Vector2(S * 2, S * 2);
            var hex = g.GetComponent<UIHex>();
            Color col = cell.sec.Length > 0 ? SkillsData.SECTORS[cell.sec[0]].col : UIK.INK;
            hex.color = owned ? col : can ? new Color(col.r * 0.6f, col.g * 0.6f, col.b * 0.6f, 0.95f) : new Color(0.16f, 0.14f, 0.12f, 0.92f);
            hex.edge = can ? UIK.GOLD : owned ? new Color(0.1f, 0.08f, 0.06f, 0.9f) : new Color(col.r, col.g, col.b, 0.35f);
            hex.SetVerticesDirty();
            string iconKey = cell.ring == 0 ? null : cell.fx.abil ?? cell.fx.perk ?? cell.fx.n.Keys.FirstOrDefault(k => ArtLib.Icon(k) != null);
            var icon = UIK.I(g, "Icon");
            icon.sprite = iconKey != null ? ArtLib.Icon(iconKey) : null;
            icon.enabled = icon.sprite != null;
            icon.color = owned ? Color.white : can ? new Color(1, 1, 1, 0.85f) : new Color(1, 1, 1, 0.3f);
            UIK.T(g, "Label").text = cell.ring == 0 ? SkillsData.RAT_CLASSES[cls].name : "";
            string sec = string.Join(" + ", cell.sec.Select(s => SkillsData.SECTORS[s].name));
            string desc = cell.ring == 0 ? $"현재 병종: {SkillsData.RAT_CLASSES[cls].name}" : $"{sec} — {SkillsData.FxText(cell.fx)}";
            string head = cell.ring == 0 ? "<b>신병</b>" : $"<b>{cell.fx.name ?? sec}</b>  {UIK.Dim($"{sec} 갈래 · {cell.ring}번째 고리")}";
            string body = cell.fx.abil != null ? Hud.AbilityTip(cell.fx.abil) : desc;
            var src = g.GetComponent<TipSource>() ?? g.gameObject.AddComponent<TipSource>();
            src.text = (cell.fx.abil != null ? "" : head + "\n") + body + "\n" + (owned ? UIK.Col("✓ 찍음", UIK.OK) : can ? UIK.Col("클릭해서 찍기", UIK.GOLD) : UIK.Dim("이웃한 칸을 먼저 찍어야 함"));
            src.onEnter = () => tipText.text = desc + (can ? " (클릭해서 찍기)" : owned ? " ✓" : "");
            var btn = g.GetComponent<Button>();
            btn.interactable = can;
            UIK.OnClick(btn, () =>
            {
                var before = Camp.ClassOf(r);
                if (Camp.TakeNode(C, r, key))
                {
                    var after = Camp.ClassOf(r);
                    if (after != before) Toast($"{r.name} — {SkillsData.RAT_CLASSES[after].name}(으)로 길이 정해졌다!");
                    Render();
                }
            });
        }
        tipText.text = UIK.Dim("칸에 마우스를 올리면 효과");
    }

    // ── 모집 ──
    void Recruit()
    {
        UIK.T(recruit, "Head").text = $"보급품 {RECRUIT_COST}로 지원자를 받습니다. 분대 최대 {SQUAD_MAX}마리. 지금 {C.squad.Count}마리.";
        var refresh = UIK.B(recruit, "Refresh");
        refresh.interactable = C.supplies >= REFRESH_COST;
        UIK.T(refresh.transform, "Text").text = $"다른 지원자 (보급품 {REFRESH_COST})";
        UIK.OnClick(refresh, () => { if (Camp.Refresh(C)) Render(); });
        var box = UIK.F(recruit, "Cands");
        Tpl.Clear(box);
        for (int i = 0; i < C.recruits.Count; i++)
        {
            var r = C.recruits[i]; int idx = i;
            var c = Tpl.Spawn("CandCard", box).transform;
            UIK.Portrait((RectTransform)UIK.F(c, "Portrait"), r, full: true);
            UIK.T(c, "Text").text = $"<size=28><b>{r.name}</b></size>\n{UIK.Dim(r.home + " 출신")}\n<i>“{r.story}”</i>\n{TraitText(r)}\n{UIK.Dim(string.Join(" / ", r.traits.Select(t => TRAITS[t].desc)))}";
            var b = UIK.B(c, "Btn");
            b.interactable = C.supplies >= RECRUIT_COST && C.squad.Count < SQUAD_MAX;
            UIK.T(b.transform, "Text").text = $"모집 (보급품 {RECRUIT_COST})";
            UIK.OnClick(b, () => { if (Camp.Recruit(C, idx)) { Toast($"{r.name} 입대"); Render(); } });
        }
    }

    // ── 추모비 ──
    void Memorial()
    {
        var box = UIK.F(memorial, "Stones/Viewport/Content");
        Tpl.Clear(box);
        UIK.T(memorial, "Head").text = C.fallen.Count == 0 ? UIK.Dim("아직 이름이 새겨지지 않았다.") : "<size=34><b>추모비</b></size>";
        foreach (var f in Enumerable.Reverse(C.fallen))
        {
            var s = Tpl.Spawn("Stone", box).transform;
            UIK.Portrait((RectTransform)UIK.F(s, "Portrait"), f, dead: true);
            UIK.T(s, "Text").text = $"<b>{f.rank} {f.name}</b>  {UIK.Dim(f.cls + " · " + f.home + " 출신")}\n{UIK.Dim($"{f.died} — {f.how} · 출전 {f.missions + 1} · 처치 {f.kills}")}\n<i>“{f.story}”</i>";
        }
    }
}

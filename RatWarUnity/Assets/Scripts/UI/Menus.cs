// 메뉴 창들 (하이라키 Canvas/Slots · Settings · Pause · Confirm, 'RatWar/메뉴 UI 만들기' 로 만든 것) 연결
//   Confirm  = 예 / 아니오 묻기
//   Slots    = 저장 슬롯 3개 (새로 하기 · 불러오기)
//   Settings = 음량 3개 · 언어
//   Pause    = 전투 일시정지 (계속 · 환경설정 · 저장하고 메인메뉴 · 저장하고 종료)
// Esc 는 GameRoot.LateUpdate → Menus.Esc(): 열린 창 닫기 → 사령부면 메인메뉴 묻기 → 전투면 일시정지
using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public static class Menus
{
    static Transform canvas;
    static Transform W(string name)
    {
        if (!canvas) canvas = GameObject.Find("Canvas").transform;
        return canvas.Find(name);
    }
    static bool Open(string name) { var t = W(name); return t && t.gameObject.activeSelf; }
    // 켤 때 맨 위로 (툴팁만 그 위)
    public static void Show(string name, bool on)
    {
        var t = W(name); if (!t) return;
        t.gameObject.SetActive(on);
        if (on) { t.SetAsLastSibling(); var tip = W("Tooltip"); if (tip) tip.SetAsLastSibling(); }
    }
    static Button B(string win, string path) => UIK.B(W(win), path);
    static Text T(string win, string path) => UIK.T(W(win), path);

    // 열린 메뉴 창이 있나 (전투 입력 막기)
    public static bool Blocking => new[] { "Slots", "Settings", "Pause", "Confirm", "DiffPicker" }.Any(Open);
    public static int escUsed = -1; // 이 프레임 Esc 를 다른 곳(컷씬 · 조준 취소)이 썼음

    // ── Esc ──
    public static void Esc()
    {
        if (escUsed == Time.frameCount || Cutscene.Playing) return;
        if (Open("Confirm")) { Show("Confirm", false); return; }
        if (Open("Settings")) { Show("Settings", false); return; }
        if (Open("DiffPicker")) { DiffPicker.Back(); return; }
        if (Open("Slots")) { Show("Slots", false); return; }
        if (Open("Pause")) { ClosePause(); return; }
        var mode = GameRoot.I.Mode;
        if (mode == "lobby") Ask("메인메뉴로 나갈까요?\n<size=20>사령부 기록은 자동 저장되어 있습니다</size>", () => GameRoot.I.ToTitle(), "나가기", "취소");
        else if (mode == "battle" && Battle.over == null) OpenPause();
    }

    // ── 확인 창 ──
    public static void Ask(string msg, Action yes, string yesLabel = "예", string noLabel = "아니오")
    {
        T("Confirm", "Box/Message").text = msg;
        B("Confirm", "Box/Yes").GetComponentInChildren<Text>().text = yesLabel;
        B("Confirm", "Box/No").GetComponentInChildren<Text>().text = noLabel;
        UIK.OnClick(B("Confirm", "Box/Yes"), () => { Show("Confirm", false); yes(); });
        UIK.OnClick(B("Confirm", "Box/No"), () => Show("Confirm", false));
        Show("Confirm", true);
    }

    // ── 저장 슬롯 ── isNew = 새로 하기 (빈 칸 · 덮어쓰기), 아니면 불러오기
    public static void OpenSlots(bool isNew, Action<int> enter)
    {
        T("Slots", "Box/Title").text = isNew ? "새로 하기 — 슬롯 고르기" : "불러오기";
        UIK.OnClick(B("Slots", "Box/Back"), () => Show("Slots", false));
        for (int i = 0; i < SaveSlots.COUNT; i++)
        {
            int n = i; var card = W("Slots").Find($"Box/Slot{i}");
            var info = SaveSlots.Read(i);
            UIK.T(card, "Info").text = SaveSlots.Summary(info);
            UIK.T(card, "When").text = info != null ? $"마지막 저장 {info.when:yyyy-MM-dd HH:mm}" + (SaveSlots.Last() == i ? "  · 최근" : "") : "";
            var main = UIK.B(card, "Main");
            main.GetComponentInChildren<Text>().text = isNew ? (info != null ? "덮어쓰고 시작" : "여기서 시작") : "불러오기";
            main.interactable = isNew || info != null;
            UIK.OnClick(main, () =>
            {
                if (!isNew) { Show("Slots", false); enter(n); return; }
                Action pick = () => DiffPicker.Open(RULES.defaultDiff, d =>
                {
                    SaveSlots.Delete(n); SaveSlots.Cur = n; Camp.Save(Camp.Fresh(d));
                    Show("Slots", false); enter(n);
                }, null);
                if (info != null) Ask($"슬롯 {n + 1} 의 기록을 지우고 새 전쟁을 시작할까요?\n<size=20>{SaveSlots.Summary(info)}</size>", pick, "덮어쓰기", "취소");
                else pick();
            });
            var del = UIK.B(card, "Delete");
            UIK.Show(del.transform, info != null);
            UIK.OnClick(del, () => Ask($"슬롯 {n + 1} 을(를) 지울까요? 되돌릴 수 없습니다.\n<size=20>{SaveSlots.Summary(info)}</size>", () => { SaveSlots.Delete(n); OpenSlots(isNew, enter); }, "삭제", "취소"));
        }
        Show("Slots", true);
    }

    // ── 환경설정 ──
    public static void OpenSettings()
    {
        Bind("Master", () => Settings.Master, v => Settings.Master = v);
        Bind("Music", () => Settings.MusicVol, v => Settings.MusicVol = v);
        Bind("Sfx", () => Settings.SfxVol, v => Settings.SfxVol = v);
        Action lang = () =>
        {
            T("Settings", "Box/Lang/Value").text = Settings.LANGS[Settings.Lang];
            T("Settings", "Box/Note").text = Settings.Lang == 0 ? "" : "아직 한국어만 있습니다 — 설정만 저장됨";
        };
        UIK.OnClick(B("Settings", "Box/Lang/Prev"), () => { Settings.Lang--; lang(); });
        UIK.OnClick(B("Settings", "Box/Lang/Next"), () => { Settings.Lang++; lang(); });
        lang();
        UIK.OnClick(B("Settings", "Box/Close"), () => { Settings.Save(); Show("Settings", false); });
        Show("Settings", true);
    }
    static void Bind(string row, Func<float> get, Action<float> set)
    {
        var r = W("Settings").Find("Box/" + row);
        var s = r.Find("Slider").GetComponent<Slider>(); var val = UIK.T(r, "Value");
        s.onValueChanged.RemoveAllListeners();
        s.minValue = 0; s.maxValue = 1; s.value = get();
        val.text = Mathf.RoundToInt(s.value * 100).ToString();
        s.onValueChanged.AddListener(v => { set(v); val.text = Mathf.RoundToInt(v * 100).ToString(); });
    }

    // ── 전투 일시정지 ── 저장은 아군 차례에만 되므로, 안 될 땐 나가기 버튼을 막고 이유를 씀
    public static void OpenPause()
    {
        bool can = BattleSave.CanSave;
        UIK.OnClick(B("Pause", "Box/Resume"), ClosePause);
        UIK.OnClick(B("Pause", "Box/Settings"), OpenSettings);
        UIK.OnClick(B("Pause", "Box/ToMenu"), () => { if (BattleSave.Save()) { ClosePause(); GameRoot.I.ToTitle(); } });
        UIK.OnClick(B("Pause", "Box/Quit"), () => { if (BattleSave.Save()) Quit(); });
        B("Pause", "Box/ToMenu").interactable = can; B("Pause", "Box/Quit").interactable = can;
        T("Pause", "Box/Note").text = can ? "전투는 지금 상태 그대로 저장됩니다" : "고양이 차례 · 행동 중에는 저장할 수 없습니다\n아군 차례에 다시 열어 주세요";
        Time.timeScale = 0;
        Show("Pause", true);
    }
    public static void ClosePause() { Show("Pause", false); Time.timeScale = 1; }

    public static void Quit()
    {
        Settings.Save();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}

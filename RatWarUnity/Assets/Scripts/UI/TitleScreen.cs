// 메인메뉴 (Canvas/Title): 로고 + 이어하기 · 새로 하기 · 불러오기 · 환경설정 · 종료
// 로고 그림은 Resources/Art/out/ui/logo.png 를 넣으면 자동으로 씀 (없으면 임시 글자)
using UnityEngine;
using UnityEngine.UI;

public class TitleScreen : MonoBehaviour
{
    Transform menu;

    void Awake()
    {
        menu = UIK.F(transform, "Panel/Menu");
        UIK.OnClick(UIK.B(menu, "Continue"), () => { int s = SaveSlots.Last(); if (s >= 0) Enter(s); });
        UIK.OnClick(UIK.B(menu, "New"), () => Menus.OpenSlots(true, Enter));
        UIK.OnClick(UIK.B(menu, "Load"), () => Menus.OpenSlots(false, Enter));
        UIK.OnClick(UIK.B(menu, "Settings"), Menus.OpenSettings);
        UIK.OnClick(UIK.B(menu, "Quit"), () => Menus.Ask(Loc.T("ui.title.quit_ask"), Menus.Quit, Loc.T("ui.common.quit"), Loc.T("ui.common.cancel")));
        var logo = Resources.Load<Sprite>("Art/out/ui/logo");
        var li = UIK.I(transform, "Panel/Logo");
        if (logo) { li.sprite = logo; li.enabled = true; UIK.Show(UIK.F(transform, "Panel/Logo/Text"), false); UIK.Show(UIK.F(transform, "Panel/Logo/Sub"), false); }
        UIK.T(transform, "Panel/Version").text = "v" + Application.version;
        // 언어를 바꾸면 슬롯 요약 글자도 다시
        Loc.Changed += () => { if (this && gameObject.activeInHierarchy) OnEnable(); };
    }

    void OnEnable()
    {
        if (!menu) return;
        int last = SaveSlots.Last();
        UIK.B(menu, "Continue").interactable = last >= 0;
        UIK.B(menu, "Load").interactable = SaveSlots.Any;
        UIK.T(menu, "Continue/Info").text = last >= 0 ? Loc.F("ui.title.continue_info", last + 1, SaveSlots.Summary(SaveSlots.Read(last))) : "";
    }

    // 슬롯 하나로 들어감: 진행 중 전투가 있으면 전투로, 아니면 사령부
    void Enter(int slot)
    {
        SaveSlots.Cur = slot;
        gameObject.SetActive(false);
        if (BattleSave.Exists && GameRoot.I.Resume()) return;
        GameRoot.I.SetMode("lobby"); Lobby.I.Show();
    }
}

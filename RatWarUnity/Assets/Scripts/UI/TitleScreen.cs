// 타이틀: 사령부로 (기록이 없으면 먼저 난이도 고르고 새 전쟁) · (저장한 전투가 있으면) 이어하기
using UnityEngine;

public class TitleScreen : MonoBehaviour
{
    void Awake()
    {
        UIK.OnClick(UIK.B(transform, "Panel/Go"), () =>
        {
            if (Camp.Load() != null) { ToLobby(); return; }
            DiffPicker.Open(GetComponentInParent<Canvas>().rootCanvas.transform, RULES.defaultDiff, d => { Camp.Save(Camp.Fresh(d)); ToLobby(); }, null);
        });
        var cont = UIK.B(transform, "Panel/Continue");
        UIK.OnClick(cont, () => { gameObject.SetActive(false); if (!GameRoot.I.Resume()) { GameRoot.I.SetMode("lobby"); Lobby.I.Show(); } });
    }
    void ToLobby() { gameObject.SetActive(false); GameRoot.I.SetMode("lobby"); Lobby.I.Show(); }
    void OnEnable() => UIK.Show(UIK.F(transform, "Panel/Continue"), BattleSave.Exists);
}

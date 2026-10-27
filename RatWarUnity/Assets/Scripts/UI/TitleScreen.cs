// 타이틀: 사령부로 · (저장한 전투가 있으면) 이어하기
using UnityEngine;

public class TitleScreen : MonoBehaviour
{
    void Awake()
    {
        UIK.OnClick(UIK.B(transform, "Panel/Go"), () => { gameObject.SetActive(false); GameRoot.I.SetMode("lobby"); Lobby.I.Show(); });
        var cont = UIK.B(transform, "Panel/Continue");
        UIK.OnClick(cont, () => { gameObject.SetActive(false); if (!GameRoot.I.Resume()) { GameRoot.I.SetMode("lobby"); Lobby.I.Show(); } });
    }
    void OnEnable() => UIK.Show(UIK.F(transform, "Panel/Continue"), BattleSave.Exists);
}

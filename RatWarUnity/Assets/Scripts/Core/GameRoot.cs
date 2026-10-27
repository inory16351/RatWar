// 시작 · 화면 흐름: 타이틀 → 사령부(로비) → 출격 → 전투 → 결과 → 사령부
// 글꼴 · 안개 · 정렬 같은 전역 설정도 여기서
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class GameRoot : MonoBehaviour
{
    public static GameRoot I;
    public Font fontMedium, fontBold;   // IBM Plex Sans KR (원작 NKK 글꼴)
    public AudioSource musicSource, sfxSource;
    public GameObject hud, lobby, board;
    string mode = "title";

    public static bool TypingBlocked => false;
    // 마우스가 전장 위 (UI 위가 아님, 전투 중)
    public static bool PointerOnBoard => I && I.mode == "battle" && Battle.over == null && !(EventSystem.current && EventSystem.current.IsPointerOverGameObject());

    void Awake()
    {
        I = this;
        Application.targetFrameRate = 60;
        // 공기 원근: URP 안개 하나로 (핸드오프: Linear, #B5AD95)
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogColor = new Color(0.71f, 0.68f, 0.58f);
        RenderSettings.fogStartDistance = 30;
        RenderSettings.fogEndDistance = 175;
        // 투명 판때기 정렬: 뒤(+Z)부터
        var cam = Camera.main;
        if (cam) { cam.transparencySortMode = TransparencySortMode.CustomAxis; cam.transparencySortAxis = new Vector3(0, 0, 1); }
        GraphicsSettings_Sort();
        ApplyFonts();
        Sfx.Init(sfxSource);
        Music.Init(musicSource);
        SetMode("title");
    }

    static void GraphicsSettings_Sort()
    {
        UnityEngine.Rendering.GraphicsSettings.transparencySortMode = TransparencySortMode.CustomAxis;
        UnityEngine.Rendering.GraphicsSettings.transparencySortAxis = new Vector3(0, 0, 1);
    }

    // 글꼴이 빠진 글자(꺼진 틀 포함)에 한글 글꼴. 씬에서는 MCP 로 미리 넣어 둠 (굵게 = Bold 글꼴)
    void ApplyFonts()
    {
        foreach (var canvas in FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            foreach (var t in canvas.GetComponentsInChildren<Text>(true))
            {
                if (t.font == fontMedium || t.font == fontBold) continue;
                bool bold = t.fontStyle == FontStyle.Bold || t.fontStyle == FontStyle.BoldAndItalic;
                t.font = bold ? fontBold : fontMedium;
                if (t.fontStyle == FontStyle.Bold) t.fontStyle = FontStyle.Normal;
            }
    }

    public void SetMode(string m)
    {
        mode = m;
        hud.SetActive(m == "battle");
        lobby.SetActive(m == "lobby");
        board.SetActive(m == "battle");
        Time.timeScale = 1;
    }

    // 저장한 전투 이어하기
    public bool Resume()
    {
        var d = BattleSave.Load();
        if (d == null) return false;
        SetMode("battle");
        if (!Battle.Restore(d)) { SetMode("lobby"); return false; }
        Music.Play("battle");
        BoardView.I.Build();
        Hud.I.Restart();
        return true;
    }

    // 출격 → 전투
    public void Deploy(MissionDef m, List<string> deploy)
    {
        SetMode("battle");
        Music.Play("battle");
        Battle.StartBattle(m, deploy);
        BoardView.I.Build();
        Hud.I.Restart();
    }
}

// 환경설정 (PlayerPrefs): 전체 · 배경음 · 효과음 크기, 언어
// 언어는 지금 한국어만 실제로 있음 — 고르는 기능과 저장만 (번역 붙일 때 Lang 을 읽으면 됨)
using UnityEngine;

public static class Settings
{
    public static readonly string[] LANGS = { "한국어", "English (준비 중)" };
    public static readonly string[] LANG_CODES = { "ko", "en" };

    public static float Master { get => PlayerPrefs.GetFloat("vol_master", 1f); set { PlayerPrefs.SetFloat("vol_master", Mathf.Clamp01(value)); Apply(); } }
    public static float MusicVol { get => PlayerPrefs.GetFloat("vol_music", 1f); set { PlayerPrefs.SetFloat("vol_music", Mathf.Clamp01(value)); Apply(); } }
    public static float SfxVol { get => PlayerPrefs.GetFloat("vol_sfx", 1f); set { PlayerPrefs.SetFloat("vol_sfx", Mathf.Clamp01(value)); Apply(); } }
    public static int Lang { get => Mathf.Clamp(PlayerPrefs.GetInt("lang", 0), 0, LANGS.Length - 1); set { PlayerPrefs.SetInt("lang", (value % LANGS.Length + LANGS.Length) % LANGS.Length); PlayerPrefs.Save(); } }
    public static string LangCode => LANG_CODES[Lang];

    // 소리에 반영 (시작할 때 한 번 + 바꿀 때마다)
    public static void Apply()
    {
        AudioListener.volume = Master;
        Music.Volume(Music.BASE * MusicVol);
        Sfx.vol = SfxVol;
    }
    public static void Save() => PlayerPrefs.Save();
}

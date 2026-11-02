// 환경설정 (PlayerPrefs): 전체 · 배경음 · 효과음 크기, 언어 (글자는 Loc 이 Lang 을 읽음)
using UnityEngine;

public static class Settings
{
    public static string[] LANGS => Loc.NAMES;

    public static float Master { get => PlayerPrefs.GetFloat("vol_master", 1f); set { PlayerPrefs.SetFloat("vol_master", Mathf.Clamp01(value)); Apply(); } }
    public static float MusicVol { get => PlayerPrefs.GetFloat("vol_music", 1f); set { PlayerPrefs.SetFloat("vol_music", Mathf.Clamp01(value)); Apply(); } }
    public static float SfxVol { get => PlayerPrefs.GetFloat("vol_sfx", 1f); set { PlayerPrefs.SetFloat("vol_sfx", Mathf.Clamp01(value)); Apply(); } }
    public static int Lang { get => Mathf.Clamp(PlayerPrefs.GetInt("lang", 0), 0, LANGS.Length - 1); set { PlayerPrefs.SetInt("lang", (value % LANGS.Length + LANGS.Length) % LANGS.Length); PlayerPrefs.Save(); Loc.Reload(); } }

    // 소리에 반영 (시작할 때 한 번 + 바꿀 때마다)
    public static void Apply()
    {
        AudioListener.volume = Master;
        Music.Volume(Music.BASE * MusicVol);
        Sfx.vol = SfxVol;
    }
    public static void Save() => PlayerPrefs.Save();
}

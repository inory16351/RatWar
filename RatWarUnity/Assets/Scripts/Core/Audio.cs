// 소리: 효과음은 웹처럼 걸러 낸 잡음으로 만듦 (총 · 폭발 · 박격포 · 맞음 · 쓰러짐 · 재장전)
// 배경음: Resources/Art/audio/lobby · battle (제미나이 생성 mp3), 바꿀 때 서서히
using System.Collections.Generic;
using UnityEngine;

public static class Sfx
{
    static AudioSource src;
    static readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
    public static void Init(AudioSource s) => src = s;

    // dur 초 · 저역 통과 freq · 크기 gain. 끝으로 갈수록 (1 − t)^2.2 로 줄어듦
    static AudioClip Noise(string key, float dur, float freq, float gain)
    {
        if (clips.TryGetValue(key, out var c)) return c;
        int rate = 44100, n = Mathf.Max(1, (int)(rate * dur));
        var d = new float[n];
        var rng = new System.Random(key.GetHashCode());
        float a = 1 - Mathf.Exp(-2 * Mathf.PI * freq / rate), y = 0;
        for (int i = 0; i < n; i++)
        {
            float x = (float)(rng.NextDouble() * 2 - 1) * Mathf.Pow(1 - (float)i / n, 2.2f);
            y += a * (x - y);
            d[i] = Mathf.Clamp(y * gain * 2.2f, -1, 1);
        }
        c = AudioClip.Create(key, n, 1, rate, false);
        c.SetData(d, 0);
        return clips[key] = c;
    }
    public static float vol = 1; // 환경설정 효과음 크기
    static void Play(AudioClip c) { if (src) src.PlayOneShot(c, vol); }

    public static void Shot(string w) => Play(Noise("shot_" + w, w == "sniper" ? 0.35f : w == "shotgun" ? 0.3f : 0.16f, w == "sniper" ? 2600 : 1800, 0.35f));
    public static void Boom() => Play(Noise("boom", 1.2f, 380, 0.9f));
    public static void Mortar() => Play(Noise("mortar", 0.25f, 700, 0.5f));
    public static void Hit() => Play(Noise("hit", 0.08f, 900, 0.2f));
    public static void Death() => Play(Noise("death", 0.2f, 500, 0.15f));
    public static void Reload() => Play(Noise("reload", 0.05f, 4000, 0.12f));
}

public static class Music
{
    static AudioSource src;
    static string cur;
    public const float BASE = 0.45f;   // 곡 기본 크기 (환경설정 배경음 1.0 일 때)
    public static float vol = BASE;
    public static void Init(AudioSource s) { src = s; src.loop = true; src.volume = 0; }
    // 환경설정에서 바꿀 때: 지금 나오는 곡에도 바로
    public static void Volume(float v) { vol = v; if (src && src.isPlaying && cur != null) src.volume = v; }

    public static async void Play(string name)
    {
        if (cur == name || !src) return;
        cur = name;
        // 지금 곡 줄이고 → 새 곡 키움
        float from = src.volume;
        for (float t = 0; t < 0.7f && src.isPlaying; t += Time.unscaledDeltaTime) { src.volume = from * (1 - t / 0.7f); await Awaitable.NextFrameAsync(); }
        if (cur != name) return;
        var clip = Resources.Load<AudioClip>("Art/audio/" + name);
        if (!clip) return;
        src.clip = clip; src.volume = 0; src.Play();
        for (float t = 0; t < 0.9f; t += Time.unscaledDeltaTime) { if (cur != name) return; src.volume = vol * t / 0.9f; await Awaitable.NextFrameAsync(); }
        src.volume = vol;
    }
}

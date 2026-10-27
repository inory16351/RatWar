// 전투 규칙(Battle)이 부르는 화면 쪽 창구. 실제 그리기는 BoardView(월드) · Hud(UI) · CameraRig
using System;
using UnityEngine;

public static class View
{
    static BoardView B => BoardView.I;
    static Hud H => Hud.I;
    static Color C(string hex) => SkillsData.Hex(hex);

    // ── 카메라 ──
    public static void Focus(Unit u) { if (u != null) CameraRig.I.Focus(BoardView.Foot(u.px, u.pz)); }
    public static void FocusPoint(Vector3 p) => CameraRig.I.Focus(p);
    public static void FocusPair(Unit a, int x, int z) => CameraRig.I.Focus((BoardView.Foot(a.px, a.pz) + BoardView.TilePos(x, z)) / 2);

    // ── 시간 ──
    // ms 동안 k = 0 → 1 (Time.timeScale 을 따름: 슬로 모션이면 느려짐)
    public static async Awaitable Tween(float ms, Action<float> fn)
    {
        float t = 0; ms = Mathf.Max(1, ms);
        while (t < ms) { fn(t / ms); await Awaitable.NextFrameAsync(); t += Time.deltaTime * 1000; }
        fn(1);
    }
    static async Awaitable RealSleep(float ms) { float end = Time.unscaledTime + ms / 1000f; while (Time.unscaledTime < end) await Awaitable.NextFrameAsync(); }

    public static async Awaitable Aim(Unit u, float ms)
    {
        if (u.view) u.view.aiming = true;
        await Awaitable.WaitForSecondsAsync(ms / 1000f);
        _ = AimOff(u);
    }
    static async Awaitable AimOff(Unit u) { await Awaitable.WaitForSecondsAsync(0.5f); if (u.view) u.view.aiming = false; }

    // ── 시네마틱: 치명타 · 마무리 한 발 (줌 · 레터박스 · 슬로) ──
    static bool cineOn;
    public static async Awaitable Cinematic(Unit a, Unit b, bool crit)
    {
        cineOn = true;
        CameraRig.I.Cine(true);
        FocusPair(a, b.x, b.z);
        H?.Letterbox(true);
        Time.timeScale = 0.32f;
        await RealSleep(380);
    }
    // 맞는 순간: 번쩍 + 큰 타격 + 히트스톱
    public static async Awaitable BulletTime(Unit a, Unit b, bool crit)
    {
        H?.Flash(crit ? 1 : 0.6f);
        CameraRig.I.Shake(crit ? 22 : 14);
        if (b.view) B.Slash(a, b);
        float keep = Time.timeScale; Time.timeScale = 0.03f;
        await RealSleep(crit ? 140 : 90);
        Time.timeScale = keep;
    }
    public static async void EndCinematic()
    {
        await RealSleep(520);
        Time.timeScale = 1; cineOn = false;
        CameraRig.I.Cine(false);
        H?.Letterbox(false);
    }
    // 정신 이상 등: 잠깐 느리게
    public static async void Slowmo(float ts, float sec) { Time.timeScale = ts; await RealSleep(sec * 1000); if (!cineOn) Time.timeScale = 1; }

    // ── 효과 ──
    public static void Popup(Unit u, string txt, string col) { if (u?.view && u.view.gameObject.activeInHierarchy) H?.Popup(u.view.Chest + Vector3.up * 0.9f, txt, C(col), false); }
    public static void Shout(Unit u, string txt) { if (!u.view) return; H?.Popup(u.view.Top + Vector3.up * 0.3f, txt, C("#ffcf4a"), true); B.Ring(u, new Color(1f, 0.81f, 0.29f, 0.7f), 8); CameraRig.I.Shake(5); }
    public static void Muzzle(Unit a, Unit b, bool hit, bool crit, bool last, string weapon) => B.Muzzle(a, b, hit, crit, weapon);
    public static void Blood(Unit u, int n) => B.Blood(u, n);
    public static Awaitable Die(Unit u, string gore) => B.Die(u, gore);
    public static void Revive(Unit u) { u.hide.Clear(); }
    public static void Explosion(int x, int z, float r) => B.Explosion(x, z, r);
    public static void Debris(int x, int z, char prop) => B.Debris(x, z, prop);
    public static void GroundDirty() => B.MarkDirty();
    public static void FiresChanged() => B.FiresChanged();
    public static void Dig(int x, int z) => B.Dig(x, z);
    public static void Slash(Unit a, Unit b) => B.Slash(a, b);
    public static void RatsRun(Unit a, Unit b) => B.RatsRun(a, b);
    public static Awaitable Lob(Unit a, int x, int z, float ms) => B.Lob(a, x, z, ms);
    public static Awaitable Shell(Unit a, int x, int z, float ms) => B.Shell(a, x, z, ms);
    public static Awaitable Flame(Unit a, int x, int z) => B.Flame(a, x, z);
    public static Awaitable Psi(Unit a, Unit b, string col) => B.Psi(a, b, C(col));

    // ── UI ──
    public static void Banner(string text, string side) => H?.Banner(text, side);
    public static void ShowEnd(string result, Camp.SettleOut o) => EndScreen.I?.Show(result, o);
}

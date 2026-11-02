// 저장 슬롯 3개: 슬롯마다 사령부 기록(slot<n>_camp.json) + 진행 중 전투(slot<n>_battle.json)
// Camp · BattleSave 는 지금 슬롯(Cur) 파일을 씀. 예전 단일 저장(war_camp_v2 · war_battle_v1)은 처음 한 번 슬롯 1로 옮김
using System;
using System.IO;
using System.Linq;
using UnityEngine;

public static class SaveSlots
{
    public const int COUNT = 3;
    static string Dir => Application.persistentDataPath;
    public static string CampPath(int i) => Path.Combine(Dir, $"slot{i}_camp.json");
    public static string BattlePath(int i) => Path.Combine(Dir, $"slot{i}_battle.json");

    // 지금 쓰는 슬롯 (마지막에 고른 것)
    public static int Cur
    {
        get => Mathf.Clamp(PlayerPrefs.GetInt("slot", 0), 0, COUNT - 1);
        set { PlayerPrefs.SetInt("slot", value); PlayerPrefs.Save(); }
    }

    public static bool Used(int i) => File.Exists(CampPath(i));
    public static bool Any => Enumerable.Range(0, COUNT).Any(Used);

    // 가장 최근에 저장된 슬롯 (이어하기), 없으면 −1
    public static int Last()
    {
        int best = -1; DateTime t = DateTime.MinValue;
        for (int i = 0; i < COUNT; i++)
        {
            if (!Used(i)) continue;
            var w = LastWrite(i);
            if (w > t) { t = w; best = i; }
        }
        return best;
    }
    static DateTime LastWrite(int i)
    {
        var t = File.GetLastWriteTime(CampPath(i));
        if (File.Exists(BattlePath(i))) { var b = File.GetLastWriteTime(BattlePath(i)); if (b > t) t = b; }
        return t;
    }

    public static void Delete(int i)
    {
        try { if (File.Exists(CampPath(i))) File.Delete(CampPath(i)); if (File.Exists(BattlePath(i))) File.Delete(BattlePath(i)); }
        catch (Exception e) { Debug.LogWarning("슬롯 지우기 실패 " + e.Message); }
    }

    // 슬롯 카드에 쓸 요약 (빈 슬롯 = null)
    public class Info { public int diff, turn, land, squad, supplies, battles; public bool inBattle; public string over; public DateTime when; }
    public static Info Read(int i)
    {
        if (!Used(i)) return null;
        try
        {
            var c = JsonUtility.FromJson<CampData>(File.ReadAllText(CampPath(i)));
            if (c == null) return null;
            return new Info
            {
                diff = c.diff, turn = c.war?.turn ?? 0, land = c.war?.own?.Count(o => o == "rat") ?? 0, squad = c.squad.Count,
                supplies = c.supplies, battles = c.battles, over = c.war?.over ?? "", inBattle = File.Exists(BattlePath(i)), when = LastWrite(i),
            };
        }
        catch (Exception e) { Debug.LogWarning("슬롯 읽기 실패 " + e.Message); return null; }
    }
    public static string Summary(Info s) => s == null ? "빈 슬롯"
        : $"{RULES.DiffOf(s.diff).name} · {s.turn}턴 · 땅 {s.land}칸 · 분대 {s.squad} · 보급품 {s.supplies}"
          + (s.over == "win" ? " · 승리" : s.over == "lose" ? " · 패배" : s.inBattle ? " · 전투 중" : "");

    // 예전 단일 저장 → 슬롯 1 (슬롯이 하나도 없을 때만)
    public static void MigrateLegacy()
    {
        try
        {
            string oldCamp = Path.Combine(Dir, "war_camp_v2.json"), oldBattle = Path.Combine(Dir, "war_battle_v1.json");
            if (!Any && File.Exists(oldCamp))
            {
                File.Move(oldCamp, CampPath(0));
                if (File.Exists(oldBattle)) File.Move(oldBattle, BattlePath(0));
                Cur = 0;
            }
        }
        catch (Exception e) { Debug.LogWarning("예전 저장 옮기기 실패 " + e.Message); }
    }
}

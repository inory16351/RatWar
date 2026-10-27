// 전투 중간 저장 · 이어하기: persistentDataPath/war_battle_v1.json
// 아군 차례에만 저장 (진행 중인 동작 · 연출이 없을 때). 전투가 끝나면 지움.
// 캠페인 기록(CampData)도 이 전투가 시작될 때의 것을 같이 넣어 둠 → 이어하면 같은 쥐 기록으로 정산됨
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using UnityEngine;

public class BattleSaveData
{
    public int v = 1;
    public string missionId;
    public CampData camp;
    public int round, shells, scan, uid, statKills, statLost;
    public List<string> alerted = new List<string>();
    public List<int[]> fires = new List<int[]>();        // x, z, 남은 턴
    public List<int[]> tunnels = new List<int[]>();      // ax, az, bx, bz
    public string[] props, grounds;                      // 칸마다 (부서진 엄폐물 · 구덩이 반영)
    public List<Unit> units;
    public List<string[]> log = new List<string[]>();
}

public static class BattleSave
{
    static string PathOf => Path.Combine(Application.persistentDataPath, "war_battle_v1.json");
    public static bool Exists => File.Exists(PathOf);
    public static void Delete() { try { if (Exists) File.Delete(PathOf); } catch { /* 무시 */ } }

    // 지금 저장할 수 있나 (아군 차례 · 동작 없음 · 끝나지 않음)
    public static bool CanSave => Battle.world != null && Battle.over == null && Battle.turn == "rat" && Battle.mode != "busy";

    public static bool Save()
    {
        if (!CanSave) return false;
        foreach (var u in Battle.units) { u.suppressedById = u.suppressedBy?.id ?? 0; u.suppressingId = u.suppressing?.id ?? 0; }
        var w = Battle.world;
        var d = new BattleSaveData
        {
            missionId = Battle.mission.id, camp = Battle.camp, round = Battle.round, shells = Battle.shells, scan = Battle.scan,
            uid = Battle.units.Count == 0 ? 0 : Battle.units.Max(u => u.id), statKills = Battle.statKills, statLost = Battle.statLost,
            alerted = Battle.alerted.ToList(),
            fires = Battle.fires.Select(f => new[] { f.x, f.z, f.turns }).ToList(),
            tunnels = w.tunnels.Select(t => new[] { t.a.x, t.a.z, t.b.x, t.b.z }).ToList(),
            props = w.tiles.Select(t => t.HasProp ? t.prop.ToString() : "").ToArray(),
            grounds = w.tiles.Select(t => t.ground).ToArray(),
            units = Battle.units,
            log = Battle.log.Select(l => new[] { l.msg, l.kind }).ToList(),
        };
        try
        {
            File.WriteAllText(PathOf, JsonConvert.SerializeObject(d, new JsonSerializerSettings { ReferenceLoopHandling = ReferenceLoopHandling.Ignore }));
            return true;
        }
        catch (System.Exception e) { Debug.LogWarning("전투 저장 실패 " + e.Message); return false; }
    }

    public static BattleSaveData Load()
    {
        try { return Exists ? JsonConvert.DeserializeObject<BattleSaveData>(File.ReadAllText(PathOf)) : null; }
        catch (System.Exception e) { Debug.LogWarning("전투 저장 읽기 실패 " + e.Message); return null; }
    }
}

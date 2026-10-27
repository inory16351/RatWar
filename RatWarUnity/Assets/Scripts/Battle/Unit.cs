// 전투 유닛 (쥐 · 고양이). 웹 makeUnit 의 필드를 그대로 옮김
using System.Collections.Generic;
using UnityEngine;

// 그리기용 동작 상태 (리그 포즈가 읽음)
public class AnimState
{
    public float seed = Random.value * 6;
    public float? walk, death;
    public float crouch, aim, recoil, throwK, hit;
}

// 전투 저장(BattleSave)은 이 객체를 그대로 JSON 으로 씀. [JsonIgnore] = 실행 중에만 쓰는 참조 (불러올 때 다시 이어 붙임)
public class Unit
{
    public int id; public string side, cls, name;
    [Newtonsoft.Json.JsonIgnore] public ClassDef def;
    public int x, z; public float px, pz;
    public int maxHp, hp, aim, ap, ammo, clip, grenades, medkits, charges, cheese, psi, flame;
    public int critB, defB, dmgB, healB, boom, burnB, sight, whisker, mob;
    public float blast; public bool climb;
    public List<string> perks = new List<string>(), abil = new List<string>();
    public Dictionary<string, int> wounds = new Dictionary<string, int>(); // 1 부상 · 2 잘림
    public bool downed, alive = true, overwatch, hunker, moved, fired, active, seen, visible, revealed, target, hidden, marked, inCover;
    [Newtonsoft.Json.JsonIgnore] public Unit suppressedBy, suppressing;
    public int suppressedById, suppressingId; // 저장용 (위 참조의 id)
    public string pod;
    // 스트레스 (쥐만) · 정신 이상 · 버프
    public int stress, panic, buffAim, ward, burn, controlled;
    public string broke, brokeName, deathHow, gore;
    public HashSet<string> used = new HashSet<string>();
    // 기록
    [Newtonsoft.Json.JsonIgnore] public RatRecord r; public string rosterId; public int xp, kills;
    // 그리기
    public float fdx, fdz;
    [Newtonsoft.Json.JsonIgnore] public AnimState st = new AnimState();
    [Newtonsoft.Json.JsonIgnore] public string face; [Newtonsoft.Json.JsonIgnore] public float faceT;
    public int faceIdx = -1;        // 개체 얼굴 번호 (머리 파츠 head_f<n>)
    [Newtonsoft.Json.JsonIgnore] public Color tint = Color.white;
    public bool forceSide, corpse;
    [Newtonsoft.Json.JsonIgnore] public bool mortarT;
    public HashSet<string> hide = new HashSet<string>();
    [Newtonsoft.Json.JsonIgnore] public UnitView view;

    [Newtonsoft.Json.JsonIgnore] public string Weapon => def.weapon;
}

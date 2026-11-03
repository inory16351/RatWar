// 전투 유닛 (쥐 · 고양이). 웹 makeUnit 의 필드를 그대로 옮김
using System.Collections.Generic;
using UnityEngine;

// 그리기용 동작 상태 (리그 포즈가 읽음)
public class AnimState
{
    public float seed = Random.value * 6;
    public float? walk, death;
    public float crouch, aim, recoil, throwK, hit;
    public float stab, flame; // 칼 찌르기 (1 → 0) · 화염방사 중 (1 = 뿜는 중)
}

// 전투 저장(BattleSave)은 이 객체를 그대로 JSON 으로 씀. [JsonIgnore] = 실행 중에만 쓰는 참조 (불러올 때 다시 이어 붙임)
public class Unit
{
    public int id; public string side, cls, name;
    [Newtonsoft.Json.JsonIgnore] public ClassDef def;
    public int x, z; public float px, pz;
    public int maxHp, hp, aim, ap, ammo, clip, grenades, medkits, charges, psi, flame, baseMob;
    public string weapon;                                                     // 스킬로 바뀐 무기 (화염방사기). null = 병과 무기
    public Dictionary<string, int> cd = new Dictionary<string, int>();        // 능력 재사용 대기 (남은 내 차례 수)
    public Dictionary<string, int> left = new Dictionary<string, int>();      // 전투당 남은 횟수 (부비트랩 등)
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
    public int missStreak; // 연속 빗나감 (난이도 운 보정)
    public bool painkiller, rageAim, weakMarked; public int dazed; // 진통제 · 분노(다음 사격 +15) · 약점 간파 표적 · 큰 귀 멍함
    // 판 끝 획득 특성 판정용 기록
    public int knifeKills, nearBlasts; public bool legBlast; public string hitByFrenzy;
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

    [Newtonsoft.Json.JsonIgnore] public string Weapon => weapon ?? def.weapon;
}

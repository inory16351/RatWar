// 동작: (보기, 상태, 시간) → 부위 각도 · 루트 (웹 rig.js pose 그대로)
// 좌표는 리그 그림 px (y 아래로 +), 각도 + = 시계 방향. 루트 dy: + = 아래로
using System.Collections.Generic;
using UnityEngine;

public class Pose
{
    public Dictionary<string, float> a = new Dictionary<string, float>();
    public Dictionary<string, Vector2> sc = new Dictionary<string, Vector2>(), off = new Dictionary<string, Vector2>();
    public float dx, dy, rot;
    public float A(string n) => a.TryGetValue(n, out var v) ? v : 0;
}

public static class RigPose
{
    // legK = 이 리그 다리 길이 / 쥐 다리 길이. 웅크림 내려앉기 값이 쥐 기준이라 다리 짧은 고양이는 땅에 박혔음
    public static Pose Get(string view, AnimState st, float t, float legK = 1)
    {
        var P = new Pose(); var a = P.a; var sc = P.sc;
        bool side = view == "side";
        bool shortLegs = legK < 0.8f; // 고양이 리그 (쥐 = 1, 고양이 ≈ 0.6)
        float br = Mathf.Sin(t * 2.2f + st.seed);
        float A(string n) => a.TryGetValue(n, out var v) ? v : 0;
        // 대기 숨쉬기 + 살짝 숙인 전투 자세
        a["torso"] = side ? 3 + br * 0.8f : br * 0.4f;
        a["head"] = side ? -br * 1.2f : br * 0.6f;
        sc["torso"] = new Vector2(1, 1 + br * 0.006f);
        a["tail"] = Mathf.Sin(t * 1.4f + 1) * 4;
        if (side) a["arm"] = br * 0.8f;

        if (st.walk != null)
        {
            float ph = st.walk.Value * Mathf.PI * 2, s = Mathf.Sin(ph), c = Mathf.Cos(ph);
            P.dy -= Mathf.Abs(c) * 10;
            if (side)
            {
                a["torso"] += 5;
                a["thighN"] = s * 24; a["shinN"] = Mathf.Max(0, -s) * 32 + 4;
                a["thighF"] = -s * 24; a["shinF"] = Mathf.Max(0, s) * 32 + 4;
                a["arm"] = A("arm") + c * 2;
            }
            else
            {
                a["legL"] = s * 7; a["legR"] = -s * 7;
                sc["legL"] = new Vector2(1, 1 - Mathf.Max(0, s) * 0.08f); sc["legR"] = new Vector2(1, 1 - Mathf.Max(0, -s) * 0.08f);
                a["torso"] += s * 1.5f;
            }
            a["tail"] += s * 8;
        }
        if (st.crouch > 0)
        {
            // 리그 무릎 꿇기 (옆모습): 앞다리는 세우고 뒷다리는 무릎을 땅에
            float k = st.crouch;
            if (side && shortLegs)
            {
                // 고양이: 쥐 무릎 꿇기 각도면 외투 밑으로 군화만 앞뒤로 튀어나와 달려드는 자세처럼 보였음 → 두 다리 다 쪼그려 앉기
                a["torso"] += 10 * k;
                a["head"] += -6 * k;
                a["thighN"] = A("thighN") * (1 - k) - 60 * k; a["shinN"] = A("shinN") * (1 - k) + 60 * k;
                a["thighF"] = A("thighF") * (1 - k) - 50 * k; a["shinF"] = A("shinF") * (1 - k) + 50 * k;
                P.dy += 50 * k * legK; // 허벅지가 60° 들리면 엉덩이가 허벅지 길이 절반쯤 내려감 (다리 180px · legK 의 반이 허벅지)
            }
            else if (side)
            {
                a["torso"] += 16 * k;
                a["head"] += -10 * k;
                a["thighN"] = A("thighN") * (1 - k) - 78 * k; a["shinN"] = A("shinN") * (1 - k) + 82 * k;
                a["thighF"] = A("thighF") * (1 - k) - 8 * k; a["shinF"] = A("shinF") * (1 - k) + 86 * k;
                P.dy += 132 * k * legK;
            }
            else
            {
                sc["legL"] = new Vector2(1, 1 - 0.42f * k); sc["legR"] = new Vector2(1, 1 - 0.42f * k);
                a["legL"] = -6 * k; a["legR"] = 6 * k;
                P.dy += 120 * k * legK;
                a["head"] += 3 * k;
            }
        }
        if (st.aim > 0 && side) { float k = st.aim; a["arm"] = A("arm") - 9 * k; a["head"] += 4 * k; a["torso"] += 2 * k; }
        if (st.recoil > 0)
        {
            float r = st.recoil;
            if (side) { a["arm"] = A("arm") - 7 * r; P.off["arm"] = new Vector2(-10 * r, 0); a["torso"] -= 2 * r; }
            else P.dy += 6 * r;
        }
        if (st.throwK > 0 && side)
        {
            float k = st.throwK;
            a["arm"] = A("arm") - 150 * k * (1 - k) * 4 * 0.5f; a["torso"] -= 8 * Mathf.Sin(k * Mathf.PI);
        }
        if (st.hit > 0)
        {
            float h = st.hit;
            if (side) { a["torso"] -= 9 * h; a["head"] -= 12 * h; P.dx -= 10 * h; }
            else { a["torso"] += 6 * h; a["head"] -= 8 * h; P.dy -= 4 * h; }
        }
        if (st.death != null)
        {
            // 쓰러짐: 0~0.25 맞고 젖혀짐 → 0.25~0.6 무릎 꺾임 → 0.6~1 바닥
            float d = st.death.Value;
            float e1 = Mathf.Min(1, d / 0.25f), e2 = Mathf.Clamp01((d - 0.25f) / 0.35f), e3 = Mathf.Clamp01((d - 0.6f) / 0.4f);
            float ease = 1 - (1 - e3) * (1 - e3);
            a["torso"] -= 14 * e1 - 6 * e2;
            a["head"] -= 18 * e1;
            if (side && shortLegs)
            {
                // 고양이: 무릎을 크게 꺾은 채 뒤로 누우면 큰 군화가 하늘로 들려 꺾여 보임 → 다리는 거의 편 채 살짝만
                a["thighN"] = -12 * e2; a["shinN"] = 18 * e2; a["thighF"] = -4 * e2; a["shinF"] = 12 * e2;
                a["arm"] = A("arm") + 50 * e2 + 30 * e3;
            }
            else if (side)
            {
                a["thighN"] = -30 * e2; a["shinN"] = 50 * e2; a["thighF"] = -10 * e2; a["shinF"] = 40 * e2;
                a["arm"] = A("arm") + 50 * e2 + 30 * e3;
            }
            // 쥐는 웹 그대로 −100°(무릎 굽혀 다리가 내려옴). 다리를 편 고양이는 −100° 면 발이 하늘로 들려 −90° (반듯이 누움)
            P.rot = shortLegs ? -88 * ease - 2 * e2 : -88 * ease - 12 * e2;
            P.dy += 60 * e2 * (1 - e3);
            P.dx -= 140 * ease + 30 * e2;
            a["tail"] += 20 * e3;
        }
        return P;
    }
}

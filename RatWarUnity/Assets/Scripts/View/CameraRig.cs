// 카메라: 진짜 원근(Perspective). 줌 = 카메라를 앞으로 미는 것(돌리) — FOV 는 그대로
// 돈스타브식 낮은 시점: 웹판 칸 깊이 34/72 ≈ 28° 숙임 + 맨 뒤 줄 0.42배 원근에 맞춤.
// 당길수록 더 낮게 숙여(pitchNear) 원근이 세지고, 맵 뒤 배경 층(들판 · 언덕 · 먼 마을 · 하늘)이 화면 위에 보임
// 회전 없음: 늘 쥐(앞, -Z)에서 적(뒤, +Z)을 봄. WASD/화살표(Shift 빠르게) · 우클릭 끌기 · 휠 줌
using UnityEngine;
using UnityEngine.InputSystem;

public class CameraRig : MonoBehaviour
{
    public static CameraRig I;
    // 웹 확대(가까이) ≈ 28° · 축소(멀리) = 지평선 · 하늘이 화면 위 1/4 쯤 → 12°
    public float fov = 40, pitchNear = 26, pitchFar = 12;
    public float dist = 15, distMin = 8, distMax = 36;
    float pitch;
    public Camera cam;
    Vector3 focus, focusT;
    float distT, cineMul = 1, cineT = 1;
    float shake; Vector2 kick;
    bool following;
    bool dragging; Vector2 dragStart, lastMouse;
    public bool rightDragged; // 우클릭이 끌기였는지 (아니면 취소)
    public Rect bounds = new Rect(0, 0, 18, 22);

    void Awake()
    {
        I = this;
        if (!cam) cam = GetComponentInChildren<Camera>();
        distT = dist;
    }

    public void Focus(Vector3 p, bool snap = false) { focusT = new Vector3(p.x, 0, p.z); following = true; if (snap) focus = focusT; }
    public void Shake(float s) => shake = Mathf.Max(shake, Mathf.Min(s, 16));
    public void Kick(Vector2 k) => kick += k;
    public void Cine(bool on) => cineT = on ? 0.62f : 1;

    void LateUpdate()
    {
        float dt = Time.unscaledDeltaTime;
        var kb = Keyboard.current; var ms = Mouse.current;
        if (kb != null && !GameRoot.TypingBlocked)
        {
            Vector2 mv = Vector2.zero;
            if (kb.wKey.isPressed || kb.upArrowKey.isPressed) mv.y += 1;
            if (kb.sKey.isPressed || kb.downArrowKey.isPressed) mv.y -= 1;
            if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) mv.x -= 1;
            if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) mv.x += 1;
            if (mv != Vector2.zero)
            {
                float sp = (kb.leftShiftKey.isPressed ? 26 : 13) * dt * (dist / 22);
                focusT += new Vector3(mv.x, 0, mv.y) * sp;
                following = true;
            }
        }
        if (ms != null && GameRoot.PointerOnBoard)
        {
            float wheel = ms.scroll.ReadValue().y;
            if (Mathf.Abs(wheel) > 0.01f) distT = Mathf.Clamp(distT - Mathf.Sign(wheel) * distT * 0.12f, distMin, distMax);
        }
        // 우클릭 끌기: 잡은 땅 점이 마우스를 따라오게
        if (ms != null)
        {
            // 우클릭 끌기: 마우스가 움직인 화면 거리만큼 (줌 · 숙임에 맞춰) 옮김.
            // 예전엔 커서 아래 땅 점을 잡았는데, 낮은 시점이라 화면 위쪽(지평선 너머)에선 땅이 안 잡혀 원점으로 튀었음
            if (ms.rightButton.wasPressedThisFrame && GameRoot.PointerOnBoard) { dragging = true; rightDragged = false; dragStart = lastMouse = ms.position.ReadValue(); }
            if (dragging && ms.rightButton.isPressed)
            {
                var now = ms.position.ReadValue();
                if ((now - dragStart).sqrMagnitude > 36) rightDragged = true;
                if (rightDragged)
                {
                    var delta = now - lastMouse;
                    float k = 2f * dist * Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad) / Mathf.Max(1, Screen.height); // 초점 거리에서 화면 1px = 월드 몇 칸
                    float kz = k / Mathf.Max(0.35f, Mathf.Sin(pitch * Mathf.Deg2Rad));                     // 세로는 숙임만큼 눌려 보이니 늘림
                    focusT -= new Vector3(delta.x * k, 0, delta.y * kz);
                    following = true;
                }
                lastMouse = now;
            }
            if (ms.rightButton.wasReleasedThisFrame) dragging = false;
        }
        focusT.x = Mathf.Clamp(focusT.x, bounds.xMin - 6, bounds.xMax + 6);
        focusT.z = Mathf.Clamp(focusT.z, bounds.yMin - 6, bounds.yMax + 4);
        // 끄는 중엔 바로 따라오고, 아니면 부드럽게 (끈 뒤 제자리로 미끄러져 돌아가는 일 없음 — 둘 다 같은 목표를 씀)
        focus = dragging && rightDragged ? focusT : following ? Vector3.Lerp(focus, focusT, Mathf.Min(1, dt * 6)) : focus;
        dist = Mathf.Lerp(dist, distT, Mathf.Min(1, dt * 8));
        cineMul = Mathf.Lerp(cineMul, cineT, Mathf.Min(1, dt * 8));
        // 흔들림은 짧게: 지수 감쇠(약 0.35초에 1/10). 예전 선형 감쇠는 폭발 한 번에 7초씩 흔들렸음
        shake *= Mathf.Exp(-dt * 6.5f); if (shake < 0.05f) shake = 0;
        kick *= Mathf.Pow(0.0005f, dt);
        Apply();
    }

    void Apply()
    {
        cam.fieldOfView = fov;
        float d = dist * cineMul;
        pitch = Mathf.Lerp(pitchNear, pitchFar, Mathf.InverseLerp(distMin, distMax, d));
        var rot = Quaternion.Euler(pitch, 0, 0);
        var sh = new Vector3((Random.value - 0.5f) * shake + kick.x * 0.5f, (Random.value - 0.5f) * shake + kick.y * 0.5f, 0) * 0.015f;
        transform.position = focus + rot * new Vector3(0, 0, -d) + sh;
        transform.rotation = rot;
    }

    // 화면 점 → 높이 y 평면 위 점
    public Vector3 GroundAt(Vector2 screen, float y = 0)
    {
        var ray = cam.ScreenPointToRay(screen);
        var pl = new Plane(Vector3.up, new Vector3(0, y, 0));
        return pl.Raycast(ray, out var t) ? ray.GetPoint(t) : Vector3.zero;
    }
}

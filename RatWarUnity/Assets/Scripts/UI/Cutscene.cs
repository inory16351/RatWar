// 컷씬: 그림 슬라이드(천천히 확대 · 이동) + 한 글자씩 나오는 자막. 클릭 · 스페이스 = 다음, Esc = 건너뛰기
// 그림 = Resources/Art/out/cut/<img>.jpg (art_requests/cutscenes 시트를 4등분)
using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class Cutscene : MonoBehaviour
{
    public static Cutscene I;
    Image slide, fade; Text title, caption, hint;
    Slide[] slides; Action done; int idx; float t, typed; string head;
    const float CPS = 28, FADE = 0.45f;

    void Awake()
    {
        I = this;
        slide = UIK.I(transform, "Slide"); fade = UIK.I(transform, "Fade");
        title = UIK.T(transform, "Title"); caption = UIK.T(transform, "Caption"); hint = UIK.T(transform, "Hint");
        gameObject.SetActive(false);
    }

    public static bool Playing => I && I.gameObject.activeSelf;

    public void Play(string heading, Slide[] list, Action onDone)
    {
        if (list == null || list.Length == 0) { onDone?.Invoke(); return; }
        slides = list; done = onDone; head = heading; idx = -1;
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
        Next();
    }

    void Next()
    {
        idx++;
        if (idx >= slides.Length) { Finish(); return; }
        var s = slides[idx];
        slide.sprite = ArtLib.Sprite("out/cut/" + s.img + ".jpg");
        slide.enabled = slide.sprite != null;
        // 화면을 꽉 채우게 (비율 유지, 넘치는 쪽은 잘림)
        if (slide.sprite)
        {
            var box = ((RectTransform)transform).rect;
            float asp = slide.sprite.rect.width / slide.sprite.rect.height;
            slide.rectTransform.sizeDelta = box.width / box.height > asp ? new Vector2(box.width, box.width / asp) : new Vector2(box.height * asp, box.height);
        }
        t = 0; typed = 0;
        title.text = idx == 0 ? head : "";
        caption.text = "";
        hint.text = UIK.Dim($"{idx + 1} / {slides.Length}    클릭 · 스페이스 다음    Esc 건너뛰기");
    }

    void Finish()
    {
        gameObject.SetActive(false);
        var d = done; done = null;
        d?.Invoke();
    }

    void Update()
    {
        if (slides == null) return;
        float dt = Time.unscaledDeltaTime;
        t += dt;
        var s = slides[idx];
        // 켄 번스: 슬라이드마다 확대 방향 · 흐름을 번갈아
        float k = Mathf.Clamp01(t / 9f);
        float sc = idx % 2 == 0 ? Mathf.Lerp(1.02f, 1.1f, k) : Mathf.Lerp(1.1f, 1.02f, k);
        slide.rectTransform.localScale = new Vector3(sc, sc, 1);
        slide.rectTransform.anchoredPosition = new Vector2((idx % 2 == 0 ? -1 : 1) * Mathf.Lerp(-18, 18, k), 0);
        // 검은 막: 들어올 때 걷힘
        fade.color = new Color(0, 0, 0, 1 - Mathf.Clamp01(t / FADE));
        // 자막 타자
        if (t > FADE * 0.6f) typed = Mathf.Min(s.text.Length, typed + dt * CPS);
        caption.text = s.text.Substring(0, (int)typed);

        var kb = Keyboard.current; var ms = Mouse.current;
        if (kb != null && kb.escapeKey.wasPressedThisFrame) { Menus.escUsed = Time.frameCount; Finish(); return; }
        bool adv = (kb != null && (kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame)) || (ms != null && ms.leftButton.wasPressedThisFrame);
        if (!adv) return;
        if (typed < s.text.Length) { typed = s.text.Length; t = Mathf.Max(t, FADE); }
        else Next();
    }
}

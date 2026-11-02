// 컷씬: 그림 슬라이드(천천히 확대 · 이동) + 한 글자씩 나오는 자막. 클릭 · 스페이스 = 다음, Esc = 건너뛰기
// 그림 = Resources/Art/out/cut/<img>.jpg (art_requests/cutscenes 시트를 4등분)
using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class Cutscene : MonoBehaviour
{
    public static Cutscene I;
    Image slide, fade; Text title, caption, hint; RectTransform portrait;
    Slide[] slides; Action done; int idx; float t, typed; string head;
    const float CPS = 28, FADE = 0.45f;

    void Awake()
    {
        I = this;
        slide = UIK.I(transform, "Slide"); fade = UIK.I(transform, "Fade");
        title = UIK.T(transform, "Title"); caption = UIK.T(transform, "Caption"); hint = UIK.T(transform, "Hint");
        portrait = (RectTransform)UIK.F(transform, "Portrait");
        capBase = new Layout(caption); titleBase = new Layout(title);
        title.verticalOverflow = VerticalWrapMode.Overflow; // 이름 아래 한 줄 (출신 · 출전)
        gameObject.SetActive(false);
    }

    // 에필로그 쥐 장면은 왼쪽 큰 초상 · 오른쪽 이름과 이야기. 다른 장면은 장면에 놓인 그대로
    struct Layout
    {
        Vector2 min, max, pivot, pos, size; TextAnchor al;
        public Layout(Text t) { var r = t.rectTransform; min = r.anchorMin; max = r.anchorMax; pivot = r.pivot; pos = r.anchoredPosition; size = r.sizeDelta; al = t.alignment; }
        public Layout(Vector2 pos, Vector2 size, TextAnchor al) { min = max = pivot = new Vector2(0.5f, 0.5f); this.pos = pos; this.size = size; this.al = al; }
        public void To(Text t) { var r = t.rectTransform; r.anchorMin = min; r.anchorMax = max; r.pivot = pivot; r.anchoredPosition = pos; r.sizeDelta = size; t.alignment = al; }
    }
    Layout capBase, titleBase;
    static readonly Layout CAP_RAT = new Layout(new Vector2(250, -60), new Vector2(940, 520), TextAnchor.UpperLeft);
    static readonly Layout TITLE_RAT = new Layout(new Vector2(250, 270), new Vector2(940, 120), TextAnchor.LowerLeft);

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
        if (s.text != null && Loc.Has(s.text)) s.text = Loc.T(s.text);
        // 에필로그 쥐 한 마리: 그림 대신 검은 바탕에 초상
        slide.sprite = s.rat != null || s.img == null ? null : ArtLib.Sprite("out/cut/" + s.img + ".jpg");
        slide.enabled = slide.sprite != null;
        UIK.Show(portrait, s.rat != null);
        if (s.rat != null) UIK.Portrait(portrait, s.rat, full: true);
        if (s.rat != null) { CAP_RAT.To(caption); TITLE_RAT.To(title); } else { capBase.To(caption); titleBase.To(title); }
        // 화면을 꽉 채우게 (비율 유지, 넘치는 쪽은 잘림)
        if (slide.sprite)
        {
            var box = ((RectTransform)transform).rect;
            float asp = slide.sprite.rect.width / slide.sprite.rect.height;
            slide.rectTransform.sizeDelta = box.width / box.height > asp ? new Vector2(box.width, box.width / asp) : new Vector2(box.height * asp, box.height);
        }
        t = 0; typed = 0;
        title.text = s.title != null ? s.title + (s.sub != null ? "\n" + UIK.Dim($"<size=24>{s.sub}</size>") : "") : idx == 0 ? head : "";
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

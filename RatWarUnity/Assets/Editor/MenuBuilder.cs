// 메뉴 UI 를 씬 하이라키에 만드는 도구 (메뉴 'RatWar/메뉴 UI 만들기'). 다시 누르면 지우고 새로 만듦
// 만드는 것 (Canvas 아래, 모두 꺼 둔 채): Title 패널 안 로고 · 메뉴 버튼 / Slots / Settings / Pause / Confirm / DiffPicker
// 런타임 스크립트(TitleScreen · SlotsPanel · SettingsPanel · PauseMenu · ConfirmBox · DiffPicker)는 이름으로 찾아 연결만 함
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class MenuBuilder
{
    static Font fm, fb;
    static Sprite S(string n) => AssetDatabase.LoadAssetAtPath<Sprite>($"Assets/Resources/Art/out/ui/{n}.png");
    static readonly Color INK = new Color32(0xec, 0xe4, 0xcc, 255), DIM = new Color32(0xa5, 0x9c, 0x84, 255), GOLD = new Color32(0xff, 0xcf, 0x4a, 255);

    [MenuItem("RatWar/메뉴 UI 만들기")]
    public static void Build()
    {
        var root = Object.FindObjectsByType<GameRoot>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault();
        fm = root ? root.fontMedium : null; fb = root ? root.fontBold : null;
        var canvas = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None).First(c => c.isRootCanvas && c.name == "Canvas").transform;
        var title = canvas.Find("Title");

        BuildTitle(title);
        int after = title.GetSiblingIndex() + 1; // 메뉴 창들은 타이틀 위 · 컷씬 · 툴팁 아래
        foreach (var n in new[] { "Slots", "Settings", "DiffPicker", "Pause", "Confirm" }) { var o = canvas.Find(n); if (o) Object.DestroyImmediate(o.gameObject); }
        var slots = BuildSlots(canvas); var settings = BuildSettings(canvas); var diff = BuildDiff(canvas); var pause = BuildPause(canvas); var confirm = BuildConfirm(canvas);
        foreach (var t in new[] { slots, diff, pause, settings, confirm }) { t.SetSiblingIndex(after++); t.gameObject.SetActive(false); }

        EditorSceneManager.MarkSceneDirty(title.gameObject.scene);
        EditorSceneManager.SaveScene(title.gameObject.scene);
        Debug.Log("[MenuBuilder] 메뉴 UI 만듦");
    }

    // ── 타이틀 = 메인메뉴: 왼쪽 악당 그림 유지, 오른쪽 로고(임시 글자) + 버튼 5개 ──
    static void BuildTitle(Transform title)
    {
        var panel = (RectTransform)title.Find("Panel");
        foreach (var n in new[] { "Name", "Story", "Sub", "Go", "Continue", "Logo", "Menu", "Version" }) { var o = panel.Find(n); if (o) Object.DestroyImmediate(o.gameObject); }
        panel.sizeDelta = new Vector2(1160, 760);
        var villain = (RectTransform)panel.Find("Villain");
        if (villain) { Center(villain); villain.anchoredPosition = new Vector2(-330, 30); villain.sizeDelta = new Vector2(420, 420); }

        // 로고 자리: 나중에 로고 그림(Resources/Art/out/ui/logo.png)을 넣으면 Image 가 켜지고 글자는 꺼짐 (TitleScreen)
        var logo = Node(panel, "Logo", new Vector2(220, 250), new Vector2(620, 170));
        var li = logo.gameObject.AddComponent<Image>(); li.preserveAspect = true; li.enabled = false;
        var lt = Txt(logo, "Text", "쥐들의 전쟁", 84, true, INK, Vector2.zero, new Vector2(620, 120));
        lt.gameObject.AddComponent<Outline>().effectColor = new Color(0, 0, 0, 0.7f);
        Txt(logo, "Sub", "RATS AT WAR", 22, false, DIM, new Vector2(0, -70), new Vector2(620, 30));

        var menu = Node(panel, "Menu", new Vector2(220, -120), new Vector2(420, 480));
        string[] ids = { "Continue", "New", "Load", "Settings", "Quit" }, labels = { "이어하기", "새로 하기", "불러오기", "환경설정", "종료" };
        // 이어하기 밑에 슬롯 요약 글씨 자리를 비워 둠 (나머지는 그만큼 아래로)
        for (int i = 0; i < ids.Length; i++) Btn(menu, ids[i], labels[i], new Vector2(0, i == 0 ? 200 : 160 - i * 90), new Vector2(400, 80), i == 0 ? "btn_hi" : "btn", 30);
        // 이어하기 아래 작은 글씨: 어느 슬롯인지
        Txt(menu.Find("Continue"), "Info", "", 17, false, DIM, new Vector2(0, -52), new Vector2(560, 24));
        Txt(panel, "Version", "", 16, false, DIM, new Vector2(500, -360), new Vector2(200, 24)).alignment = TextAnchor.MiddleRight;
    }

    static Transform BuildSlots(Transform canvas)
    {
        var box = Window(canvas, "Slots", new Vector2(1100, 720));
        Txt(box, "Title", "새로 하기", 44, true, INK, new Vector2(0, 300), new Vector2(900, 60));
        for (int i = 0; i < 3; i++)
        {
            var card = Img(box, $"Slot{i}", "panel", new Vector2(0, 160 - i * 170), new Vector2(960, 150), Image.Type.Sliced).transform;
            Txt(card, "Name", $"슬롯 {i + 1}", 32, true, GOLD, new Vector2(-300, 30), new Vector2(300, 44)).alignment = TextAnchor.MiddleLeft;
            Txt(card, "Info", "빈 슬롯", 21, false, INK, new Vector2(-130, -10), new Vector2(640, 30)).alignment = TextAnchor.MiddleLeft;
            Txt(card, "When", "", 17, false, DIM, new Vector2(-130, -42), new Vector2(640, 24)).alignment = TextAnchor.MiddleLeft;
            Btn(card, "Main", "여기서 시작", new Vector2(320, 18), new Vector2(240, 64), "btn_hi", 24);
            Btn(card, "Delete", "삭제", new Vector2(320, -42), new Vector2(240, 44), "btn_red", 18);
        }
        Btn(box, "Back", "뒤로", new Vector2(0, -300), new Vector2(240, 72), "btn", 26);
        return box.parent;
    }

    static Transform BuildSettings(Transform canvas)
    {
        var box = Window(canvas, "Settings", new Vector2(860, 600));
        Txt(box, "Title", "환경설정", 44, true, INK, new Vector2(0, 230), new Vector2(700, 60));
        string[] ids = { "Master", "Music", "Sfx" }, labels = { "전체 음량", "배경음", "효과음" };
        for (int i = 0; i < 3; i++)
        {
            var row = Node(box, ids[i], new Vector2(0, 120 - i * 80), new Vector2(720, 60));
            Txt(row, "Label", labels[i], 26, false, INK, new Vector2(-250, 0), new Vector2(220, 50)).alignment = TextAnchor.MiddleLeft;
            var sl = DefaultControls.CreateSlider(new DefaultControls.Resources { background = S("bar"), standard = S("bar_fill"), knob = S("ring") });
            sl.name = "Slider"; sl.transform.SetParent(row, false);
            var srt = (RectTransform)sl.transform; Center(srt); srt.anchoredPosition = new Vector2(40, 0); srt.sizeDelta = new Vector2(360, 26);
            var hs = (RectTransform)sl.transform.Find("Handle Slide Area/Handle"); hs.sizeDelta = new Vector2(34, 0);
            Txt(row, "Value", "100", 24, false, GOLD, new Vector2(300, 0), new Vector2(90, 50)).alignment = TextAnchor.MiddleRight;
        }
        var lang = Node(box, "Lang", new Vector2(0, -130), new Vector2(720, 60));
        Txt(lang, "Label", "언어", 26, false, INK, new Vector2(-250, 0), new Vector2(220, 50)).alignment = TextAnchor.MiddleLeft;
        Btn(lang, "Prev", "◀", new Vector2(-100, 0), new Vector2(64, 56), "btn", 24);
        Txt(lang, "Value", "한국어", 24, false, GOLD, new Vector2(80, 0), new Vector2(280, 50));
        Btn(lang, "Next", "▶", new Vector2(260, 0), new Vector2(64, 56), "btn", 24);
        Txt(box, "Note", "", 17, false, DIM, new Vector2(0, -185), new Vector2(720, 26));
        Btn(box, "Close", "닫기", new Vector2(0, -245), new Vector2(240, 72), "btn_hi", 26);
        return box.parent;
    }

    static Transform BuildPause(Transform canvas)
    {
        var box = Window(canvas, "Pause", new Vector2(560, 600));
        Txt(box, "Title", "일시정지", 44, true, INK, new Vector2(0, 230), new Vector2(480, 60));
        string[] ids = { "Resume", "Settings", "ToMenu", "Quit" }, labels = { "계속", "환경설정", "저장하고 메인메뉴", "저장하고 종료" };
        for (int i = 0; i < ids.Length; i++) Btn(box, ids[i], labels[i], new Vector2(0, 120 - i * 90), new Vector2(400, 76), i == 0 ? "btn_hi" : "btn", 26);
        Txt(box, "Note", "", 18, false, DIM, new Vector2(0, -240), new Vector2(480, 50));
        return box.parent;
    }

    static Transform BuildConfirm(Transform canvas)
    {
        var box = Window(canvas, "Confirm", new Vector2(720, 340));
        Txt(box, "Message", "", 26, false, INK, new Vector2(0, 50), new Vector2(620, 160));
        Btn(box, "Yes", "예", new Vector2(140, -105), new Vector2(240, 72), "btn_hi", 26);
        Btn(box, "No", "아니오", new Vector2(-140, -105), new Vector2(240, 72), "btn", 26);
        return box.parent;
    }

    // 난이도 카드 4장 (DiffPicker 가 글을 채움)
    static Transform BuildDiff(Transform canvas)
    {
        var dim = Node(canvas, "DiffPicker", Vector2.zero, Vector2.zero); Stretch(dim);
        dim.gameObject.AddComponent<Image>().color = new Color(0.05f, 0.04f, 0.03f, 0.985f);
        Txt(dim, "Title", "새 전쟁 — 난이도", 52, true, INK, new Vector2(0, 450), new Vector2(1400, 80));
        Txt(dim, "Sub", "전쟁을 시작하면 끝날 때까지 바꿀 수 없습니다", 24, false, DIM, new Vector2(0, 395), new Vector2(1400, 40));
        const float W = 400, H = 680, GAP = 26;
        for (int i = 0; i < 4; i++)
        {
            var card = Img(dim, $"Card{i}", "panel_sq", new Vector2(-1.5f * (W + GAP) + i * (W + GAP), 0), new Vector2(W, H), Image.Type.Sliced).transform;
            card.gameObject.AddComponent<Button>();
            var ol = card.gameObject.AddComponent<Outline>(); ol.effectColor = GOLD; ol.effectDistance = new Vector2(4, -4);
            Txt(card, "Name", "", 40, true, GOLD, new Vector2(0, 262), new Vector2(W - 100, 56));
            Txt(card, "Tag", "", 21, false, DIM, new Vector2(0, 218), new Vector2(W - 100, 32));
            var b = Txt(card, "Body", "", 21, false, INK, new Vector2(0, -50), new Vector2(W - 110, 470));
            b.alignment = TextAnchor.UpperLeft; b.lineSpacing = 1.1f;
        }
        Btn(dim, "Back", "뒤로", new Vector2(-240, -440), new Vector2(220, 84), "btn", 28);
        Btn(dim, "Start", "전쟁 시작", new Vector2(150, -440), new Vector2(480, 84), "btn_hi", 30);
        return dim;
    }

    // ── 도우미 ──
    // 화면 전체 어둡게 (뒤 클릭 막음) + 가운데 상자. 반환 = 상자
    static RectTransform Window(Transform canvas, string name, Vector2 size)
    {
        var dim = Node(canvas, name, Vector2.zero, Vector2.zero); Stretch(dim);
        dim.gameObject.AddComponent<Image>().color = new Color(0, 0, 0, 0.72f);
        return Img(dim, "Box", "panel_sq", Vector2.zero, size, Image.Type.Sliced).rectTransform;
    }
    static RectTransform Node(Transform parent, string name, Vector2 pos, Vector2 size)
    {
        var g = new GameObject(name, typeof(RectTransform));
        var r = (RectTransform)g.transform; r.SetParent(parent, false); Center(r);
        r.anchoredPosition = pos; r.sizeDelta = size; g.layer = parent.gameObject.layer;
        return r;
    }
    static Image Img(Transform parent, string name, string sprite, Vector2 pos, Vector2 size, Image.Type type)
    {
        var im = Node(parent, name, pos, size).gameObject.AddComponent<Image>();
        im.sprite = S(sprite); im.type = type;
        return im;
    }
    static Text Txt(Transform parent, string name, string s, int size, bool bold, Color c, Vector2 pos, Vector2 box)
    {
        var t = Node(parent, name, pos, box).gameObject.AddComponent<Text>();
        t.font = bold ? fb : fm; t.fontSize = size; t.color = c; t.text = s;
        t.alignment = TextAnchor.MiddleCenter; t.supportRichText = true; t.raycastTarget = false;
        t.horizontalOverflow = HorizontalWrapMode.Wrap; t.verticalOverflow = VerticalWrapMode.Overflow;
        return t;
    }
    static Button Btn(Transform parent, string name, string label, Vector2 pos, Vector2 size, string sprite, int fontSize)
    {
        var im = Img(parent, name, sprite, pos, size, Image.Type.Sliced);
        var b = im.gameObject.AddComponent<Button>();
        var t = Txt(im.transform, "Text", label, fontSize, true, INK, Vector2.zero, size - new Vector2(30, 8));
        t.gameObject.AddComponent<Shadow>().effectColor = new Color(0, 0, 0, 0.6f);
        return b;
    }
    static void Center(RectTransform r) { r.anchorMin = r.anchorMax = r.pivot = new Vector2(0.5f, 0.5f); }
    static void Stretch(RectTransform r) { r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = r.offsetMax = Vector2.zero; }
}

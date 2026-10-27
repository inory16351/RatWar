// 그림 묶음: Resources/Art 아래 PNG(스프라이트) + 웹에서 쓰던 표(assets.json · manifest.json · rig.json)
// 웹 경로 '../art/out/props/S.png' → Resources 경로 'Art/out/props/S'
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;

// 판때기 그림 하나: 원본 px 크기 + 발 밑(피벗) 자리
public class ArtItem { public Sprite sprite; public float w, h, px, py, tiles; }

public class RigPart { public string name, parent; public Vector2 pivot; public float x, y, w, h; public int faces; public Sprite img; public Sprite[] faceImgs; }
public class RigComposite { public Sprite img; public float x, y, w, h; public Vector2 neck; public string headView; }
public class RigView
{
    public float w, h; public Vector2 ground; public List<string> order = new List<string>();
    public Dictionary<string, RigPart> parts = new Dictionary<string, RigPart>();
    public RigComposite crouch; public RigComposite[] throwFrames;
}
public class RigData { public string name, kind; public Dictionary<string, RigView> views = new Dictionary<string, RigView>(); }

public static class ArtLib
{
    static JObject assets, manifest;
    static readonly Dictionary<string, Sprite> sprites = new Dictionary<string, Sprite>();
    static readonly Dictionary<string, RigData> rigs = new Dictionary<string, RigData>();

    public static Sprite Sprite(string path)
    {
        if (sprites.TryGetValue(path, out var s)) return s;
        string p = "Art/" + path;
        int dot = p.LastIndexOf('.'); if (dot > p.LastIndexOf('/')) p = p.Substring(0, dot);
        s = Resources.Load<Sprite>(p);
        if (s == null) Debug.LogWarning("그림 없음: " + p);
        return sprites[path] = s;
    }

    static JObject Json(string path) { var t = Resources.Load<TextAsset>("Art/" + path); return t ? JObject.Parse(t.text) : null; }
    static JObject Assets => assets ??= Json("out/assets");
    static JObject Manifest => manifest ??= Json("out/manifest");

    static ArtItem Item(JToken a, string baseDir)
    {
        if (a == null) return null;
        var pv = a["pivot"] as JArray;
        return new ArtItem
        {
            sprite = Sprite(baseDir + (string)a["img"]), w = (float)a["w"], h = (float)a["h"],
            px = pv != null ? (float)pv[0] : (float)a["w"] / 2, py = pv != null ? (float)pv[1] : (float)a["h"],
            tiles = a["tiles"] != null ? (float)a["tiles"] : 0,
        };
    }
    // group = rat · cat · props · weapons · terrain · backdrop
    public static ArtItem Asset(string group, string name) => Item(Assets[group]?[name], "out/");
    public static ArtItem Deco(string name) => Item(Manifest["deco"]?[name], "out/");
    public static IEnumerable<string> DecoNames() { foreach (var p in (JObject)Manifest["deco"]) yield return p.Key; }
    public static Sprite[] Fx(string name)
    {
        var arr = Manifest["fx"]?[name] as JArray;
        if (arr == null) return new Sprite[0];
        var o = new Sprite[arr.Count];
        for (int i = 0; i < arr.Count; i++) o[i] = Sprite("out/" + (string)arr[i]);
        return o;
    }
    public static Sprite Icon(string key)
    {
        var p = Manifest["icons"]?[key];
        return p != null ? Sprite("out/" + (string)p) : null;
    }

    static Vector2 V2(JToken t) => t is JArray a ? new Vector2((float)a[0], (float)a[1]) : Vector2.zero;
    static RigComposite Comp(JToken t, string dir)
    {
        if (t == null) return null;
        return new RigComposite { img = Sprite(dir + (string)t["img"]), x = (float)t["x"], y = (float)t["y"], w = (float)t["w"], h = (float)t["h"], neck = V2(t["neck"]), headView = (string)t["head"] };
    }

    // 리그 하나 (rat · rat_sniper · cat_officer …). 없으면 null
    public static RigData Rig(string name)
    {
        if (rigs.TryGetValue(name, out var r)) return r;
        var j = Json("rigs/" + name + "/rig");
        if (j == null) return rigs[name] = null;
        string dir = "rigs/" + name + "/";
        r = new RigData { name = name, kind = name.Split('_')[0] };
        foreach (var vp in (JObject)j["views"])
        {
            var V = vp.Value;
            var view = new RigView { w = (float)V["w"], h = (float)V["h"], ground = V2(V["ground"]) };
            foreach (var o in (JArray)V["order"]) view.order.Add((string)o);
            foreach (var pp in (JObject)V["parts"])
            {
                var P = pp.Value;
                var part = new RigPart
                {
                    name = pp.Key, parent = (string)P["parent"], pivot = V2(P["pivot"]),
                    x = (float)P["x"], y = (float)P["y"], w = (float)P["w"], h = (float)P["h"],
                    faces = P["faces"] != null ? (int)P["faces"] : 0, img = Sprite(dir + (string)P["img"]),
                };
                part.faceImgs = new Sprite[part.faces];
                for (int f = 0; f < part.faces; f++) part.faceImgs[f] = Sprite($"{dir}parts/{vp.Key}/head_f{f}.png");
                view.parts[pp.Key] = part;
            }
            view.crouch = Comp(V["crouch"], dir);
            if (V["throw"] is JArray th) { view.throwFrames = new RigComposite[th.Count]; for (int i = 0; i < th.Count; i++) view.throwFrames[i] = Comp(th[i], dir); }
            r.views[vp.Key] = view;
        }
        return rigs[name] = r;
    }

    // 유닛이 쓸 리그 (웹 view.js rigOf): 쥐 = rat_<병종>(신병은 rat), 고양이 = cat_<병종>(없으면 cat)
    // 장비 버전: 스킬로 생긴 장비가 먼저 (공병 + 화염방사기 = rat_engineer_flame), 아니면 개체마다 정해진 버전 gear
    public static RigData RigFor(bool rat, string cls, int gear, bool hasFlame)
    {
        string kind = rat ? "rat" : "cat";
        var b = BaseRig(rat, cls);
        if (!rat) return b;
        if (cls == "engineer" && hasFlame && Rig("rat_engineer_flame") != null) return Rig("rat_engineer_flame");
        return (gear > 0 ? Rig($"{kind}_{cls}_v{gear}") : null) ?? b;
    }
    public static RigData BaseRig(bool rat, string cls) { string kind = rat ? "rat" : "cat"; return Rig(kind + "_" + cls) ?? Rig(kind); }
}

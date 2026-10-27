// 틀(템플릿) 복사: 런타임에 생기는 오브젝트는 전부 하이라키에 있는 원본 틀을 복사해서 만듦
// 월드 틀 = 씬의 'Templates' 아래, UI 틀 = 'Canvas/UITemplates' 아래 (둘 다 꺼 둔 상태)
using System.Collections.Generic;
using UnityEngine;

public static class Tpl
{
    static readonly Dictionary<string, GameObject> cache = new Dictionary<string, GameObject>();

    public static GameObject Get(string name)
    {
        if (cache.TryGetValue(name, out var g) && g) return g;
        foreach (var root in new[] { "Templates", "Canvas/UITemplates" })
        {
            var r = GameObject.Find(root) ?? FindInactive(root);
            var t = r ? r.transform.Find(name) : null;
            if (t) return cache[name] = t.gameObject;
        }
        Debug.LogError("틀 없음: " + name);
        return null;
    }

    // 꺼 둔 루트도 찾기 (GameObject.Find 는 켜진 것만)
    static GameObject FindInactive(string path)
    {
        var parts = path.Split('/');
        foreach (var go in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
        {
            if (go.name != parts[0]) continue;
            if (parts.Length == 1) return go;
            var t = go.transform.Find(string.Join("/", parts, 1, parts.Length - 1));
            if (t) return t.gameObject;
        }
        return null;
    }

    // 틀을 복사해 parent 아래 켠 상태로
    public static GameObject Spawn(string name, Transform parent)
    {
        var t = Get(name);
        var g = Object.Instantiate(t, parent, false);
        g.name = name;
        g.SetActive(true);
        return g;
    }
    public static T Spawn<T>(string name, Transform parent) where T : Component => Spawn(name, parent).GetComponent<T>();

    // 자식 전부 지우기 (복사본만 담는 그릇을 비울 때)
    public static void Clear(Transform t)
    {
        for (int i = t.childCount - 1; i >= 0; i--)
        {
            var c = t.GetChild(i).gameObject;
            c.SetActive(false); // Destroy 는 프레임 끝에 → 그 전에 레이아웃에서 빠지게
            c.transform.SetParent(null, false);
            Object.Destroy(c);
        }
    }
}

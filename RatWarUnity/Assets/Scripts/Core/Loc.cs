// 글자 테이블: Resources/Tables/<언어>/<묶음>.json = { "키": "문장" }
// 코드는 Loc.T("키") · Loc.F("키", 값0, 값1…) 로 읽음. 지금 언어에 없으면 한국어, 그것도 없으면 키 그대로
// 문장 안 자리: {0} {1}… · 숫자 모양 {0:0.#} · 한국어 조사 {0:은} {0:이} {0:을} {0:과} {0:으로} {0:이라} (다른 언어에선 조사만 빠짐)
// 장면에 박힌 글자는 LocText 컴포넌트가 키로 바꿔 끼움
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using UnityEngine;

public static class Loc
{
    public static readonly string[] CODES = { "ko", "en", "ja", "zh", "ru", "fr", "de", "es" };
    public static readonly string[] NAMES = { "한국어", "English", "日本語", "简体中文", "Русский", "Français", "Deutsch", "Español" };

    static Dictionary<string, string> ko, cur;
    static string curCode;
    public static event Action Changed;

    public static string Code => CODES[Mathf.Clamp(Settings.Lang, 0, CODES.Length - 1)];

    static Dictionary<string, string> LoadLang(string code)
    {
        var d = new Dictionary<string, string>();
        foreach (var ta in Resources.LoadAll<TextAsset>("Tables/" + code))
        {
            var part = Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, string>>(ta.text);
            foreach (var kv in part) d[kv.Key] = kv.Value;
        }
        return d;
    }

    static void Ensure()
    {
        if (ko == null) ko = LoadLang("ko");
        if (curCode != Code) { curCode = Code; cur = curCode == "ko" ? ko : LoadLang(curCode); }
    }

    // 언어를 바꾼 뒤 부름: 장면 글자 · 열린 화면이 다시 그려짐
    public static void Reload() { curCode = null; Ensure(); Changed?.Invoke(); }

    public static bool Has(string key) { Ensure(); return cur.ContainsKey(key) || ko.ContainsKey(key); }

    public static string T(string key)
    {
        Ensure();
        if (cur.TryGetValue(key, out var s) || ko.TryGetValue(key, out s)) return s;
        return key;
    }

    public static string F(string key, params object[] args) => Format(T(key), args);

    static readonly Regex SLOT = new Regex(@"\{(\d+)(?::([^}]+))?\}");
    static readonly HashSet<string> JOSA = new HashSet<string> { "은", "는", "이", "가", "을", "를", "과", "와", "으로", "로", "이라", "라", "아", "야" };

    public static string Format(string s, params object[] args)
    {
        return SLOT.Replace(s, m =>
        {
            int i = int.Parse(m.Groups[1].Value);
            if (i >= args.Length) return m.Value;
            object a = args[i];
            string spec = m.Groups[2].Success ? m.Groups[2].Value : null;
            if (spec != null && JOSA.Contains(spec))
            {
                string w = Convert.ToString(a, CultureInfo.InvariantCulture);
                return Code == "ko" ? w + Josa(w, spec) : w;
            }
            if (spec != null && a is IFormattable f) return f.ToString(spec, CultureInfo.InvariantCulture);
            return Convert.ToString(a, CultureInfo.InvariantCulture);
        });
    }

    // 받침 따라 조사 고르기. 태그(<b> 등)는 건너뛰고 마지막 글자를 봄
    public static string Josa(string word, string j)
    {
        string plain = Regex.Replace(word ?? "", "<[^>]*>", "").TrimEnd();
        char c = plain.Length > 0 ? plain[plain.Length - 1] : 'a';
        bool hangul = c >= 0xAC00 && c <= 0xD7A3;
        int jong = hangul ? (c - 0xAC00) % 28 : (char.IsDigit(c) ? "013678".IndexOf(c) >= 0 ? 1 : 0 : 0);
        bool batchim = jong != 0, rieul = jong == 8 || (char.IsDigit(c) && (c == '1' || c == '7' || c == '8'));
        switch (j)
        {
            case "은": case "는": return batchim ? "은" : "는";
            case "이": case "가": return batchim ? "이" : "가";
            case "을": case "를": return batchim ? "을" : "를";
            case "과": case "와": return batchim ? "과" : "와";
            case "으로": case "로": return batchim && !rieul ? "으로" : "로";
            case "이라": case "라": return batchim ? "이라" : "라";
            case "아": case "야": return batchim ? "아" : "야";
        }
        return j;
    }
}

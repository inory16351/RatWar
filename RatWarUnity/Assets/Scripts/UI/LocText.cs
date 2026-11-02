// 장면에 박힌 글자를 테이블 키로 (언어 바꾸면 바로 바뀜). 코드가 덮어쓰는 글자엔 안 붙임
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Text))]
public class LocText : MonoBehaviour
{
    public string key;

    void OnEnable() { Apply(); Loc.Changed += Apply; }
    void OnDisable() { Loc.Changed -= Apply; }
    void Apply() { if (!string.IsNullOrEmpty(key) && Loc.Has(key)) GetComponent<Text>().text = Loc.T(key); }
}

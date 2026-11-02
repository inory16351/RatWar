// 장면에 박힌 글자를 테이블 키로 (언어 바꾸면 바로 바뀜). 코드가 덮어쓰는 글자엔 안 붙임
// owner = 붙일 때의 부모 이름. 코드가 버튼을 복제해 다른 글자를 쓰면(사령부 난이도 버튼 등) 부모 이름이 달라 건드리지 않음
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Text))]
public class LocText : MonoBehaviour
{
    public string key, owner;

    void OnEnable() { Apply(); Loc.Changed += Apply; }
    void OnDisable() { Loc.Changed -= Apply; }
    void Apply()
    {
        if (string.IsNullOrEmpty(key) || !Loc.Has(key)) return;
        if (!string.IsNullOrEmpty(owner) && (!transform.parent || transform.parent.name != owner)) return;
        GetComponent<Text>().text = Loc.T(key);
    }
}

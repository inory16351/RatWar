using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// 마우스를 올리면 툴팁 (롤식)
public class TipSource : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public string text;
    public System.Action onEnter;
    public void OnPointerEnter(PointerEventData e) { onEnter?.Invoke(); if (!string.IsNullOrEmpty(text)) Tooltip.I?.Show(text); }
    public void OnPointerExit(PointerEventData e) => Tooltip.I?.Hide();
    void OnDisable() { if (Tooltip.I && Tooltip.I.owner == this) Tooltip.I.Hide(); }
}

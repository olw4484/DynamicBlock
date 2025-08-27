using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public sealed class PanelToggleOnClick : MonoBehaviour, IPointerClickHandler
{
    [SerializeField] string key = "Options";
    [SerializeField] bool on = true;              // 열기 버튼이면 true, 닫기 버튼이면 false
    [SerializeField] float cooldown = 0.12f;

    float _cool;

    void Update()
    {
        if (_cool > 0f) _cool -= Time.unscaledDeltaTime; // 쿨다운 감소
    }

    public void OnPointerClick(PointerEventData _) => Invoke();

    public void Invoke()
    {
        if (_cool > 0f || !Game.IsBound || string.IsNullOrEmpty(key)) return;
        _cool = cooldown;

        var bus = Game.Bus;
        var evt = new PanelToggle(key, on);

        // 상태 저장 및 즉시 반영
        bus.PublishSticky(evt, alsoEnqueue: false);
        bus.PublishImmediate(evt);
        // 디버그
        // Debug.Log($"[Click] PanelToggle {key} -> {on}");
    }
}

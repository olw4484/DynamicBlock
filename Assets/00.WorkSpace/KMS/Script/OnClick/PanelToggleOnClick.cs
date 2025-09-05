using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public sealed class PanelToggleOnClick : MonoBehaviour, IPointerClickHandler
{
    [SerializeField] string key = "Options";
    [SerializeField] bool on = true;              // ���� ��ư�̸� true, �ݱ� ��ư�̸� false
    [SerializeField] float cooldown = 0.12f;

    float _cool;

    void Update()
    {
        if (_cool > 0f) _cool -= Time.unscaledDeltaTime; // ��ٿ� ����
    }

    public void OnPointerClick(PointerEventData _) => Invoke();

    public void Invoke()
    {
        if (_cool > 0f || !Game.IsBound || string.IsNullOrEmpty(key)) return;
        _cool = cooldown;

        var bus = Game.Bus;
        var evt = new PanelToggle(key, on);

        // ���� ���� �� ��� �ݿ�
        bus.PublishSticky(evt, alsoEnqueue: false);
        bus.PublishImmediate(evt);
        // �����
        // Debug.Log($"[Click] PanelToggle {key} -> {on}");
    }
}

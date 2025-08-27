using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public sealed class PanelSwitchOnClick : MonoBehaviour
{
    [SerializeField] string offKey = "Main";
    [SerializeField] string onKey = "Game";
    [SerializeField] float cooldown = 0.12f;
    [SerializeField] bool viaEvent = true; // true: PanelToggle 이벤트, false: 직접 SetPanel

    float _cool;

    void Update()
    {
        if (_cool > 0f) _cool -= Time.unscaledDeltaTime;
    }

    public void Invoke() // Button OnClick에 연결
    {
        Debug.Log($"[PanelSwitch] click, IsBound={Game.IsBound}, cool={_cool}");
        if (_cool > 0f || !Game.IsBound) return;
        if (string.IsNullOrEmpty(offKey) || string.IsNullOrEmpty(onKey)) return;
        if (offKey == onKey) return;

        _cool = cooldown;

        if (viaEvent)
        {
            Game.Bus.Publish(new PanelToggle(offKey, false));
            Game.Bus.Publish(new PanelToggle(onKey, true));
        }
        else
        {
            Game.UI.SetPanel(offKey, false);
            Game.UI.SetPanel(onKey, true);
        }
    }
}
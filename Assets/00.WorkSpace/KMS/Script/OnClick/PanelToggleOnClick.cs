using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public sealed class PanelToggleOnClick : MonoBehaviour, IPointerClickHandler
{
    [SerializeField] string key = "Options";
    [SerializeField] bool on = false;         // ¡ç ´Ý±â
    [SerializeField] float cooldown = 0.12f;
    float _cool;

    public void OnPointerClick(PointerEventData _) => Invoke();

    public void Invoke()
    {
        if (_cool > 0f || !Game.IsBound) return;
        _cool = cooldown;
        Game.Bus.Publish(new PanelToggle(key, on));
    }
}

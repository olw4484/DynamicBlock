using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public sealed class PanelToggleOnClick : MonoBehaviour, IPointerClickHandler
{
    [SerializeField] string key = "Options";
    [SerializeField] bool on = true;
    [SerializeField] float cooldown = 0.12f;

    float _cool;

    void Update()
    {
        if (_cool > 0f) _cool -= Time.unscaledDeltaTime;
    }

    public void OnPointerClick(PointerEventData _) => Invoke();

    public void Invoke()
    {
        if (_cool > 0f || !Game.IsBound || string.IsNullOrEmpty(key)) return;
        _cool = cooldown;

        if (Game.UI != null && Game.UI.TryGetPanelRoot(key, out var root))
        {
            if (root == this.gameObject)
            {
                Debug.LogError($"[UI] '{key}'의 root가 버튼 자신입니다: {root.name}. " +
                               $"UIManager Panels에서 root를 '옵션 창 루트(컨테이너)'로 다시 지정하세요.");
                return;
            }
        }

        Sfx.Button();
        Game.Bus.PublishImmediate(new PanelToggle(key, on));
    }
}

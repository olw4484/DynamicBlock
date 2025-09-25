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

    public void SetKey(string value)
    {
        key = value;
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
                Debug.LogError($"[UI] '{key}'�� root�� ��ư �ڽ��Դϴ�: {root.name}. " +
                               $"UIManager Panels���� root�� '�ɼ� â ��Ʈ(�����̳�)'�� �ٽ� �����ϼ���.");
                return;
            }
        }

        Sfx.Button();
        Game.Bus.PublishImmediate(new PanelToggle(key, on));
    }
}

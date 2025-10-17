using UnityEngine;
using UnityEngine.EventSystems;

public sealed class CloseTopModalOnClick : MonoBehaviour, IPointerClickHandler
{
    [SerializeField] string specificPanelKey; // ����θ� �ڵ� Ž��

    public void OnPointerClick(PointerEventData eventData)
    {
        if (!Game.IsBound || Game.UI == null) return;
        Sfx.Button();

        // 1) Ư�� Ű�� �����Ǿ� �ְ� ������ �����ϸ� �� �г� �ݱ�
        if (!string.IsNullOrEmpty(specificPanelKey) &&
            Game.UI.TryGetPanelRoot(specificPanelKey, out var __root)) // ���� ����
        {
            Game.UI.SetPanel(specificPanelKey, false, ignoreDelay: true);
            return;
        }

        // 2) �ڽ��� ���� �г� Ű�� ã�� �ݱ� (UIManager�� TryResolvePanelKeyByRoot ���� �ʿ�)
        if (Game.UI.TryResolvePanelKeyByRoot(gameObject, out var key))
        {
            Game.UI.SetPanel(key, false, ignoreDelay: true);
            return;
        }

        // 3) ���������� Top ��� �ݱ�
        Game.UI.CloseTopModal();
    }
}
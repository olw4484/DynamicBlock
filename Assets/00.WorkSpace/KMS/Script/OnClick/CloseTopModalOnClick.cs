using UnityEngine;
using UnityEngine.EventSystems;

public sealed class CloseTopModalOnClick : MonoBehaviour, IPointerClickHandler
{
    [SerializeField] string specificPanelKey; // 비워두면 자동 탐색

    public void OnPointerClick(PointerEventData eventData)
    {
        if (!Game.IsBound || Game.UI == null) return;
        Sfx.Button();

        // 1) 특정 키가 지정되어 있고 실제로 존재하면 그 패널 닫기
        if (!string.IsNullOrEmpty(specificPanelKey) &&
            Game.UI.TryGetPanelRoot(specificPanelKey, out var __root)) // 더미 변수
        {
            Game.UI.SetPanel(specificPanelKey, false, ignoreDelay: true);
            return;
        }

        // 2) 자신이 속한 패널 키를 찾아 닫기 (UIManager에 TryResolvePanelKeyByRoot 구현 필요)
        if (Game.UI.TryResolvePanelKeyByRoot(gameObject, out var key))
        {
            Game.UI.SetPanel(key, false, ignoreDelay: true);
            return;
        }

        // 3) 마지막으로 Top 모달 닫기
        Game.UI.CloseTopModal();
    }
}
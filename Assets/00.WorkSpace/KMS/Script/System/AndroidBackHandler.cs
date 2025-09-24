using UnityEngine;

public class AndroidBackHandler : MonoBehaviour
{
    [SerializeField] UIManager ui;
    [SerializeField] string gameOptionKey = "Game_Options";
    [SerializeField] string mainKey = "Main";
    [SerializeField] string exitConfirmKey = "ExitConfirm";

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            Debug.Log("[Back] ESC detected");
            HandleBack();
        }
    }

    void HandleBack()
    {
        if (!ui) { Debug.LogWarning("[Back] UIManager not set"); return; }

        // 1) 모달이 떠있으면 ESC 허용되는 모달만 닫기
        if (ui.TryCloseTopByEscape()) { Debug.Log("[Back] Closed top modal"); return; }

        // 2) 현재 메인/게임 어디인지 판단
        bool onGame = ui.TryGetPanelRoot("Game", out var gameRoot) && gameRoot.activeInHierarchy;
        bool onMain = ui.TryGetPanelRoot(mainKey, out var mainRoot) && mainRoot.activeInHierarchy;

        if (onGame)
        {
            Debug.Log("[Back] Open Game Options");
            ui.SetPanel(gameOptionKey, true);
            return;
        }

        if (onMain)
        {
            Debug.Log("[Back] Open ExitConfirm");
            ui.SetPanel(exitConfirmKey, true);
            return;
        }

        // 3) 상태 모르면 일단 종료 확인창
        Debug.Log("[Back] Fallback → ExitConfirm");
        ui.SetPanel(exitConfirmKey, true);
    }
}

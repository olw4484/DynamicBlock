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

        // 1) ����� �������� ESC ���Ǵ� ��޸� �ݱ�
        if (ui.TryCloseTopByEscape()) { Debug.Log("[Back] Closed top modal"); return; }

        // 2) ���� ����/���� ������� �Ǵ�
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

        // 3) ���� �𸣸� �ϴ� ���� Ȯ��â
        Debug.Log("[Back] Fallback �� ExitConfirm");
        ui.SetPanel(exitConfirmKey, true);
    }
}

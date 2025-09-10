using _00.WorkSpace.GIL.Scripts.Managers;
using UnityEngine;

public sealed class RestartOnClick : MonoBehaviour
{
    public enum RestartMode { SoftReset, ReloadSceneViaEvent, ReloadSceneDirect }

    [SerializeField] RestartMode mode = RestartMode.SoftReset;
    [SerializeField] string[] closePanels = { "Options", "GameOver" }; // �ʿ��
    [SerializeField] string openPanelAfter = "Game";    // SoftReset�� �� �� �г�
    [SerializeField] string gameplayScene = "Gameplay"; // �ϵ� ���� ��� ��
    [SerializeField] float cooldown = 0.12f;

    float _cool;

    void Update() { if (_cool > 0f) _cool -= Time.unscaledDeltaTime; }

    public void Invoke()
    {
        if (_cool > 0f || !Game.IsBound) return;
        _cool = cooldown;

        Sfx.Button();

        switch (mode)
        {
            case RestartMode.SoftReset:
                RestartFlow.SoftReset(openPanelAfter, closePanels);
                // GIL_Add
                // 지금은 소프트 리셋 시작이라 여기에 클래식 맵 생성 알고리즘 적용
                MapManager.Instance.GenerateClassicStartingMap(minTotalTiles: 30, maxPlacements: 8);
                break;
            case RestartMode.ReloadSceneViaEvent:
                RestartFlow.ReloadViaEvent(gameplayScene);
                break;
            case RestartMode.ReloadSceneDirect:
                RestartFlow.ReloadDirect(gameplayScene);
                break;
        }
    }
}
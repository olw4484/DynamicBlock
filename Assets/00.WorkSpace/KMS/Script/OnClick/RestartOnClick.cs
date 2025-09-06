using UnityEngine;

public sealed class RestartOnClick : MonoBehaviour
{
    public enum RestartMode { SoftReset, ReloadSceneViaEvent, ReloadSceneDirect }

    [SerializeField] RestartMode mode = RestartMode.SoftReset;
    [SerializeField] string[] closePanels = { "Options", "GameOver" }; // 필요시
    [SerializeField] string openPanelAfter = "Game";    // SoftReset일 때 열 패널
    [SerializeField] string gameplayScene = "Gameplay"; // 하드 리셋 대상 씬
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
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

        Time.timeScale = 1f;

        // 현재 떠있는 패널 정리
        foreach (var k in closePanels) Game.Bus.Publish(new PanelToggle(k, false));

        switch (mode)
        {
            case RestartMode.SoftReset:
                // Sticky GameOver 재생 방지
                Game.Bus.ClearSticky<GameOver>();
                // 게임 화면으로 스위치(원하면)
                if (!string.IsNullOrEmpty(openPanelAfter))
                    Game.Bus.Publish(new PanelToggle(openPanelAfter, true));
                // 각 매니저 ResetRuntime() 호출 트리거
                Game.Bus.Publish(new GameResetRequest());
                break;

            case RestartMode.ReloadSceneViaEvent:
                Game.Bus.ClearSticky<GameOver>();
                Game.Bus.Publish(new SceneChangeRequest(gameplayScene));
                break;

            case RestartMode.ReloadSceneDirect:
                Game.Bus.ClearSticky<GameOver>();
                Game.Scene.LoadScene(gameplayScene);
                break;
        }
    }
}
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

        foreach (var k in closePanels)
            Game.Bus.Publish(new PanelToggle(k, false));

        switch (mode)
        {
            case RestartMode.SoftReset:
                Game.Bus.ClearSticky<GameOver>();
                Game.Bus.PublishImmediate(new GameResetting());
                if (!string.IsNullOrEmpty(openPanelAfter))
                    Game.Bus.Publish(new PanelToggle(openPanelAfter, true));
                Game.Bus.PublishImmediate(new GameResetRequest());
                Game.Bus.PublishImmediate(new GameResetDone());
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
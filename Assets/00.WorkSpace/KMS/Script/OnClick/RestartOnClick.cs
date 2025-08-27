using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public sealed class RestartOnClick : MonoBehaviour
{
    [SerializeField] string[] closePanels = { "Options", "GameOver" };
    [SerializeField] string gameplayScene = "Gameplay";
    [SerializeField] float cooldown = 0.12f;
    [SerializeField] bool useSceneEvent = true; // true면 이벤트, false면 직접 로드
    float _cool;

    void Update() { if (_cool > 0f) _cool -= Time.unscaledDeltaTime; }

    public void Invoke()
    {
        if (_cool > 0f || !Game.IsBound) return;
        _cool = cooldown;

        foreach (var k in closePanels)
            Game.Bus.Publish(new PanelToggle(k, false));

        Time.timeScale = 1f;

        if (useSceneEvent)
            Game.Bus.Publish(new SceneChangeRequest(gameplayScene));
        else
            Game.Scene.LoadScene(gameplayScene);
    }
}
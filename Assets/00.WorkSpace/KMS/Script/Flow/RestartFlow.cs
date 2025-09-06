using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class RestartFlow
{
    public static void SoftReset(string openPanelAfter = "Game", string[] closePanels = null)
    {
        if (!Game.IsBound) return;

        Time.timeScale = 1f;

        // 닫을 패널(기본: Options, GameOver)
        if (closePanels == null || closePanels.Length == 0)
            closePanels = new[] { "Options", "GameOver" };
        foreach (var k in closePanels)
            Game.Bus.Publish(new PanelToggle(k, false));

        Game.Bus.ClearSticky<GameOver>();
        Game.Bus.PublishImmediate(new GameResetting());
        if (!string.IsNullOrEmpty(openPanelAfter))
            Game.Bus.Publish(new PanelToggle(openPanelAfter, true));
        Game.Bus.PublishImmediate(new GameResetRequest());
        Game.Bus.PublishImmediate(new GameResetDone());
    }

    public static void ReloadViaEvent(string gameplayScene)
    {
        if (!Game.IsBound) return;
        Game.Bus.ClearSticky<GameOver>();
        Game.Bus.Publish(new SceneChangeRequest(gameplayScene));
    }

    public static void ReloadDirect(string gameplayScene)
    {
        if (!Game.IsBound) return;
        Game.Bus.ClearSticky<GameOver>();
        Game.Scene.LoadScene(gameplayScene);
    }
}


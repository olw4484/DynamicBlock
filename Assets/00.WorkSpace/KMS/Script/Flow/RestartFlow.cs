using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class RestartFlow
{
    public static void SoftReset(string openPanelAfter = "Game", string[] closePanels = null)
    {
        if (!Game.IsBound) return;

        Time.timeScale = 1f;

        // 0) Main���� Ȯ���� �ݱ�
        Game.Bus.Publish(new PanelToggle("Main", false));
        Game.Bus.ClearSticky<PanelToggle>();

        // 1) ��Ÿ �г� �ݱ�
        if (closePanels == null || closePanels.Length == 0)
            closePanels = new[] { "Options", "GameOver" };
        foreach (var k in closePanels)
            Game.Bus.Publish(new PanelToggle(k, false));

        // 2) ���� �̺�Ʈ
        Game.Bus.ClearSticky<GameOver>();
        Game.Bus.PublishImmediate(new GameResetting());
        Game.Bus.PublishImmediate(new GameResetRequest());
        Game.Bus.PublishImmediate(new GameResetDone());

        // 3) ���� ������ ���� Game �Ѽ� ���� ���� �����
        CoroutineHost.Run(EnsureOpenNextFrame(openPanelAfter));
    }

    static IEnumerator EnsureOpenNextFrame(string key)
    {
        yield return new WaitForEndOfFrame ();
        Game.Bus.Publish(new PanelToggle(key, true));
        Game.UI?.ForceMainUIClean();
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


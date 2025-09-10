using System.Collections;
using System.Collections.Generic;
using _00.WorkSpace.GIL.Scripts.Managers;
using UnityEngine;

public sealed class PanelSwitchOnClick : MonoBehaviour
{
    [SerializeField] string targetPanel = "Game"; // "Game" or "Main"
    [SerializeField] bool closeModalFirst = true; // GameOver ���� ��� ���� ����
    [SerializeField] string[] modalsToClose = { "GameOver", "Option" };
    [SerializeField] float cooldown = 0.12f;

    float _cool;

    void Update() { if (_cool > 0f) _cool -= Time.unscaledDeltaTime; }

    public void Invoke()
    {
        if (_cool > 0f || !Game.IsBound) return;
        _cool = cooldown;

        var bus = Game.Bus;

        if (closeModalFirst && modalsToClose != null)
        {
            for (int i = 0; i < modalsToClose.Length; i++)
            {
                var k = modalsToClose[i];
                if (string.IsNullOrEmpty(k)) continue;
                var off = new PanelToggle(k, false);
                bus.PublishSticky(off, alsoEnqueue: false);
                bus.PublishImmediate(off);
                if (k == "GameOver") bus.ClearSticky<GameOver>();
            }
        }

        // ���� ���� + UI ��ȯ ��û(������)
        bus.PublishImmediate(new GameResetRequest(targetPanel));
        
        // GIL Add
        // 클래식 모드일 경우 맵 생성 알고리즘 작동.
        MapManager.Instance.GenerateClassicStartingMap();
    }
}

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public sealed class PanelSwitchOnClick : MonoBehaviour
{
    [SerializeField] string targetPanel = "Game"; // "Game" or "Main"
    [SerializeField] bool closeModalFirst = true; // GameOver 같은 모달 먼저 끄기
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

        // 전역 리셋 + UI 전환 요청(원자적)
        bus.PublishImmediate(new GameResetRequest(targetPanel));
    }
}

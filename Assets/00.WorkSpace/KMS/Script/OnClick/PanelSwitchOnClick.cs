using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public sealed class PanelSwitchOnClick : MonoBehaviour
{
    [SerializeField] string offKey = "GameOver";
    [SerializeField] string onKey = "Game";
    [SerializeField] float cooldown = 0.12f;
    [SerializeField] bool viaEvent = true;
    [SerializeField] bool softResetWhenTurnOn = true; // Classic 진입 시 리셋

    float _cool;

    void Update()
    {
        if (_cool > 0f) _cool -= Time.unscaledDeltaTime;
    }

    public void Invoke() // Button OnClick에 연결
    {
        if (_cool > 0f || !Game.IsBound) return;
        if (string.IsNullOrEmpty(offKey) && string.IsNullOrEmpty(onKey)) return;

        _cool = cooldown;

        if (viaEvent)
        {
            var bus = Game.Bus;

            // 1) 먼저 끄기 (상태 이벤트는 Sticky+Immediate)
            if (!string.IsNullOrEmpty(offKey))
            {
                var off = new PanelToggle(offKey, false);
                bus.PublishSticky(off, alsoEnqueue: false);
                bus.PublishImmediate(off);

                if (offKey == "GameOver")
                    bus.ClearSticky<GameOver>(); // 재등장 방지
            }

            // 2) 켜기
            if (!string.IsNullOrEmpty(onKey))
            {
                var on = new PanelToggle(onKey, true);
                bus.PublishSticky(on, alsoEnqueue: false);
                bus.PublishImmediate(on);
            }

            // 3) Game을 켰다면 소프트 리셋 파이프라인 실행
            if (softResetWhenTurnOn && onKey == "Game")
            {
                Time.timeScale = 1f;                  // 안전보정
                bus.PublishImmediate(new GameResetting());
                bus.PublishImmediate(new GameResetRequest());
                bus.PublishImmediate(new GameResetDone());
            }
        }
        else
        {
            // 상태를 남기지 않으므로 가급적 viaEvent 사용 권장
            if (!string.IsNullOrEmpty(offKey)) Game.UI.SetPanel(offKey, false);
            if (!string.IsNullOrEmpty(onKey)) Game.UI.SetPanel(onKey, true);

            if (softResetWhenTurnOn && onKey == "Game")
            {
                Time.timeScale = 1f;
                Game.Bus.PublishImmediate(new GameResetting());
                Game.Bus.PublishImmediate(new GameResetRequest());
                Game.Bus.PublishImmediate(new GameResetDone());
            }
        }
    }
}
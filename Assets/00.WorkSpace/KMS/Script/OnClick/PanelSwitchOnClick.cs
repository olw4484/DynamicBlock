using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public sealed class PanelSwitchOnClick : MonoBehaviour
{
    [SerializeField] string offKey = "GameOver";
    [SerializeField] string onKey = "Game";
    [SerializeField] float cooldown = 0.12f;
    [SerializeField] bool viaEvent = true;
    [SerializeField] bool softResetWhenTurnOn = true; // Classic ���� �� ����

    float _cool;

    void Update()
    {
        if (_cool > 0f) _cool -= Time.unscaledDeltaTime;
    }

    public void Invoke() // Button OnClick�� ����
    {
        if (_cool > 0f || !Game.IsBound) return;
        if (string.IsNullOrEmpty(offKey) && string.IsNullOrEmpty(onKey)) return;

        _cool = cooldown;

        if (viaEvent)
        {
            var bus = Game.Bus;

            // 1) ���� ���� (���� �̺�Ʈ�� Sticky+Immediate)
            if (!string.IsNullOrEmpty(offKey))
            {
                var off = new PanelToggle(offKey, false);
                bus.PublishSticky(off, alsoEnqueue: false);
                bus.PublishImmediate(off);

                if (offKey == "GameOver")
                    bus.ClearSticky<GameOver>(); // ����� ����
            }

            // 2) �ѱ�
            if (!string.IsNullOrEmpty(onKey))
            {
                var on = new PanelToggle(onKey, true);
                bus.PublishSticky(on, alsoEnqueue: false);
                bus.PublishImmediate(on);
            }

            // 3) Game�� �״ٸ� ����Ʈ ���� ���������� ����
            if (softResetWhenTurnOn && onKey == "Game")
            {
                Time.timeScale = 1f;                  // ��������
                bus.PublishImmediate(new GameResetting());
                bus.PublishImmediate(new GameResetRequest());
                bus.PublishImmediate(new GameResetDone());
            }
        }
        else
        {
            // ���¸� ������ �����Ƿ� ������ viaEvent ��� ����
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
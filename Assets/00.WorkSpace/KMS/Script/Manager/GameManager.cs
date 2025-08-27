using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// ================================
// Script : GameManager.cs
// Desc  : ������ �ҽ�(����/�޺�)
// ================================
public sealed class GameManager : IManager, IRuntimeReset
{
    public int Order => 10;
    private readonly EventQueue _bus;

    public int Score { get; private set; }
    public int Combo { get; private set; }

    public GameManager(EventQueue bus) => _bus = bus;

    public void PreInit() { }
    public void Init()
    {
        Score = 0;
        Combo = 0;

        // �ʱ� ���¸� Sticky�� �÷��θ� UI ���� ��� �����
        _bus.PublishSticky(new ScoreChanged(Score), alsoEnqueue: false);
        _bus.PublishSticky(new ComboChanged(Combo), alsoEnqueue: false);
    }
    public void PostInit() { }

    // �ܺ� API
    public void AddScore(int add)
    {
        Score += add;
        _bus.Publish(new ScoreChanged(Score));
    }
    public void SetCombo(int value)
    {
        Combo = value;
        _bus.Publish(new ComboChanged(Combo));
    }
    public void ResetRuntime()
    {
        Score = 0; Combo = 0;
        _bus.PublishSticky(new ScoreChanged(Score), alsoEnqueue: false);
        _bus.PublishSticky(new ComboChanged(Combo), alsoEnqueue: false);
        _bus.ClearSticky<GameOver>();
    }
}

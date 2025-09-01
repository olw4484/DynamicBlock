using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// ================================
// Script : GameManager.cs
// Desc  : 데이터 소스(점수/콤보)
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

        // 초기 상태를 Sticky로 올려두면 UI 구독 즉시 재생됨
        _bus.PublishSticky(new ScoreChanged(Score), alsoEnqueue: false);
        _bus.PublishSticky(new ComboChanged(Combo), alsoEnqueue: false);
    }
    public void PostInit()
    {
        _bus.Subscribe<GameResetRequest>(_ => ResetRuntime(), replaySticky: false);
    }

    // 외부 API
    public void ResetRuntime()
    {
        Score = 0; Combo = 0;
        _bus.PublishSticky(new ScoreChanged(Score), alsoEnqueue: false);
        _bus.PublishSticky(new ComboChanged(Combo), alsoEnqueue: false);
    }
}

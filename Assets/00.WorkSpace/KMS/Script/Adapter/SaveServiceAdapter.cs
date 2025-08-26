using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// ================================
// Script  : SaveServiceAdapter.cs
// Desc    : 세이브 어댑터
// ================================
public sealed class SaveServiceAdapter : IManager, ISaveService
{
    public int Order => 40;

    private EventQueue _bus;
    private SaveManager _legacy; // 원본 MonoBehaviour
    private const int DefaultStages = 200;

    public GameData Data => _legacy.gameData;

    public void SetDependencies(EventQueue bus, SaveManager legacy)
    {
        _bus = bus;
        _legacy = legacy;
    }

    public void PreInit()
    {
        // 원본 Awake에서 LoadGame 호출됨 → 초기 Sticky 보강
        _bus.PublishSticky(new GameDataChanged(Data), alsoEnqueue: false);

        // 원본 콜백과 연결 → 상태 바뀔 때마다 Sticky 통지
        _legacy.AfterLoad += d => _bus.PublishSticky(new GameDataChanged(d), alsoEnqueue: false);
        _legacy.AfterSave += d => _bus.PublishSticky(new GameDataChanged(d), alsoEnqueue: false);
    }

    public void Init() { }

    public void PostInit()
    {
        // 명령 이벤트 → 원본 API로 라우팅
        _bus.Subscribe<SaveRequested>(_ => _legacy.SaveGame(), replaySticky: false);
        _bus.Subscribe<LoadRequested>(_ => _legacy.LoadGame(), replaySticky: false);
        _bus.Subscribe<ResetRequested>(_ =>
        {
            _legacy.gameData = GameData.NewDefault(DefaultStages);
            _legacy.SaveGame();
        }, replaySticky: false);
    }

    // ISaveService 직접 호출 경로(옵션)
    public bool LoadOrCreate() { _legacy.LoadGame(); return true; }
    public void Save() { _legacy.SaveGame(); }
    public void ResetData() { _legacy.gameData = GameData.NewDefault(DefaultStages); _legacy.SaveGame(); }
}

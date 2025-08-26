using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// ================================
// Script  : SaveServiceAdapter.cs
// Desc    : ���̺� �����
// ================================
public sealed class SaveServiceAdapter : IManager, ISaveService
{
    public int Order => 40;

    private EventQueue _bus;
    private SaveManager _legacy; // ���� MonoBehaviour
    private const int DefaultStages = 200;

    public GameData Data => _legacy.gameData;

    public void SetDependencies(EventQueue bus, SaveManager legacy)
    {
        _bus = bus;
        _legacy = legacy;
    }

    public void PreInit()
    {
        // ���� Awake���� LoadGame ȣ��� �� �ʱ� Sticky ����
        _bus.PublishSticky(new GameDataChanged(Data), alsoEnqueue: false);

        // ���� �ݹ�� ���� �� ���� �ٲ� ������ Sticky ����
        _legacy.AfterLoad += d => _bus.PublishSticky(new GameDataChanged(d), alsoEnqueue: false);
        _legacy.AfterSave += d => _bus.PublishSticky(new GameDataChanged(d), alsoEnqueue: false);
    }

    public void Init() { }

    public void PostInit()
    {
        // ��� �̺�Ʈ �� ���� API�� �����
        _bus.Subscribe<SaveRequested>(_ => _legacy.SaveGame(), replaySticky: false);
        _bus.Subscribe<LoadRequested>(_ => _legacy.LoadGame(), replaySticky: false);
        _bus.Subscribe<ResetRequested>(_ =>
        {
            _legacy.gameData = GameData.NewDefault(DefaultStages);
            _legacy.SaveGame();
        }, replaySticky: false);
    }

    // ISaveService ���� ȣ�� ���(�ɼ�)
    public bool LoadOrCreate() { _legacy.LoadGame(); return true; }
    public void Save() { _legacy.SaveGame(); }
    public void ResetData() { _legacy.gameData = GameData.NewDefault(DefaultStages); _legacy.SaveGame(); }
}

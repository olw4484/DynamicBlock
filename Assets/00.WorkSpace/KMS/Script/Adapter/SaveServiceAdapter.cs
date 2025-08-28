using System;
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
        // ���� �ݹ�� ���� �� ���� �ٲ� ������ Sticky ����
        _legacy.AfterLoad += d => _bus.PublishSticky(new GameDataChanged(d), alsoEnqueue: false);
        _legacy.AfterSave += d => _bus.PublishSticky(new GameDataChanged(d), alsoEnqueue: false);

        if (_legacy.gameData == null)
            _legacy.LoadGame();

        TryMigrateLegacyLanguage();

        // ���� Awake���� LoadGame ȣ��� �� �ʱ� Sticky ����
        _bus.PublishSticky(new GameDataChanged(Data), alsoEnqueue: false);
    }

    public void Init() { }

    public void PostInit()
    {
        _bus.Subscribe<SaveRequested>(_ => _legacy.SaveGame(), replaySticky: false);
        _bus.Subscribe<LoadRequested>(_ => _legacy.LoadGame(), replaySticky: false);
        _bus.Subscribe<ResetRequested>(_ =>
        {
            _legacy.gameData = GameData.NewDefault(DefaultStages);
            _legacy.SaveGame();
        }, replaySticky: false);

        // �ΰ��� ���� ���� ��: �ְ��� ��ŵǸ� ��� ����
        _bus.Subscribe<ScoreChanged>(e =>
        {
            // ��ٿ���� �ʿ��ϸ� ���⼭ ���� �߰� ����
            _legacy.TryUpdateHighScore(e.value);
        }, replaySticky: true);

        // ������: ���ӿ��� ���� ���� ���(�÷���Ƚ��/��Ʈ���ھ�/���̽��ھ� ����)
        _bus.Subscribe<GameOver>(e =>
        {
            _legacy.UpdateClassicScore(e.score);
        }, replaySticky: false);
    }

    private void TryMigrateLegacyLanguage()
    {
#if UNITY_EDITOR
        string legacyPath = System.IO.Path.Combine(Application.dataPath, "00.WorkSpace/SJH/SaveFile/SaveData.json");
#else
    string legacyPath = System.IO.Path.Combine(Application.persistentDataPath, "SaveFile/SaveData.json");
#endif
        try
        {
            if (!System.IO.File.Exists(legacyPath)) return;

            string json = System.IO.File.ReadAllText(legacyPath);
            SJH.GameData legacy = JsonUtility.FromJson<SJH.GameData>(json);
            if (legacy == null) return;

            // ���� �ٸ��� �ݿ��ϰ� ����
            if (_legacy.gameData == null) _legacy.gameData = GameData.NewDefault();
            if (_legacy.gameData.LanguageIndex != legacy.LanguageIndex)
            {
                _legacy.gameData.LanguageIndex = legacy.LanguageIndex;
                _legacy.SaveGame();
            }

            // �縶�̱׷��̼� ������ ���
            string bak = legacyPath + ".bak";
            if (System.IO.File.Exists(bak)) System.IO.File.Delete(bak);
            System.IO.File.Move(legacyPath, bak);
#if UNITY_EDITOR
            Debug.Log($"[SaveServiceAdapter] Migrated LanguageIndex={legacy.LanguageIndex} from legacy file.");
#endif
        }
        catch (Exception ex) { Debug.LogException(ex); }
    }

    public void SetLanguageIndex(int index)
    {
        if (_legacy.gameData == null) _legacy.LoadGame();
        if (_legacy.gameData.LanguageIndex == index) return;

        _legacy.gameData.LanguageIndex = index;

        // UI/���ö����� ��� �ݿ�
        _bus.PublishSticky(new GameDataChanged(_legacy.gameData), alsoEnqueue: false);

        // ��ũ �ݿ�
        _legacy.SaveGame();
    }

    // ISaveService ���� ȣ�� ���(�ɼ�)
    public bool LoadOrCreate() { _legacy.LoadGame(); return true; }
    public void Save() { _legacy.SaveGame(); }
    public void ResetData() { _legacy.gameData = GameData.NewDefault(DefaultStages); _legacy.SaveGame(); }
}

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using _00.WorkSpace.GIL.Scripts.Messages;

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

    private bool _newBestThisRun = false;

    public void SetDependencies(EventQueue bus, SaveManager legacy)
    {
        _bus = bus;
        _legacy = legacy;
    }

    public void PreInit()
    {
        _legacy.AfterLoad += PublishState;
        _legacy.AfterSave += PublishState;

        if (_legacy.gameData == null)
            _legacy.LoadGame();

        TryMigrateLegacyLanguage();

        PublishState(_legacy.gameData);
    }

    public void Init() { }

    public void PostInit()
    {
        _bus.Subscribe<GameResetRequest>(_ => { _newBestThisRun = false; }, replaySticky: false);
        _bus.Subscribe<GameResetting>(_ => { _newBestThisRun = false; }, replaySticky: false);
        _bus.Subscribe<ResetRequested>(_ => {
            _legacy.gameData = GameData.NewDefault(DefaultStages);
            _legacy.SaveGame();
        }, replaySticky: false);

        _bus.Subscribe<GameOverConfirmed>(e =>
        {
            _legacy.UpdateClassicScore(e.score);

            if (e.isNewBest) { Game.Fx.PlayNewScoreAt(); Sfx.NewRecord(); }
            else { Game.Fx.PlayGameOverAt(); Sfx.GameOver(); }

            Debug.Log($"[SaveAdapter] FINAL total={e.score}, persistedHigh={_legacy.gameData?.highScore}");
        }, replaySticky: false);

        _bus.Subscribe<LanguageChangeRequested>(e =>
        {
            SetLanguageIndex(e.index);
        }, replaySticky: false);

        _bus.Subscribe<AllClear>(e =>
        {
            Debug.Log("[SaveAdapter] ALL CLEAR!");
            //AllClearCount++;
        }, replaySticky: false);
    }

    public int CurrentHighScore => _legacy?.gameData?.highScore ?? 0;

    public void EnsureLoaded()
    {
        if (_legacy.gameData == null)
            _legacy.LoadGame();
    }

    public int GetPersistedHighScoreFresh()
    {
        _legacy.LoadGame(); // AfterLoad�� UI ���� �̺�Ʈ ���� �� ���� (����)
        return _legacy.gameData?.highScore ?? 0;
    }


    public bool TryUpdateHighScore(int score, bool save = true)
    => _legacy.TryUpdateHighScore(score, save);
    public void UpdateClassicScore(int score)
        => _legacy.UpdateClassicScore(score);

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

        // ��� UI �ݿ�
        PublishState(_legacy.gameData);

        _legacy.SaveGame(); // ��ũ �ݿ�(AfterSave���� �ٽ� PublishState ȣ���)
    }

    private void PublishState(GameData d)
    {
        var evt = new GameDataChanged(d);
        _bus.PublishSticky(evt, alsoEnqueue: false); // ���� ĳ��
        _bus.PublishImmediate(evt);                  // ��� �ݿ�
        Debug.Log($"[SaveAdapter] PublishState high={d?.highScore}");
    }

    // ISaveService ���� ȣ�� ���(�ɼ�)
    public bool LoadOrCreate() { _legacy.LoadGame(); return true; }
    public void Save() { _legacy.SaveGame(); }
    public void ResetData() { _legacy.gameData = GameData.NewDefault(DefaultStages); _legacy.SaveGame(); }
}

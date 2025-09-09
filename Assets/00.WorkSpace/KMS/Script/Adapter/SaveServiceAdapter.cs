using System;
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
        _bus.Subscribe<SaveRequested>(_ => _legacy.SaveGame(), replaySticky: false);
        _bus.Subscribe<LoadRequested>(_ => _legacy.LoadGame(), replaySticky: false);
        _bus.Subscribe<ResetRequested>(_ => {
            _legacy.gameData = GameData.NewDefault(DefaultStages);
            _legacy.SaveGame();
        }, replaySticky: false);

        _bus.Subscribe<ScoreChanged>(e =>
        {
            // 새 기록이면 바로 반영 + 저장
            if (_legacy.gameData == null) _legacy.LoadGame();

            if (e.value > _legacy.gameData.highScore)
            {
                _legacy.gameData.highScore = e.value;
                _legacy.SaveGame();          
                Debug.Log($"[SaveAdapter] NEW HIGH SCORE {e.value} (saved)");
            }
            else
            {
                Debug.Log($"[SaveAdapter] ScoreChanged={e.value}, High={_legacy.gameData.highScore}");
            }
        }, replaySticky: true);

        _bus.Subscribe<GameOver>(e =>
        {
            _legacy.UpdateClassicScore(e.score);
            if (_legacy.gameData != null && e.score > _legacy.gameData.highScore)
            {
                _legacy.gameData.highScore = e.score;
                _legacy.SaveGame();

                Game.Fx.PlayNewScore();
            }
            else
            {
                Game.Fx.PlayGameOver();
            }
            Debug.Log($"[SaveAdapter] GameOver total={e.score}, High={_legacy.gameData?.highScore}");
        }, replaySticky: false);

        _bus.Subscribe<LanguageChangeRequested>(e =>
        {
            SetLanguageIndex(e.index);
        }, replaySticky: false);

        _bus.Subscribe<AllClear>(e =>
        {
            Game.Fx.PlayAllClear();
            Debug.Log("[SaveAdapter] ALL CLEAR!");
            //AllClearCount++;
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

            // 값이 다르면 반영하고 저장
            if (_legacy.gameData == null) _legacy.gameData = GameData.NewDefault();
            if (_legacy.gameData.LanguageIndex != legacy.LanguageIndex)
            {
                _legacy.gameData.LanguageIndex = legacy.LanguageIndex;
                _legacy.SaveGame();
            }

            // 재마이그레이션 방지용 백업
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

        // 즉시 UI 반영
        PublishState(_legacy.gameData);

        _legacy.SaveGame(); // 디스크 반영(AfterSave에서 다시 PublishState 호출됨)
    }

    private void PublishState(GameData d)
    {
        var evt = new GameDataChanged(d);
        _bus.PublishSticky(evt, alsoEnqueue: false); // 상태 캐시
        _bus.PublishImmediate(evt);                  // 즉시 반영
        Debug.Log($"[SaveAdapter] PublishState high={d?.highScore}");
    }

    // ISaveService 직접 호출 경로(옵션)
    public bool LoadOrCreate() { _legacy.LoadGame(); return true; }
    public void Save() { _legacy.SaveGame(); }
    public void ResetData() { _legacy.gameData = GameData.NewDefault(DefaultStages); _legacy.SaveGame(); }
}

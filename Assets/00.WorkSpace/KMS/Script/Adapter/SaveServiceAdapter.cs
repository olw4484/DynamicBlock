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
        // 원본 콜백과 연결 → 상태 바뀔 때마다 Sticky 통지
        _legacy.AfterLoad += d => _bus.PublishSticky(new GameDataChanged(d), alsoEnqueue: false);
        _legacy.AfterSave += d => _bus.PublishSticky(new GameDataChanged(d), alsoEnqueue: false);

        if (_legacy.gameData == null)
            _legacy.LoadGame();

        TryMigrateLegacyLanguage();

        // 원본 Awake에서 LoadGame 호출됨 → 초기 Sticky 보강
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

        // 인게임 점수 변동 시: 최고점 경신되면 즉시 저장
        _bus.Subscribe<ScoreChanged>(e =>
        {
            // 디바운싱이 필요하면 여기서 조건 추가 가능
            _legacy.TryUpdateHighScore(e.value);
        }, replaySticky: true);

        // 안전망: 게임오버 최종 점수 기록(플레이횟수/라스트스코어/하이스코어 포함)
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

        // UI/로컬라이즈 즉시 반영
        _bus.PublishSticky(new GameDataChanged(_legacy.gameData), alsoEnqueue: false);

        // 디스크 반영
        _legacy.SaveGame();
    }

    // ISaveService 직접 호출 경로(옵션)
    public bool LoadOrCreate() { _legacy.LoadGame(); return true; }
    public void Save() { _legacy.SaveGame(); }
    public void ResetData() { _legacy.gameData = GameData.NewDefault(DefaultStages); _legacy.SaveGame(); }
}

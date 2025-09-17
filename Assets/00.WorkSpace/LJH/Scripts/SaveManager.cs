using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif
using _00.WorkSpace.GIL.Scripts;
using _00.WorkSpace.GIL.Scripts.Blocks;
using _00.WorkSpace.GIL.Scripts.Managers;
using _00.WorkSpace.GIL.Scripts.Shapes;
using _00.WorkSpace.GIL.Scripts.Messages;
using System.Collections;
using UnityEngine.Localization.Settings;

// ================================
// System : SaveManager (Unified)
// Role  : 단일 세이브/로드 + 어댑터(IManager/ISaveService) 흡수
// Order : 40 (이벤트큐 이후, 사운드/UI 이전)
// Path  : <persistentDataPath>/SaveFile/save.json
// Legacy: <...>/SaveFile/SaveData.json → LanguageIndex 1회 마이그 후 .bak
// ================================
[AddComponentMenu("System/SaveManager (Unified)")]
public sealed class SaveManager : MonoBehaviour, IManager, ISaveService
{
    // ---- IManager ----
    public int Order => 40;

    // 이벤트 버스 (ManagerGroup에서 주입)
    private EventQueue _bus;

    // 외부에서도 구독 가능
    public event Action<GameData> AfterLoad;
    public event Action<GameData> AfterSave;

    // 파일 경로 통일
    private const string DirName = "SaveFile";
    private const string FileName = "save.json";
    private string _filePath;
    private string _backupPath;

    // 런타임 상태
    public GameData gameData;
    public GameData Data => gameData;
    [NonSerialized] public bool skipNextGridSnapshot = false;

    [SerializeField] private float _snapshotDebounce = 0.25f;   // 디바운스 대기
    [SerializeField] private float _snapshotMinInterval = 1.0f; // 최소 저장 간격(초)

    private Coroutine _snapshotJob;
    private bool _snapshotDirty;
    private float _lastSnapshotAt = -999f;
    private bool _pendingSnapshotApply;
    private const int DefaultStages = 200;
    bool _suppressSnapshot;
    bool _internalClearGuard;

    public enum SnapshotSource { Auto, Manual, EnterRequest, EnterIntent, Reset }

    public void SuppressSnapshotsFor(float seconds)
    => StartCoroutine(CoSuppress(seconds));

    IEnumerator CoSuppress(float s)
    {
        _suppressSnapshot = true;
        yield return new WaitForSecondsRealtime(s);
        _suppressSnapshot = false;
    }

    // ------------- Lifecycle (Mono) -------------
    private void Awake()
    {
        var dir = Path.Combine(Application.persistentDataPath, "SaveFile");
        if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

        _filePath = Path.Combine(dir, "save.json");
        _backupPath = Path.Combine(dir, "save.json.bak");

        TryMigrateLegacyLanguageOnce();
        LoadGame();
        StartCoroutine(CoApplyLocale(gameData?.LanguageIndex ?? 0));
    }


#if UNITY_EDITOR
    [ContextMenu("Open Save Folder")]
    private void OpenSaveFolder()
    {
        var dir = Path.GetDirectoryName(_filePath);
        if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
        EditorUtility.RevealInFinder(dir);
    }
#endif

    // ------------- IManager Hook -------------
    // ManagerGroup에서 호출: 의존성 주입
    public void SetDependencies(EventQueue bus) { _bus = bus; }


    public void PreInit()
    {
        if (_bus == null)
        {
            Debug.LogError("[Save] EventQueue not injected. PublishState skipped.");
            return;
        }
        PublishState(gameData); // Sticky 시드
    }


    public void Init() { }

    public void PostInit()
    {
        if (_bus == null) { Debug.LogError("[Save] EventQueue not injected. Subscriptions skipped."); return; }

        // ---- 스냅샷 저장 트리거 ----
        _bus.Subscribe<ScoreChanged>(_ => MarkSnapshotDirty(), replaySticky: false);
        _bus.Subscribe<ComboChanged>(_ => MarkSnapshotDirty(), replaySticky: false);
        _bus.Subscribe<LinesCleared>(_ => MarkSnapshotDirty(), replaySticky: false);
        _bus.Subscribe<GridCleared>(_ => MarkSnapshotDirty(), replaySticky: false);
        // (권장) 블록 커밋 시점 이벤트가 있다면 여기도 연결
        // _bus.Subscribe<BlockCommitted>(_ => MarkSnapshotDirty(), replaySticky: false);

        // ---- 런 종료/재시작: GameOver/Restart에서 스냅샷 삭제 ----
        _bus.Subscribe<GameResetRequest>(e =>
        {
            if (e.reason == ResetReason.GameOver || e.reason == ResetReason.Restart)
            {
                ClearRunState(save: true);
                Debug.Log($"[Save] Snapshot cleared by GameResetRequest({e.reason}).");
            }
        }, replaySticky: false);

        // ---- 클래식 입장: '대기'만 설정 ----
        _bus.Subscribe<GameEnterRequest>(e =>
        {
            if (e.mode == GameMode.Classic)
            {
                SuppressSnapshotsFor(0.5f);
                ArmSnapshotApply("EnterRequest");
            }
        }, replaySticky: false);

        _bus.Subscribe<GameEnterIntent>(e =>
        {
            if (e.mode == GameMode.Classic)
            {
                SuppressSnapshotsFor(0.5f);
                ArmSnapshotApply(e.forceLoadSave ? "EnterIntent(force)" : "EnterIntent");
            }
        }, replaySticky: false);

        // ---- 실제 복원 타이밍: 보드/그리드 준비 or 게임 진입 완료 ----
        _bus.Subscribe<BoardReady>(_ => TryApplyPending("BoardReady"), replaySticky: false);
        _bus.Subscribe<GridReady>(_ => TryApplyPending("GridReady"), replaySticky: false);
        _bus.Subscribe<GameEntered>(e =>
        {
            if (e.mode == GameMode.Classic) TryApplyPending("GameEntered");
        }, replaySticky: false);

        // ---- 수동 저장/로드 등 기존 구독 유지 ----
        _bus.Subscribe<SaveRequested>(_ => SaveGame(), replaySticky: false);
        _bus.Subscribe<LoadRequested>(_ => { LoadGame(); /* TryApplyRunSnapshot(); */ }, replaySticky: false);
        _bus.Subscribe<ResetRequested>(_ => { gameData = GameData.NewDefault(DefaultStages); SaveGame(); }, replaySticky: false);
        _bus.Subscribe<GameOverConfirmed>(e =>
        {
            UpdateClassicScore(e.score);
            if (e.isNewBest) { Game.Fx.PlayNewScoreAt(); Sfx.NewRecord(); }
            else { Game.Fx.PlayGameOverAt(); Sfx.GameOver(); }
            Debug.Log($"[Save] FINAL total={e.score}, persistedHigh={gameData?.highScore}");
        }, replaySticky: false);
        _bus.Subscribe<LanguageChangeRequested>(e => SetLanguageIndex(e.index), replaySticky: false);
        _bus.Subscribe<AllClear>(_ => Debug.Log("[Save] ALL CLEAR!"), replaySticky: false);
    }

    // ------------- Public Save/Load -------------
    public void SaveGame()
    {
        if (gameData == null) return;

        var json = JsonUtility.ToJson(gameData, true);
        try
        {
            AtomicWriteWithBackup(_filePath, _backupPath, json);
            Debug.Log($"[Save] Saved: {_filePath}");
            AfterSave?.Invoke(gameData);
            PublishState(gameData);
        }
        catch (Exception ex)
        {
            Debug.LogError("[Save] Save failed: " + ex);
        }
    }
    private static void AtomicWriteWithBackup(string mainPath, string bakPath, string content)
    {
        var tmp = mainPath + ".tmp";
        File.WriteAllText(tmp, content);              // 1) 임시파일에 기록
                                                      // 일부 플랫폼에서 Replace 미지원일 수 있어 try/fallback
        try
        {
            // 메인이 있으면 백업을 갱신하며 교체, 없으면 교체 동작 = 이동
            if (File.Exists(mainPath))
                File.Replace(tmp, mainPath, bakPath);
            else
            {
                // 메인이 없을 때는 단순 Move 후, 기존 백업 갱신
                if (File.Exists(bakPath)) File.Delete(bakPath);
                File.Move(tmp, mainPath);
                File.Copy(mainPath, bakPath);
            }
        }
        catch
        {
            // Replace 불가 플랫폼 폴백
            if (File.Exists(bakPath)) File.Delete(bakPath);
            if (File.Exists(mainPath)) File.Copy(mainPath, bakPath);
            File.Copy(tmp, mainPath, overwrite: true);
            File.Delete(tmp);
        }
        if (File.Exists(tmp)) File.Delete(tmp);
    }

    public void LoadGame()
    {
        try
        {
            if (File.Exists(_filePath))
            {
                var json = File.ReadAllText(_filePath);
                gameData = JsonUtility.FromJson<GameData>(json);
                if (gameData == null) throw new Exception("FromJson returned null");
            }
            else if (File.Exists(_backupPath))
            {
                Debug.LogWarning("[Save] Main missing. Trying backup restore...");
                RestoreFromBackup();
            }
            else
            {
                gameData = GameData.NewDefault(DefaultStages);
            }

            // 보정
            if (gameData.Version < 2) gameData.Version = 2;
            gameData.currentShapeNames ??= new List<string>();
            gameData.currentSpriteNames ??= new List<string>();
            gameData.currentBlockSlots ??= new List<int>();
            gameData.currentShapes ??= new List<ShapeData>();
            gameData.currentShapeSprites ??= new List<Sprite>();
        }
        catch (Exception ex)
        {
            Debug.LogError("[Save] Load failed: " + ex);
            // 백업 시도
            if (TryRestoreAndReload()) { /* ok */ }
            else gameData = GameData.NewDefault(DefaultStages);
        }

        Debug.Log($"[Save] Loaded: blocks={gameData.currentBlockSlots?.Count}");
        AfterLoad?.Invoke(gameData);
        PublishState(gameData);
    }

    private void RestoreFromBackup()
    {
        File.Copy(_backupPath, _filePath, overwrite: true);
        var json2 = File.ReadAllText(_filePath);
        gameData = JsonUtility.FromJson<GameData>(json2);
        if (gameData == null) throw new Exception("Backup FromJson returned null");
    }

    private bool TryRestoreAndReload()
    {
        try
        {
            if (!File.Exists(_backupPath)) return false;
            RestoreFromBackup();
            return true;
        }
        catch (Exception ex2)
        {
            Debug.LogError("[Save] Backup restore failed: " + ex2);
            return false;
        }
    }

    // === 앱 수명주기 자동 저장 ===
    private void OnApplicationPause(bool pause)
    {
        if (pause) SaveGame();
    }
    private void OnApplicationQuit()
    {
        SaveGame();
    }

    // ------------- Legacy Migration (1회) -------------
    private void TryMigrateLegacyLanguageOnce()
    {
#if UNITY_EDITOR
        string legacyPath = Path.Combine(Application.dataPath, "00.WorkSpace/SJH/SaveFile/SaveData.json");
#else
        string legacyPath = Path.Combine(Application.persistentDataPath, "SaveFile/SaveData.json");
#endif
        string bak = legacyPath + ".bak";
        try
        {
            if (!File.Exists(legacyPath) || File.Exists(bak)) return;

            string json = File.ReadAllText(legacyPath);
            SJH.GameData legacy = JsonUtility.FromJson<SJH.GameData>(json);
            if (legacy == null) return;

            if (gameData == null) gameData = GameData.NewDefault(DefaultStages);

            int before = gameData.LanguageIndex;
            gameData.LanguageIndex = legacy.LanguageIndex;

            SaveGame();

            if (File.Exists(bak)) File.Delete(bak);
            File.Move(legacyPath, bak);

#if UNITY_EDITOR
            Debug.Log($"[Save] Migrated LanguageIndex {before}→{legacy.LanguageIndex} from legacy file.");
#endif
        }
        catch (Exception ex) { Debug.LogException(ex); }
    }

    // ------------- State Broadcast -------------
    private void PublishState(GameData d)
    {
        if (_bus == null || d == null) return;
        var evt = new global::GameDataChanged(d);
        _bus.PublishSticky(evt, alsoEnqueue: false);
        _bus.PublishImmediate(evt);
        Debug.Log($"[Save] PublishState high={d?.highScore}");
    }

    // ------------- Language API -------------
    public void SetLanguageIndex(int index)
    {
        if (gameData == null) LoadGame();

        var locales = LocalizationSettings.AvailableLocales?.Locales;
        if (locales == null || locales.Count == 0)
        {
            Debug.LogWarning("[Lang] No locales. Will only persist index.");
            gameData.LanguageIndex = index;
            PublishState(gameData);
            SaveGame();
            return;
        }
        index = Mathf.Clamp(index, 0, locales.Count - 1);

        gameData.LanguageIndex = index;
        StartCoroutine(CoApplyLocale(index));

        PublishState(gameData);
        SaveGame();
    }

    private IEnumerator CoApplyLocale(int index)
    {
        var initOp = LocalizationSettings.InitializationOperation;
        if (!initOp.IsDone) yield return initOp;

        var locales = LocalizationSettings.AvailableLocales.Locales;
        if (locales == null || locales.Count == 0) yield break;

        index = Mathf.Clamp(index, 0, locales.Count - 1);
        var target = locales[index];

        if (LocalizationSettings.SelectedLocale != target)
        {
            Debug.Log($"[Lang] Apply locale: {target?.Identifier.Code} (index={index})");
            LocalizationSettings.SelectedLocale = target;
        }
    }

    // ------------- Score/Stage API -------------
    public void UpdateClassicScore(int score)
    {
        if (gameData == null) LoadGame();

        gameData.lastScore = score;
        gameData.playCount++;
        if (score > gameData.highScore) gameData.highScore = score;

        SaveGame();
    }

    public bool TryUpdateHighScore(int score, bool save = true)
    {
        if (gameData == null) LoadGame();
        if (score > gameData.highScore)
        {
            gameData.highScore = score;
            if (save) SaveGame();
            return true;
        }
        return false;
    }

    public int CurrentHighScore => gameData?.highScore ?? 0;

    public int GetPersistedHighScoreFresh()
    {
        LoadGame(); // UI 갱신 이벤트 나갈 수 있음(정상)
        return gameData?.highScore ?? 0;
    }

    public void ClearStage(int stageIndex, int score)
    {
        if (gameData == null) LoadGame();
        if (stageIndex < 0 || stageIndex >= gameData.stageCleared.Length) return;

        gameData.stageCleared[stageIndex] = 1;
        if (score > gameData.stageScores[stageIndex]) gameData.stageScores[stageIndex] = score;

        SaveGame();
    }
    public bool IsStageCleared(int stageIndex) => gameData.stageCleared[stageIndex] == 1;
    public int GetStageScore(int stageIndex) => gameData.stageScores[stageIndex];

    public GameMode GetGameMode() => gameData != null ? gameData.gameMode : GameMode.Tutorial;
    public void SetGameMode(GameMode mode, bool save = true)
    {
        if (gameData == null) gameData = GameData.NewDefault(DefaultStages);
        gameData.gameMode = mode;
        if (save) SaveGame();
    }

    // ------------- Hand/Blocks Snapshot -------------
    public void ClearCurrentBlocks(bool save = true)
    {
        if (!_internalClearGuard)
        {
            Debug.LogWarning("[Save] ClearCurrentBlocks ignored (external caller)");
            return; // 외부 호출 차단
        }
        Debug.Log("[Save] Cleared current blocks (by reset).\n" +
          new System.Diagnostics.StackTrace(true));
        if (gameData == null) return;

        if (gameData.currentShapes == null) gameData.currentShapes = new List<ShapeData>();
        else gameData.currentShapes.Clear();

        if (gameData.currentShapeSprites == null) gameData.currentShapeSprites = new List<Sprite>();
        else gameData.currentShapeSprites.Clear();

        try { gameData.currentShapeNames?.Clear(); } catch { }
        try { gameData.currentSpriteNames?.Clear(); } catch { }
        try { gameData.currentBlockSlots?.Clear(); } catch { }

        if (save) SaveGame();
        Debug.Log("[Save] Cleared current blocks (by reset).");
    }

    public void SaveCurrentBlocksFromStorage(BlockStorage storage, bool save = true)
    {
        if (gameData == null || storage == null) return;

        // 런타임 캐시(동세션 빠른 복원용)
        gameData.currentShapes = new List<ShapeData>();
        gameData.currentShapeSprites = new List<Sprite>();

        // 영속 데이터(세션 넘어도 유효)
        gameData.currentShapeNames = new List<string>();
        gameData.currentSpriteNames = new List<string>();
        gameData.currentBlockSlots = new List<int>();

        var shapes = storage.CurrentBlocksShapedata;
        var sprites = storage.CurrentBlocksSpriteData;
        var blocks = storage.CurrentBlocks;

        int n = Mathf.Min(shapes?.Count ?? 0, sprites?.Count ?? 0);
        for (int i = 0; i < n; i++)
        {
            var sh = shapes[i];
            var sp = sprites[i];

            gameData.currentShapes.Add(sh);
            gameData.currentShapeSprites.Add(sp);

            gameData.currentShapeNames.Add(sh ? sh.name : string.Empty);
            gameData.currentSpriteNames.Add(sp ? sp.name : string.Empty);
            gameData.currentBlockSlots.Add(blocks != null && i < blocks.Count && blocks[i] ? blocks[i].SpawnSlotIndex : -1);
        }

        if (save) SaveGame();
        Debug.Log($"[Save] Saved current blocks (persisted by names): {n}");
    }

    public bool TryRestoreBlocksToStorage(BlockStorage storage)
    {
        if (gameData == null || storage == null) return false;

        var shapes = gameData.currentShapes;
        var sprites = gameData.currentShapeSprites;
        var slots = gameData.currentBlockSlots;

        // 필요시 이름→객체 복원
        if ((shapes == null || shapes.Count == 0)
            && (gameData.currentShapeNames?.Count ?? 0) > 0)
        {
            shapes = gameData.currentShapeNames.Select(n => GDS.I.GetShapeByName(n)).ToList();
            sprites = (gameData.currentSpriteNames ?? new List<string>())
                        .Select(n => GDS.I.GetBlockSpriteByName(n)).ToList();

            gameData.currentShapes = shapes;
            gameData.currentShapeSprites = sprites;
        }

        int nShapes = shapes?.Count ?? 0;
        int nSprites = sprites?.Count ?? 0;

        if (nShapes == 0 || nSprites == 0)
        {
            Debug.Log("[Save] TryRestoreBlocksToStorage: empty -> false");
            return false;
        }

        int n = Mathf.Min(nShapes, nSprites);

        if (slots != null && slots.Count == n)
            return storage.RebuildBlocksFromLists(shapes, sprites, slots);

        return storage.RebuildBlocksFromLists(shapes, sprites);
    }

    // ------------- Run Snapshot (Grid/Score/Combo) -------------
    public void ClearRunState(bool save = true)
    {
        Debug.Log("[Save] ClearRunState CALLED");
        _internalClearGuard = true;
        ClearCurrentBlocks(false);
        _internalClearGuard = false;
        if (gameData == null) return;

        ClearCurrentBlocks(false);

        gameData.currentMapLayout?.Clear();
        try { gameData.currentBlockSlots?.Clear(); } catch { }
        try { gameData.currentShapeNames?.Clear(); } catch { }
        try { gameData.currentSpriteNames?.Clear(); } catch { }

        gameData.currentScore = 0;
        gameData.currentCombo = 0;
        gameData.isClassicModePlaying = false;

        if (save) SaveGame();
        Debug.Log("[Save] ClearRunState: grid/blocks/score cleared.");
    }
    private static bool HasMeaningfulBoard(List<int> layout)
    => layout != null && layout.Count > 0 && layout.Any(v => v > 0);

    public void SaveRunSnapshot(bool saveBlocksToo = true, SnapshotSource src = SnapshotSource.Auto)
    {
        if (_suppressSnapshot) { Debug.Log($"[Save] Snapshot suppressed ({src})"); return; }
        if (src == SnapshotSource.EnterRequest || src == SnapshotSource.EnterIntent || src == SnapshotSource.Reset)
        {
            Debug.Log($"[Save] Skip snapshot on {src}.");
            return;
        }

        var gm = GridManager.Instance;
        int cellCount = (gm != null) ? gm.rows * gm.cols : -1;
        Debug.Log($"[Save] SaveRunSnapshot START rows*cols={cellCount}");

        if (gm != null)
        {
            var layout = gm.ExportLayoutCodes();
            if (!HasMeaningfulBoard(layout)) { Debug.Log("[Save] Skip snapshot: layout empty."); return; }

            gameData.currentMapLayout = layout;
            gameData.isClassicModePlaying = true;

            gameData.currentScore = ScoreManager.Instance ? ScoreManager.Instance.Score : 0;
            gameData.currentCombo = ScoreManager.Instance ? ScoreManager.Instance.Combo : 0;
        }

        if (saveBlocksToo)
        {
            var storage = UnityEngine.Object.FindFirstObjectByType<BlockStorage>();
            if (storage) SaveCurrentBlocksFromStorage(storage, save: false);
        }

        SaveGame();
        Debug.Log($"[Save] SaveRunSnapshot DONE: layoutCount={gameData?.currentMapLayout?.Count}, " +
                  $"score={gameData?.currentScore}, combo={gameData?.currentCombo}, " +
                  $"classicFlag={gameData?.isClassicModePlaying}");
    }

    public bool HasRunSnapshot()
    {
        if (gameData == null) LoadGame();
        return gameData != null
            && gameData.isClassicModePlaying
            && HasMeaningfulBoard(gameData.currentMapLayout);
    }

    public bool TryApplyRunSnapshot()
    {
        if (gameData == null) LoadGame();
        if (!HasRunSnapshot()) return false;

        var gm = GridManager.Instance;
        var layout = gameData.currentMapLayout;

        // 1) 보드 적용
        bool boardApplied = false;
        if (gm != null && layout != null && layout.Count > 0)
        {
            try
            {
                gm.ImportLayoutCodes(layout);
                boardApplied = true;
            }
            catch { }
        }
        if (!boardApplied)
        {
            MapManager.Instance?.LoadCurrentClassicMap();
        }

        // 2) 손패 복원
        var storage = UnityEngine.Object.FindFirstObjectByType<BlockStorage>();
        TryRestoreBlocksToStorage(storage);

        // 3) 점수/콤보 동기화
        if (ScoreManager.Instance)
            ScoreManager.Instance.RestoreScoreState(gameData.currentScore, gameData.currentCombo, silent: false);
        else
        {
            _bus?.PublishImmediate(new ScoreChanged(gameData.currentScore));
            _bus?.PublishImmediate(new ComboChanged(gameData.currentCombo));
        }

        PublishState(gameData);
        Debug.Log("[Save] Applied run snapshot (grid+hand+score restored).");
        return true;
    }

    private void MarkSnapshotDirty()
    {
        if (skipNextGridSnapshot)
        {
            // 한 번만 스킵하고 플래그 내림
            skipNextGridSnapshot = false;
            Debug.Log("[Save] Snapshot skip (one-shot).");
            return;
        }

        _snapshotDirty = true;
        if (_snapshotJob == null) _snapshotJob = StartCoroutine(CoDebouncedSnapshot());
    }

    private IEnumerator CoDebouncedSnapshot()
    {
        yield return new WaitForSecondsRealtime(_snapshotDebounce);
        float dt = Time.realtimeSinceStartup - _lastSnapshotAt;
        if (dt < _snapshotMinInterval)
            yield return new WaitForSecondsRealtime(_snapshotMinInterval - dt);

        if (_snapshotDirty)
        {
            SaveRunSnapshot(saveBlocksToo: true, src: SnapshotSource.Auto);
            _lastSnapshotAt = Time.realtimeSinceStartup;
            _snapshotDirty = false;
        }
        _snapshotJob = null;
    }

    private void ArmSnapshotApply(string src)
    {
        if (!HasRunSnapshot()) return;
        _pendingSnapshotApply = true;
        Debug.Log($"[Save] Snapshot pending (src={src})");
    }


    private void TryApplyPending(string src)
    {
        if (!_pendingSnapshotApply) return;
        StartCoroutine(CoApplyPending(src));
    }

    private IEnumerator CoApplyPending(string src)
    {
        // 진입 파이프라인 후행 초기화가 끝나길 '조금' 기다린다
        yield return null;                     // 다음 프레임
        yield return new WaitForEndOfFrame();  // 해당 프레임의 제일 마지막

        SkipNextSnapshot($"Apply pending at {src}");
        bool ok = TryApplyRunSnapshot();
        Debug.Log($"[Save] Snapshot {(ok ? "applied" : "FAILED")} (delayed) at {src}. " +
                  $"layout={gameData?.currentMapLayout?.Count}, score={gameData?.currentScore}, combo={gameData?.currentCombo}");
        _pendingSnapshotApply = false;
    }
    public void SkipNextSnapshot(string reason = null) { skipNextGridSnapshot = true; Debug.Log($"[Save] Skip next grid snapshot (reason: {reason})"); }
    // ------------- ISaveService (직접 호출 경로) -------------
    public bool LoadOrCreate() { LoadGame(); return true; }
    public void Save() { SaveGame(); }
    public void ResetData() { gameData = GameData.NewDefault(DefaultStages); SaveGame(); }
}

using _00.WorkSpace.GIL.Scripts;
using _00.WorkSpace.GIL.Scripts.Blocks;
using _00.WorkSpace.GIL.Scripts.Managers;
using _00.WorkSpace.GIL.Scripts.Shapes;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

public class SaveManager : MonoBehaviour
{
    public event Action<GameData> AfterLoad;
    public event Action<GameData> AfterSave;

    private string filePath;
    public GameData gameData;
    
    [NonSerialized] public bool skipNextGridSnapshot = false;
    
    private void Awake()
    {
        filePath = Path.Combine(Application.persistentDataPath, "save.json");
        LoadGame();

    }

    // ���� ������ ����
    public void SaveGame()
    {
        Debug.Log("[SaveManager] SaveGame 호출됨");
        Debug.Log($"[SaveManager] 저장 데이터 상태: " +
                  $"blocks={gameData.currentBlockSlots?.Count}");

        var json = JsonUtility.ToJson(gameData, true);
        File.WriteAllText(filePath, json);
        Debug.Log("���� �Ϸ�: " + filePath);
        AfterSave?.Invoke(gameData);
    }

    // ���� ������ �ҷ�����
    public void LoadGame()
    {
        Debug.Log("[SaveManager] LoadGame 호출됨");

        try
        {
            if (File.Exists(filePath))
            {
                string json = File.ReadAllText(filePath);
                gameData = JsonUtility.FromJson<GameData>(json);
                if (gameData == null) throw new Exception("FromJson returned null");
            }
            else
            {
                gameData = GameData.NewDefault(200);
            }

            if (gameData.Version < 2)
            {
                gameData.Version = 2;
            }

            gameData.currentShapeNames ??= new List<string>();
            gameData.currentSpriteNames ??= new List<string>();
            gameData.currentBlockSlots ??= new List<int>();

            gameData.currentShapes ??= new List<ShapeData>();
            gameData.currentShapeSprites ??= new List<Sprite>();
        }
        catch (Exception ex)
        {
            Debug.LogError("[Save] Load failed: " + ex);
            gameData = GameData.NewDefault(200);
        }
        Debug.Log($"[SaveManager] 로드 완료: " +
          $"blocks={gameData.currentBlockSlots?.Count}, ");
        AfterLoad?.Invoke(gameData);
    }

    // ============= Ŭ���� ��� =============
    public void UpdateClassicScore(int score)
    {
        gameData.lastScore = score;
        gameData.playCount++;
        if (score > gameData.highScore)
            gameData.highScore = score;

        SaveGame();
    }

    // ============= ��庥ó ��� =============
    public void ClearStage(int stageIndex, int score)
    {
        if (stageIndex < 0 || stageIndex >= gameData.stageCleared.Length) return;

        gameData.stageCleared[stageIndex] = 1; // Ŭ���� üũ
        if (score > gameData.stageScores[stageIndex])
            gameData.stageScores[stageIndex] = score;

        SaveGame();
    }

    public bool IsStageCleared(int stageIndex)
    {
        return gameData.stageCleared[stageIndex] == 1;
    }

    public int GetStageScore(int stageIndex)
    {
        return gameData.stageScores[stageIndex];
    }
    
    public void ClearCurrentBlocks(bool save = true)
    {
        if (gameData == null) return;

        // 런타임 캐시 리스트 비우기 (없으면 새로 만들어 둠)
        if (gameData.currentShapes == null) gameData.currentShapes = new List<ShapeData>();
        else gameData.currentShapes.Clear();

        if (gameData.currentShapeSprites == null) gameData.currentShapeSprites = new List<Sprite>();
        else gameData.currentShapeSprites.Clear();

        // (있다면) 이름/슬롯 리스트도 같이 비우기 — 없는 프로젝트도 예외 없이 통과
        try { gameData.currentShapeNames?.Clear(); } catch { }
        try { gameData.currentSpriteNames?.Clear(); } catch { }
        try { gameData.currentBlockSlots?.Clear(); } catch { }

        if (save) SaveGame();
        Debug.Log("[Save] Cleared current blocks (by reset).");
    }
    
    /// <summary>���� highScore���� ũ�� �����ϰ� ����.</summary>
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
    
    // API: GameMode
    public GameMode GetGameMode() => gameData != null ? gameData.gameMode : GameMode.Tutorial;

    public void SetGameMode(GameMode mode, bool save = true)
    {
        if (gameData == null) gameData = GameData.NewDefault(200);
        gameData.gameMode = mode;
        if (save) SaveGame();
    }
    
    public void SkipNextSnapshot(string reason = null)
    {
        skipNextGridSnapshot = true;
        Debug.Log($"[Save] Skip next grid snapshot (reason: {reason})");
    }
    
    // ==== Save/Restore current blocks (hand) ====
    public void SaveCurrentBlocksFromStorage(BlockStorage storage)
    {
        if (gameData == null || storage == null) return;
        if (gameData.currentShapes == null)       gameData.currentShapes       = new List<ShapeData>();
        if (gameData.currentShapeSprites == null) gameData.currentShapeSprites = new List<Sprite>();
        gameData.currentShapes.Clear();
        gameData.currentShapeSprites.Clear();

        var shapes  = storage.CurrentBlocksShapedata;
        var sprites = storage.CurrentBlocksSpriteData;
        if (shapes == null || sprites == null) return;

        int n = Mathf.Min(shapes.Count, sprites.Count);
        for (int i = 0; i < n; i++)
        {
            gameData.currentShapes.Add(shapes[i]);
            gameData.currentShapeSprites.Add(sprites[i]);
        }
        
        // (옵션) 디스크 직렬화용 이름 리스트도 갱신
            gameData.currentShapeNames  = new List<string>(n);
        gameData.currentSpriteNames = new List<string>(n);
        for (int i = 0; i < n; i++)
        {
            gameData.currentShapeNames.Add(shapes[i]  ? shapes[i].name : string.Empty);
            gameData.currentSpriteNames.Add(sprites[i] ? sprites[i].name : string.Empty);
        }
        
        gameData.currentBlockSlots = storage.CurrentBlocks
            .Select(b => b ? b.SpawnSlotIndex : -1)
            .ToList();
        
        SaveGame();
        Debug.Log($"[Save] Saved current blocks: {n}");
    }

    public bool TryRestoreBlocksToStorage(BlockStorage storage)
    {
        if (gameData == null || storage == null) return false;
        var shapes  = gameData.currentShapes;
        var sprites = gameData.currentShapeSprites;
        var slots   = gameData.currentBlockSlots;
        
        if ((shapes == null || shapes.Count == 0) &&
            (gameData.currentShapeNames != null && gameData.currentShapeNames.Count > 0))
        {
            shapes  = gameData.currentShapeNames
                .Select(n => GDS.I.GetShapeByName(n))
                .ToList();

            sprites = (gameData.currentSpriteNames ?? new List<string>())
                .Select(n => GDS.I.GetBlockSpriteByName(n))
                .ToList();

            // 게임 데이터도 채워주기
            gameData.currentShapes       = shapes;
            gameData.currentShapeSprites = sprites;
        }
        
        if (shapes != null && sprites != null && slots != null
            && shapes.Count > 0
            && shapes.Count == sprites.Count
            && shapes.Count == slots.Count)
        {
            return storage.RebuildBlocksFromLists(shapes, sprites, slots);
        }
           
        return storage.RebuildBlocksFromLists(shapes, sprites);
    }
    
    public void ClearRunState(bool save = true)
    {
        if (gameData == null) return;

        // 손패(블록) 비우기
        ClearCurrentBlocks(false); // 여기서 SaveGame은 하지 않음

        // 그리드/점수/슬롯 등 러닝 상태 전부 초기화
        gameData.currentMapLayout?.Clear();
        try { gameData.currentBlockSlots?.Clear(); } catch { }
        try { gameData.currentShapeNames?.Clear(); } catch { }
        try { gameData.currentSpriteNames?.Clear(); } catch { }

        gameData.currentScore = 0;
        gameData.currentCombo = 0;
        gameData.isClassicModePlaying = false; // 사용 중이면 유지

        if (save) SaveGame();
        Debug.Log("[Save] ClearRunState: grid/blocks/score cleared.");
    }
    public void SaveRunSnapshot(bool saveBlocksToo = true)
    {
        var gm = GridManager.Instance;
        if (gm != null)
        {
            if (gameData.currentMapLayout == null)
                gameData.currentMapLayout = new List<int>();
            gameData.currentMapLayout = gm.ExportLayoutCodes();
            gameData.isClassicModePlaying = true;

            gameData.currentScore = ScoreManager.Instance ? ScoreManager.Instance.Score : 0;
            gameData.currentCombo = ScoreManager.Instance ? ScoreManager.Instance.Combo : 0;
        }

        if (saveBlocksToo)
        {
            var storage = UnityEngine.Object.FindFirstObjectByType<BlockStorage>();
            if (storage) SaveCurrentBlocksFromStorage(storage);
        }

        SaveGame();
        Debug.Log("[Save] SaveRunSnapshot done.");
    }
}


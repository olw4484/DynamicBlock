using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using System;
using System.Linq;
using _00.WorkSpace.GIL.Scripts;
using _00.WorkSpace.GIL.Scripts.Blocks;
using _00.WorkSpace.GIL.Scripts.Shapes;

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
        var json = JsonUtility.ToJson(gameData, true);
        File.WriteAllText(filePath, json);
        Debug.Log("���� �Ϸ�: " + filePath);
        AfterSave?.Invoke(gameData);
    }

    // ���� ������ �ҷ�����
    public void LoadGame()
    {
        if (File.Exists(filePath))
        {
            string json = File.ReadAllText(filePath);
            gameData = JsonUtility.FromJson<GameData>(json);
        }
        else
        {
            gameData = new GameData();
            gameData.stageCleared = new int[200];
            gameData.stageScores = new int[200];
            gameData = GameData.NewDefault(200);
        }
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
        SaveGame();
        Debug.Log($"[Save] Saved current blocks: {n}");
    }

    public bool TryRestoreBlocksToStorage(BlockStorage storage)
    {
        if (gameData == null || storage == null) return false;
        var shapes  = gameData.currentShapes;
        var sprites = gameData.currentShapeSprites;
        if (shapes == null || sprites == null) return false;

        // 갯수만 맞춰 복원: 부족해도 추가 생성하지 않음
        int n = Mathf.Min(shapes.Count, sprites.Count);
        if (n <= 0) return false;

        var subShapes  = shapes.GetRange(0, n);
        var subSprites = sprites.GetRange(0, n);
        return storage.RebuildBlocksFromLists(subShapes, subSprites);
    }

}


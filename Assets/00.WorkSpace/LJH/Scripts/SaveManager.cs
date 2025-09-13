using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using System;

public class SaveManager : MonoBehaviour
{
    public event Action<GameData> AfterLoad;
    public event Action<GameData> AfterSave;

    private string filePath;
    public GameData gameData;

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
}

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

    // 게임 데이터 저장
    public void SaveGame()
    {
        var json = JsonUtility.ToJson(gameData, true);
        File.WriteAllText(filePath, json);
        Debug.Log("저장 완료: " + filePath);
        AfterSave?.Invoke(gameData);
    }

    // 게임 데이터 불러오기
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

    // ============= 클래식 모드 =============
    public void UpdateClassicScore(int score)
    {
        gameData.lastScore = score;
        gameData.playCount++;
        if (score > gameData.highScore)
            gameData.highScore = score;

        SaveGame();
    }

    // ============= 어드벤처 모드 =============
    public void ClearStage(int stageIndex, int score)
    {
        if (stageIndex < 0 || stageIndex >= gameData.stageCleared.Length) return;

        gameData.stageCleared[stageIndex] = 1; // 클리어 체크
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

    /// <summary>현재 highScore보다 크면 갱신하고 저장.</summary>
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
}

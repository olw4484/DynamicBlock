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
        AfterLoad?.Invoke(gameData);
    }

    // ���� ������ ����
    public void SaveGame()
    {
        string json = JsonUtility.ToJson(gameData, true);
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
            // �ʱ�ȭ (�������� 100����� ����)
            gameData = new GameData();
            gameData.stageCleared = new int[200];
            gameData.stageScores = new int[200];
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
}

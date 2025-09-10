using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class GameData
{
    public int Version = 1;

    public int LanguageIndex; // 0 = �⺻

    // Ŭ���� ���
    public int highScore;        // �ְ� ����
    public int lastScore;        // ������ �÷��� ����
    public int playCount;        // �÷��� Ƚ��

    // ��庥ó ���
    public int[] stageCleared;   // 0 = ��Ŭ����, 1 = Ŭ����
    public int[] stageScores;    // �� �������� �ְ� ����

    public static GameData NewDefault(int stages = 200)
    {
        return new GameData
        {
            LanguageIndex = 0,
            lastScore = 0,
            highScore = 0,
            playCount = 0,
            stageCleared = new int[stages],
            stageScores = new int[stages],
        };
    }
}

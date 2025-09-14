using System;
using System.Collections;
using System.Collections.Generic;
using _00.WorkSpace.GIL.Scripts.Shapes;
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
    
    // �� ���� ������
    [Obsolete("Use gameMode instead")]
    public bool isTutorialPlayed; // Ʃ�丮���� �����Ͽ��°�?
    public GameMode gameMode;
    // Ŭ���� ���
    public bool isClassicModePlaying;       // Ŭ���� ��� �÷��� ���ΰ�?
    public List<string> currentShapes;   // �� ���� ���� ������
    public List<int> currentShapeSprites;// �� ���� ���� ��������Ʈ
    public List<int> currentMapLayout;      // �� ���� �� ����
    public int currentScore;                // �� ���� ����
    public int currentCombo;                // �� ���� ���� ����
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
            // �� ���� ������
            gameMode = GameMode.Tutorial,
            // Ŭ���� ���
            isClassicModePlaying = false,
            currentShapes = new (),
            currentShapeSprites = new (),
            currentMapLayout = new List<int>(),
            currentScore = 0,
            currentCombo = 0
        };
    }
}

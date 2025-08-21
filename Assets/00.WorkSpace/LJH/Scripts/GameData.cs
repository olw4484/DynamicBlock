using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class GameData
{
    // Ŭ���� ���
    public int highScore;        // �ְ� ����
    public int lastScore;        // ������ �÷��� ����
    public int playCount;        // �÷��� Ƚ��

    // ��庥ó ���
    public int[] stageCleared;   // 0 = ��Ŭ����, 1 = Ŭ����
    public int[] stageScores;    // �� �������� �ְ� ����
}

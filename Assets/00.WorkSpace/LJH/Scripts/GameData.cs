using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class GameData
{
    public int Version = 1;

    // 클래식 모드
    public int highScore;        // 최고 점수
    public int lastScore;        // 마지막 플레이 점수
    public int playCount;        // 플레이 횟수

    // 어드벤처 모드
    public int[] stageCleared;   // 0 = 미클리어, 1 = 클리어
    public int[] stageScores;    // 각 스테이지 최고 점수

    public static GameData NewDefault(int stages = 200)
    {
        return new GameData
        {
            stageCleared = new int[stages],
            stageScores = new int[stages]
        };
    }
}

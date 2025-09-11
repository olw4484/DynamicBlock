using System.Collections;
using System.Collections.Generic;
using _00.WorkSpace.GIL.Scripts.Shapes;
using UnityEngine;

[System.Serializable]
public class GameData
{
    public int Version = 1;

    public int LanguageIndex; // 0 = 기본

    // 클래식 모드
    public int highScore;        // 최고 점수
    public int lastScore;        // 마지막 플레이 점수
    public int playCount;        // 플레이 횟수

    // 어드벤처 모드
    public int[] stageCleared;   // 0 = 미클리어, 1 = 클리어
    public int[] stageScores;    // 각 스테이지 최고 점수
    
    // 인 게임 데이터
    public bool isTutorialPlayed; // 튜토리얼을 실행하였는가?
    // 클래식 모드
    public bool isClassicModePlaying;       // 클래식 모드 플레이 중인가?
    public List<ShapeData> currentShapes;   // 현 시점 블럭 데이터
    public List<Sprite> currentShapeSprites;// 현 시점 블럭 스프라이트
    public List<int> currentMapLayout;      // 현 시점 맵 상태
    public int currentScore;                // 현 시점 점수
    public int currentCombo;                // 현 시점 라인 곰보
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
            // 인 게임 데이터
            isTutorialPlayed = false,
            // 클래식 모드
            isClassicModePlaying = false,
            currentShapes = new List<ShapeData>(),
            currentShapeSprites = new List<Sprite>(),
            currentMapLayout = new List<int>(),
            currentScore = 0,
            currentCombo = 0
        };
    }
}

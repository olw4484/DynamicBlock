using System;
using System.Collections.Generic;
using UnityEngine;
using _00.WorkSpace.GIL.Scripts.Shapes;

public enum GameMode { Tutorial, Classic, Adventure }

[Serializable]
public struct UnlockedAchievement   // Unity 직렬화 안전한 struct
{
    public int id;        // AchievementId enum의 int 값 (직렬화 호환)
    public int tier;      // 1/2/3
    public long utc;      // 해금 시각(Unix ms) - 팝업 날짜용
}

[Serializable]
public class GameData
{
    public int Version = 3;

    // ===== 공통 설정 =====
    public int LanguageIndex;               // 0 = 기본

    // ===== 통계/누적 =====
    public int highScore;                   // 최고 점수 (기존)
    public int lastScore;                   // 마지막 플레이 점수 (기존)
    public int playCount;                   // 총 플레이 횟수 (기존)

    public int bestCombo;                   // 최고 콤보(위젯/업적에 사용)
    public int loginDays;                   // 누적 로그인 일수
    public int lastLoginYmd;                // yyyyMMdd(UTC) 정수 보관 (예: 20250922)

    // ===== 어드벤처/클래식 진행 요약 (업적에 쓰일 누적치) =====
    public int adventureStageClears;        // 어드벤처 클리어 수(누적 또는 챕터 단위는 서비스에서 해석)
    public int adventureWinStreak;          // 현재 연승
    public int adventureBestWinStreak;      // 최고 연승

    public int totalBlocksRemoved;          // 전체 블록 제거 수
    public int specialBlocksRemoved;        // 특수 블록 제거 수
    public int fruitCollected;              // 과일 수집 수

    // ===== 스테이지 단위 기록(기존) =====
    public int[] stageCleared;              // 0=미클,1=클
    public int[] stageScores;               // 각 스테이지 최고 점수

    // ===== 런타임/모드 =====
    [Obsolete("Use gameMode instead")]
    public bool isTutorialPlayed;           // 과거 호환
    public GameMode gameMode;               // 현재 모드
    public bool isClassicModePlaying;       // 과거/호환 플래그

    [NonSerialized] public List<ShapeData> currentShapes;
    [NonSerialized] public List<Sprite> currentShapeSprites;

    public List<int> currentBlockSlots;
    public List<int> currentMapLayout;
    public List<string> currentShapeNames;
    public List<string> currentSpriteNames;
    public int currentScore;
    public int currentCombo;

    // ===== 다운/광고 보류(기존) =====
    public bool classicDownedPending;
    public int classicDownedScore;
    public long classicDownedUtc;

    // ===== 업적 해금 로그 =====
    public List<UnlockedAchievement> unlocked = new(); // 마지막 해금 티어/시각

    // ---------- 팩토리 ----------
    public static GameData NewDefault(int stages = 200)
    {
        return new GameData
        {
            Version = 3,
            LanguageIndex = 0,
            lastScore = 0,
            highScore = 0,
            playCount = 0,

            bestCombo = 0,
            loginDays = 0,
            lastLoginYmd = 0,

            adventureStageClears = 0,
            adventureWinStreak = 0,
            adventureBestWinStreak = 0,

            totalBlocksRemoved = 0,
            specialBlocksRemoved = 0,
            fruitCollected = 0,

            stageCleared = new int[stages],
            stageScores = new int[stages],

            gameMode = GameMode.Tutorial,
            isClassicModePlaying = false,

            currentShapes = new(),
            currentShapeSprites = new(),
            currentBlockSlots = new(),
            currentMapLayout = new(),
            currentShapeNames = new(),
            currentSpriteNames = new(),
            currentScore = 0,
            currentCombo = 0,

            classicDownedPending = false,
            classicDownedScore = 0,
            classicDownedUtc = 0,

            unlocked = new()
        };
    }

    // ---------- 일일 로그인 갱신 ----------
    public bool EnsureDailyLoginUpdatedUtc(DateTime utcNow)
    {
        int todayYmd = utcNow.Year * 10000 + utcNow.Month * 100 + utcNow.Day;
        if (lastLoginYmd == todayYmd) return false;   // 이미 반영됨
        // 하루 이상 지났으면 1 증가(연속/결석 로직은 필요 시 별도)
        loginDays++;
        lastLoginYmd = todayYmd;
        return true;
    }

    // ---------- 라운드 종료 반영(점수/콤보/모드별 누적) ----------
    public void ApplyRoundResult(GameMode mode, int finalScore, int bestComboThisRound,
                                 int removedBlocks, int removedSpecial, int fruits,
                                 bool isWin, bool stageClearedThisRound)
    {
        playCount++;
        lastScore = finalScore;
        if (finalScore > highScore) highScore = finalScore;

        if (bestComboThisRound > bestCombo) bestCombo = bestComboThisRound;

        totalBlocksRemoved += Mathf.Max(0, removedBlocks);
        specialBlocksRemoved += Mathf.Max(0, removedSpecial);
        fruitCollected += Mathf.Max(0, fruits);

        if (mode == GameMode.Adventure)
        {
            if (isWin)
            {
                adventureWinStreak++;
                if (adventureWinStreak > adventureBestWinStreak) adventureBestWinStreak = adventureWinStreak;
            }
            else
            {
                adventureWinStreak = 0; // 연승 끊김
            }

            if (stageClearedThisRound) adventureStageClears++;
        }
    }

    // ---------- 업적 해금 기록 업데이트 ----------
    public void RecordAchievementUnlocked(int achievementId, int tier, DateTime utcNow)
    {
        int idx = unlocked.FindIndex(a => a.id == achievementId);
        long ts = new DateTimeOffset(utcNow).ToUnixTimeMilliseconds();

        if (idx >= 0)
        {
            // 더 높은 티어면 갱신
            if (tier > unlocked[idx].tier)
                unlocked[idx] = new UnlockedAchievement { id = achievementId, tier = tier, utc = ts };
        }
        else
        {
            unlocked.Add(new UnlockedAchievement { id = achievementId, tier = tier, utc = ts });
        }
    }

    // ---------- 버전 마이그레이션 ----------
    public void MigrateIfNeeded()
    {
        if (Version < 3)
        {
            // v2 → v3 기본 필드 보강
            bestCombo = Mathf.Max(bestCombo, currentCombo);
            // 로그인 초기화
            if (lastLoginYmd == 0 && loginDays == 0)
            {
                var now = DateTime.UtcNow;
                lastLoginYmd = now.Year * 10000 + now.Month * 100 + now.Day;
            }
            // 누락 컬렉션 가드
            unlocked ??= new List<UnlockedAchievement>();
            currentBlockSlots ??= new List<int>();
            currentMapLayout ??= new List<int>();
            currentShapeNames ??= new List<string>();
            currentSpriteNames ??= new List<string>();
            if (stageCleared == null) stageCleared = new int[200];
            if (stageScores == null) stageScores = new int[200];

            Version = 3;
        }
    }
}

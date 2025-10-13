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
public struct AchievementTierStamp
{
    public int id;   // AchievementId as int
    public int tier; // 1-based
    public long utc; // Unix ms (UTC)
}

[Serializable]
public class GameData
{
    public int Version = 5;

    // ===== 공통 설정 =====
    public int LanguageIndex;               // 0 = 기본

    // ===== 통계/누적 =====
    [Obsolete("Use classicHighScore/adventureHighScore")]
    public int highScore;                   // (레거시) 최고 점수
    [Obsolete("Use classicLastScore/adventureLastScore")]
    public int lastScore;                   // (레거시) 마지막 점수
    public int playCount;                   // 총 플레이 횟수

    // 모드별 점수
    public int classicHighScore;
    public int classicLastScore;
    public int adventureHighScore;
    public int adventureLastScore;

    public int bestCombo;                   // 최고 콤보(위젯/업적)
    public int loginDays;                   // 누적 로그인 일수
    public int lastLoginYmd;                // yyyyMMdd(UTC)

    // ===== 어드벤처/클래식 진행 요약 =====
    public int adventureStageClears;
    public int adventureWinStreak;
    public int adventureBestWinStreak;

    public int totalBlocksRemoved;
    public int specialBlocksRemoved;
    public int fruitCollected;

    public int adventureBestIndex = 0;

    // ===== 스테이지 단위 기록 =====
    public int[] stageCleared;              // 0=미클,1=클
    public int[] stageScores;               // 각 스테이지 최고 점수

    // ===== 콤보 기록 =====
    public int combo5PlusRuns;
    public int combo10PlusRuns;

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
    public List<UnlockedAchievement> unlocked = new();
    public List<AchievementTierStamp> achievementTierStamps = new();

    // ---------- 팩토리 ----------
    public static GameData NewDefault(int stages = 200)
    {
        return new GameData
        {
            Version = 5,
            LanguageIndex = 0,

            // 레거시(호환)
            lastScore = 0,
            highScore = 0,

            // 신규: 모드별
            classicHighScore = 0,
            classicLastScore = 0,
            adventureHighScore = 0,
            adventureLastScore = 0,

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

            unlocked = new(),
            achievementTierStamps = new()
        };
    }

    // ---------- 일일 로그인 갱신 ----------
    public bool EnsureDailyLoginUpdatedUtc(DateTime utcNow)
    {
        int todayYmd = utcNow.Year * 10000 + utcNow.Month * 100 + utcNow.Day;
        if (lastLoginYmd == todayYmd) return false;   // 이미 반영됨
        loginDays++;
        lastLoginYmd = todayYmd;
        return true;
    }

    // ---------- 라운드 종료 반영(점수/콤보/모드별 누적) ----------
    public void ApplyRoundResult(
        GameMode mode, int finalScore, int bestComboThisRound,
        int removedBlocks, int removedSpecial, int fruits,
        bool isWin, bool stageClearedThisRound)
    {
        playCount++;

        // 모드별 점수 반영
        if (mode == GameMode.Classic)
        {
            classicLastScore = finalScore;
            if (finalScore > classicHighScore) classicHighScore = finalScore;

            // 레거시 필드: 클래식만 미러링(호환)
            lastScore = classicLastScore;
            highScore = classicHighScore;
        }
        else if (mode == GameMode.Adventure)
        {
            adventureLastScore = finalScore;
            if (finalScore > adventureHighScore) adventureHighScore = finalScore;
            // 레거시는 건드리지 않음
        }

        // 누적/콤보
        if (bestComboThisRound > bestCombo) bestCombo = bestComboThisRound;
        totalBlocksRemoved += Mathf.Max(0, removedBlocks);
        specialBlocksRemoved += Mathf.Max(0, removedSpecial);
        fruitCollected += Mathf.Max(0, fruits);

        if (mode == GameMode.Adventure)
        {
            if (isWin)
            {
                adventureWinStreak++;
                if (adventureWinStreak > adventureBestWinStreak)
                    adventureBestWinStreak = adventureWinStreak;
            }
            else adventureWinStreak = 0;

            if (stageClearedThisRound) adventureStageClears++;
        }

        if (bestComboThisRound >= 5) combo5PlusRuns++;
        if (bestComboThisRound >= 10) combo10PlusRuns++;
    }

    // ---------- 업적 해금 기록 업데이트 ----------
    public void RecordAchievementUnlocked(int achievementId, int tier, DateTime utcNow)
    {
        int idx = unlocked.FindIndex(a => a.id == achievementId);
        long ts = new DateTimeOffset(utcNow).ToUnixTimeMilliseconds();

        if (idx >= 0)
        {
            if (tier > unlocked[idx].tier)
                unlocked[idx] = new UnlockedAchievement { id = achievementId, tier = tier, utc = ts };
        }
        else
        {
            unlocked.Add(new UnlockedAchievement { id = achievementId, tier = tier, utc = ts });
        }

        UpsertTierStamp(achievementId, tier, ts);
    }

    // ---------- 티어별 타임스탬프 조회 ----------
    public bool TryGetAchievementUnlockUtc(int achievementId, int tier, out DateTime utc)
    {
        if (achievementTierStamps != null)
        {
            for (int i = 0; i < achievementTierStamps.Count; i++)
            {
                var s = achievementTierStamps[i];
                if (s.id == achievementId && s.tier == tier && s.utc > 0)
                {
                    utc = DateTimeOffset.FromUnixTimeMilliseconds(s.utc).UtcDateTime;
                    return true;
                }
            }
        }

        // (폴백) 구버전: 최고티어만 기록돼 있었던 경우
        if (unlocked != null)
        {
            for (int i = 0; i < unlocked.Count; i++)
            {
                var u = unlocked[i];
                if (u.id == achievementId && u.tier == tier && u.utc > 0)
                {
                    utc = DateTimeOffset.FromUnixTimeMilliseconds(u.utc).UtcDateTime;
                    return true;
                }
            }
        }

        utc = default;
        return false;
    }

    // ---------- 버전 마이그레이션 ----------
    public void MigrateIfNeeded()
    {
        if (Version < 3)
        {
            // v2 → v3
            bestCombo = Mathf.Max(bestCombo, currentCombo);
            if (lastLoginYmd == 0 && loginDays == 0)
            {
                var now = DateTime.UtcNow;
                lastLoginYmd = now.Year * 10000 + now.Month * 100 + now.Day;
            }
            unlocked ??= new List<UnlockedAchievement>();
            currentBlockSlots ??= new List<int>();
            currentMapLayout ??= new List<int>();
            currentShapeNames ??= new List<string>();
            currentSpriteNames ??= new List<string>();
            if (stageCleared == null) stageCleared = new int[200];
            if (stageScores == null) stageScores = new int[200];

            Version = 3;
        }

        // v3 → v4: 최고티어 → 티어 스탬프 이식
        if (Version < 4)
        {
            achievementTierStamps ??= new List<AchievementTierStamp>();
            if (unlocked != null)
            {
                foreach (var u in unlocked)
                {
                    if (u.tier > 0 && u.utc > 0)
                        UpsertTierStamp(u.id, u.tier, u.utc);
                }
            }
            Version = 4;
        }

        // v4 → v5: 모드별 점수 이관
        if (Version < 5)
        {
            // classic으로 승계(레거시 -> 신규)
            if (classicHighScore < highScore) classicHighScore = highScore;
            if (classicLastScore < lastScore) classicLastScore = lastScore;

            // adventure 최고점 초기화: stageScores의 최대값으로 추정
            int advMax = 0;
            if (stageScores != null)
            {
                for (int i = 0; i < stageScores.Length; i++)
                    if (stageScores[i] > advMax) advMax = stageScores[i];
            }
            if (adventureHighScore < advMax) adventureHighScore = advMax;

            Version = 5;
        }
    }

    // ---------- 내부 헬퍼 ----------
    private void UpsertTierStamp(int achievementId, int tier, long unixMs)
    {
        achievementTierStamps ??= new List<AchievementTierStamp>();
        int k = achievementTierStamps.FindIndex(e => e.id == achievementId && e.tier == tier);
        if (k >= 0)
        {
            var e = achievementTierStamps[k];
            e.utc = unixMs;
            achievementTierStamps[k] = e;
        }
        else
        {
            achievementTierStamps.Add(new AchievementTierStamp
            {
                id = achievementId,
                tier = tier,
                utc = unixMs
            });
        }
    }

    // ---------- 편의 헬퍼 ----------
    public int GetHighScore(GameMode mode)
        => (mode == GameMode.Adventure) ? adventureHighScore : classicHighScore;

    public int GetLastScore(GameMode mode)
        => (mode == GameMode.Adventure) ? adventureLastScore : classicLastScore;
}

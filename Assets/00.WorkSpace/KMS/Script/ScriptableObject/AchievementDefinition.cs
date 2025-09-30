using UnityEngine;

public enum AchievementId
{
    ScoreChampion,
    InvincibleLegend,
    Perseverance,
    BlockMaster,
    Opportunist,
    FruitTycoon,
    Unwavering,
    Adventurer,
    MasterCleaner
}

public enum ModeFilter { Any, Classic, Adventure }

public enum AchievementMetric
{
    BestScore,
    BestCombo,
    PlayCount,
    LoginDays,
    AdventureStageClears,
    AdventureBestWinStreak,
    TotalBlocksRemoved,
    SpecialBlocksRemoved,
    FruitCollected,
    ComboAtLeastCount,
    // 필요 시 확장: StageScoreAtLeast, StageClearCount 등
}

[CreateAssetMenu(menuName = "Game/Achievements/Definition", fileName = "ACH_")]
public sealed class AchievementDefinition : ScriptableObject
{
    [Header("Identity")]
    public AchievementId id;
    public ModeFilter mode = ModeFilter.Any;

    [Header("Localization Keys")]
    public string table = "LanguageTable";
    public string titleKey;     // ex) "1042" or "ach.score_champion.title"
    public string descKey;      // (폴백) 공통 설명 키

    [Tooltip("티어별 설명 키(1티어=Element 0). 비어있으면 descKey를 사용합니다.")]
    public string[] descTierKeys;

    [Header("Evaluation")]
    public AchievementMetric metric;
    [Tooltip("오름차순 (예: [10000, 30000, 60000])")]
    public int[] thresholds = new int[] { 1, 2, 3 };
    [Tooltip("minCombo 파라미터(예: [5,10,30])")]
    public int[] minComboByTier;

    [Header("Visuals")]
    public Sprite[] tierSprites;

    public bool dimWhenLocked = true;
    public bool resetOnChapterChange = false;

    public int ClampTierIndex(int tier) => Mathf.Clamp(tier - 1, 0, Mathf.Max(0, (tierSprites?.Length ?? 1) - 1));

    // 티어별 설명 키 선택 (없으면 descKey 반환)
    public string GetDescKeyForTier(int tier)
    {
        if (descTierKeys != null && tier >= 1)
        {
            int idx = tier - 1;
            if (idx >= 0 && idx < descTierKeys.Length && !string.IsNullOrEmpty(descTierKeys[idx]))
                return descTierKeys[idx];
        }
        return descKey;
    }

    public string GetTitle() => Loc.GetFrom(table, titleKey);

#if UNITY_EDITOR
    private void OnValidate()
    {
        // thresholds 오름차순 보정
        if (thresholds != null && thresholds.Length > 1)
        {
            for (int i = 1; i < thresholds.Length; i++)
                if (thresholds[i] < thresholds[i - 1]) thresholds[i] = thresholds[i - 1];
        }
    }
#endif
}

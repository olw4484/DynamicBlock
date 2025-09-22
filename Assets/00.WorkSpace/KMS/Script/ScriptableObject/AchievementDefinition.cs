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
    // 필요 시 확장: StageScoreAtLeast, StageClearCount 등
}

[CreateAssetMenu(menuName = "Game/Achievements/Definition", fileName = "ACH_")]
public sealed class AchievementDefinition : ScriptableObject
{
    [Header("Identity")]
    public AchievementId id;
    public ModeFilter mode = ModeFilter.Any;

    [Header("Localization Keys")]
    public string titleKey;  // 예: "ach.score_champion.title"
    public string descKey;   // 예: "ach.score_champion.desc"  (팝업 문구)

    [Header("Localization")]
    public string tableName = "Achievement";

    [Header("Evaluation")]
    public AchievementMetric metric;
    [Tooltip("오름차순으로 입력 (예: [10000, 30000, 60000])")]
    public int[] thresholds = new int[] { 1, 2, 3 };

    [Header("Visuals")]
    [Tooltip("index 0=1티어, 1=2티어, 2=3티어 ...")]
    public Sprite[] tierSprites;

    [Header("Options")]
    public bool dimWhenLocked = true;
    public bool resetOnChapterChange = false;

    public int ClampTierIndex(int tier) => Mathf.Clamp(tier - 1, 0, tierSprites.Length - 1);

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (tierSprites == null || tierSprites.Length < 1 || tierSprites[0] == null)
            Debug.LogWarning($"[ACH DEF] {name}: tierSprites[0] missing");
        if (thresholds != null && thresholds.Length > 1)
            for (int i = 1; i < thresholds.Length; i++)
                if (thresholds[i] < thresholds[i - 1]) thresholds[i] = thresholds[i - 1];
    }
#endif
}

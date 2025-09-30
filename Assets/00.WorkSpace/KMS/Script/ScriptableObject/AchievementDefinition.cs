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
    // �ʿ� �� Ȯ��: StageScoreAtLeast, StageClearCount ��
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
    public string descKey;      // (����) ���� ���� Ű

    [Tooltip("Ƽ� ���� Ű(1Ƽ��=Element 0). ��������� descKey�� ����մϴ�.")]
    public string[] descTierKeys;

    [Header("Evaluation")]
    public AchievementMetric metric;
    [Tooltip("�������� (��: [10000, 30000, 60000])")]
    public int[] thresholds = new int[] { 1, 2, 3 };
    [Tooltip("minCombo �Ķ����(��: [5,10,30])")]
    public int[] minComboByTier;

    [Header("Visuals")]
    public Sprite[] tierSprites;

    public bool dimWhenLocked = true;
    public bool resetOnChapterChange = false;

    public int ClampTierIndex(int tier) => Mathf.Clamp(tier - 1, 0, Mathf.Max(0, (tierSprites?.Length ?? 1) - 1));

    // Ƽ� ���� Ű ���� (������ descKey ��ȯ)
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
        // thresholds �������� ����
        if (thresholds != null && thresholds.Length > 1)
        {
            for (int i = 1; i < thresholds.Length; i++)
                if (thresholds[i] < thresholds[i - 1]) thresholds[i] = thresholds[i - 1];
        }
    }
#endif
}

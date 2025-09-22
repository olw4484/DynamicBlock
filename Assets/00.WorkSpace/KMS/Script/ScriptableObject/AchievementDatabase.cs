using UnityEngine;

[CreateAssetMenu(menuName = "Game/Achievements/Database", fileName = "AchievementDatabase")]
public sealed class AchievementDatabase : ScriptableObject
{
    public AchievementDefinition[] items;

    public AchievementDefinition Get(AchievementId id)
    {
        if (items == null) return null;
        for (int i = 0; i < items.Length; i++)
            if (items[i] && items[i].id == id) return items[i];
        return null;
    }
}

using System.Collections.Generic;
using System.Linq;
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

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (items == null) return;

        // 편집 중(null 슬롯 존재)에는 건드리지 말고 탈출
        if (items.Any(x => x == null)) return;

        // 여기부터는 모두 채워졌을 때만 정리/정렬
        var nonNull = items; // 이미 null 없음
                             // 중복 경고만 하고(자동 삭제 X), 보기 좋게 정렬만
        var dups = nonNull.GroupBy(x => x.id).Where(g => g.Count() > 1);
        foreach (var g in dups)
            Debug.LogWarning($"[ACH DB] Duplicate id: {g.Key} x{g.Count()}", this);

        items = nonNull.OrderBy(x => (int)x.id).ToArray();
    }
#endif
}

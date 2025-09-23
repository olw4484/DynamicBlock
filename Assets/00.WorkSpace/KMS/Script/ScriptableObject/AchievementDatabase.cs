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

        // ���� ��(null ���� ����)���� �ǵ帮�� ���� Ż��
        if (items.Any(x => x == null)) return;

        // ������ʹ� ��� ä������ ���� ����/����
        var nonNull = items; // �̹� null ����
                             // �ߺ� ��� �ϰ�(�ڵ� ���� X), ���� ���� ���ĸ�
        var dups = nonNull.GroupBy(x => x.id).Where(g => g.Count() > 1);
        foreach (var g in dups)
            Debug.LogWarning($"[ACH DB] Duplicate id: {g.Key} x{g.Count()}", this);

        items = nonNull.OrderBy(x => (int)x.id).ToArray();
    }
#endif
}

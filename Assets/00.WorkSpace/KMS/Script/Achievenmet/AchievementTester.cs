using System;
using System.Linq;
using TMPro;
using UnityEngine;

public sealed class AchievementTester : MonoBehaviour
{
    [Header("Refs")]
    public AchievementDatabase database;
    public MedalItemView[] medalViews;
    public AchievementPopupController popup;

    private AchievementService _svc;
    private GameData _data;

    void Awake()
    {
        Loc.DefaultTable = "Achievement"; // String Table 이름과 일치시킬 것
        _data = _00.WorkSpace.GIL.Scripts.Managers.MapManager.Instance?.saveManager?.gameData
                ?? GameData.NewDefault();
        _data.MigrateIfNeeded();
        _svc = new AchievementService(_data, database);
    }

    void Start()
    {
        RefreshUIAndPop();
    }

    [ContextMenu("Simulate Classic Win + ScoreUp")]
    public void SimulateProgress()
    {
        // 점수/콤보/플레이/블록 제거 등 임의 누적
        _data.ApplyRoundResult(GameMode.Classic, finalScore: UnityEngine.Random.Range(5000, 20000),
            bestComboThisRound: UnityEngine.Random.Range(3, 12),
            removedBlocks: UnityEngine.Random.Range(30, 120),
            removedSpecial: UnityEngine.Random.Range(0, 10),
            fruits: UnityEngine.Random.Range(0, 8),
            isWin: true, stageClearedThisRound: false);

        RefreshUIAndPop();
    }

    private void RefreshUIAndPop()
    {
        var list = _svc.EvaluateAll(recordUnlocks: true, DateTime.UtcNow, out var newly);

        foreach (var v in medalViews)
        {
            if (!v) continue;
            var def = typeof(MedalItemView).GetField("def", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                      ?.GetValue(v) as AchievementDefinition;
            if (!def) continue;
            var prog = list.FirstOrDefault(p => p.id == def.id);
            v.Refresh(prog);
        }

        // 새 해금 팝업(있으면 1개만 샘플로 표시)
        if (newly != null && newly.Count > 0 && popup)
        {
            var def = newly[0];
            var tier = _data.unlocked.FindLast(u => u.id == (int)def.id).tier;
            popup.Show(def, tier, DateTime.UtcNow, def.thresholds != null && def.thresholds.Length > 0 ? new object[] { def.thresholds[Mathf.Clamp(tier - 1, 0, def.thresholds.Length - 1)] } : Array.Empty<object>());
        }
    }
}

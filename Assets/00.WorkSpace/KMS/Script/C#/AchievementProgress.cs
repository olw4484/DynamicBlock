using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class AchievementProgress
{
    public AchievementId id;
    public int currentValue;
    public int tier;        // 0=미달성, 1/2/3...
    public int nextTarget;  // 다음 티어 목표치(없으면 0)
}

public interface ILocalization
{
    string Get(string key, params object[] args);
}

public sealed class AchievementService
{
    private readonly GameData _data;
    private readonly AchievementDatabase _db;

    public AchievementService(GameData data, AchievementDatabase db)
    {
        _data = data; _db = db;
    }

    public AchievementProgress Evaluate(AchievementDefinition def)
    {
        int val = GetMetricValue(def.metric, _data);
        // 모드 필터가 있으면 필요 시 현재 모드/누적 해석으로 제한 (여기선 누적값 사용)
        int tier = 0; int next = 0;
        if (def.thresholds != null && def.thresholds.Length > 0)
        {
            for (int i = 0; i < def.thresholds.Length; i++)
            {
                if (val >= def.thresholds[i]) tier = i + 1;
                else { next = def.thresholds[i]; break; }
            }
        }
        return new AchievementProgress { id = def.id, currentValue = val, tier = tier, nextTarget = next };
    }

    public List<AchievementProgress> EvaluateAll(bool recordUnlocks, DateTime utcNow, out List<AchievementDefinition> newlyUnlocked)
    {
        newlyUnlocked = new List<AchievementDefinition>();
        var list = new List<AchievementProgress>();
        if (_db?.items == null) return list;

        for (int i = 0; i < _db.items.Length; i++)
        {
            var def = _db.items[i];
            if (!def) continue;

            var prog = Evaluate(def);
            list.Add(prog);

            // 이미 해금한 최대 티어
            int prevTier = 0;
            for (int u = 0; u < _data.unlocked.Count; u++)
                if (_data.unlocked[u].id == (int)def.id)
                    prevTier = Mathf.Max(prevTier, _data.unlocked[u].tier);

            if (recordUnlocks && prog.tier > prevTier)
            {
                _data.RecordAchievementUnlocked((int)def.id, prog.tier, utcNow);
                newlyUnlocked.Add(def);
            }
        }
        return list;
    }

    private static int GetMetricValue(AchievementMetric metric, GameData d)
    {
        switch (metric)
        {
            case AchievementMetric.BestScore: return d.highScore;
            case AchievementMetric.BestCombo: return d.bestCombo;
            case AchievementMetric.PlayCount: return d.playCount;
            case AchievementMetric.LoginDays: return d.loginDays;
            case AchievementMetric.AdventureStageClears: return d.adventureStageClears;
            case AchievementMetric.AdventureBestWinStreak: return d.adventureBestWinStreak;
            case AchievementMetric.TotalBlocksRemoved: return d.totalBlocksRemoved;
            case AchievementMetric.SpecialBlocksRemoved: return d.specialBlocksRemoved;
            case AchievementMetric.FruitCollected: return d.fruitCollected;
            default: return 0;
        }
    }
}

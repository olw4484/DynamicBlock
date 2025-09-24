using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.Localization.Settings;

public sealed class AchievementUIBinder : MonoBehaviour
{
    [Header("Data/Defs")]
    [SerializeField] private SaveManager saveManager;
    [SerializeField] private AchievementDatabase database;

    [Header("Stats UI")]
    [SerializeField] private TMP_Text txtHighestCombo;
    [SerializeField] private TMP_Text txtBestScore;
    [SerializeField] private TMP_Text txtRounds;
    [SerializeField] private TMP_Text txtLoginDays;

    [Header("Medals")]
    [SerializeField] private MedalItemView[] medalViews;

    [Header("Popup (optional)")]
    [SerializeField] private AchievementPopupController popup;
    [SerializeField] private bool showPopupOnUnlock = true;

    [Header("Unlock FX / Badge")]
    [SerializeField] private RectTransform unlockFxAnchor; // 업적 버튼 등 이펙트 기준점
    [SerializeField] private GameObject achievementBadgeDot; // 빨간 점 GO
    [SerializeField] private string achievementPanelKey = "Achievement";
    [SerializeField] private AchievementsButtonNotifier notifier;

    private readonly Dictionary<AchievementId, int> _seenMaxTier = new();
    private bool _seededSeen;

    private GameData _data;
    private AchievementService _svc;

    void Awake()
    {
        if (!database) Debug.LogWarning("[AchievementUIBinder] Database not set.");
        Loc.DefaultTable = "LanguageTable";

        _data = saveManager ? saveManager.gameData : GameData.NewDefault();
        _data.MigrateIfNeeded();
        _svc = new AchievementService(_data, database);
    }

    IEnumerator Start()
    {
        yield return LocalizationSettings.InitializationOperation;
        RefreshAll(recordUnlocks: false);
    }

    void OnEnable()
    {
        LocalizationSettings.SelectedLocaleChanged += OnLocaleChanged;
        if (saveManager)
        {
            saveManager.AfterLoad += OnAfterSaveLoad;
            saveManager.AfterSave += OnAfterSave;
        }

        if (Game.IsBound) Game.Bus.Subscribe<PanelToggle>(OnPanelToggle, replaySticky: false);

        RefreshAll(recordUnlocks: false);
    }

    void OnDisable()
    {
        LocalizationSettings.SelectedLocaleChanged -= OnLocaleChanged;
        if (saveManager)
        {
            saveManager.AfterLoad -= OnAfterSaveLoad;
            saveManager.AfterSave -= OnAfterSave;
        }
        if (Game.IsBound) Game.Bus.Unsubscribe<PanelToggle>(OnPanelToggle);
    }

    void OnPanelToggle(PanelToggle e)
    {
        if (e.key == achievementPanelKey && e.on)
        {
            notifier?.ClearNotification();

            foreach (var u in _data.unlocked)
            {
                var id = (AchievementId)u.id;
                if (!_seenMaxTier.TryGetValue(id, out var prev) || u.tier > prev)
                    _seenMaxTier[id] = u.tier;
            }
        }
    }

    void OnAfterSave(GameData d)
    {
        _data = d ?? _data;
        RefreshAll(recordUnlocks: false);
    }

    void OnLocaleChanged(UnityEngine.Localization.Locale _)
    {
        // 숫자는 그대로지만, 메달 타이틀/설명 키가 로케일 영향을 받으니 재그리기
        RefreshAll(recordUnlocks: false);
    }

    void OnAfterSaveLoad(GameData data)
    {
        _data = data ?? _data;
        _svc = new AchievementService(_data, database);
        RefreshAll(recordUnlocks: false);
    }

    // 외부에서 라운드 종료/진행 업데이트 후 이걸 호출해도 됨
    public void RefreshAll(bool recordUnlocks)
    {
        if (_data == null || _svc == null) return;

        // (1) 통계
        if (txtHighestCombo) txtHighestCombo.text = Mathf.Max(_data.bestCombo, _data.currentCombo).ToString();
        if (txtBestScore) txtBestScore.text = _data.highScore.ToString("N0");
        if (txtRounds) txtRounds.text = _data.playCount.ToString();
        if (txtLoginDays) txtLoginDays.text = _data.loginDays.ToString();

        // (2) 평가
        var list = _svc.EvaluateAll(recordUnlocks, DateTime.UtcNow, out var newlyUnlocked);
        var byId = new Dictionary<AchievementId, AchievementProgress>(list.Count);
        foreach (var p in list) byId[p.id] = p;

        // (3) 메달 반영
        if (medalViews != null)
        {
            foreach (var v in medalViews)
            {
                if (!v) continue;
                var def = v.Definition;
                if (!def) continue;
                if (byId.TryGetValue(def.id, out var prog))
                    v.Refresh(prog);
            }
        }

        var currentTiers = new Dictionary<AchievementId, int>();
        foreach (var p in list)
            currentTiers[p.id] = Mathf.Max(currentTiers.TryGetValue(p.id, out var prev) ? prev : 0, p.tier);

        bool anyTierUp = false;
        if (!_seededSeen)
        {
            foreach (var kv in currentTiers) _seenMaxTier[kv.Key] = kv.Value;
            _seededSeen = true;   // 첫 진입 배지 방지
        }
        else
        {
            foreach (var kv in currentTiers)
            {
                int seen = _seenMaxTier.TryGetValue(kv.Key, out var s) ? s : 0;
                if (kv.Value > 0 && kv.Value > seen)
                {
                    _seenMaxTier[kv.Key] = kv.Value;
                    anyTierUp = true;
                }
            }
        }

        if (anyTierUp) TriggerUnlockVisuals();

        // 5) 팝업
        if (showPopupOnUnlock && popup && (newlyUnlocked?.Count ?? 0) > 0)
        {
            var def = newlyUnlocked[0];
            var rec = _data.unlocked.FindLast(u => u.id == (int)def.id);
            int tier = rec.tier;
            object[] args = (def.thresholds != null && def.thresholds.Length > 0)
                ? new object[] { def.thresholds[Mathf.Clamp(tier - 1, 0, def.thresholds.Length - 1)] }
                : Array.Empty<object>();
            popup.Show(def, tier, DateTime.UtcNow, args);
        }
    }

    private void TriggerUnlockVisuals()
    {
        notifier?.NotifyUnlocked();
    }

    public void ShowPopupFor(AchievementDefinition def)
    {
        Game.UI.SetPanel("Achievement_Popup", true);

        var prog = _svc.Evaluate(def);
        int tierToShow = Mathf.Max(1, prog.tier);

        popup.Show(
            def, tierToShow, System.DateTime.UtcNow,
            prog.tier > 0 ? new object[] { def.thresholds[tierToShow - 1] }
                          : (prog.nextTarget > 0 ? new object[] { prog.nextTarget } : System.Array.Empty<object>())
        );
    }
}

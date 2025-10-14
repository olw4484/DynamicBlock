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

    private GameData _data;
    private AchievementService _svc;

    void Awake()
    {
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

        if (LocalizationSettings.InitializationOperation.IsDone)
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

        // 1) 통계 텍스트
        if (txtHighestCombo) txtHighestCombo.text = Mathf.Max(_data.bestCombo, _data.currentCombo).ToString();
        if (txtBestScore) txtBestScore.text = _data.highScore.ToString("#0");
        if (txtRounds) txtRounds.text = _data.playCount.ToString();
        if (txtLoginDays) txtLoginDays.text = _data.loginDays.ToString();

        // 2) 업적 평가(+해금 기록)
        var list = _svc.EvaluateAll(recordUnlocks, DateTime.UtcNow, out var newlyUnlocked);
        var byId = new Dictionary<AchievementId, AchievementProgress>(list.Count);
        foreach (var p in list) byId[p.id] = p;
        foreach (var v in medalViews)
        {
            var def = v ? v.Definition : null;
            var inDb = def && byId.ContainsKey(def.id);
        }

        // 3) 메달 UI 반영
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

        // 4) 팝업
        if (showPopupOnUnlock && popup && newlyUnlocked != null && newlyUnlocked.Count > 0)
        {
            var def = newlyUnlocked[0];
            var rec = _data.unlocked.FindLast(u => u.id == (int)def.id);
            int tier = rec.tier;

            // 해금 시각 사용 (utcTicks, utc 등 저장 형태에 맞춰 변환)
            DateTime unlockedAtUtc = GetAchievementUnlockedAtUtc(rec);

            object[] args = (def.thresholds != null && def.thresholds.Length > 0)
                ? new object[] { def.thresholds[Mathf.Clamp(tier - 1, 0, def.thresholds.Length - 1)] }
                : Array.Empty<object>();

            popup.Show(def, tier, unlockedAtUtc, args);
        }
    }

    public void ShowPopupFor(AchievementDefinition def)
    {
        Sfx.Button();
        Game.UI.SetPanel("Achievement_Popup", true);

        var prog = _svc.Evaluate(def);
        int tierToShow = Mathf.Max(1, prog.tier);

        // 먼저 tier 일치로 찾고, 없으면 id만으로 폴백
        int idx = _data.unlocked.FindLastIndex(u => u.id == (int)def.id && u.tier == tierToShow);
        if (idx < 0) idx = _data.unlocked.FindLastIndex(u => u.id == (int)def.id);

        DateTime unlockedAtUtc = (idx >= 0)
            ? GetAchievementUnlockedAtUtc(_data.unlocked[idx])
            : DateTime.UtcNow; // 기록 없으면 현재시간(원하면 MinValue로 넘기고 UI에서 숨겨도 OK)

        popup.Show(
            def, tierToShow, unlockedAtUtc,
            prog.tier > 0 ? new object[] { def.thresholds[tierToShow - 1] }
                          : (prog.nextTarget > 0 ? new object[] { prog.nextTarget } : Array.Empty<object>())
        );
    }
    private static DateTime GetAchievementUnlockedAtUtc(object rec)
    {
        if (rec == null) return DateTime.UtcNow;

        var t = rec.GetType();

        // 1) ticks(long)로 저장된 경우: utcTicks
        var fTicks = t.GetField("utcTicks");
        if (fTicks != null && fTicks.FieldType == typeof(long))
        {
            long ticks = (long)fTicks.GetValue(rec);
            return new DateTime(ticks, DateTimeKind.Utc);
        }

        // 2) Unix time (초/밀리초)로 저장된 경우: utcSeconds / utcMs
        var fSec = t.GetField("utcSeconds");
        if (fSec != null && fSec.FieldType == typeof(long))
        {
            long sec = (long)fSec.GetValue(rec);
            return DateTimeOffset.FromUnixTimeSeconds(sec).UtcDateTime;
        }
        var fMs = t.GetField("utcMs");
        if (fMs != null && fMs.FieldType == typeof(long))
        {
            long ms = (long)fMs.GetValue(rec);
            return DateTimeOffset.FromUnixTimeMilliseconds(ms).UtcDateTime;
        }

        // 3) DateTime 자체로 저장된 경우: unlockedAtUtc
        var fDt = t.GetField("unlockedAtUtc");
        if (fDt != null && fDt.FieldType == typeof(DateTime))
        {
            var dt = (DateTime)fDt.GetValue(rec);
            return DateTime.SpecifyKind(dt, DateTimeKind.Utc).ToUniversalTime();
        }

        // 폴백
        return DateTime.UtcNow;
    }
}

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
        // ������ �ٲ�� ��/���� �ٽ� �׸���
        LocalizationSettings.SelectedLocaleChanged += OnLocaleChanged;
        // ���̺� ���ε� �� ��׸���(������Ʈ�� AfterLoad �̺�Ʈ�� �̹� ����)
        if (saveManager) saveManager.AfterLoad += OnAfterSaveLoad;
        RefreshAll(recordUnlocks: false);
    }

    void OnDisable()
    {
        LocalizationSettings.SelectedLocaleChanged -= OnLocaleChanged;
        if (saveManager) saveManager.AfterLoad -= OnAfterSaveLoad;
    }

    void OnLocaleChanged(UnityEngine.Localization.Locale _)
    {
        // ���ڴ� �״������, �޴� Ÿ��Ʋ/���� Ű�� ������ ������ ������ ��׸���
        RefreshAll(recordUnlocks: false);
    }

    void OnAfterSaveLoad(GameData data)
    {
        _data = data ?? _data;
        _svc = new AchievementService(_data, database);
        RefreshAll(recordUnlocks: false);
    }

    // �ܺο��� ���� ����/���� ������Ʈ �� �̰� ȣ���ص� ��
    public void RefreshAll(bool recordUnlocks)
    {
        if (_data == null || _svc == null) return;

        // 1) ��� �ؽ�Ʈ
        if (txtHighestCombo) txtHighestCombo.text = Mathf.Max(_data.bestCombo, _data.currentCombo).ToString();
        if (txtBestScore) txtBestScore.text = _data.highScore.ToString("N0");
        if (txtRounds) txtRounds.text = _data.playCount.ToString();
        if (txtLoginDays) txtLoginDays.text = _data.loginDays.ToString();

        // 2) ���� ��(+�ر� ���)
        var list = _svc.EvaluateAll(recordUnlocks, DateTime.UtcNow, out var newlyUnlocked);
        var byId = new Dictionary<AchievementId, AchievementProgress>(list.Count);
        foreach (var p in list) byId[p.id] = p;
        foreach (var v in medalViews)
        {
            var def = v ? v.Definition : null;
            var inDb = def && byId.ContainsKey(def.id);
            Debug.Log($"[ACH-BIND] view={v?.name} def={(def ? def.id.ToString() : "NULL")} inDb={inDb}");
        }

        // 3) �޴� UI �ݿ�
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

        // 4) �˾�(�ɼ�)
        if (showPopupOnUnlock && popup && newlyUnlocked != null && newlyUnlocked.Count > 0)
        {
            // ���� �ֱ� �رݸ� ���÷� ǥ��(���ϸ� ť�� ������ ��)
            var def = newlyUnlocked[0];
            var rec = _data.unlocked.FindLast(u => u.id == (int)def.id);
            int tier = rec.tier;
            object[] args = (def.thresholds != null && def.thresholds.Length > 0)
                ? new object[] { def.thresholds[Mathf.Clamp(tier - 1, 0, def.thresholds.Length - 1)] }
                : Array.Empty<object>();
            popup.Show(def, tier, DateTime.UtcNow, args);
        }
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

using System;
using UnityEngine;
using UnityEngine.UI;

public sealed class ServiceNoticeGate : MonoBehaviour
{
    [Header("Panel key")]
    [SerializeField] string panelKey = "ServiceNotice";

    [Header("Window (Local time)")]
    [SerializeField] int startYear = 2026;
    [SerializeField] int startMonth = 4;
    [SerializeField] int startDay = 3;
    [SerializeField] int endYear = 2026, endMonth = 5, endDay = 3; // [start <= today < end]

    [Header("UI")]
    [SerializeField] Toggle dontShowTodayToggle;
    [SerializeField] Button okButton;

    const string SnoozeKey = "ServiceNotice_SnoozeUntilUtc";

    EventQueue _bus;
    System.Action<ServiceNoticeCheck> _onCheck;

    void Awake()
    {
        if (okButton) okButton.onClick.AddListener(OnClickOk);
    }

    void OnEnable()
    {
        StartCoroutine(GameBindingUtil.WaitAndRun(Bind));
    }

    void Bind()
    {
        _bus = Game.Bus;
        _bus.Subscribe<ServiceNoticeCheck>(_ => TryOpenIfNeeded(), replaySticky: false);
        
        TryOpenIfNeeded();
    }


    void OnDisable()
    {
        if (_bus != null && _onCheck != null)
            _bus.Unsubscribe(_onCheck);
    }

    public void TryOpenIfNeeded()
    {
        var bus = _bus ?? Game.Bus;
        if (bus == null) return;

        // 디버그 로그(반드시 찍혀야 함)
        Debug.Log($"[Notice] TryOpen ENTER now={DateTime.UtcNow:u}, key={panelKey}");

        var nowUtc = DateTime.UtcNow;
        var startUtc = LocalToUtc(new DateTime(startYear, startMonth, startDay, 0, 0, 0, DateTimeKind.Unspecified));
        var endUtc = LocalToUtc(new DateTime(endYear, endMonth, endDay, 0, 0, 0, DateTimeKind.Unspecified));

        if (!(nowUtc >= startUtc && nowUtc < endUtc))
        {
            bus.PublishImmediate(new PanelToggle(panelKey, false));
            return;
        }

        var snoozeUntil = GetSnoozeUtc();
        if (nowUtc < snoozeUntil)
        {
            bus.PublishImmediate(new PanelToggle(panelKey, false));
            return;
        }

        var onEvt = new PanelToggle(panelKey, true);
        bus.PublishSticky(onEvt, alsoEnqueue: false);
        bus.PublishImmediate(onEvt);
        Debug.Log("[Notice] OPEN");
    }

    public void OnClickOk()
    {
        Sfx.Button();

        if (dontShowTodayToggle && dontShowTodayToggle.isOn)
        {
            var until = DateTime.UtcNow.AddHours(24);
            PlayerPrefs.SetString(SnoozeKey, until.Ticks.ToString());
            PlayerPrefs.Save();
            Debug.Log($"[Notice] Snooze set until {until:u}");
        }

        var bus = _bus ?? Game.Bus;
        if (bus != null)
        {
            var off = new PanelToggle(panelKey, false);
            bus.PublishSticky(off, alsoEnqueue: false);
            bus.PublishImmediate(off);
        }
        else
        {
            (Game.UI as UIManager)?.SetPanel(panelKey, false, ignoreDelay: true);
            Debug.LogWarning("[Notice] OnClickOk: bus==null, closed via UIManager fallback.");
        }
    }


    static DateTime LocalToUtc(DateTime localUnspecified)
    {
        var local = DateTime.SpecifyKind(localUnspecified, DateTimeKind.Local);
        return local.ToUniversalTime();
    }

    static DateTime GetSnoozeUtc()
    {
        if (!PlayerPrefs.HasKey(SnoozeKey)) return DateTime.MinValue;
        var str = PlayerPrefs.GetString(SnoozeKey, "0");
        if (long.TryParse(str, out var ticks) && ticks > 0) return new DateTime(ticks, DateTimeKind.Utc);
        return DateTime.MinValue;
    }
}

using System.Collections;
using UnityEngine;

public sealed class ReviveRouter : MonoBehaviour
{
    public static ReviveRouter I { get; private set; }

    public enum State { Idle, DownedGuard, PanelOpen, AdPlaying, Revived, GiveUp }
    [Header("Config")]
    [SerializeField] float reviveDelaySec = 1.0f;   // ٿ  г  
    [SerializeField] float panelWindowSec = 5.0f;   // ̺  â ȿð(ǥÿ)
    [SerializeField] bool reviveOncePerRun = true;

    State _state = State.Idle;
    bool _reviveConsumedThisRun;
    int _downedScore;
    bool _busWired;

    EventQueue Bus => Game.Bus;
    IAdService Ads => Game.Ads;

    void Awake()
    {
        if (I != null) { Destroy(gameObject); return; }
        I = this; DontDestroyOnLoad(gameObject);
    }

    // === ܺο     ===
    public static ReviveRouter CreateIfNeeded()
    {
        if (I) return I;
        var go = new GameObject("[ReviveRouter]");
        return go.AddComponent<ReviveRouter>();
    }

    // ===    /  ȣ (ɼ) ===
    public void ResetRun()
    {
        _state = State.Idle;
        _reviveConsumedThisRun = false;
        DisarmAll("reset_run");
        Time.timeScale = 1f;
    }

    // === ٿ  ===
    public void OnPlayerDowned(int score)
    {
        if (_state != State.Idle) return;

        _downedScore = score;
        StartCoroutine(CoDowned());
    }

    IEnumerator CoDowned()
    {
        _state = State.DownedGuard;

        //   ȣ 
        AdStateProbe.IsRevivePending = true;
        ReviveGate.Arm(reviveDelaySec + panelWindowSec + 0.2f);
        ReviveLatch.Arm(panelWindowSec + 1.0f, "downed");

        // Է¶ + Ͻ
        Bus?.PublishImmediate(new InputLock(true, "PreRevive"));
        Time.timeScale = 0f;

        yield return new WaitForSecondsRealtime(Mathf.Max(0f, reviveDelaySec));

        if (reviveOncePerRun && _reviveConsumedThisRun)
        {
            ForceGiveUp("no_revive_left"); // ٷ 
            yield break;
        }

        OpenRevivePanel();
    }

    void OpenRevivePanel()
    {
        _state = State.PanelOpen;
        // UIManager г 
        var ui = FindFirstObjectByType<UIManager>(FindObjectsInactive.Include);
        ui?.SetPanel("Revive", true, ignoreDelay: true);

        // ʿ /Ÿ̸ ȳ  (UI ó)
        Bus?.PublishImmediate(new InputLock(false, "PreRevive"));
    }

    // === ReviveScreen ȣ: /ŸӾƿ ===
    public void ForceGiveUp(string reason)
    {
        if (_state != State.PanelOpen && _state != State.AdPlaying) return;

        DisarmAll($"giveup:{reason}");
        Time.timeScale = 1f;
        _state = State.GiveUp;

        var ui = FindFirstObjectByType<UIManager>(FindObjectsInactive.Include);
        ui?.ClosePanelImmediate("Revive");

        Bus?.PublishImmediate(new GiveUpRequest(reason));
    }

    // === ReviveScreen ȣ:  // ===
    public void OnAdStart()
    {
        if (_state != State.PanelOpen) return;
        _state = State.AdPlaying;

        var ui = FindFirstObjectByType<UIManager>(FindObjectsInactive.Include);
        ui?.SetPanel("Revive", false, ignoreDelay: true);
    }

    public void OnAdRewardGranted()
    {
        // once-per-run ó
        _reviveConsumedThisRun = true;
    }

    public void OnAdClosed(bool rewarded)
    {
        if (_state != State.AdPlaying) return;

        if (rewarded)
        {
            // ſ ɸ  publish õ/Ʈ ߶
            GameOverUtil.CancelPending("revive_granted");
            GameOverGate.Reset("revive_granted");
            GameOverUtil.SuppressFor(2.0f, "revive_grace");

            DisarmAll("revive_granted");
            Time.timeScale = 1f;

            Bus?.PublishImmediate(new ContinueGranted());
            Bus?.PublishImmediate(new RevivePerformed());

            _state = State.Revived;
            StartCoroutine(CoBackToIdleDelayed(2.0f));
        }
        else
        {
            ForceGiveUp("ad_closed_no_reward");
        }
    }

    public void SetPolicy(bool oncePerRun, float delaySec, float windowSec)
    {
        reviveOncePerRun = oncePerRun;
        reviveDelaySec = delaySec;
        panelWindowSec = windowSec;
        Debug.Log($"[ReviveRouter] Policy set: once={oncePerRun}, delay={delaySec}, window={windowSec}");
    }

    public void SetPolicyFromMode(GameMode mode)
    {
        if (mode == GameMode.Adventure)
            SetPolicy(oncePerRun: false, delaySec: 1.0f, windowSec: 5.0f);
        else // Classic
            SetPolicy(oncePerRun: true, delaySec: 1.0f, windowSec: 5.0f);
    }

    IEnumerator CoBackToIdleDelayed(float sec)
    {
        yield return new WaitForSecondsRealtime(Mathf.Max(0.1f, sec));
        _state = State.Idle;
    }

    void DisarmAll(string why)
    {
        AdStateProbe.IsRevivePending = false;
        ReviveGate.Disarm();
        ReviveLatch.Disarm(why);
        UIStateProbe.DisarmReviveGrace();
        UIStateProbe.DisarmResultGuard();
        GameOverUtil.ResetAll(why);
    }

    public void BindToGameBus()
    {
        if (_busWired || Game.Bus == null) return;
        _busWired = true;

        // ٿ   
        Game.Bus.Subscribe<PlayerDowned>(e => { OnPlayerDowned(e.score); }, replaySticky: false);

        //       
        Game.Bus.Subscribe<GameResetDone>(_ => { ResetRun(); }, replaySticky: false);
    }

}

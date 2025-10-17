using System.Collections;
using UnityEngine;

public sealed class ReviveRouter : MonoBehaviour
{
    public static ReviveRouter I { get; private set; }

    public enum State { Idle, DownedGuard, PanelOpen, AdPlaying, Revived, GiveUp }
    [Header("Config")]
    [SerializeField] float reviveDelaySec = 1.0f;   // 다운 후 패널 열기까지 지연
    [SerializeField] float panelWindowSec = 5.0f;   // 리바이브 선택 창 유효시간(표시용)
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

    // === 외부에서 한 번만 만들어도 됨 ===
    public static ReviveRouter CreateIfNeeded()
    {
        if (I) return I;
        var go = new GameObject("[ReviveRouter]");
        return go.AddComponent<ReviveRouter>();
    }

    // === 러닝 한 판 시작/리셋 시 호출 (옵션) ===
    public void ResetRun()
    {
        _state = State.Idle;
        _reviveConsumedThisRun = false;
        DisarmAll("reset_run");
        Time.timeScale = 1f;
    }

    // === 다운 진입점 ===
    public void OnPlayerDowned(int score)
    {
        if (_state != State.Idle) return;

        _downedScore = score;
        StartCoroutine(CoDowned());
    }

    IEnumerator CoDowned()
    {
        _state = State.DownedGuard;

        // 결과 라우팅 보호 상태
        AdStateProbe.IsRevivePending = true;
        ReviveGate.Arm(reviveDelaySec + panelWindowSec + 0.2f);
        ReviveLatch.Arm(panelWindowSec + 1.0f, "downed");

        // 입력락 + 일시정지
        Bus?.PublishImmediate(new InputLock(true, "PreRevive"));
        Time.timeScale = 0f;

        yield return new WaitForSecondsRealtime(Mathf.Max(0f, reviveDelaySec));

        if (reviveOncePerRun && _reviveConsumedThisRun)
        {
            ForceGiveUp("no_revive_left"); // 바로 결과로
            yield break;
        }

        OpenRevivePanel();
    }

    void OpenRevivePanel()
    {
        _state = State.PanelOpen;
        // UIManager가 패널 열기
        var ui = FindFirstObjectByType<UIManager>(FindObjectsInactive.Include);
        ui?.SetPanel("Revive", true, ignoreDelay: true);

        // 필요시 사운드/타이머 안내 시작 (UI에서 처리)
        Bus?.PublishImmediate(new InputLock(false, "PreRevive"));
    }

    // === ReviveScreen에서 호출: 포기/타임아웃 ===
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

    // === ReviveScreen에서 호출: 광고 시작/보상/종료 ===
    public void OnAdStart()
    {
        if (_state != State.PanelOpen) return;
        _state = State.AdPlaying;

        var ui = FindFirstObjectByType<UIManager>(FindObjectsInactive.Include);
        ui?.SetPanel("Revive", false, ignoreDelay: true);
    }

    public void OnAdRewardGranted()
    {
        // once-per-run 처리
        _reviveConsumedThisRun = true;
    }

    public void OnAdClosed(bool rewarded)
    {
        if (_state != State.AdPlaying) return;

        if (rewarded)
        {
            // 과거에 걸린 결과 publish 시도/게이트를 잘라냄
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
    }

    public void BindToGameBus()
    {
        if (_busWired || Game.Bus == null) return;
        _busWired = true;

        // 다운 → 라우터 진입
        Game.Bus.Subscribe<PlayerDowned>(e => { OnPlayerDowned(e.score); }, replaySticky: false);

        // 한 판 리셋 → 라우터 상태 리셋
        Game.Bus.Subscribe<GameResetDone>(_ => { ResetRun(); }, replaySticky: false);
    }

}

// AdManager.cs
using _00.WorkSpace.GIL.Scripts.Messages;
using GoogleMobileAds.Api;
using System;
using System.Collections;
using System.Reflection;
using UnityEngine;

public class AdManager : MonoBehaviour, IAdService
{
    public static AdManager Instance { get; private set; }

    public InterstitialAdController Interstitial { get; private set; }
    public BannerAdController Banner { get; private set; }
    public RewardAdController Reward { get; private set; }

    public int Order => 80;

    [Header("Feature switches")]
    public bool DisableInterstitials = true;

    [Header("Policy (쿨다운은 자동노출용)")]
    public int RewardTime = 90;
    public int InterstitialTime = 120;

    public DateTime NextRewardTime = DateTime.MinValue;
    public DateTime NextInterstitialTime = DateTime.MinValue;

    const float POST_RESUME_DEBOUNCE = 0.8f;
    float _lastResumeAt = -999f;

    bool _adsReady;
    bool _guardsWired;

    bool _rewardInProgress;
    bool _interstitialInProgress;

    float _lastRewardShowAt = -999f;
    float _lastInterShowAt = -999f;

    const float WATCHDOG_SEC = 12f;
    Coroutine _rewardWatchdog;
    Coroutine _interstitialWatchdog;

    int _bannerBlockCount = 0;

#if DEVELOPMENT_BUILD || UNITY_EDITOR
    [Header("DEV / QA")]
    public bool ForceDevSimulateReward = true;
    public float DevSimDelay = 0.2f;
#endif

    // ===== Unity =====
    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        try
        {
            var prop = typeof(MobileAds).GetProperty("RaiseAdEventsOnUnityMainThread",
                BindingFlags.Public | BindingFlags.Static);
            if (prop != null && prop.CanWrite) prop.SetValue(null, true, null);
        }
        catch { }

        AdsInitGate.EnsureInit();
        AdsInitGate.WhenReady(() =>
        {
            Debug.Log("[Ads] MobileAds initialized (via gate)");

            if (!DisableInterstitials)
            {
                Interstitial = new InterstitialAdController();
                Interstitial.Init();
            }
            Banner = new BannerAdController(); Banner.InitAdaptive();
            Reward = new RewardAdController(); Reward.Init();

            WireAdGuards(Interstitial, Reward);
            _adsReady = true;

            StartCoroutine(Co_ConsumeReviveTokenAfterBind());
        });
    }

    void OnEnable() { Game.BindAds(this); }
    void OnDisable() { Game.UnbindAds(this); }

    void OnApplicationPause(bool pause)
    {
        if (!pause)
        {
            if (AdStateProbe.IsRevivePending || ReviveGate.IsArmed)
                StartCoroutine(Co_ConsumeReviveTokenSoon());
        }
    }


    void OnApplicationFocus(bool focus)
    {
        if (focus)
        {
            _lastResumeAt = Time.realtimeSinceStartup;

            AdPauseGuard.OnAdClosedOrFailed();
            UIStateProbe.DisarmResultGuard();
            UIStateProbe.DisarmReviveGrace();
            GameOverUtil.ResetAll("focus_cleanup");

            AdStateProbe.IsFullscreenShowing = false;
            AdStateProbe.IsRevivePending = false;
            ReviveLatch.Disarm("app focus");
            Time.timeScale = 1f;

            bool hadAnyAd = _interstitialInProgress || _rewardInProgress;
            if (hadAnyAd && !AdReviveToken.HasPending() && Game.IsBound)
                Game.Bus.PublishImmediate(new AdFinished());

            if (!UIStateProbe.IsResultOpen && !UIStateProbe.IsReviveOpen
                && (AdStateProbe.IsRevivePending || ReviveGate.IsArmed))
            {
                StartCoroutine(Co_ConsumeReviveTokenSoon());
            }

            Refresh();
        }
    }

    // ===== IAdService =====
    public void InitAds(bool userConsent) => Refresh();

    public bool IsRewardedReady()
    {
#if DEVELOPMENT_BUILD || UNITY_EDITOR
        if (ForceDevSimulateReward) return true;
#endif
        return Reward != null && Reward.IsReady;
    }
    public bool IsInterstitialReady()
        => !DisableInterstitials && Interstitial != null && Interstitial.IsReady;

    public void ShowRewarded(Action onReward, Action onClosed = null, Action onFailed = null)
    {
        StartCoroutine(Co_ShowRewardedSafely(onReward, onClosed, onFailed));
    }

    public void ShowInterstitial(Action onClosed = null)
    {
        if (DisableInterstitials)
        {
            Debug.Log("[Ads] Interstitials disabled by switch");
            return;
        }
            if (_interstitialInProgress) { Debug.LogWarning("[Ads] Already showing interstitial"); return; }
        if (!_adsReady) { Debug.LogWarning("[Ads] Not ready"); return; }
        if (!Application.isFocused) { Debug.LogWarning("[Ads] deny interstitial: not focused"); return; }
        if (Interstitial == null || !Interstitial.IsReady) { Debug.LogWarning("[Ads] Interstitial not ready"); return; }

        StartCoroutine(Co_ShowInterstitialSafely(onClosed));
    }

    private IEnumerator Co_ShowInterstitialSafely(Action onClosed)
    {
        if (Time.realtimeSinceStartup - _lastResumeAt < POST_RESUME_DEBOUNCE)
            yield return new WaitForSeconds(POST_RESUME_DEBOUNCE);

        yield return WaitUntilForeground(0.5f);
        if (!Application.isFocused || !AndroidHasWindowFocus())
        {
            Debug.LogWarning("[Ads] Abort interstitial: no foreground/window focus");
            yield break;
        }

        if (Interstitial == null || !Interstitial.IsReady)
        {
            Debug.LogWarning("[Ads] Abort interstitial: not ready");
            yield break;
        }

        void Cleanup()
        {
            if (Interstitial != null)
            {
                Interstitial.Opened -= OnOpened;
                Interstitial.Closed -= OnClosedInternal;
                Interstitial.Failed -= OnFailedInternal;
            }
            _interstitialInProgress = false;
            if (_interstitialWatchdog != null) { StopCoroutine(_interstitialWatchdog); _interstitialWatchdog = null; }
        }

        void OnOpened()
        {
            _interstitialInProgress = true;
            AnalyticsManager.Instance?.InterstitialLog();
        }

        void OnClosedInternal()
        {
            Cleanup();
            try { onClosed?.Invoke(); } catch (Exception e) { Debug.LogException(e); }
            if (InterstitialTime > 0) NextInterstitialTime = DateTime.UtcNow.AddSeconds(InterstitialTime);
            Refresh();
        }

        void OnFailedInternal()
        {
            // 실패 후라도 보상이 이미 찍혔다면 커밋 분기는 유지
            Cleanup();
            UIStateProbe.DisarmResultGuard();
            UIStateProbe.DisarmReviveGrace();
            GameOverUtil.ResetAll("reward_failed");
            Refresh();
        }

        Interstitial.Opened += OnOpened;
        Interstitial.Closed += OnClosedInternal;
        Interstitial.Failed += OnFailedInternal;

        Debug.Log("[Ads] ShowInterstitial(safe)");
        _lastInterShowAt = Time.realtimeSinceStartup;

        Interstitial.ShowAd();
        _interstitialWatchdog = StartCoroutine(Co_Watchdog(isReward: false, timeout: WATCHDOG_SEC));
    }

    public void ToggleBanner(bool show)
    {
        if (Banner == null) return;
        StartCoroutine(Co_ToggleBannerSafely(show));
    }

    IEnumerator Co_ToggleBannerSafely(bool show)
    {
        yield return WaitUntilForeground(0.1f);
        if (!Application.isFocused || !AndroidHasWindowFocus()) yield break;
        if (show) Banner.ShowAd(); else Banner.HideAd();
    }

    public void Refresh()
    {
        if (!Application.isFocused) { Debug.Log("[Ads] Skip preload: not focused"); return; }
        if (Time.realtimeSinceStartup - _lastResumeAt < 0.3f) return;

        if (Reward != null && !Reward.IsReady) Reward.Init();
        if (!DisableInterstitials && Interstitial != null && !Interstitial.IsReady)
            Interstitial.Init();
    }

    // ===== 내부 =====
    void WireAdGuards(InterstitialAdController i, RewardAdController r)
    {
        if (_guardsWired) return;

        if (i != null)
        {
            i.Opened += AdPauseGuard.OnAdOpened;
            i.Closed += AdPauseGuard.OnAdClosedOrFailed;
            i.Failed += AdPauseGuard.OnAdClosedOrFailed;
        }
        if (r != null)
        {
            r.Opened += AdPauseGuard.OnAdOpened;
            r.Closed += AdPauseGuard.OnAdClosedOrFailed;
            r.Failed += AdPauseGuard.OnAdClosedOrFailed;
        }
        _guardsWired = true;
    }

    IEnumerator Co_Watchdog(bool isReward, float timeout)
    {
        float start = Time.realtimeSinceStartup;
        while ((isReward ? _rewardInProgress : _interstitialInProgress)
               && Time.realtimeSinceStartup - start < timeout)
        {
            yield return null;
        }

        bool still = isReward ? _rewardInProgress : _interstitialInProgress;
        if (still)
        {
            Debug.LogWarning("[Ads] Watchdog fired → Forcing resume");
            AdPauseGuard.OnAdClosedOrFailed();
            AdStateProbe.IsFullscreenShowing = false;
            AdStateProbe.IsRevivePending = false;
            ReviveGate.Disarm();
            ReviveLatch.Disarm("watchdog");

            if (isReward) _rewardInProgress = false;
            else _interstitialInProgress = false;
        }
    }

    public static bool AndroidHasWindowFocus()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
            {
                if (activity == null) return Application.isFocused;
                using (var window = activity.Call<AndroidJavaObject>("getWindow"))
                {
                    if (window == null) return Application.isFocused;
                    using (var decor = window.Call<AndroidJavaObject>("getDecorView"))
                    {
                        if (decor == null) return Application.isFocused;
                        return decor.Call<bool>("hasWindowFocus");
                    }
                }
            }
        }
        catch { return Application.isFocused; }
#else
        return Application.isFocused;
#endif
    }

    IEnumerator WaitUntilForeground(float timeoutSec = 1.0f)
    {
        float end = Time.realtimeSinceStartup + timeoutSec;
        yield return new WaitForEndOfFrame();

        while (Time.realtimeSinceStartup < end)
        {
            if (Application.isFocused && AndroidHasWindowFocus())
                yield break;
            yield return null;
        }
    }

    private IEnumerator Co_ShowRewardedSafely(Action onReward, Action onClosed, Action onFailed)
    {

#if DEVELOPMENT_BUILD || UNITY_EDITOR
        // Dev/Editor: 시뮬레이터 켜져 있으면 실제 SDK 호출 없이 즉시 성공 커밋
        if (ForceDevSimulateReward)
        {
            Debug.Log("[Ads] DEV simulate rewarded flow");
            Game.Audio?.StopContinueTimeCheckSE(); // 패널 타임체크음 끄기
            yield return new WaitForSecondsRealtime(Mathf.Max(0.05f, DevSimDelay));

            // 실제 보상 콜백과 동일하게 동작
            try { onReward?.Invoke(); } catch { }

            // CommitNow와 동일한 효과를 별도 함수로 추출해서 사용하면 깔끔
            DevCommitRevive("dev_simulated", onClosed);
            yield break;
        }
#endif
        if (!CanOfferReviveNow())
        {
            try { onFailed?.Invoke(); } catch { }
            yield break;
        }

        var rc = Reward;
        const float TIMEOUT = 12f;
        float deadline = Time.realtimeSinceStartup + TIMEOUT;

        if (Time.realtimeSinceStartup - _lastResumeAt < POST_RESUME_DEBOUNCE)
            yield return new WaitForSeconds(POST_RESUME_DEBOUNCE);

        yield return WaitUntilForeground(0.5f);
        if (!Application.isFocused || !AndroidHasWindowFocus())
        {
            try { onFailed?.Invoke(); } catch { }
            yield break;
        }

        if (rc == null) { try { onFailed?.Invoke(); } catch { } yield break; }
        if (!rc.IsReady && !rc.IsLoading) rc.Init();
        while (!rc.IsReady && Time.realtimeSinceStartup < deadline) yield return null;
        if (!rc.IsReady) { try { onFailed?.Invoke(); } catch { } yield break; }

        bool rewardGranted = false;
        bool closed = false;
        bool committed = false;

        void grantOnce()
        {
            if (rewardGranted) return;
            rewardGranted = true;
            try { onReward?.Invoke(); } catch (Exception e) { Debug.LogException(e); }
        }

        void Cleanup()
        {
            rc.Opened -= OnOpened;
            rc.Closed -= OnClosedInternal;
            rc.Failed -= OnFailedInternal;
            rc.Rewarded -= OnRewardedInternal;
            _rewardInProgress = false;
            if (_rewardWatchdog != null) { StopCoroutine(_rewardWatchdog); _rewardWatchdog = null; }
            ReviveLatch.Disarm("cleanup");
        }

        void CommitNow(string reason)
        {
            if (committed) return;
            committed = true;

            UIStateProbe.ResetAllShields();

            Cleanup();
            grantOnce();

            AdPauseGuard.OnAdClosedOrFailed();
            AdStateProbe.IsFullscreenShowing = false;
            AdStateProbe.IsRevivePending = false;
            ReviveGate.Disarm();
            ReviveLatch.Disarm("commit");
            GameOverGate.Reset($"revive commit ({reason})");

            // 실제 부활 처리
            Game.Save?.TryConsumeDownedPending(out _);
            Game.Bus?.PublishImmediate(new ContinueGranted());
            Game.Bus?.PublishImmediate(new RevivePerformed());
            if (Game.IsBound)
            {
                Game.Bus.ClearSticky<GameOver>();
                Game.Bus.ClearSticky<GameOverConfirmed>();
                Game.Bus.ClearSticky<AdventureStageCleared>();
                Game.Bus.ClearSticky<AdventureStageFailed>();
            }

            AdReviveToken.ConsumeIfFresh(double.MaxValue);

            if (RewardTime > 0) NextRewardTime = DateTime.UtcNow.AddSeconds(RewardTime);
            try { onClosed?.Invoke(); } catch { }
            Game.Bus?.PublishImmediate(new AdFinished());
            Debug.Log($"[Ads] Commit revive ({reason})");
            Refresh();
        }


        void OnOpened()
        {
            _rewardInProgress = true;
            // 보수적으로 전면/대기 켜두기 (플랫폼 콜백 순서 차이 방지)
            AdStateProbe.IsFullscreenShowing = true;
            AdStateProbe.IsRevivePending = true;
            Game.Audio?.StopContinueTimeCheckSE();
            AnalyticsManager.Instance?.RewardLog();
        }

        void OnRewardedInternal()
        {
            grantOnce();
        }

        void OnClosedInternal()
        {
            closed = true;

            // 닫힘 전에 이미 보상 확정이라면 여기서 커밋
            if (rewardGranted && !committed)
            {
                CommitNow("closed_cb");
                return;
            }

            // 보상 미지급 케이스
            Cleanup();
            UIStateProbe.DisarmResultGuard();
            UIStateProbe.DisarmReviveGrace();
            GameOverUtil.ResetAll("closed_no_reward");
            AdStateProbe.IsFullscreenShowing = false;
            AdStateProbe.IsRevivePending = false;
            ReviveGate.Disarm();

            if (RewardTime > 0) NextRewardTime = DateTime.UtcNow.AddSeconds(RewardTime);
            try { onClosed?.Invoke(); } catch { }
            Game.Bus?.PublishImmediate(new AdFinished());
            Refresh();
        }

        void OnFailedInternal()
        {
            // 실패 후라도 보상이 이미 찍혔다면 커밋
            if (rewardGranted && !committed)
            {
                CommitNow("failed_after_granted");
                return;
            }

            Cleanup();
            AdStateProbe.IsFullscreenShowing = false;
            AdStateProbe.IsRevivePending = false;
            ReviveGate.Disarm();

            try { onFailed?.Invoke(); } catch { }
            Game.Bus?.PublishImmediate(new AdFinished());
            Refresh();
        }

        // 구독
        rc.Opened += OnOpened;
        rc.Closed += OnClosedInternal;
        rc.Failed += OnFailedInternal;
        rc.Rewarded += OnRewardedInternal;

        _lastRewardShowAt = Time.realtimeSinceStartup;
        _rewardWatchdog = StartCoroutine(Co_Watchdog(isReward: true, timeout: WATCHDOG_SEC));

        // Show 직전: 부활 락 + revive pending 켜기
        ReviveLatch.Arm(20f, "before ShowAd");
        AdStateProbe.IsRevivePending = true;

        // 커밋 폴백 (닫힘 콜백 누락/지연 방지)
        IEnumerator CoCommitFallback()
        {
            float t = 0f;
            while (!rewardGranted && t < 8f)
            {
                t += Time.unscaledDeltaTime;
                yield return null;
            }
            if (!rewardGranted) yield break;

            t = 0f;
            while (!closed && AdStateProbe.IsFullscreenShowing && t < 1.5f)
            {
                t += Time.unscaledDeltaTime;
                yield return null;
            }
            if (!committed) CommitNow("fallback_timeout");
        }
        StartCoroutine(CoCommitFallback());

        bool ok = rc.ShowAd(onReward: grantOnce);
        if (!ok)
        {
            OnFailedInternal();
            yield break;
        }

        float openDeadline = Time.realtimeSinceStartup + 1.2f;
        while (Time.realtimeSinceStartup < openDeadline
               && !_rewardInProgress
               && !AdStateProbe.IsFullscreenShowing)
        {
            yield return null;
        }
        if (!_rewardInProgress && !AdStateProbe.IsFullscreenShowing)
        {
            Debug.LogWarning("[Ads] Open-timeout → treating as failed");
            OnFailedInternal();
            yield break;
        }
    }

    public void PushBannerBlock()
    {
        _bannerBlockCount++;
        ToggleBanner(false);
    }

    public void PopBannerBlock()
    {
        _bannerBlockCount = Mathf.Max(0, _bannerBlockCount - 1);
        if (_bannerBlockCount == 0) ToggleBanner(true);
    }

    IEnumerator Co_ConsumeReviveTokenAfterBind()
    {
        yield return null;

        if (AdReviveToken.ConsumeIfFresh(180.0))
        {
            if (!ReviveGate.IsArmed) ReviveGate.Arm(2f);

            Game.Bus?.PublishImmediate(new ContinueGranted());
            Game.Bus?.PublishImmediate(new RevivePerformed());

            if (Game.IsBound) Game.Bus.PublishImmediate(new AdFinished());
        }
        else
        {
            if (Game.IsBound) Game.Bus.PublishImmediate(new AdFinished());
        }
    }

    IEnumerator Co_ConsumeReviveTokenSoon()
    {
        float until = Time.realtimeSinceStartup + 2f;
        while ((!Game.IsBound || Game.Bus == null) && Time.realtimeSinceStartup < until)
            yield return null;

        if (!(AdStateProbe.IsRevivePending || ReviveGate.IsArmed || UIStateProbe.IsReviveOpen))
            yield break;

        if (UIStateProbe.IsResultOpen)
            yield break;

        if (UIStateProbe.ResultGuardActive || UIStateProbe.ReviveGraceActive)
            yield break;

        if (UIStateProbe.IsReviveOpen)
            yield break;

        if (!AdReviveToken.HasPending()) yield break;

        if (AdReviveToken.ConsumeIfFresh(180.0))
        {
            if (!ReviveGate.IsArmed) ReviveGate.Arm(2f);
            Game.Bus?.PublishImmediate(new RevivePerformed());
            Game.Bus?.PublishImmediate(new ContinueGranted());
            GameOverGate.Reset("revived");
            if (Game.IsBound) Game.Bus.PublishImmediate(new AdFinished());
        }
    }
    public bool IsRewardCooldownActive(out float remainSec)
    {
        if (RewardTime <= 0) { remainSec = 0f; return false; }
        var now = DateTime.UtcNow;
        if (now < NextRewardTime)
        {
            remainSec = (float)(NextRewardTime - now).TotalSeconds;
            return true;
        }
        remainSec = 0f; return false;
    }

    public bool CanOfferReviveNow()
    {
        float _;
#if DEVELOPMENT_BUILD || UNITY_EDITOR
        if (ForceDevSimulateReward && !IsRewardCooldownActive(out _))
        {
            Debug.Log("[Ads] gate=OK (DEV simulate)");
            return true;
        }
#endif

        if (!_adsReady) return false;
        if (IsRewardCooldownActive(out _)) return false;
        return IsRewardedReady();
    }

#if DEVELOPMENT_BUILD || UNITY_EDITOR
    private void DevCommitRevive(string reason, Action onClosed)
    {
        if (_rewardWatchdog != null) { StopCoroutine(_rewardWatchdog); _rewardWatchdog = null; }
        _rewardInProgress = false;
        AdPauseGuard.OnAdClosedOrFailed();
        AdStateProbe.IsFullscreenShowing = false;
        AdStateProbe.IsRevivePending = false;
        ReviveGate.Disarm();
        ReviveLatch.Disarm("dev-commit");
        GameOverGate.Reset($"revive commit ({reason})");

        UIStateProbe.ResetAllShields();

        Game.Save?.TryConsumeDownedPending(out _);
        Game.Bus?.PublishImmediate(new ContinueGranted());
        Game.Bus?.PublishImmediate(new RevivePerformed());
        if (Game.IsBound)
        {
            Game.Bus.ClearSticky<GameOver>();
            Game.Bus.ClearSticky<GameOverConfirmed>();
            Game.Bus.ClearSticky<AdventureStageCleared>();
            Game.Bus.ClearSticky<AdventureStageFailed>();
        }
        AdReviveToken.ConsumeIfFresh(double.MaxValue);

        if (RewardTime > 0) NextRewardTime = DateTime.UtcNow.AddSeconds(RewardTime);

        try { onClosed?.Invoke(); } catch { }
        Game.Bus?.PublishImmediate(new AdFinished());
        Refresh();
    }
#endif

    public void PreInit() { }
    public void Init() { }
    public void PostInit() { }
}

using GoogleMobileAds.Api;
using System;
using System.Collections;
using System.Reflection;
using Unity.VisualScripting;
using UnityEngine;

public class AdManager : MonoBehaviour, IAdService
{
    public static AdManager Instance { get; private set; }

    public InterstitialAdController Interstitial { get; private set; }
    public BannerAdController Banner { get; private set; }
    public RewardAdController Reward { get; private set; }

    public int Order => 80;

    [Header("Policy (쿨다운은 자동노출용)")]
    public int RewardTime = 90;
    public int InterstitialTime = 120;

    public DateTime NextRewardTime = DateTime.MinValue;        // 첫 판부터 가능
    public DateTime NextInterstitialTime = DateTime.MinValue;

    // 내부 상태
    bool _adsReady;
    bool _guardsWired;

    bool _rewardInProgress;
    bool _interstitialInProgress;

    // 광고 시도 시각(초) - realtimeSinceStartup 기준
    float _lastRewardShowAt = -999f;
    float _lastInterShowAt = -999f;

    const float WATCHDOG_SEC = 12f;
    Coroutine _rewardWatchdog;
    Coroutine _interstitialWatchdog;

    // ===== Unity =====
    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        // 메인스레드 이벤트
        try
        {
            var prop = typeof(MobileAds).GetProperty("RaiseAdEventsOnUnityMainThread",
                BindingFlags.Public | BindingFlags.Static);
            if (prop != null && prop.CanWrite) prop.SetValue(null, true, null);
        }
        catch { }

        // MobileAds.Initialize는 여기서 직접 호출하지 않고 Gate를 사용
        AdsInitGate.EnsureInit();
        AdsInitGate.WhenReady(() =>
        {
            Debug.Log("[Ads] MobileAds initialized (via gate)");

            // 컨트롤러 생성 & 초기화
            Interstitial = new InterstitialAdController(); Interstitial.Init();
            Banner = new BannerAdController(); Banner.InitAdaptive(); // ← Adaptive로!
            Reward = new RewardAdController(); Reward.Init();

            WireAdGuards(Interstitial, Reward);
            _adsReady = true;

            // 필요 시 바로 노출할 건 여기서 Show
            // Banner.ShowAd();  // 전역 상시 노출이면 켜고,
            // 특정 화면에서만 노출이면 꺼둔 채 ToggleBanner로 제어
        });
    }

    void OnEnable() { Game.BindAds(this); }
    void OnDisable() { Game.UnbindAds(this); }

    // 앱 포커스/백그라운드에 따른 강제 복구(콜백 누락 방어)
    void OnApplicationPause(bool pause)
    {
        if (pause)
        {
            if (_rewardInProgress || (Time.realtimeSinceStartup - _lastRewardShowAt) < 2f)
            {
                Debug.Log("[Ads] App paused near reward show → assume ad opened");
                if (!AdPauseGuard.IsAdShowing) AdPauseGuard.OnAdOpened();
                _rewardInProgress = true;
            }
            if (_interstitialInProgress || (Time.realtimeSinceStartup - _lastInterShowAt) < 2f)
            {
                Debug.Log("[Ads] App paused near interstitial show → assume ad opened");
                if (!AdPauseGuard.IsAdShowing) AdPauseGuard.OnAdOpened();
                _interstitialInProgress = true;
            }
            return;
        }

        // resume
        if (AdPauseGuard.PausedByAd || Time.timeScale == 0f)
        {
            Debug.LogWarning("[Ads] App resume → ensure resume");
            AdPauseGuard.OnAdClosedOrFailed();
            _rewardInProgress = false;
            _interstitialInProgress = false;

            if (_rewardWatchdog != null) { StopCoroutine(_rewardWatchdog); _rewardWatchdog = null; }
            if (_interstitialWatchdog != null) { StopCoroutine(_interstitialWatchdog); _interstitialWatchdog = null; }
        }
        Refresh();
    }

    void OnApplicationFocus(bool focus)
    {
        if (focus) Refresh();
        if (focus && Game.IsBound)
            Game.Bus.PublishImmediate(new AdFinished());
    }

    // ===== IAdService =====
    public void InitAds(bool userConsent)
    {
        // 필요시 동의 설정 반영
        Refresh();
    }

    public bool IsRewardedReady() => Reward != null && Reward.IsReady;
    public bool IsInterstitialReady() => Interstitial != null && Interstitial.IsReady;

    public void ShowRewarded(Action onReward, Action onClosed = null, Action onFailed = null)
    {
        if (_rewardInProgress) { onFailed?.Invoke(); return; }
        if (Reward == null || !Reward.IsReady) { Debug.LogWarning("[Ads] Reward not ready → Refresh only"); Refresh(); onFailed?.Invoke(); return; }
        if (!Application.isFocused) { Debug.LogWarning("[Ads] deny show: app not focused"); onFailed?.Invoke(); return; }

        // === 이벤트 구독 & 정리 루틴 ===
        void Cleanup()
        {
            if (Reward != null)
            {
                Reward.Opened -= OnOpened;
                Reward.Closed -= OnClosedInternal;
                Reward.Failed -= OnFailedInternal;
                Reward.Rewarded -= OnRewardedInternal;
            }
            _rewardInProgress = false;
            if (_rewardWatchdog != null) { StopCoroutine(_rewardWatchdog); _rewardWatchdog = null; }
        }

        void OnOpened()
        {
            _rewardInProgress = true;
            AnalyticsManager.Instance?.RewardLog();
        }

        void OnRewardedInternal()
        {
            // 실제 보상 부여는 Revive 버튼 쪽 콜백에서 처리하더라도,
            // 안전하게 한 번 더 호출해도 무방(멱등)하게 설계했으면 여기서도 onReward 보조 호출 가능
            try { onReward?.Invoke(); } catch (Exception e) { Debug.LogException(e); }
        }

        void OnClosedInternal()
        {
            Cleanup();
            try { onClosed?.Invoke(); } catch (Exception e) { Debug.LogException(e); }
            if (RewardTime > 0) NextRewardTime = DateTime.UtcNow.AddSeconds(RewardTime);
            Refresh();
        }

        void OnFailedInternal()
        {
            Cleanup();
            try { onFailed?.Invoke(); } catch (Exception e) { Debug.LogException(e); }
            Refresh();
        }

        // === 실제 구독 ===
        Reward.Opened += OnOpened;
        Reward.Closed += OnClosedInternal;
        Reward.Failed += OnFailedInternal;
        Reward.Rewarded += OnRewardedInternal;

        Debug.Log("[Ads] ShowRewarded()");
        _lastRewardShowAt = Time.realtimeSinceStartup;

        // Rewarded 광고 호출 (보상 시 onReward가 호출되도록 그대로 전달)
        bool ok = Reward.ShowAd(onReward);
        if (!ok)
        {
            // 즉시 실패 처리
            OnFailedInternal();
            return;
        }

        // 워치독 시작 (닫힘 콜백 누락 대비)
        _rewardWatchdog = StartCoroutine(Co_Watchdog(isReward: true, timeout: WATCHDOG_SEC));
    }

    public void ShowInterstitial(Action onClosed = null)
    {
        if (!_adsReady) { Debug.LogWarning("[Ads] Not ready"); return; }
        if (!Application.isFocused) { Debug.LogWarning("[Ads] deny interstitial: not focused"); return; }
        if (Interstitial == null || !Interstitial.IsReady) { Debug.LogWarning("[Ads] Interstitial not ready"); return; }

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
            AnalyticsManager.Instance?.InterstitialLog();   // 전면 광고 노출 로그
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
            Cleanup();
            Refresh();
            // 실패 로깅도 원하면:
            // AnalyticsManager.Instance?.LogEvent("Interstitial_Failed");
        }

        Cleanup();
        Interstitial.Opened += OnOpened;
        Interstitial.Closed += OnClosedInternal;
        Interstitial.Failed += OnFailedInternal;

        Debug.Log("[Ads] ShowInterstitial()");
        _lastInterShowAt = Time.realtimeSinceStartup;

        Interstitial.ShowAd();
        _interstitialWatchdog = StartCoroutine(Co_Watchdog(isReward: false, timeout: WATCHDOG_SEC));
    }

    public void ToggleBanner(bool show)
    {
        if (Banner == null) return;
        if (show) Banner.ShowAd();
        else Banner.HideAd();
    }

    public void Refresh()
    {
        if (!Application.isFocused)
        {
            Debug.Log("[Ads] Skip preload: not focused");
            return;
        }

        if (Reward != null && !Reward.IsReady) Reward.Init();
        if (Interstitial != null && !Interstitial.IsReady) Interstitial.Init();
        // 배너는 필요 시에만
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
            if (isReward) _rewardInProgress = false;
            else _interstitialInProgress = false;
        }
    }

    static bool AndroidHasWindowFocus()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
    try
    {
        using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
        using (var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
        using (var window   = activity?.Call<AndroidJavaObject>("getWindow"))
        using (var decor    = window?.Call<AndroidJavaObject>("getDecorView"))
        {
            return decor?.Call<bool>("hasWindowFocus") ?? false;
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
        // 최소 한 프레임은 넘긴다 (UI 전환/터치 처리 완료용)
        yield return new WaitForEndOfFrame();

        while (Time.realtimeSinceStartup < end)
        {
            if (Application.isFocused && AndroidHasWindowFocus())
                yield break; // OK, 포그라운드 확정
            yield return null;
        }
    }

    private IEnumerator Co_ShowRewardedSafely(Action onReward, Action onClosed, Action onFailed)
    {
        var rc = Reward;
        const float TIMEOUT = 12f;
        float deadline = Time.realtimeSinceStartup + TIMEOUT;

        // 1) 포그라운드 보장
        yield return WaitUntilForeground(1.0f);

        // 2) 준비 대기 (필요 시 로드)
        if (rc == null) { onFailed?.Invoke(); yield break; }
        if (!rc.IsReady && !rc.IsLoading) rc.Init();
        while (!rc.IsReady && Time.realtimeSinceStartup < deadline) yield return null;
        if (!rc.IsReady) { onFailed?.Invoke(); yield break; }

        // 3) 이벤트 릴레이 준비
        void Cleanup()
        {
            rc.Opened -= OnOpened;
            rc.Closed -= OnClosedInternal;
            rc.Failed -= OnFailedInternal;
            _rewardInProgress = false;
            if (_rewardWatchdog != null) { StopCoroutine(_rewardWatchdog); _rewardWatchdog = null; }
        }
        void OnOpened()
        {
            _rewardInProgress = true;
            AnalyticsManager.Instance?.RewardLog(); // 원하면
        }
        void OnClosedInternal()
        {
            Cleanup();
            try { onClosed?.Invoke(); } catch { }
            if (RewardTime > 0) NextRewardTime = DateTime.UtcNow.AddSeconds(RewardTime);
            Refresh(); // 다음 로드 큐
        }
        void OnFailedInternal()
        {
            Cleanup();
            try { onFailed?.Invoke(); } catch { }
            Refresh(); // 다음 로드 큐
        }

        rc.Opened += OnOpened;
        rc.Closed += OnClosedInternal;
        rc.Failed += OnFailedInternal;

        // 4) 실제 노출 (보상 콜백은 rc.ShowAd의 reward 콜백/Rewarded 이벤트에서 발생)
        _lastRewardShowAt = Time.realtimeSinceStartup;
        _rewardWatchdog = StartCoroutine(Co_Watchdog(isReward: true, timeout: WATCHDOG_SEC));

        bool ok = rc.ShowAd(onReward);
        if (!ok)
        {
            // CanShowAd()가 순간 false였던 케이스 등
            OnFailedInternal();
        }
    }



    // (필요하면) 생명주기용 훅
    public void PreInit() { }
    public void Init() { }
    public void PostInit() { }
}

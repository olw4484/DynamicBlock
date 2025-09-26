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

        // (옵션) GMA 이벤트를 메인 스레드로 올림 - 리플렉션으로 안전 적용
        try
        {
            var prop = typeof(MobileAds).GetProperty("RaiseAdEventsOnUnityMainThread",
                BindingFlags.Public | BindingFlags.Static);
            if (prop != null && prop.CanWrite) prop.SetValue(null, true, null);
        }
        catch { /* 무시 */ }

        MobileAds.Initialize(_ =>
        {
            Debug.Log("[Ads] MobileAds.Initialize success");

            // 컨트롤러 선로딩
            Interstitial = new InterstitialAdController(); Interstitial.Init();
            Banner = new BannerAdController(); Banner.Init();
            Reward = new RewardAdController(); Reward.Init();

            WireAdGuards(Interstitial, Reward);
            _adsReady = true;
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

        // 준비 안 되었으면 바로 실패로 끝내지 말고 Refresh만 하고 실패 콜백
        if (Reward == null || !Reward.IsReady)
        {
            Debug.LogWarning("[Ads] Reward not ready → Refresh only");
            Refresh();
            onFailed?.Invoke();
            return;
        }

        // 포그라운드가 아니면 여기서도 컷
        if (!Application.isFocused)
        {
            Debug.LogWarning("[Ads] deny show: app not focused");
            onFailed?.Invoke();
            return;
        }

        StartCoroutine(Co_ShowRewardedSafely(onReward, onClosed, onFailed));
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

    IEnumerator Co_ShowRewardedSafely(Action onReward, Action onClosed, Action onFailed)
    {
        // 1) 포그라운드 보장
        yield return WaitUntilForeground(1.0f);
        if (!Application.isFocused || !AndroidHasWindowFocus())
        {
            Debug.LogWarning("[Ads] Still not foreground → cancel show");
            onFailed?.Invoke();
            yield break;
        }

        // 2) 준비 안됐으면 "짧은 대기 후 1회 재시도"
        const float QUICK_WAIT = 2.5f; // 2~3초 권장
        if (Reward == null || !Reward.IsReady)
        {
            Debug.LogWarning("[Ads] Reward not ready at safe point → quick wait & retry");
            Refresh();                                  // 로딩 트리거
            yield return WaitUntilRewardReady(QUICK_WAIT);
        }

        // 3) 그래도 준비 안됐으면 실패
        if (Reward == null || !Reward.IsReady)
        {
            Debug.LogWarning("[Ads] Reward still not ready → fail");
            onFailed?.Invoke();
            yield break;
        }

        // 4) 이벤트 구독 및 Show (쿨다운 무시)
        bool opened = false;
        void CleanupLocal()
        {
            if (Reward != null)
            {
                Reward.Opened -= OnOpenedEvt;
                Reward.Closed -= OnClosedEvt;
                Reward.Failed -= OnFailedEvt;
                Reward.Rewarded -= OnRewardedEvt;
            }
            _rewardInProgress = false;
            if (_rewardWatchdog != null) { StopCoroutine(_rewardWatchdog); _rewardWatchdog = null; }
        }
        void OnOpenedEvt()
        {
            opened = true;
            _rewardInProgress = true;
            AnalyticsManager.Instance?.RewardLog();
        }
        void OnClosedEvt() { CleanupLocal(); try { onClosed?.Invoke(); } catch (Exception e) { Debug.LogException(e); } Refresh(); }
        void OnFailedEvt() { CleanupLocal(); try { onFailed?.Invoke(); } catch (Exception e) { Debug.LogException(e); } Refresh(); }
        void OnRewardedEvt() { try { onReward?.Invoke(); } catch (Exception e) { Debug.LogException(e); } }

        CleanupLocal();
        Reward.Opened += OnOpenedEvt;
        Reward.Closed += OnClosedEvt;
        Reward.Failed += OnFailedEvt;
        Reward.Rewarded += OnRewardedEvt;

        Debug.Log("[Ads] ShowRewarded(safe)");
        _lastRewardShowAt = Time.realtimeSinceStartup;

        if (!Reward.ShowAd(onReward: null, ignoreCooldown: true))
        {
            CleanupLocal();
            onFailed?.Invoke();
            yield break;
        }

        _rewardWatchdog = StartCoroutine(Co_Watchdog(isReward: true, timeout: WATCHDOG_SEC));
    }

    IEnumerator WaitUntilRewardReady(float maxWaitSec)
    {
        float end = Time.realtimeSinceStartup + maxWaitSec;
        while (Time.realtimeSinceStartup < end)
        {
            if (Reward != null && Reward.IsReady) yield break;
            yield return null;
        }
    }

    // (필요하면) 생명주기용 훅
    public void PreInit() { }
    public void Init() { }
    public void PostInit() { }
}

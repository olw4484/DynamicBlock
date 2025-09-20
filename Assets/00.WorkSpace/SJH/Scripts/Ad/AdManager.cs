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
            // 광고 시도 직후 포그라운드 → 백그라운드 전환이면 "광고가 열렸다" 가정
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
        }
        else
        {
            // 복귀 시 광고 SDK 콜백이 없어도 게임이 멈추지 않도록 강제 정상화
            if (AdPauseGuard.PausedByAd || Time.timeScale == 0f)
            {
                Debug.LogWarning("[Ads] App resume → ensure resume");
                AdPauseGuard.OnAdClosedOrFailed();
                _rewardInProgress = false;
                _interstitialInProgress = false;

                if (_rewardWatchdog != null) { StopCoroutine(_rewardWatchdog); _rewardWatchdog = null; }
                if (_interstitialWatchdog != null) { StopCoroutine(_interstitialWatchdog); _interstitialWatchdog = null; }
            }
            Refresh(); // 필요 시 다음 로드
        }
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

        // 앱이 포그라운드가 아니면 즉시 실패 (SDK가 거절함)
        if (!Application.isFocused)
        {
            Debug.LogWarning("[Ads] deny show: app not focused");
            onFailed?.Invoke();
            return;
        }

        if (Reward == null || !Reward.IsReady) { onFailed?.Invoke(); return; }

        void Cleanup()
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

        void OnOpenedEvt() { _rewardInProgress = true; }
        void OnClosedEvt() { Cleanup(); try { onClosed?.Invoke(); } catch (Exception e) { Debug.LogException(e); } Refresh(); }
        void OnFailedEvt() { Cleanup(); try { onFailed?.Invoke(); } catch (Exception e) { Debug.LogException(e); } Refresh(); }
        void OnRewardedEvt() { try { onReward?.Invoke(); } catch (Exception e) { Debug.LogException(e); } }

        Cleanup(); // 선제 정리
        Reward.Opened += OnOpenedEvt;
        Reward.Closed += OnClosedEvt;
        Reward.Failed += OnFailedEvt;
        Reward.Rewarded += OnRewardedEvt;

        Debug.Log("[Ads] ShowRewarded()");
        _lastRewardShowAt = Time.realtimeSinceStartup;

        if (!Reward.ShowAd(onReward: null)) // 보상은 이벤트에서 처리
        {
            Cleanup();
            onFailed?.Invoke();
            return;
        }

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

        void OnOpened() { _interstitialInProgress = true; }
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
        }

        Cleanup(); // 선제 정리
        Interstitial.Opened += OnOpened;
        Interstitial.Closed += OnClosedInternal;
        Interstitial.Failed += OnFailedInternal;

        Debug.Log("[Ads] ShowInterstitial()");
        _lastInterShowAt = Time.realtimeSinceStartup;

        Interstitial.ShowAd(); // 내부에서 CanShow/쿨다운 체크

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

    // (필요하면) 생명주기용 훅
    public void PreInit() { }
    public void Init() { }
    public void PostInit() { }
}

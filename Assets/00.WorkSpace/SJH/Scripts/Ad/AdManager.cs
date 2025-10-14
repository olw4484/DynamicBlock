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

    const float POST_RESUME_DEBOUNCE = 0.8f; // 재진입 직후 자동 노출 방지용
    float _lastResumeAt = -999f;

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
    
    // 업적창 배너 광고 숨기기
    int _bannerBlockCount = 0;

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

            Interstitial = new InterstitialAdController(); Interstitial.Init();
            Banner = new BannerAdController(); Banner.InitAdaptive();
            Reward = new RewardAdController(); Reward.Init();

            WireAdGuards(Interstitial, Reward);
            _adsReady = true;

            StartCoroutine(Co_ConsumeReviveTokenAfterBind());
        });
    }

    void OnEnable() { Game.BindAds(this); }
    void OnDisable() { Game.UnbindAds(this); }

    // 앱 포커스/백그라운드에 따른 강제 복구(콜백 누락 방어)
    void OnApplicationPause(bool pause)
    {
        if (!pause)
        {
            StartCoroutine(Co_ConsumeReviveTokenSoon());
        }
    }

    void OnApplicationFocus(bool focus)
    {
        if (focus)
        {
            _lastResumeAt = Time.realtimeSinceStartup;
            if (!AdReviveToken.HasPending() && Game.IsBound) Game.Bus.PublishImmediate(new AdFinished());
            StartCoroutine(Co_ConsumeReviveTokenSoon());
            Refresh();
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
        StartCoroutine(Co_ShowRewardedSafely(onReward, onClosed, onFailed));
    }

    public void ShowInterstitial(Action onClosed = null)
    {
        if (_interstitialInProgress) { Debug.LogWarning("[Ads] Already showing interstitial"); return; }
        if (!_adsReady) { Debug.LogWarning("[Ads] Not ready"); return; }
        if (!Application.isFocused) { Debug.LogWarning("[Ads] deny interstitial: not focused"); return; }
        if (Interstitial == null || !Interstitial.IsReady) { Debug.LogWarning("[Ads] Interstitial not ready"); return; }

        StartCoroutine(Co_ShowInterstitialSafely(onClosed));
    }

    private IEnumerator Co_ShowInterstitialSafely(Action onClosed)
    {
        // 0) Resume 직후 디바운스 (Transsion 회피에 도움)
        if (Time.realtimeSinceStartup - _lastResumeAt < POST_RESUME_DEBOUNCE)
            yield return new WaitForSeconds(POST_RESUME_DEBOUNCE);

        // 1) 창 포커스/앱 포커스 보장 (최소 0.5s 권장)
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

        // 2) 이벤트 구독/워치독
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
            Cleanup();
            Refresh();
        }

        // 구독 후 Show
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
        if (!Application.isFocused)
        {
            Debug.Log("[Ads] Skip preload: not focused");
            return;
        }

        if (Time.realtimeSinceStartup - _lastResumeAt < 0.3f) return;

        if (Reward != null && !Reward.IsReady) Reward.Init();
        if (Interstitial != null && !Interstitial.IsReady) Interstitial.Init();
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
        }

        void OnOpened()
        {
            _rewardInProgress = true;
            AnalyticsManager.Instance?.RewardLog();
        }

        void OnRewardedInternal()
        {
            grantOnce();
        }

        void OnClosedInternal()
        {
            Cleanup();
            if (RewardTime > 0) NextRewardTime = DateTime.UtcNow.AddSeconds(RewardTime);
            try { onClosed?.Invoke(); } catch { }
            Refresh();
        }

        void OnFailedInternal()
        {
            Cleanup();
            try { onFailed?.Invoke(); } catch { }
            Refresh();
        }

        // 구독
        rc.Opened += OnOpened;
        rc.Closed += OnClosedInternal;
        rc.Failed += OnFailedInternal;
        rc.Rewarded += OnRewardedInternal;

        _lastRewardShowAt = Time.realtimeSinceStartup;
        _rewardWatchdog = StartCoroutine(Co_Watchdog(isReward: true, timeout: WATCHDOG_SEC));

        bool ok = rc.ShowAd(onReward: grantOnce);
        if (!ok)
        {
            OnFailedInternal();
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
            if (!ReviveGate.IsArmed) ReviveGate.Arm(2f); // 과도한 중복 방지

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
        // Game.BindAds / Game.Bus 준비까지 최대 수 프레임 대기
        float until = Time.realtimeSinceStartup + 2f;
        while ((!Game.IsBound || Game.Bus == null) && Time.realtimeSinceStartup < until)
            yield return null;

        if (AdReviveToken.ConsumeIfFresh(180.0))
        {
            if (!ReviveGate.IsArmed) ReviveGate.Arm(2f); // 잠깐 게임오버 체크 억제
            Game.Bus?.PublishImmediate(new ContinueGranted());
            Game.Bus?.PublishImmediate(new RevivePerformed());
            GameOverGate.Reset("revived"); // 다음 사이클 대비
                                           // 부활을 먼저 끝내고 UI 해제
            if (Game.IsBound) Game.Bus.PublishImmediate(new AdFinished());
        }
    }

    // (필요하면) 생명주기용 훅
    public void PreInit() { }
    public void Init() { }
    public void PostInit() { }
}

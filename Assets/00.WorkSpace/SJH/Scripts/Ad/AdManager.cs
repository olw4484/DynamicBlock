using GoogleMobileAds.Api;
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class AdManager : MonoBehaviour, IAdService
{
    public static AdManager Instance { get; private set; }

    public InterstitialAdController Interstitial { get; private set; }
    public BannerAdController Banner { get; private set; }
    public RewardAdController Reward { get; private set; }

    bool _rewardInProgress;
    bool _guardsWired;

    public int Order => 80;

    [Header("UI Buttons (optional demo)")]
    [SerializeField] private Button _showBtn;     // 전면 데모용
    [SerializeField] private Button _bannerBtn;   // 배너 데모용
    [SerializeField] private Button _rewardBtn;   // 리워드 데모용
    [SerializeField] private Button _classicBtn;  // 게임 시작 데모

    public int RewardTime = 90;
    public int InterstitialTime = 120;
    public DateTime NextRewardTime = DateTime.MaxValue;
    public DateTime NextInterstitialTime = DateTime.MinValue;

    private bool _adsReady = false;
    bool _rewardLocked;
    Coroutine _btnUpdateLoop;

    // ===== Unity =====
    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        if (_classicBtn)
        {
            _classicBtn.onClick.RemoveAllListeners();

			NextInterstitialTime = DateTime.UtcNow.AddSeconds(InterstitialTime);

			_classicBtn.onClick.AddListener(() =>
            {
                NextRewardTime = DateTime.UtcNow.AddSeconds(RewardTime);
            });
        }

        MobileAds.Initialize(initStatus =>
        {
            if (initStatus == null)
            {
                Debug.LogError("[Ads] MobileAds.Initialize failed");
                return;
            }
            Debug.Log("[Ads] MobileAds.Initialize success");

            _adsReady = true;
        });
    }

    void Update()
    {
        if (_adsReady)
        {
            _adsReady = false;

            Interstitial = new InterstitialAdController(); Interstitial.Init();
            Banner = new BannerAdController(); Banner.Init();
            Reward = new RewardAdController(); Reward.Init();

            WireAdGuards(Interstitial, Reward);

#if UNITY_EDITOR
            WireDemoUI();
#endif

            if (_btnUpdateLoop == null)
                _btnUpdateLoop = StartCoroutine(Co_UpdateRewardButton());
        }
    }


    void OnEnable() { Game.BindAds(this); }
    void OnDisable() { Game.UnbindAds(this); }

    // ===== IAdService 구현 =====
    public void InitAds(bool userConsent)
    {
        // 동의 옵션 적용 필요 시 추가
        Refresh(); // 각 광고 재로드
    }

    public bool IsRewardedReady() => Reward != null && Reward.IsReady;
    public bool IsInterstitialReady() => Interstitial != null && Interstitial.IsReady;

    public void ShowRewarded(Action onReward, Action onClosed = null, Action onFailed = null)
    {
        if (_rewardInProgress) { Debug.LogWarning("[Ads] Reward already in progress"); return; }
        if (Reward == null || !Reward.IsReady) { Debug.LogWarning("[Ads] Reward not ready"); onFailed?.Invoke(); return; }

        _rewardInProgress = true;

        void Cleanup()
        {
            Reward.Closed -= OnClosed;
            Reward.Failed -= OnFailedEvt;
            Reward.Rewarded -= OnRewardedEvent;
            _rewardInProgress = false;
        }

        void OnClosed() { Cleanup(); try { onClosed?.Invoke(); } catch (Exception e) { Debug.LogException(e); } Refresh(); }
        void OnFailedEvt() { Cleanup(); try { onFailed?.Invoke(); } catch (Exception e) { Debug.LogException(e); } Refresh(); }
        void OnRewardedEvent() { try { onReward?.Invoke(); } catch (Exception e) { Debug.LogException(e); } }

        Reward.Closed += OnClosed;
        Reward.Failed += OnFailedEvt;
        Reward.Rewarded += OnRewardedEvent;

        Debug.Log("[Ads] ShowRewarded()");
        Reward.ShowAd(); // 보상은 Rewarded 이벤트에서 처리
    }

    public void ShowInterstitial(Action onClosed = null)
    {
        if (Interstitial == null || !Interstitial.IsReady)
        {
            Debug.LogWarning("[Ads] Interstitial not ready");
            return;
        }

        void Cleanup()
        {
            Interstitial.Closed -= OnClosedInternal;
            Interstitial.Failed -= OnFailedInternal;
        }

        void OnClosedInternal()
        {
            Cleanup();
            try { onClosed?.Invoke(); } catch (Exception e) { Debug.LogException(e); }
            Refresh(); // 닫힌 뒤 다음 로드
        }

        void OnFailedInternal()
        {
            Cleanup();
            // 실패는 굳이 콜백 전달 안 해도 됨(정책에 따라 필요하면 onClosed 호출 가능)
            Refresh();
        }

        Interstitial.Closed += OnClosedInternal;
        Interstitial.Failed += OnFailedInternal;

        Debug.Log("[Ads] ShowInterstitial()");
        Interstitial.ShowAd();
    }

    public void ToggleBanner(bool show)
    {
        if (Banner == null) return;
        if (show) Banner.ShowAd();
        else Banner.HideAd();
    }

    public void Refresh()
    {
        // 네 컨트롤러는 Load가 없으므로 Init()이 곧 로드 의미
        Reward?.Init();
        Interstitial?.Init();
        // 배너는 상시 유지하고 필요시만 Show/Hide. 여기선 새로고침 의미의 Init만 호출 가능.
        // Banner?.Init(); // 필요할 때만
    }

    // ===== 데모/디버그 UI =====
    void WireDemoUI()
    {
        if (_showBtn)
        {
            _showBtn.onClick.RemoveAllListeners();
            _showBtn.onClick.AddListener(() => ShowInterstitial());
        }

        if (_bannerBtn)
        {
            _bannerBtn.onClick.RemoveAllListeners();
            _bannerBtn.onClick.AddListener(() =>
            {
                if (Banner == null) return;
                ToggleBanner(!Banner.IsVisible);
            });
        }

        if (_rewardBtn)
        {
            _rewardBtn.onClick.RemoveAllListeners();
            _rewardBtn.onClick.AddListener(OnClickRewardDemo);
        }
    }

#if UNITY_EDITOR
    // 데모: 보상 닫힘 후 소프트 리셋(빌드 금지)
    void OnClickRewardDemo()
    {
        ShowRewarded(
            onReward: () => Debug.Log("[Ads] demo onReward"),
            onClosed: () => { },
            onFailed: () =>
            {
                Debug.LogWarning("[Ads] demo onFailed → SoftReset");
                RestartFlow.SoftReset("AdFailed");
            }
        );
    }
#else
void OnClickRewardDemo() { /* no-op in build */ }
#endif

    IEnumerator Co_UpdateRewardButton()
    {
        var wait = new WaitForSeconds(0.25f);
        while (true)
        {
            if (_rewardBtn && !_rewardLocked)
                _rewardBtn.interactable = IsRewardedReady();
            yield return wait;
        }
    }

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

    public void PreInit()
    {
    }

    public void Init()
    {
    }

    public void PostInit()
    {
    }
}

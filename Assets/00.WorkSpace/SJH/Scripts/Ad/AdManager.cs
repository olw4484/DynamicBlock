using GoogleMobileAds.Api;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class AdManager : MonoBehaviour
{
    public static AdManager Instance { get; private set; }
    public InterstitialAdController Interstitial { get; private set; }
    public BannerAdController Banner { get; private set; }
    public RewardAdController Reward { get; private set; }

    [Header("UI Buttons")]
    [SerializeField] private Button _showBtn;   // 전면
    [SerializeField] private Button _bannerBtn; // 배너 토글
    [SerializeField] private Button _rewardBtn; // 리워드

    bool _rewardLocked; // 광고 진행 중 버튼 잠금

    [SerializeField] private Button _classicBtn;    // 클래식 시작 버튼
    public int RewardTime = 90; // 90
    public int InterstitialTime = 120; // 120
    public DateTime NextRewardTime = DateTime.MaxValue;
    public DateTime NextInterstitialTime = DateTime.MinValue;

    void Awake()
    {
        Instance = this;
        DontDestroyOnLoad(gameObject);

        MobileAds.Initialize(status =>
        {
            if (status == null)
            {
                Debug.LogError("모바일 광고 초기화 실패");
                return;
            }

            Debug.Log("모바일 광고 초기화 성공");

            Interstitial = new InterstitialAdController(); Interstitial.Init();
            Banner = new BannerAdController(); Banner.Init();
            Reward = new RewardAdController(); Reward.Init();

            WireUI();
            StartCoroutine(Co_UpdateRewardButton());
        });

        // 클래식 시작버튼 이벤트 연결
        NextInterstitialTime = DateTime.UtcNow.AddSeconds(InterstitialTime);
        _classicBtn.onClick.AddListener(() =>
        {
            NextRewardTime = DateTime.UtcNow.AddSeconds(RewardTime);
		});
    }

    void WireUI()
    {
        // 전면
        if (_showBtn)
        {
            _showBtn.onClick.RemoveAllListeners();
            _showBtn.onClick.AddListener(() => Interstitial?.ShowAd());
        }

        // 배너 토글
        if (_bannerBtn)
        {
            _bannerBtn.onClick.RemoveAllListeners();
            _bannerBtn.onClick.AddListener(() => Banner?.AdToggle());
        }

        // 리워드 = 보상 후 닫힘에서 재시작
        if (_rewardBtn)
        {
            _rewardBtn.onClick.RemoveAllListeners();
            _rewardBtn.onClick.AddListener(OnClickRewardAndRestart);
        }
    }

    void OnClickRewardAndRestart()
    {
        if (Reward == null || !Reward.IsReady || _rewardLocked)
        {
            Debug.Log("리워드 준비 중 또는 진행 중");
            return;
        }

        _rewardLocked = true;
        _rewardBtn.interactable = false;

        bool rewarded = false;

        void Cleanup()
        {
            Reward.Closed -= OnClosed;
            Reward.Failed -= OnFailed;
            _rewardLocked = false;
            // 준비 상태는 코루틴에서 주기적으로 반영
        }

        void OnClosed()
        {
            Cleanup();
            if (rewarded)
                RestartFlow.SoftReset(); // 공용 재시작 시퀀스
        }

        void OnFailed()
        {
            Cleanup();
            // 실패 UX가 있으면 여기에서 처리
        }

        Reward.Closed += OnClosed;
        Reward.Failed += OnFailed;

        // 보상 콜백에서는 플래그만 세팅(광고가 닫힌 뒤 재시작)
        Reward.ShowAd(() => rewarded = true);
    }

    IEnumerator Co_UpdateRewardButton()
    {
        var wait = new WaitForSeconds(0.25f);
        while (true)
        {
            if (_rewardBtn && !_rewardLocked) // 광고 중일 땐 건드리지 않음
            {
                bool ready = Reward != null && Reward.IsReady;
                _rewardBtn.interactable = ready;
            }
            yield return wait;
        }
    }
}

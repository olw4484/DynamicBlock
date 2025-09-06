using GoogleMobileAds.Api;
using System;
using UnityEngine;

public class InterstitialAdController
{
	/*
	 * 전면 테스트 광고 : "ca-app-pub-3940256099942544/1033173712"
	 * 캐싱된 전면 광고의 유효시간은 1시간
	 */
	private const string _interstitialAdId = "ca-app-pub-3940256099942544/1033173712";
	private InterstitialAd _loadedAd;

    public bool IsReady { get; private set; }
    public event Action Closed;
    public event Action Failed;

    public void Init()
	{
		// 초기화 과정에서 광고 한번은 로드하기
		if (_loadedAd != null) DestroyAd();
		// TODO : 1시간마다 광고가 만료되서 시간도 체크해서 로드하기

		Debug.Log("전면 광고 로딩 시작");

		InterstitialAd.Load(_interstitialAdId, new AdRequest(), (ad, error) =>
		{
			if (error != null)
			{
				Debug.LogError($"전면 광고 로딩 실패 : [{error.ToString()}]");
				_loadedAd = null;
				return;
			}
			if (ad == null)
			{
				Debug.LogError($"로딩할 전면 광고 없음");
				return;
			}

			Debug.Log("전면 광고 로딩 성공");
            _loadedAd = ad;
            IsReady = true;
            EventConnect(_loadedAd);
        });
	}
	public void ShowAd()
	{
		// 1로딩 1재생
		if (_loadedAd != null && _loadedAd.CanShowAd())
		{
			_loadedAd.Show();
		}
		else
		{
			Init();
		}
	}
    public void DestroyAd()
    {
        if (_loadedAd == null) return;

        Debug.Log("전면 광고 제거");
        _loadedAd.Destroy();
        _loadedAd = null;
        IsReady = false;
    }
    void EventConnect(InterstitialAd ad)
    {
        if (ad == null) return;

        ad.OnAdPaid += (AdValue v) => Debug.Log($"전면 광고 수익 {v.CurrencyCode}/{v.Value}");
        ad.OnAdImpressionRecorded += () => Debug.Log("전면 광고 봄");
        ad.OnAdClicked += () => Debug.Log("전면 광고 클릭");

        ad.OnAdFullScreenContentOpened += () =>
        {
            Debug.Log("전면 광고 활성화");
        };

        ad.OnAdFullScreenContentClosed += () =>
        {
            Debug.Log("전면 광고 닫음");
            IsReady = false;
            Closed?.Invoke();
            Init(); // 다음 로드
        };

        ad.OnAdFullScreenContentFailed += (AdError e) =>
        {
            Debug.LogError($"전면 광고 에러 : [{e}]");
            IsReady = false;
            Failed?.Invoke();
            Init();
        };
    }
}

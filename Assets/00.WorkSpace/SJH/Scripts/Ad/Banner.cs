using GoogleMobileAds.Api;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class Banner
{
	/*
	 * 배너 테스트 광고 : "ca-app-pub-3940256099942544/6300978111"
	 * 배너 광고 : "ca-app-pub-4554669209191050/5110942945"
	 */
	private const string _bannerAdId = "ca-app-pub-3940256099942544/6300978111";
	private BannerView _loadedBanner;

	public void Init()
	{
		if (_loadedBanner != null) _loadedBanner.Destroy();

		Debug.Log("배너 광고 초기화 시작");

		//AdSize adSize = new AdSize(100, 80);
		//_loadedBanner = new BannerView(_bannerAdId, adSize, 0, 100);
		//_loadedBanner = new BannerView(_bannerAdId, AdSize.Banner, 0, 100);
		_loadedBanner = new BannerView(_bannerAdId, AdSize.Banner, AdPosition.Bottom);

		Debug.Log("배너 광고 초기화 성공");

		_loadedBanner.LoadAd(new AdRequest());
		BannerEventConnect();
	}

	void BannerEventConnect()
	{
		Debug.Log("광고 이벤트 추가");

		// 배너 광고가 등록됐을 때
		_loadedBanner.OnBannerAdLoaded += () =>
		{
			Debug.Log("배너 광고 등록");
		};
		// 배너 광고 등록 실패
		_loadedBanner.OnBannerAdLoadFailed += (LoadAdError error) =>
		{
			Debug.LogError($"배너 광고 등록 실패 : [{error}]");
		};
		// 광고 수익 발생했을 때
		_loadedBanner.OnAdPaid += (AdValue adValue) =>
		{
			Debug.Log($"배너 광고 수익 {adValue.CurrencyCode} / {adValue.Value}");
		};
		// 유저가 광고를 봤을 때
		_loadedBanner.OnAdImpressionRecorded += () =>
		{
			Debug.Log("배너 광고 봄");
		};
		// 유저가 광고를 클릭했을 때
		_loadedBanner.OnAdClicked += () =>
		{
			Debug.Log("배너 광고 클릭");
		};
		// 전면 광고가 활성화됐을 때
		_loadedBanner.OnAdFullScreenContentOpened += () =>
		{
			// TODO : 게임 사이클 정지
			Debug.Log("배너 광고 활성화");
		};
		// 전면 광고 닫았을 때
		_loadedBanner.OnAdFullScreenContentClosed += () =>
		{
			// TODO : 보상 지급
			Debug.Log("배너 광고 닫음");
		};
	}
}

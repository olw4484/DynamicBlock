using GoogleMobileAds.Api;
using UnityEngine;

public class BannerAdController
{
	/*
	 * 배너 테스트 광고 : "ca-app-pub-3940256099942544/6300978111"
	 * 배너 광고 : "ca-app-pub-4554669209191050/5110942945"
	 * 
	 * 배너 광고 갱신은 구글 애드몹에서 갱신되게 설정하면 자동 갱신
	 */
	private const string _bannerAdId = "ca-app-pub-3940256099942544/6300978111";
	private BannerView _loadedAd;
	private bool _isShow = false;
	private bool _isLoaded = false;

	public void Init()
	{
		if (_loadedAd != null) _loadedAd.Destroy();

		Debug.Log("배너 광고 초기화 시작");

		//AdSize adSize = new AdSize(100, 80);
		//_loadedBanner = new BannerView(_bannerAdId, adSize, 0, 100);
		//_loadedBanner = new BannerView(_bannerAdId, AdSize.Banner, 0, 100);
		_loadedAd = new BannerView(_bannerAdId, AdSize.Banner, AdPosition.Bottom);

		Debug.Log("배너 광고 초기화 성공");

		EventConnect();
	}
	public void AdToggle()
	{
		if (_loadedAd == null) Init();

		if (_isShow) HideAd();
		else ShowAd();
	}
	public void ShowAd()
	{
		if (_loadedAd != null)
		{
			if (!_isLoaded) _loadedAd.LoadAd(new AdRequest());
			Debug.Log("배너 광고 On");
			_loadedAd.Show();
			_isShow = true;
		}
	}
	public void HideAd()
	{
		if (_loadedAd != null)
		{
			Debug.Log("배너 광고 Off");
			_loadedAd.Hide();
			_isShow = false;
		}
	}
	public void DestroyAd()
	{
		if (_loadedAd == null) return;

		Debug.Log("배너 광고 제거");
		_loadedAd.Destroy();
		_loadedAd = null;
	}
	public void EventConnect()
	{
		if (_loadedAd == null)
		{
			Init();
			return;
		}
		Debug.Log("배너 광고 이벤트 추가");

		// 배너 광고가 등록됐을 때
		_loadedAd.OnBannerAdLoaded += () =>
		{
			Debug.Log("배너 광고 등록");
			_isLoaded = true;
		};

		// 배너 광고 등록 실패
		_loadedAd.OnBannerAdLoadFailed += (LoadAdError error) =>
		{
			Debug.LogError($"배너 광고 등록 실패 : [{error}]");
		};

		// 광고 수익 발생했을 때
		_loadedAd.OnAdPaid += (AdValue adValue) =>
		{
			Debug.Log($"배너 광고 수익 {adValue.CurrencyCode} / {adValue.Value}");
		};

		// 유저가 광고를 봤을 때
		_loadedAd.OnAdImpressionRecorded += () =>
		{
			Debug.Log("배너 광고 봄");
		};

		// 유저가 광고를 클릭했을 때
		_loadedAd.OnAdClicked += () =>
		{
			Debug.Log("배너 광고 클릭");
		};

		// 배너 광고가 활성화됐을 때
		_loadedAd.OnAdFullScreenContentOpened += () =>
		{
			Debug.Log("배너 광고 활성화");
		};

		// 배너 광고 닫았을 때
		_loadedAd.OnAdFullScreenContentClosed += () =>
		{
			Debug.Log("배너 광고 닫음");
		};
	}
}

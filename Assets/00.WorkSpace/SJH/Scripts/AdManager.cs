using GoogleMobileAds.Api;
using UnityEngine;

public class AdManager : MonoBehaviour
{
	/*
	 * 전면 테스트 광고 : "ca-app-pub-3940256099942544/1033173712"
	 * 전면 광고 : "ca-app-pub-4554669209191050/7419140576"
	 * 
	 * 배너 테스트 광고 : "ca-app-pub-3940256099942544/6300978111"
	 * 배너 광고 : "ca-app-pub-4554669209191050/5110942945"
	 */
	private const string _interstitialAdId = "ca-app-pub-3940256099942544/1033173712"; // 전면 광고 테스트 아이디
	private const string _bannerAdId = "ca-app-pub-3940256099942544/6300978111";

	private InterstitialAd _loadedInterstitial;
	private BannerView _loadedBanner;

	void Awake()
	{
		MobileAds.Initialize(status =>
		{
			if (status == null)
			{
				Debug.LogError("모바일 광고 초기화 실패");
				return;
			}

			Debug.Log("모바일 광고 초기화 성공");
			InterstitialInit();
			BannerInit();
		});
	}

	public void InterstitialInit()
	{
		// 초기화 과정에서 광고 한번은 로드하기

		// TODO : 1시간마다 광고가 만료되서 시간도 체크해서 로드하기

		Debug.Log("전면 광고 로딩 시작");

		InterstitialAd.Load(_interstitialAdId, new AdRequest(), (ad, error) =>
		{
			if (error != null)
			{
				Debug.LogError($"전면 광고 로딩 실패 : [{error.ToString()}]");
				_loadedInterstitial = null;
				return;
			}
			if (ad == null)
			{
				Debug.LogError($"로딩할 전면 광고 없음");
				return;
			}

			Debug.Log("전면 광고 로딩 성공");
			_loadedInterstitial = ad;

			InterstitialEventConnect(ad);
		});
	}

	public void BannerInit()
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

	public void ShowAd()
	{
		// 1로딩 1재생 해야함

		if (_loadedInterstitial != null && _loadedInterstitial.CanShowAd())
		{
			_loadedInterstitial.Show();
			_loadedInterstitial = null;
		}
		else
		{
			InterstitialInit();
			ShowAd();
		}
	}

	void InterstitialEventConnect(InterstitialAd ad)
	{
		Debug.Log("광고 이벤트 추가");
		//_loadedAd.OnAdFullScreenContentClosed -= LoadAd;
		//_loadedAd.OnAdFullScreenContentClosed += LoadAd;

		// 광고 수익 발생했을 때
		ad.OnAdPaid += (AdValue adValue) =>
		{
			Debug.Log($"전면 광고 수익 {adValue.CurrencyCode} / {adValue.Value}");
		};
		// 유저가 광고를 봤을 때
		ad.OnAdImpressionRecorded += () =>
		{
			Debug.Log("전면 광고 봄");
		};
		// 유저가 광고를 클릭했을 때
		ad.OnAdClicked += () =>
		{
			Debug.Log("전면 광고 클릭");
		};
		// 전면 광고가 활성화됐을 때
		ad.OnAdFullScreenContentOpened += () =>
		{
			// TODO : 게임 사이클 정지
			Debug.Log("전면 광고 활성화");
		};
		// 전면 광고 닫았을 때
		ad.OnAdFullScreenContentClosed += () =>
		{
			// TODO : 보상 지급
			Debug.Log("전면 광고 닫음");
			InterstitialInit();
		};
		// 전면 광고 에러
		ad.OnAdFullScreenContentFailed += (AdError error) =>
		{
			Debug.LogError($"전면 광고 에러 : [{error}]");
		};
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

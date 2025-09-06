using GoogleMobileAds.Api;
using UnityEngine;

public class InterstitialAdController
{
	/*
	 * 전면 테스트 광고 : "ca-app-pub-3940256099942544/1033173712"
	 * 캐싱된 전면 광고의 유효시간은 1시간
	 */
	private const string _interstitialAdId = "ca-app-pub-3940256099942544/1033173712";
	private InterstitialAd _loadedAd;

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

			EventConnect();
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
			ShowAd();
		}
	}
	public void DestroyAd()
	{
		if (_loadedAd == null) return;

		Debug.Log("전면 광고 제거");
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
		Debug.Log("전면 광고 이벤트 추가");

		// 광고 수익 발생했을 때
		_loadedAd.OnAdPaid += (AdValue adValue) =>
		{
			Debug.Log($"전면 광고 수익 {adValue.CurrencyCode} / {adValue.Value}");
		};

		// 유저가 광고를 봤을 때
		_loadedAd.OnAdImpressionRecorded += () =>
		{
			Debug.Log("전면 광고 봄");
		};

		// 유저가 광고를 클릭했을 때
		_loadedAd.OnAdClicked += () =>
		{
			Debug.Log("전면 광고 클릭");
		};

		// 전면 광고가 활성화됐을 때
		_loadedAd.OnAdFullScreenContentOpened += () =>
		{
			// TODO : 게임 사이클 정지
			Debug.Log("전면 광고 활성화");
		};

		// 전면 광고 닫았을 때
		_loadedAd.OnAdFullScreenContentClosed += () =>
		{
			// TODO : 보상 지급
			Debug.Log("전면 광고 닫음");
			Init();
		};

		// 전면 광고 에러
		_loadedAd.OnAdFullScreenContentFailed += (AdError error) =>
		{
			Debug.LogError($"전면 광고 에러 : [{error}]");
		};
	}
}

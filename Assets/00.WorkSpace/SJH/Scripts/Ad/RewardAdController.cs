using GoogleMobileAds.Api;
using System;
using UnityEngine;

public class RewardAdController
{
	/*
	 * 보상 테스트 광고 : "ca-app-pub-3940256099942544/5224354917"
	 * 보상 광고 : "ca-app-pub-4554669209191050/7651445993"
	 */

	private const string _rewardAdId = "ca-app-pub-4554669209191050/7651445993";
	private RewardedAd _loadedAd;
	private Action<Reward> _rewardAction;

	public void Init()
	{
		if (_loadedAd != null) DestroyAd();

		Debug.Log("리워드 광고 로딩 시작");
		var adRequest = new AdRequest();

		RewardedAd.Load(_rewardAdId, adRequest, (RewardedAd ad, LoadAdError error) =>
		{
			if (error != null)
			{
				Debug.LogError($"리워드 광고 로딩 실패 : [{error.ToString()}]");
				_loadedAd = null;
				return;
			}
			if (ad == null)
			{
				Debug.LogError($"로딩할 리워드 광고 없음");
				return;
			}

			Debug.Log("리워드 광고 로딩 성공");
			_loadedAd = ad;

			EventConnect();
		});
	}
	public void DestroyAd()
	{
		if (_loadedAd == null) return;

		Debug.Log("리워드 광고 제거");
		_loadedAd.Destroy();
		_loadedAd = null;
	}
	public void ShowAd()
	{
		if (_loadedAd != null && _loadedAd.CanShowAd())
		{
			_loadedAd.Show(_rewardAction);
			_loadedAd = null;
		}
		else
		{
			Init();
			ShowAd();
		}
	}
	public void EventConnect()
	{
		if (_loadedAd == null)
		{
			Init();
			return;
		}
		Debug.Log("리워드 광고 이벤트 추가");

		// 광고 수익 발생했을 때
		_loadedAd.OnAdPaid += (AdValue adValue) =>
		{
			Debug.Log($"리워드 광고 수익 {adValue.CurrencyCode} / {adValue.Value}");
		};

		// 유저가 광고를 봤을 때
		_loadedAd.OnAdImpressionRecorded += () =>
		{
			Debug.Log("리워드 광고 봄");
		};

		// 유저가 광고를 클릭했을 때
		_loadedAd.OnAdClicked += () =>
		{
			Debug.Log("리워드 광고 클릭");
		};

		// 리워드 광고가 활성화됐을 때
		_loadedAd.OnAdFullScreenContentOpened += () =>
		{
			// TODO : 게임 사이클 정지
			Debug.Log("리워드 광고 활성화");
		};

		// 리워드 광고 닫았을 때
		_loadedAd.OnAdFullScreenContentClosed += () =>
		{
			// TODO : 보상 지급
			Debug.Log("리워드 광고 닫음");
			Init();
		};

		// 전면 광고 에러
		_loadedAd.OnAdFullScreenContentFailed += (AdError error) =>
		{
			Debug.LogError($"리워드 광고 에러 : [{error}]");
		};

		// TODO : 리워드 광고 후 실행할 액션
		_rewardAction += (reward) =>
		{
			Debug.Log($"리워드 획득 : Type [{reward.Type}] / Amount [{reward.Amount}]");
		};
	}
}

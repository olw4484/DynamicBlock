using GoogleMobileAds.Api;
using UnityEngine;
using UnityEngine.UI;

public class AdManager : MonoBehaviour
{
	public static AdManager Instance { get; private set; }

	public InterstitialAdController Interstitial { get; private set; }
	public BannerAdController Banner { get; private set; }
	public RewardAdController Reward { get; private set; }

	// 테스트 버튼들
	[SerializeField] private Button _showBtn;
	[SerializeField] private Button _bannerBtn;
	[SerializeField] private Button _rewardBtn;

	void Awake()
	{
		Instance = this;

		MobileAds.Initialize(status =>
		{
			if (status == null)
			{
				Debug.LogError("모바일 광고 초기화 실패");
				return;
			}

			Debug.Log("모바일 광고 초기화 성공");

			// 전면 광고 초기화
			Interstitial = new InterstitialAdController();
			Interstitial.Init();

			// 배너 광고 초기화
			Banner = new BannerAdController();
			Banner.Init();

			// 리워드 광고 초기화
			Reward = new RewardAdController();
			Reward.Init();

			if (Interstitial != null) _showBtn.onClick.AddListener(Interstitial.ShowAd);
			if (Banner != null) _bannerBtn.onClick.AddListener(Banner.AdToggle);
			if (Reward != null) _rewardBtn.onClick.AddListener(Reward.ShowAd);
		});
	}
}

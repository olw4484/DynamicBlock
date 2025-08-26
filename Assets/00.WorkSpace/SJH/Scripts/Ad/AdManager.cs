using GoogleMobileAds.Api;
using UnityEngine;
using UnityEngine.UI;

public class AdManager : MonoBehaviour
{
	private Interstitial _interstitial;
	private Banner _banner;

	[SerializeField] private Button _showBtn;

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

			// 전면 광고 초기화
			_interstitial = new Interstitial();
			_interstitial.Init();

			// 배너 광고 초기화
			_banner = new Banner();
			_banner.Init();

			if (_interstitial != null) _showBtn.onClick.AddListener(_interstitial.ShowAd);
		});
	}
}

using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ReviveScreen : MonoBehaviour
{
	[SerializeField] private Button _reviveBtn;
	[SerializeField] private TMP_Text _countText;
	private float _count = 5f;
	[SerializeField] private Image _countImage;
	private Coroutine _timerRoutine;

	void OnEnable()
	{
		// 버튼에 리워드 이벤트 연결
		_reviveBtn.onClick.AddListener(ShowRewardAd);
		
		// 5초 카운트 후 전면 광고 실행
		_timerRoutine = StartCoroutine(TimerRoutine());
	}

	void OnDisable()
	{
		if (_timerRoutine != null)
		{
			StopCoroutine(_timerRoutine);
			_timerRoutine = null;
		}

		_reviveBtn.onClick.RemoveAllListeners();

		_count = 5f;
		_countText.text = $"{_count}";
		_countImage.fillAmount = 1;
	}

	IEnumerator TimerRoutine()
	{
		_count = 5f;
		while (_count > 0f)
		{
			_count -= Time.deltaTime;

			if (_count < 0f) _count = 0f;

			//_countText.text = $"{Mathf.CeilToInt(_count)}";
			_countText.text = $"{(int)_count}";

			_countImage.fillAmount = _count / 5f;

			yield return null;
		}
		// TODO : 전면 광고 실행
		Debug.Log("카운트 종료, 전면광고 실행");
		AdManager.Instance.Interstitial.ShowAd();
		gameObject.SetActive(false);
	}

	void ShowRewardAd()
	{
		// TODO : 리워드 콜백에 클래식, 어드벤처에 따른 블럭 생성 구분해야함
		AdManager.Instance.Reward.ShowAdSimple();
		gameObject.SetActive(false);
	}
}

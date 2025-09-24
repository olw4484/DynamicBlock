using Firebase;
using Firebase.Analytics;
using Firebase.Extensions;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class AnalyticsManager : MonoBehaviour
{
    public static AnalyticsManager Instance { get; private set; }
	public FirebaseApp FirebaseApp { get; private set; }

	void Start()
	{
		Instance = this;
		Init();
	}

	public void Init()
	{
		FirebaseApp.CheckAndFixDependenciesAsync().ContinueWithOnMainThread(task => {
			var dependencyStatus = task.Result;
			if (dependencyStatus == DependencyStatus.Available)
			{
				FirebaseApp = FirebaseApp.DefaultInstance;
				Debug.Log("Analytics 초기화 성공");
				FirebaseAnalytics.LogEvent(FirebaseAnalytics.EventLogin);

				// 파이어베이스 초기화
				FirestoreManager.Instance.Init();
			}
			else
			{
				Debug.LogError($"Analytics 초기화 실패 : [{dependencyStatus}]");
			}
		});
	}

	public void LogEvent(string eventName)
	{
		FirebaseAnalytics.LogEvent(eventName);
		// TODO : 앱러빈 SDK 로깅함수 추가
	}

	public void LogEvent(string eventName, string paramName, int paramValue)
	{
		FirebaseAnalytics.LogEvent(eventName, paramName, paramValue);
	}

	public void LogEvent(string eventName, string paramName, float paramValue)
	{
		FirebaseAnalytics.LogEvent(eventName, paramName, paramValue);
	}

	public void LogEvent(string eventName, string paramName, string paramValue)
	{
		//new Firebase.Analytics.Parameter();
		FirebaseAnalytics.LogEvent(eventName, paramName, paramValue);
	}

	public void LogEvent(string eventName, params Parameter[] paramArray)
	{
		FirebaseAnalytics.LogEvent(eventName, paramArray);
	}

	public void LogEvent(string eventName, params string[] _params)
	{
		Parameter[] parameters = new Parameter[_params.Length];
		int pLen = _params.Length;
		for (int i = 0; i < pLen; i++)
		{
			parameters[i] = new Parameter($"param{i + 1}", _params[i]);
			FirebaseAnalytics.LogEvent(eventName, parameters);
		}
	}

	/// <summary>
	/// 리워드광고 실행될 때 호출
	/// </summary>
	public void RewardLog()
	{
		// 리워드 광고 시청
		LogEvent("Reward_Impression");
	}

	/// <summary>
	/// 전면광고 실행될 때 호출
	/// </summary>
	public void InterstitialLog()
	{
		// 전면 광고 시청
		LogEvent("Interstitial_Impression");
	}

	/// <summary>
	/// 클래식, 어드벤처 게임 시작할 때 호출
	/// </summary>
	/// <param name="isClassic">클래식 true, 어드벤처 false</param>
	/// <param name="stageIndex">어드벤처 스테이지 난이도</param>
	public void GameStartLog(bool isClassic, int stageIndex = 1)
	{
		// 클래식, 어드벤처 플레이 시작 횟수
		LogEvent("GameStart", (isClassic ? "classic" : "adventure"), stageIndex);
	}

	/// <summary>
	/// 다시하기 버튼 클릭시 호출
	/// </summary>
	/// <param name="isClassic">클래식 true, 어드벤처 false</param>
	public void RetryLog(bool isClassic)
	{
		// 클래식/어드벤처 게임 오버 후에 다시하기를 누른 횟수
		LogEvent("GameRetry");
	}

	/// <summary>
	/// 클래식모드 최대 점수를 갱신했을 때 호출
	/// </summary>
	/// <param name="score">최대 점수 필요시 long 타입으로 변환</param>
	public void ClassicBestLog(int score)
	{
		// 클래식 모드 최고 점수 도달 (ex. 5000, 10000, 20000...)
		LogEvent("BestScoreUpdate", "score", score);
	}

	/// <summary>
	/// 어드벤처모드 최고 스테이지 갱신했을 때 호출
	/// </summary>
	/// <param name="stageIndex">어드벤처 스테이지 난이도</param>
	/// <param name="stageName">어드벤처 스테이지 이름</param>
	public void AdventureBestLog(int stageIndex, string stageName = "Stage1")
	{
		// 어드벤처 챕터, 스테이지 클리어 정도(ex.Stage 15)
		// key = 스테이지 이름
		// value = 스테이지 난이도
		LogEvent("BestStageUpdate", stageName, stageIndex);
	}
}

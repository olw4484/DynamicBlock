using UnityEngine;
using Firebase;
using Firebase.Extensions;
using Firebase.Analytics;

public class AnalyticsManager : MonoBehaviour
{
    public static AnalyticsManager Instance { get; private set; }
	public FirebaseApp FirebaseApp { get; private set; }

	void Awake()
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
				FirebaseAnalytics.LogEvent(FirebaseAnalytics.EventLogin);
				FirebaseAnalytics.LogEvent("TestEvent");
			}
			else
			{
				Debug.LogError($"파이어베이스 초기화 실패 : [{dependencyStatus}]");
			}
		});
	}
}

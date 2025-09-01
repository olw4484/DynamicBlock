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

		FirebaseAnalytics.LogEvent(FirebaseAnalytics.EventLogin);
	}

	public void Init()
	{
		FirebaseApp.CheckAndFixDependenciesAsync().ContinueWithOnMainThread(task => {
			var dependencyStatus = task.Result;
			if (dependencyStatus == Firebase.DependencyStatus.Available)
			{
				// Create and hold a reference to your FirebaseApp,
				// where app is a Firebase.FirebaseApp property of your application class.
				FirebaseApp = FirebaseApp.DefaultInstance;
				Debug.Log("성공");
				// Set a flag here to indicate whether Firebase is ready to use by your app.
				FirebaseAnalytics.LogEvent(FirebaseAnalytics.EventLogin);
			}
			else
			{
				Debug.LogError(System.String.Format(
				  "Could not resolve all Firebase dependencies: {0}", dependencyStatus));
				// Firebase Unity SDK is not safe to use here.
			}
		});
	}
}

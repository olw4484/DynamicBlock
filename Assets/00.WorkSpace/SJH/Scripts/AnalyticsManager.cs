using UnityEngine;
using Firebase;
using Firebase.Extensions;
using Firebase.Analytics;
using TMPro;
using UnityEngine.UI;
using System.Collections;

public class AnalyticsManager : MonoBehaviour
{
    public static AnalyticsManager Instance { get; private set; }
	public FirebaseApp FirebaseApp { get; private set; }

	[SerializeField] private TMP_InputField _textInput;
	[SerializeField] private Button _sendEventBtn;

	void Awake()
	{
		Instance = this;

		Init();

		_sendEventBtn.onClick.AddListener(() =>
		{
			LogEvent(_textInput.text, "Point", 39);
			Debug.Log($"[{_textInput.text}] 이벤트 로깅");
		});
	}

	public void Init()
	{
		FirebaseApp.CheckAndFixDependenciesAsync().ContinueWithOnMainThread(task => {
			var dependencyStatus = task.Result;
			if (dependencyStatus == DependencyStatus.Available)
			{
				FirebaseApp = FirebaseApp.DefaultInstance;
				Debug.Log("파이어베이스 초기화 성공");
				FirebaseAnalytics.LogEvent(FirebaseAnalytics.EventLogin);
				FirebaseAnalytics.LogEvent("TestEvent");
			}
			else
			{
				Debug.LogError($"파이어베이스 초기화 실패 : [{dependencyStatus}]");
			}
		});
	}

	public void LogEvent(string eventName)
	{
		FirebaseAnalytics.LogEvent(eventName);
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
}

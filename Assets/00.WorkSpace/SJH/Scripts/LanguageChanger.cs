using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Components;
using UnityEngine.Localization.Settings;

public enum GameLanguage
{
	Korean,
	English,
}

public class LanguageChanger : MonoBehaviour
{
	[SerializeField] private TMP_Text _text;
	[SerializeField] private LocalizeStringEvent _testText;
	[SerializeField] private TMP_Dropdown _languageDropDown;

	void Awake()
	{
		StartCoroutine(InitRoutine());
	}

	IEnumerator InitRoutine()
	{
		bool hasSaveData = SaveLoadManager.Instance.LoadData();

		yield return LocalizationSettings.InitializationOperation;

		var locales = LocalizationSettings.AvailableLocales;
		int targetIndex = 0;

		if (hasSaveData)
		{
			// 세이브 있으면 세이브 인덱스 사용
			int saveIndex = SaveLoadManager.Instance.GameData.LanguageIndex;
			targetIndex = (saveIndex >= 0 && saveIndex < locales.Locales.Count) ? saveIndex : 0;
		}
		else
		{
			// 세이브 없으면 OS 언어 사용
			Locale sys = locales.GetLocale(Application.systemLanguage);
			if (sys == null) sys = LocalizationSettings.ProjectLocale;
			targetIndex = Mathf.Max(0, locales.Locales.IndexOf(sys)); // 세이브데이터 없으면 -1
		}

		SaveLoadManager.Instance.GameData.LanguageIndex = targetIndex;
		SaveLoadManager.Instance.SaveData();

		LocalizationSettings.SelectedLocale = locales.Locales[targetIndex];
		_languageDropDown.SetValueWithoutNotify(targetIndex);

		_languageDropDown.onValueChanged.AddListener(OnValueChanged);
		_testText.OnUpdateString.AddListener(OnUpdateString);

		_testText.RefreshString();
	}

	public void OnValueChanged(int value)
	{
		Debug.Log(value);
		// 0	ko-KR
		// 1	en
		var locale = LocalizationSettings.AvailableLocales.Locales[value];
		LocalizationSettings.SelectedLocale = locale;
		SaveLoadManager.Instance.GameData.LanguageIndex = value;
		SaveLoadManager.Instance.SaveData();
	}

	public void OnUpdateString(string text)
	{
		Debug.Log(text);
		_text.text = text;
	}
}

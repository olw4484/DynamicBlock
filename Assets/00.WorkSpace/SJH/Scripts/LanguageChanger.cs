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
	//public static LanguageChanger Instance { get; private set; }

	[SerializeField] private TMP_Text _text;
	[SerializeField] private LocalizeStringEvent _testText;
	[SerializeField] private TMP_Dropdown _languageDropDown;

	//public event Action<Locale> OnLocaleChanged;

	//[SerializeField] private TMP_FontAsset _koreanFont;
	//[SerializeField] private TMP_FontAsset _englishFont;

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
			Locale sys = locales.GetLocale(GetSystemLanguageCode(locales));
			Debug.LogWarning(sys);
			if (sys == null) sys = LocalizationSettings.ProjectLocale;
			targetIndex = Mathf.Max(0, locales.Locales.IndexOf(sys)); // 세이브데이터 없으면 -1
		}

		SaveLoadManager.Instance.GameData.LanguageIndex = targetIndex;
		SaveLoadManager.Instance.SaveData();

		var locale = LocalizationSettings.SelectedLocale = locales.Locales[targetIndex];
		_languageDropDown.SetValueWithoutNotify(targetIndex);

		_languageDropDown.onValueChanged.AddListener(OnValueChanged);
		_testText.OnUpdateString.AddListener(OnUpdateString);

		_testText.RefreshString();

		//Instance = this;
		
		//var fontChangers = FindObjectsOfType<FontChanger>(true);
		//foreach (var fc in fontChangers)
		//{
		//	OnLocaleChanged += fc.UpdateFont;
		//	fc.UpdateFont(locale);
		//}

		//OnLocaleChanged?.Invoke(locale);
		Debug.Log("LanguageChanger 초기화 완료");
	}

	public void OnValueChanged(int value)
	{
		Debug.Log(value);
		// 0	en
		// 1	ko-KR
		// 2	ja-JP
		// 3	es
		// 4	zh
		var locale = LocalizationSettings.AvailableLocales.Locales[value];
		LocalizationSettings.SelectedLocale = locale;
		SaveLoadManager.Instance.GameData.LanguageIndex = value;
		SaveLoadManager.Instance.SaveData();

		//OnLocaleChanged?.Invoke(locale);
	}

	public void OnUpdateString(string text)
	{
		Debug.Log(text);
		_text.text = text;
	}

	//public TMP_FontAsset GetFontForLocale(Locale locale)
	//{
	//	switch (locale.Identifier.Code)
	//	{
	//		case "ko-KR": return _koreanFont;
	//		case "en": return _englishFont;
	//		default: return _englishFont;
	//	}
	//}

	string GetSystemLanguageCode(ILocalesProvider locales)
	{
		var code = new LocaleIdentifier(Application.systemLanguage).Code;
		switch (code)
		{
			case "ko": return "ko-KR";
			default: return code;
		}
	}
}

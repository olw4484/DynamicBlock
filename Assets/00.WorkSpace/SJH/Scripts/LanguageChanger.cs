using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Localization.Components;
using UnityEngine.Localization.Settings;

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
		yield return LocalizationSettings.InitializationOperation;

		_languageDropDown.onValueChanged.AddListener(OnValueChanged);
		_testText.OnUpdateString.AddListener(OnUpdateString);

		_testText.RefreshString();
	}

	public void OnValueChanged(int value)
	{
		// 0 첫번째
		Debug.Log(value);
		var locale = LocalizationSettings.AvailableLocales.Locales[value];
		LocalizationSettings.SelectedLocale = locale;
	}

	public void OnUpdateString(string text)
	{
		Debug.Log(text);
		_text.text = text;
	}
}

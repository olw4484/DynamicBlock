using TMPro;
using UnityEngine;
using UnityEngine.Localization;

public class FontChanger : MonoBehaviour
{
	[SerializeField] private TMP_Text _text;

	public void UpdateFont(Locale locale)
	{
		if (_text == null) _text = GetComponent<TMP_Text>();
		//_text.font = LanguageChanger.Instance.GetFontForLocale(locale);
	}
}

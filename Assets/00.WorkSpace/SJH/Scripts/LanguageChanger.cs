using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;

public class LanguageChanger : MonoBehaviour
{
	[SerializeField] private TMP_Dropdown _languageDropDown;
    [SerializeField] private SaveServiceAdapter _save;

    void Awake()
	{
        StartCoroutine(InitRoutine());
    }

    IEnumerator InitRoutine()
    {
        // Localization 시스템 준비 대기
        yield return LocalizationSettings.InitializationOperation;

        // 1) 세이브의 현재 LanguageIndex를 GameDataChanged로부터 받기
        int initialIndex = 0;
        bool got = false;

        Game.Bus.Subscribe<GameDataChanged>(e =>
        {
            if (got) return;               // 최초 1회만 초기화에 사용
            got = true;
            initialIndex = e.data != null ? e.data.LanguageIndex : 0;
            Apply(initialIndex);
        }, replaySticky: true);

        // 혹시 이벤트가 아주 늦게 오거나(이례) 못 받는 경우를 대비한 작은 타임아웃
        float t = 0f;
        while (!got && t < 1.0f) { t += Time.unscaledDeltaTime; yield return null; }
        if (!got) Apply(initialIndex);     // fallback
    }

    private void Apply(int index)
    {
        var locales = LocalizationSettings.AvailableLocales;
        index = Mathf.Clamp(index, 0, locales.Locales.Count - 1);

        // 로케일 적용
        LocalizationSettings.SelectedLocale = locales.Locales[index];

        // 드롭다운 UI 세팅
        _languageDropDown.SetValueWithoutNotify(index);
        _languageDropDown.onValueChanged.RemoveAllListeners();
        _languageDropDown.onValueChanged.AddListener(OnValueChanged);

        Debug.Log($"[LanguageChanger] 초기화 완료 index={index}, locale={locales.Locales[index].Identifier.Code}");
    }


    public void OnValueChanged(int value)
    {
        var locales = LocalizationSettings.AvailableLocales;
        if (value < 0 || value >= locales.Locales.Count) return;

        LocalizationSettings.SelectedLocale = locales.Locales[value];

        Game.Bus.Publish(new LanguageChangeRequested(value));
        Debug.Log($"[LanguageChanger] 변경 index={value}, locale={locales.Locales[value].Identifier.Code}");
    }
}

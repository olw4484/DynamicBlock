using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;

public class LanguageChanger : MonoBehaviour
{
    [SerializeField] private List<TMP_Dropdown> _languageDropDown = new();
    [SerializeField] private SaveServiceAdapter _save;

    void Awake()
    {
        StartCoroutine(InitRoutine());
    }

    IEnumerator InitRoutine()
    {
        // Localization 시스템 준비
        yield return LocalizationSettings.InitializationOperation;

        int initialIndex = 0;
        bool got = false;

        // 세이브의 현재 LanguageIndex를 Sticky로 1회 수신
        Game.Bus.Subscribe<GameDataChanged>(e =>
        {
            if (got) return;
            got = true;
            initialIndex = (e.data != null) ? e.data.LanguageIndex : 0;
            Apply(initialIndex);
        }, replaySticky: true);

        // 안전 타임아웃 (예외 케이스)
        float t = 0f;
        while (!got && t < 1.0f) { t += Time.unscaledDeltaTime; yield return null; }
        if (!got) Apply(initialIndex);
    }

    void OnDestroy()
    {
        // 리스너 정리 (씬 전환/파괴 시 중복 방지)
        foreach (var dd in _languageDropDown)
        {
            if (dd == null) continue;
            dd.onValueChanged.RemoveListener(OnDropdownValueChanged);
        }
    }

    private void Apply(int index)
    {
        var locales = LocalizationSettings.AvailableLocales;
        if (locales == null || locales.Locales == null || locales.Locales.Count == 0) return;

        index = Mathf.Clamp(index, 0, locales.Locales.Count - 1);

        // 로케일 적용
        LocalizationSettings.SelectedLocale = locales.Locales[index];

        // 모든 드롭다운 초기 세팅 + 리스너 바인딩
        foreach (var dd in _languageDropDown)
        {
            if (dd == null) continue;

            dd.SetValueWithoutNotify(index);                  // 값만 반영 (이벤트 미발생)
            dd.onValueChanged.RemoveListener(OnDropdownValueChanged);
            dd.onValueChanged.AddListener(OnDropdownValueChanged);
        }

        Debug.Log($"[LanguageChanger] 초기화 index={index}, locale={locales.Locales[index].Identifier.Code}");
    }

    // 드롭다운 하나에서 변경되면 나머지도 동기화
    private void OnDropdownValueChanged(int value)
    {
        var locales = LocalizationSettings.AvailableLocales;
        if (locales == null || locales.Locales == null) return;
        if (value < 0 || value >= locales.Locales.Count) return;

        // 1) 로케일 적용
        LocalizationSettings.SelectedLocale = locales.Locales[value];

        // 2) 버스 통지 (세이브/표시 등)
        Game.Bus.Publish(new LanguageChangeRequested(value));

        // 3) 모든 드롭다운을 동일 값으로 맞춤 (루프 방지 위해 WithoutNotify)
        foreach (var dd in _languageDropDown)
        {
            if (dd == null) continue;
            if (dd.value != value)
                dd.SetValueWithoutNotify(value);
        }

        Debug.Log($"[LanguageChanger] 변경 index={value}, locale={locales.Locales[value].Identifier.Code}");
    }
}

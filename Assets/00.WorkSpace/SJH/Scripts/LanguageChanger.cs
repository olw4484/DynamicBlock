using _00.WorkSpace.GIL.Scripts.Managers;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Localization.Settings;

public class LanguageChanger : MonoBehaviour
{
    [SerializeField] private List<TMP_Dropdown> _languageDropDown = new();

    void OnEnable() { StartCoroutine(InitRoutine()); }
    void OnDisable()
    {
        foreach (var dd in _languageDropDown)
            if (dd) dd.onValueChanged.RemoveListener(OnDropdownValueChanged);
        // 버스 구독했으면 여기서 Unsubscribe
        // Game.Bus.Unsubscribe<GameDataChanged>(OnGameDataChanged); (구독 시)
    }

    IEnumerator InitRoutine()
    {
        // 로컬라이제이션 준비
        yield return LocalizationSettings.InitializationOperation;

        // 현재 저장된 인덱스 반영
        int idx = MapManager.Instance?.saveManager?.Data?.LanguageIndex ?? 0;
        SetAllDropdowns(idx);

        // 변경 리스너 등록
        foreach (var dd in _languageDropDown)
        {
            if (!dd) continue;
            dd.onValueChanged.RemoveListener(OnDropdownValueChanged);
            dd.onValueChanged.AddListener(OnDropdownValueChanged);
        }

        // 저장값 바뀔 때 드롭다운 동기화하고 싶으면 구독
        // Game.Bus.Subscribe<GameDataChanged>(OnGameDataChanged, replaySticky:true);
    }

    void OnDropdownValueChanged(int value)
    {
        MapManager.Instance?.saveManager?.SetLanguageIndex(value);
        SetAllDropdowns(value);

        // 실제 로케일 변경
        var locales = LocalizationSettings.AvailableLocales.Locales;
        if (value >= 0 && value < locales.Count)
            LocalizationSettings.SelectedLocale = locales[value];
    }

    // 저장값이 외부에서 바뀌었을 때 드롭다운 맞추기
    // void OnGameDataChanged(GameDataChanged e) => SetAllDropdowns(e.data.LanguageIndex);

    void SetAllDropdowns(int value)
    {
        foreach (var dd in _languageDropDown)
            if (dd && dd.value != value) dd.SetValueWithoutNotify(value);
    }
}

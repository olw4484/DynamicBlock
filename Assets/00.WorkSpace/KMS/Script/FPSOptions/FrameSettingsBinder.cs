using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public sealed class FrameSettingsBinder : MonoBehaviour
{
    [SerializeField] TMP_Dropdown dropdown;

    void Awake()
    {
        if (dropdown == null) dropdown = GetComponent<TMP_Dropdown>();
        dropdown.ClearOptions();
        dropdown.AddOptions(new System.Collections.Generic.List<string> { "30", "60", "MAX" });

        dropdown.SetValueWithoutNotify((int)FrameSettings.Current);
        dropdown.onValueChanged.AddListener(OnChanged);
    }

    void OnChanged(int idx)
    {
        FrameSettings.Current = (FrameOption)idx;
    }
}
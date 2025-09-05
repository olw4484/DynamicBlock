using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum FrameOption { Low30 = 0, High60 = 1, Max = 2 }

public static class FrameSettings
{
    const string Key = "frame_option";

    public static FrameOption Current
    {
        get => (FrameOption)PlayerPrefs.GetInt(Key, (int)FrameOption.High60);
        set { PlayerPrefs.SetInt(Key, (int)value); Apply(value); }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Bootstrap() => Apply(Current);

    public static void Apply(FrameOption option)
    {
#if UNITY_ANDROID || UNITY_IOS
        // �����: vSync�� �밳 ���õǹǷ� ��������� 0
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = TargetForMobile(option);
#else
        // PC: MAX�� vSync=1(����� �ֻ��� ����), �� �� vSync=0
        if (option == FrameOption.Max)
        {
            QualitySettings.vSyncCount = 1;   // ����� �ֻ����� ���� (60/120/144 ��)
            Application.targetFrameRate = -1; // vSync �켱
        }
        else
        {
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = option == FrameOption.Low30 ? 30 : 60;
        }
#endif
    }

    static int TargetForMobile(FrameOption option)
    {
#if UNITY_2021_2_OR_NEWER
        if (option == FrameOption.Max)
            return (int)Screen.currentResolution.refreshRateRatio.value; // 60/90/120 ��
#endif
        return option == FrameOption.Low30 ? 30 : (option == FrameOption.High60 ? 60 : -1);
    }

    // �Ϻ� ��⿡�� ��Ŀ�� ��ȯ �� ������ ������ Ǯ���� ��� ������ �뵵
    public static void Reapply() => Apply(Current);
}

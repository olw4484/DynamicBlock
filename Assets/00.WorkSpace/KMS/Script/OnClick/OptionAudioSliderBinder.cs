using UnityEngine;
using UnityEngine.UI;

public sealed class OptionAudioSliderBinder : MonoBehaviour
{
    [Header("Sliders (0~1)")]
    [SerializeField] Slider bgm;
    [SerializeField] Slider se;
    [SerializeField] Slider vib; // 0/1 �����̴��� ���, �ƴϸ� ����

    [Range(0f, 1f)] public float threshold = 0.5f; // 0.5 �̻��̸� ON

    void OnEnable()
    {
        var am = AudioManager.Instance;
        if (!am) return;

        // ǥ�ø� ������Ʈ (�̺�Ʈ �߻� ����)
        if (bgm) bgm.SetValueWithoutNotify(am.IsBgmOn ? 1f : 0f);
        if (se) se.SetValueWithoutNotify(am.IsSeOn ? 1f : 0f);
        if (vib) vib.SetValueWithoutNotify(am.VibrateEnabled ? 1f : 0f);

        // ������ ����ε�
        if (bgm)
        {
            bgm.onValueChanged.RemoveListener(OnBgmChanged);
            bgm.onValueChanged.AddListener(OnBgmChanged);
        }
        if (se)
        {
            se.onValueChanged.RemoveListener(OnSeChanged);
            se.onValueChanged.AddListener(OnSeChanged);
        }
        if (vib)
        {
            vib.onValueChanged.RemoveListener(OnVibChanged);
            vib.onValueChanged.AddListener(OnVibChanged);
        }
    }

    void OnDisable()
    {
        if (bgm) bgm.onValueChanged.RemoveListener(OnBgmChanged);
        if (se) se.onValueChanged.RemoveListener(OnSeChanged);
        if (vib) vib.onValueChanged.RemoveListener(OnVibChanged);
    }

    void OnBgmChanged(float v) => AudioUI.SetBgm(v >= threshold);
    void OnSeChanged(float v) => AudioUI.SetSe(v >= threshold);
    void OnVibChanged(float v) => AudioUI.SetVibration(v >= threshold);
}

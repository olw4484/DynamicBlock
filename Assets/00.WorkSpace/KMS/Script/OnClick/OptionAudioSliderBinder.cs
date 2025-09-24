using UnityEngine;
using UnityEngine.UI;

public sealed class OptionAudioSliderBinder : MonoBehaviour
{
    [Header("Sliders (0~1)")]
    [SerializeField] Slider bgm;
    [SerializeField] Slider se;
    [SerializeField] Slider vib; // 0/1 슬라이더면 사용, 아니면 생략

    [Range(0f, 1f)] public float threshold = 0.5f; // 0.5 이상이면 ON

    void OnEnable()
    {
        var am = AudioManager.Instance;
        if (!am) return;

        // 표시만 업데이트 (이벤트 발사 금지)
        if (bgm) bgm.SetValueWithoutNotify(am.IsBgmOn ? 1f : 0f);
        if (se) se.SetValueWithoutNotify(am.IsSeOn ? 1f : 0f);
        if (vib) vib.SetValueWithoutNotify(am.VibrateEnabled ? 1f : 0f);

        // 리스너 재바인딩
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

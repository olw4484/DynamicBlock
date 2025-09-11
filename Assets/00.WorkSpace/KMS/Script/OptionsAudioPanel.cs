using UnityEngine;
using UnityEngine.UI;
#if TMP_PRESENT
using TMPro;
#endif

public sealed class AudioToggleButton : MonoBehaviour
{
    public enum Target { Bgm, Se, Vibrate }

    [Header("Target")]
    [SerializeField] private Target target = Target.Bgm;

    [Header("UI Binding")]
    [SerializeField] private Button button;
    [SerializeField] private Image icon;
    [SerializeField] private Sprite spriteOn;
    [SerializeField] private Sprite spriteOff;
    [SerializeField] private Color colorOn = Color.white;
    [SerializeField] private Color colorOff = new Color(1f, 1f, 1f, 0.5f);
#if TMP_PRESENT
    [SerializeField] private TextMeshProUGUI label;     // ����
#else
    [SerializeField] private Text label;
#endif
    [Header("Volumes")]
    [Range(0f, 1f)][SerializeField] private float defaultOnVolume = 1f;

    const string KeyLastBgm = "LastOn_BGMVolume";
    const string KeyLastSe = "LastOn_SEVolume";

    void Reset()
    {
        button = GetComponent<Button>();
    }

    void OnEnable()
    {
        if (!button) button = GetComponent<Button>();
        button.onClick.AddListener(OnClick);
        RefreshUI();
    }

    void OnDisable()
    {
        if (button) button.onClick.RemoveListener(OnClick);
    }

    void OnClick()
    {
        var am = AudioManager.Instance;
        if (!am) return;

        switch (target)
        {
            case Target.Bgm:
                {
                    // ���������� OFF(0), ���������� ���� ���� �Ǵ� �⺻��
                    if (am.BGMVolume > 0.0001f)
                    {
                        PlayerPrefs.SetFloat(KeyLastBgm, am.BGMVolume); // ������ ON ���� ����
                        am.SetBGMVolume(0f);
                    }
                    else
                    {
                        float v = PlayerPrefs.GetFloat(KeyLastBgm, Mathf.Max(am.BGMVolume, defaultOnVolume));
                        am.SetBGMVolume(v);
                    }
                    break;
                }
            case Target.Se:
                {
                    if (am.SEVolume > 0.0001f)
                    {
                        PlayerPrefs.SetFloat(KeyLastSe, am.SEVolume);
                        am.SetSEVolume(0f);
                    }
                    else
                    {
                        float v = PlayerPrefs.GetFloat(KeyLastSe, Mathf.Max(am.SEVolume, defaultOnVolume));
                        am.SetSEVolume(v);
                    }
                    break;
                }
            case Target.Vibrate:
                {
                    am.SetVibrateEnabled(!am.VibrateEnabled);
                    break;
                }
        }

        // Ŭ�� �ǵ��
        Sfx.Button();

        RefreshUI();
    }

    void RefreshUI()
    {
        var am = AudioManager.Instance;
        bool isOn = false;
        string what = target.ToString().ToUpper();

        if (am)
        {
            isOn = target switch
            {
                Target.Bgm => am.BGMVolume > 0.0001f,
                Target.Se => am.SEVolume > 0.0001f,
                Target.Vibrate => am.VibrateEnabled,
                _ => false
            };
        }

        // ��
        if (label)
        {
            label.text = $"{what}: {(isOn ? "ON" : "OFF")}";
            label.color = isOn ? colorOn : colorOff;
        }
        // ������
        if (icon)
        {
            if (spriteOn && spriteOff)
                icon.sprite = isOn ? spriteOn : spriteOff;
            icon.color = isOn ? colorOn : colorOff;
        }
    }
}

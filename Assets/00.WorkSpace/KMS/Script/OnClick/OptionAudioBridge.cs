using UnityEngine;
using UnityEngine.UI;
#if TMP_PRESENT
using TMPro;
#endif

public sealed class OptionAudioBridge : MonoBehaviour
{
    [Header("Buttons (optional wiring)")]
    [SerializeField] private Button btnSound;     // SE
    [SerializeField] private Button btnBgm;       // BGM
    [SerializeField] private Button btnVibration; // Vibrate

    [Header("UI (optional)")]
    [SerializeField] private Image iconSe;
    [SerializeField] private Image iconBgm;
    [SerializeField] private Image iconVib;
    [SerializeField] private Sprite spriteOn;
    [SerializeField] private Sprite spriteOff;
#if TMP_PRESENT
    [SerializeField] private TextMeshProUGUI labelSe;
    [SerializeField] private TextMeshProUGUI labelBgm;
    [SerializeField] private TextMeshProUGUI labelVib;
#endif
    [SerializeField] private Color colorOn = Color.white;
    [SerializeField] private Color colorOff = new Color(1f, 1f, 1f, 0.5f);

    [Header("Volumes")]
    [Range(0f, 1f)][SerializeField] private float defaultOnVolume = 1f;

    const string KeyLastBgm = "LastOn_BGMVolume";
    const string KeyLastSe = "LastOn_SEVolume";

    void Awake()
    {
        // ��ư�� �ν����Ϳ��� �� ���ȴٸ� �ڵ� ���ε� �õ�
        btnSe?.onClick.AddListener(ToggleSe);
        btnBgm?.onClick.AddListener(ToggleBgm);
        btnVibration?.onClick.AddListener(ToggleVibration);
    }

    void OnEnable() => RefreshAll();
    void OnDisable()
    {
        btnSe?.onClick.RemoveListener(ToggleSe);
        btnBgm?.onClick.RemoveListener(ToggleBgm);
        btnVibration?.onClick.RemoveListener(ToggleVibration);
    }

    // === public: ��ư OnClick�� ���� �����ص� �� ===
    public void ToggleSe()
    {
        var am = AudioManager.Instance; if (!am) return;
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
        Sfx.Button();
        RefreshSE();
    }

    public void ToggleBgm()
    {
        var am = AudioManager.Instance; if (!am) return;
        if (am.BGMVolume > 0.0001f)
        {
            PlayerPrefs.SetFloat(KeyLastBgm, am.BGMVolume);
            am.SetBGMVolume(0f);
        }
        else
        {
            float v = PlayerPrefs.GetFloat(KeyLastBgm, Mathf.Max(am.BGMVolume, defaultOnVolume));
            am.SetBGMVolume(v);
        }
        Sfx.Button();
        RefreshBGM();
    }

    public void ToggleVibration()
    {
        var am = AudioManager.Instance; if (!am) return;
        am.SetVibrateEnabled(!am.VibrateEnabled);
        Sfx.Button();
        RefreshVibration();
    }

    // === UI �ݿ� ===
    void RefreshAll() { RefreshSE(); RefreshBGM(); RefreshVibration(); }

    void RefreshSE()
    {
        var am = AudioManager.Instance; bool on = am && am.SEVolume > 0.0001f;
        if (iconSe) { if (spriteOn && spriteOff) iconSe.sprite = on ? spriteOn : spriteOff; iconSe.color = on ? colorOn : colorOff; }
#if TMP_PRESENT
        if (labelSe)  labelSe.text = on ? "Sound ON" : "Sound OFF";
#endif
    }
    void RefreshBGM()
    {
        var am = AudioManager.Instance; bool on = am && am.BGMVolume > 0.0001f;
        if (iconBgm) { if (spriteOn && spriteOff) iconBgm.sprite = on ? spriteOn : spriteOff; iconBgm.color = on ? colorOn : colorOff; }
#if TMP_PRESENT
        if (labelBgm) labelBgm.text = on ? "BGM ON" : "BGM OFF";
#endif
    }
    void RefreshVibration()
    {
        var am = AudioManager.Instance; bool on = am && am.VibrateEnabled;
        if (iconVib) { if (spriteOn && spriteOff) iconVib.sprite = on ? spriteOn : spriteOff; iconVib.color = on ? colorOn : colorOff; }
#if TMP_PRESENT
        if (labelVib) labelVib.text = on ? "Vibration ON" : "Vibration OFF";
#endif
    }

    // �ʵ�� ���� ������
    Button btnSe => btnSound;
}

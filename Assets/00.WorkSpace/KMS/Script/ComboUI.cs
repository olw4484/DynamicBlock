using TMPro;
using UnityEngine;
using System.Collections;

public sealed class ComboUI : MonoBehaviour
{
    [Header("Refs - GameCanvas (������)")]
    [SerializeField] GameObject rainbowIcon;          // GameCanvas�� �ִ� ������ ������Ʈ

    [Header("Refs - UICanvas (�޺� �׷�)")]
    [SerializeField] CanvasGroup comboGroup;          // ComboImage + ComboText ���� �׷�
    [SerializeField] TextMeshProUGUI comboText;       // ���� TMP

    [Header("Tuning")]
    [SerializeField] float holdAfterLastHit = 0.8f;   // ������ �޺� �� ���� �ð�
    [SerializeField] float fadeOutTime = 0.2f;        // ���̵�ƿ� �ð�

    Coroutine hideCo;
    int currentCombo;
    private EventQueue _bus;
    void Awake()
    {
        // �ʱ� �����
        SetComboVisible(false);
        if (rainbowIcon) rainbowIcon.SetActive(false);

        // �ʼ� ���� ����
        if (!comboGroup) Debug.LogError("[ComboHUD] comboGroup not assigned.");
        if (!comboText) Debug.LogError("[ComboHUD] comboText not assigned.");
        if (!rainbowIcon) Debug.LogWarning("[ComboHUD] rainbowIcon not assigned (������ �Ⱦ��Ÿ� ����).");
    }

    void OnEnable() => _bus.Subscribe<ComboChanged>(OnComboChanged);
    void OnDisable() => _bus.Unsubscribe<ComboChanged>(OnComboChanged);

    /// <summary> �޺��� ���� ������ ȣ���ϼ��� (0�̸� ����) </summary>
    void OnComboChanged(ComboChanged e)
    {
        Debug.Log($"[ComboHUD] ComboChanged ����: {e.value}");

        if (e.value <= 0)
        {
            rainbowIcon?.SetActive(false);
            comboGroup.gameObject.SetActive(false);
            return;
        }

        rainbowIcon?.SetActive(true);
        comboGroup.gameObject.SetActive(true);
        comboText.text = e.value.ToString();
    }

    void RestartHideTimer()
    {
        StopHideTimer();
        hideCo = StartCoroutine(HideLater());
    }

    void StopHideTimer()
    {
        if (hideCo != null) { StopCoroutine(hideCo); hideCo = null; }
    }

    IEnumerator HideLater()
    {
        // ����
        float t = 0f;
        while (t < holdAfterLastHit) { t += Time.unscaledDeltaTime; yield return null; }

        // ���̵�ƿ�
        float a0 = comboGroup.alpha;
        float dur = Mathf.Max(0.01f, fadeOutTime);
        t = 0f;
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            comboGroup.alpha = Mathf.Lerp(a0, 0f, t / dur);
            yield return null;
        }
        SetComboVisible(false);
    }

    void SetComboVisible(bool v)
    {
        if (!comboGroup) return;
        comboGroup.gameObject.SetActive(true); // Ȱ��ȭ ���� ����(���ķ� ����)
        comboGroup.alpha = v ? 1f : 0f;
        comboGroup.interactable = false;
        comboGroup.blocksRaycasts = false;
        if (!v) comboGroup.gameObject.SetActive(false); // ���� ����
    }

    void HideAll()
    {
        if (rainbowIcon) rainbowIcon.SetActive(false);
        SetComboVisible(false);
        currentCombo = 0;
    }
}

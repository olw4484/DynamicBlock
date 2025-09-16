using TMPro;
using UnityEngine;
using System.Collections;

public sealed class ComboUI : MonoBehaviour
{
    [Header("Refs - GameCanvas (무지개)")]
    [SerializeField] GameObject rainbowIcon;          // GameCanvas에 있는 무지개 오브젝트

    [Header("Refs - UICanvas (콤보 그룹)")]
    [SerializeField] CanvasGroup comboGroup;          // ComboImage + ComboText 묶은 그룹
    [SerializeField] TextMeshProUGUI comboText;       // 숫자 TMP

    [Header("Tuning")]
    [SerializeField] float holdAfterLastHit = 0.8f;   // 마지막 콤보 후 유지 시간
    [SerializeField] float fadeOutTime = 0.2f;        // 페이드아웃 시간

    Coroutine hideCo;
    int currentCombo;
    private EventQueue _bus;
    void Awake()
    {
        // 초기 비노출
        SetComboVisible(false);
        if (rainbowIcon) rainbowIcon.SetActive(false);

        // 필수 참조 가드
        if (!comboGroup) Debug.LogError("[ComboHUD] comboGroup not assigned.");
        if (!comboText) Debug.LogError("[ComboHUD] comboText not assigned.");
        if (!rainbowIcon) Debug.LogWarning("[ComboHUD] rainbowIcon not assigned (무지개 안쓸거면 무시).");
    }

    void OnEnable() => _bus.Subscribe<ComboChanged>(OnComboChanged);
    void OnDisable() => _bus.Unsubscribe<ComboChanged>(OnComboChanged);

    /// <summary> 콤보가 변할 때마다 호출하세요 (0이면 종료) </summary>
    void OnComboChanged(ComboChanged e)
    {
        Debug.Log($"[ComboHUD] ComboChanged 수신: {e.value}");

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
        // 유지
        float t = 0f;
        while (t < holdAfterLastHit) { t += Time.unscaledDeltaTime; yield return null; }

        // 페이드아웃
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
        comboGroup.gameObject.SetActive(true); // 활성화 상태 유지(알파로 제어)
        comboGroup.alpha = v ? 1f : 0f;
        comboGroup.interactable = false;
        comboGroup.blocksRaycasts = false;
        if (!v) comboGroup.gameObject.SetActive(false); // 완전 숨김
    }

    void HideAll()
    {
        if (rainbowIcon) rainbowIcon.SetActive(false);
        SetComboVisible(false);
        currentCombo = 0;
    }
}

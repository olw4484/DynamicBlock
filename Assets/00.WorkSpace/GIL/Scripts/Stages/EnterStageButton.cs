using System;
using _00.WorkSpace.GIL.Scripts.Managers;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[Serializable]
public enum ButtonState { Locked, Cleared, Playable }

[RequireComponent(typeof(Button), typeof(Image))]
public class EnterStageButton : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Image stageButtonImage;
    [SerializeField] private Button button;
    [SerializeField] private TextMeshProUGUI buttonText;

    [Header("Button State Images")]
    [SerializeField] private Sprite normalSprite;
    [SerializeField] private Sprite activeSprite;
    [SerializeField] private Sprite clearSprite;

    [HideInInspector] public int stageNumber;
    private ButtonState stageButtonState;

    // (선택) 필요시 UIManager 빠르게 접근
    private static UIManager UI => (Game.UI as UIManager) ?? FindFirstObjectByType<UIManager>();

    void Awake()
    {
        // 누락 레퍼런스 보강
        if (!button) button = GetComponent<Button>();
        if (!stageButtonImage) stageButtonImage = GetComponent<Image>();

        // 클릭 바인딩은 한 번만
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(EnterStage);

        // 패널 전환을 이 컴포넌트에서 하지 않도록 보장(레이스 방지)
        var ps = GetComponent<PanelSwitchOnClick>();
        if (ps) ps.enabled = false;
    }

    void OnDisable()
    {
        // 버튼 애니 후 스케일 꼬임 방지
        var rt = transform as RectTransform;
        if (rt) rt.localScale = Vector3.one;
    }

    public ButtonState StageButtonState => stageButtonState;

    public void SetStageNumber(int number) => stageNumber = number;
    public int GetStageNumber() => stageNumber;
    public void SetClearSprite(Sprite sprite) => clearSprite = sprite;

    /// <summary>스테이지 진입 요청만 수행(패널 전환은 Stage/Map 흐름이 처리)</summary>
    public void EnterStage()
    {
        int idx = GetStageNumber();
        Debug.Log($"[EnterStage] Request enter stage index={idx}");
        StageManager.Instance?.EnterStageByIndex(idx, "EnterStageButton");
    }

    /// <summary>버튼 상태에 맞춰 상호작용/스프라이트/텍스트 갱신</summary>
    public void SetButtonState(ButtonState state)
    {
        stageButtonState = state;

        // 상호작용
        button.interactable = (state == ButtonState.Playable);

        // 상태별 비주얼
        switch (state)
        {
            case ButtonState.Cleared:
                stageButtonImage.sprite = clearSprite;
                if (buttonText) buttonText.enabled = false; // 클리어면 텍스트 숨김
                break;

            case ButtonState.Playable:
                stageButtonImage.sprite = activeSprite;
                if (buttonText) buttonText.enabled = true;  // 코멘트와 맞게 플레이 가능은 텍스트 ON
                break;

            case ButtonState.Locked:
                stageButtonImage.sprite = normalSprite;
                if (buttonText) buttonText.enabled = true;  // 잠김은 텍스트 ON(자물쇠 아이콘이면 바꿔도 됨)
                break;
        }
    }
}

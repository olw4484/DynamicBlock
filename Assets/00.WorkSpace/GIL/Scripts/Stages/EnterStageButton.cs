using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[Serializable]
public enum ButtonState
{
    Cleared,
    Playable,
    Locked
}

[RequireComponent(typeof(Button), typeof(Image))]
public class EnterStageButton : MonoBehaviour
{
    
    [Header("References")] 
    [SerializeField] private Image stageButtonImage;        // 기본 이미지
    [SerializeField] private Button button;
    [Header("Button State Images")]
    [SerializeField] private Sprite normalSprite;           // 일반 상태 Sprite
    [SerializeField] private Sprite activeSprite;           // 플레이 가능 상태 Sprite
    [SerializeField] private Sprite clearSprite;            // 클리어 했을 때 Sprite
    
    [HideInInspector] public int stageNumber;               // 클릭 했을 때 들어갈 번호
    private ButtonState stageButtonState;  // 현재 버튼의 상태 ( 언젠가 쓰일 수도 있기에 )
    public ButtonState StageButtonState => stageButtonState;
    // Setter
    public void SetStageNumber(int number)
    {
        stageNumber = number;
    }

    public void SetClearSprite(Sprite sprite)
    {
        clearSprite = sprite;
    }
        

    /// <summary>
    /// 디버그용 스테이지 진입 함수, 로그로 출력만 함
    /// </summary>
    public void EnterStage()
    {
        // 현재는 디버그로만 진행함
        Debug.Log($"[EnterStage] Debug : Enter Stage {stageNumber}");
        // 현재 스테이지 진입, 클리어 했다고 판정, 다음 스테이지 활성화
    }
    
    /// <summary>
    /// 현재 버튼의 상태를 변경, 버튼의 상태에 따라 해당하는 이미지 적용
    /// Cleared : 클리어함 Playable : 플레이 가능 Locked : 잠김
    /// </summary>
    /// <param name="state">버튼 상태</param>
    public void SetButtonState(ButtonState state)
    {
        var currState = stageButtonState;
        // 1) 현재 버튼의 상태 저장
        stageButtonState = state;
        
        // 2) 현재 버튼의 상태에 따라 상호작용 여부 결정
        // Playable일 경우에만 가능, 아닐 경우 불가능
        if (state == ButtonState.Playable)
            button.interactable = true;
        else
            button.interactable = false;
        
        // 3) 상태에 따른 이미지 설정
        switch (state)
        {
            case ButtonState.Cleared:
                stageButtonImage.sprite = clearSprite;
                break;
            case ButtonState.Playable:
                stageButtonImage.sprite = activeSprite;
                break;
            case ButtonState.Locked:
                stageButtonImage.sprite = normalSprite;
                break;
        }
    }
}

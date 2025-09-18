using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class EnterStageButton : MonoBehaviour, IPointerDownHandler
{
    [Header("Stage Info")]
    [SerializeField] private int stageNumber;

    public void SetStageNumber(int number)
    {
        stageNumber = number;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        // TODO : 실제 스테이지 진입하는 함수 호출
        DebugEnterStage(stageNumber);
    }
    /// <summary>
    /// 디버그용 스테이지 진입 함수, 로그로 출력만 함
    /// </summary>
    /// <param name="number">진입 할 스테이지 번호</param>
    public void DebugEnterStage(int number)
    {
        Debug.Log($"[EnterStage] Debug : Enter Stage {number}");
    }
    
}

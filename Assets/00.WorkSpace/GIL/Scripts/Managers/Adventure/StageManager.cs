using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class StageManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private StageList generator; // 호출할 StageList를 가지고 있는 오브젝트

    [SerializeField] private int currentStage = 0;
    private void OnValidate()
    {
        if (generator == null)
        {
            Debug.LogWarning("[StageManager] StageListGenerator is null, Auto Adding");
            generator = FindAnyObjectByType<StageList>();
        }
    }

    private void Update()
    {
        // 디버그용 강제 현재 스테이지 클리어 처리
        if (Input.GetKeyDown(KeyCode.Space))
        {
            SetStageClearAndActivateNext();
        }
    }
    
    /// <summary>
    /// 현재 스테이지를 클리어 처리, 다음 스테이지를 활성화
    /// </summary>
    public void SetStageClearAndActivateNext()
    {
        // 현재 스테이지가 전체 스테이지를 벗어났는가?
        // Index Out of Range Exit Condition
        if (currentStage >= generator.stageButtons.Count) return;
        generator.stageButtons[currentStage].GetComponent<EnterStageButton>().SetButtonState(ButtonState.Cleared);
        // Index Out of Range Exit Condition
        currentStage++;
        if (currentStage >= generator.stageButtons.Count) return;
        generator.stageButtons[currentStage].GetComponent<EnterStageButton>().SetButtonState(ButtonState.Playable);
    }
}

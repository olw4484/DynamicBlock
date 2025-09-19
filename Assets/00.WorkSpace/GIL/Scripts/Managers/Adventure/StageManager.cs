using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
public class StageManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private StageList generator; // 호출할 StageList를 가지고 있는 오브젝트

    [Header("Test / QA Debug"), Tooltip("테스트 및 QA용 디버그 버튼")]
    [SerializeField] private Button showStageNumberButton;      // 스테이지 번호 숫자 Text 표시 버튼
    [SerializeField] private Button setAllStageActiveButton;    // 모든 스테이지 활성화 버튼
    [SerializeField] private TMP_InputField setCurrentStageInputField; // 현재 활성화 스테이지 설정용 InputField

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
    /// <summary>
    /// 현재 활성화 스테이지 설정
    /// </summary>
    /// <param name="stageNumber">활성화할 스테이지 수, 해당 수 이전은 Cleared, 이후는 Locked로 설정</param>
    public void SetCurrentActiveStage(int stageNumber)
    {
        // 1) 1 이상인지 확인
        if (stageNumber < 1)
        {
            Debug.LogWarning("[StageManager] Stage Number must be greater than 0");
            return;
        }
        // 2) 현재 스테이지를 입력한 스테이지로 변경
        currentStage = stageNumber - 1; // 0-based index
        // 3) 모든 스테이지를 Locked로 변경
        foreach (var button in generator.stageButtons)
        {
            var enterButton = button.GetComponent<EnterStageButton>();
            // 못 찾았을 경우 Exit Contition
            if (enterButton == null) return;
            enterButton.SetButtonState(ButtonState.Locked);
        }
        // 4) 현재 스테이지까지 Cleared로 변경, 다음 스테이지를 Playable로 변경
        for (int i = 0; i < currentStage; i++)
        {
            if (i >= generator.stageButtons.Count) break;
            var enterButton = generator.stageButtons[i].GetComponent<EnterStageButton>();
            if (enterButton == null) return;
            enterButton.SetButtonState(ButtonState.Cleared);
        }
        // 현재 스테이지가 전체 스테이지를 벗어났는가?
        if (currentStage < generator.stageButtons.Count)
        {
            var enterButton = generator.stageButtons[currentStage].GetComponent<EnterStageButton>();
            if (enterButton != null)
                enterButton.SetButtonState(ButtonState.Playable);
        }

        Debug.Log($"[StageManager] Debug : Set Current Active Stage to {stageNumber}");
    }

    #region // Test / QA Debug Button Events
    /// <summary>
    /// 디버그용 스테이지 번호 표시
    /// </summary>
    public void ShowStageNumber()
    {
        foreach (var button in generator.stageButtons)
        {
            var text = button.GetComponentInChildren<TextMeshProUGUI>();
            if (text != null)
            {
                text.enabled = true;
            }
        }

        Debug.Log("[StageManager] Debug : Show Stage Number");
    }
    /// <summary>
    /// 디버그용 모든 스테이지 활성화
    /// </summary>
    public void SetAllStageActive()
    {
        foreach (var button in generator.stageButtons)
        {
            var enterButton = button.GetComponent<EnterStageButton>();
            // 못 찾았을 경우 Exit Contition
            if (enterButton == null) return;
            // 이미 클리어한 스테이지는 건너뜀
            if (enterButton.StageButtonState == ButtonState.Cleared) continue;
            // 클리어하지 않은 스테이지는 모두 플레이 가능으로 변경
            enterButton.SetButtonState(ButtonState.Playable);
        }

        Debug.Log("[StageManager] Debug : Set All Stage Active");
    }

    /// <summary>
    /// 디버그용 현재 활성화 스테이지 설정
    /// </summary>
    public void SetCurrentActiveStage_Debug()
    {
        // 1) InputField에서 숫자 파싱
        if (!int.TryParse(setCurrentStageInputField.text, out int stageNumber))
        {
            Debug.LogWarning("[StageManager] Invalid Stage Number Input");
            return;
        }

        SetCurrentActiveStage(stageNumber);
    }
#endregion
}

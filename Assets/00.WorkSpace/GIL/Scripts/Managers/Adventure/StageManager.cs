using System.Collections;
using System.Collections.Generic;
using _00.WorkSpace.GIL.Scripts.Maps;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.UIElements.Experimental;
public class StageManager : MonoBehaviour
{
    public static StageManager Instance;

    [Header("References")]
    [SerializeField] private StageList generator; // 호출할 StageList를 가지고 있는 오브젝트
    [SerializeField] private Button enterCurrentStageButton; // 현재 스테이지 진입 버튼
    [SerializeField] private Image trophyImage;
    [SerializeField] private Sprite normalTrophyImage;
    [SerializeField] private Sprite clearTrophyImage; // 클리어 이미지

    [Header("Game Mode Objects, 게임 모드마다 보여줄 오브젝트들")]
    [Tooltip("0번 : 최고 점수 텍스트, 1번 : 현재 점수 텍스트")]
    [SerializeField] private GameObject[] classicModeObjects;
    [Tooltip("0번 : 메인 화면 이동 버튼, 1번 : 점수모드 현재 상태 슬라이더")]
    [SerializeField] public GameObject[] adventureScoreModeObjects;
    [Tooltip("0번 : 메인 화면 이동 버튼, 1번 : 과일 목표치 점수 텍스트")]
    [SerializeField] public GameObject[] adventureFruitModeObjects;
 
    [Header("Test / QA Debug"), Tooltip("테스트 및 QA용 디버그 버튼")]
    [SerializeField] private Button showStageNumberButton;      // 스테이지 번호 숫자 Text 표시 버튼
    [SerializeField] private Button setAllStageActiveButton;    // 모든 스테이지 활성화 버튼
    [SerializeField] private TMP_InputField setCurrentStageInputField; // 현재 활성화 스테이지 설정용 InputField

    [SerializeField] private int currentStage = 0; // 0-based index
    [SerializeField] public bool isAllStagesCleared = false; // 모든 스테이지 클리어 여부

    public int GetCurrentStage()
    {
        return currentStage;
    }

    private void OnValidate()
    {
        if (generator == null)
        {
            Debug.LogWarning("[StageManager] StageListGenerator is null, Auto Adding");
            generator = FindAnyObjectByType<StageList>();
        }
    }

    private void Awake()
    {
        Instance = this;
        SetCurrentActiveStage(1); // 기본값 1로 설정
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
        if (currentStage >= generator.stageButtons.Count)
        {
            SetAllStagesCleared(true);
            return;
        }
        generator.stageButtons[currentStage].GetComponent<EnterStageButton>().SetButtonState(ButtonState.Cleared);
        // Index Out of Range Exit Condition
        currentStage++;
        if (currentStage >= generator.stageButtons.Count)
        {
            SetAllStagesCleared(true);
            return;
        }
        generator.stageButtons[currentStage].GetComponent<EnterStageButton>().SetButtonState(ButtonState.Playable);
        SetCurrentStageButton(currentStage+1);
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

        // 5) 현재 스테이지 진입 버튼에 현재 스테이지 정보 설정
        SetCurrentStageButton(stageNumber);

        Debug.Log($"[StageManager] Debug : Set Current Active Stage to {stageNumber}");
    }

    /// <summary>
    /// 현재 스테이지 진입 버튼 설정, 이름과 기능을 변경
    /// </summary>
    /// <param name="stageNumber">진입할 스테이지 번호 및 텍스트 번호</param>
    private void SetCurrentStageButton(int stageNumber)
    {
        enterCurrentStageButton.GetComponentInChildren<TextMeshProUGUI>().text = $"Level {stageNumber}";
        // 이전 리스너 제거 ( 안전 처리 )
        enterCurrentStageButton.onClick.RemoveAllListeners();
        // 최신 스테이지에 대한 리스너 추가
        enterCurrentStageButton.onClick.AddListener(() =>
        {
            // 기존 버튼의 Invoke
            var enterButton = generator.stageButtons[currentStage].GetComponent<EnterStageButton>();
            if (enterButton != null)
                enterButton.EnterStage();
            // PanelSwitchOnClick Invoke
            generator.stageButtons[currentStage].GetComponent<PanelSwitchOnClick>().Invoke();
        });
    }
    /// <summary>
    /// 모든 스테이지 클리어 상태 설정
    /// </summary>
    /// <param name="flag">클리어 여부, true일 경우 텍스트 변경</param>
    public void SetAllStagesCleared(bool flag)
    {
        isAllStagesCleared = flag;
        if (isAllStagesCleared)
        {
            // 모든 스테이지 클리어 텍스트로 변경
            enterCurrentStageButton.GetComponentInChildren<TextMeshProUGUI>().text = "All Clear!";
            // 트로피 이미지 변경
            trophyImage.sprite = clearTrophyImage;
            // 스테이지 진입 버튼 비활성화
            enterCurrentStageButton.transition = Selectable.Transition.None;
            enterCurrentStageButton.interactable = false;
        }
    }
    /// <summary>
    /// 게임 모드에 따라 활성화 / 비활성화 할 오브젝트 설정
    /// </summary>
    /// <param name="gameMode">게임 모드</param>
    /// <param name="goalKind">어드벤쳐일 경우 골 타입 </param>
    public void SetObjectsByGameModeNGoalKind(GameMode gameMode, MapGoalKind goalKind = default)
    {
        // 클래식 모드일 경우
        if (gameMode == GameMode.Classic)
        {
            foreach (var obj in classicModeObjects)
                obj.SetActive(true);
            foreach (var obj in adventureFruitModeObjects)
                obj.SetActive(false);
            foreach (var obj in adventureScoreModeObjects)
                obj.SetActive(false);
        }
        // 어드벤쳐 모드일 경우
        else if (gameMode == GameMode.Adventure)
        {
            // 점수 모드일 경우
            if (goalKind == MapGoalKind.Score)
            {
                foreach (var obj in classicModeObjects)
                    obj.SetActive(false);
                foreach (var obj in adventureFruitModeObjects)
                    obj.SetActive(false);
                foreach (var obj in adventureScoreModeObjects)
                    obj.SetActive(true);
            }
            // 과일 모드일 경우
            else if (goalKind == MapGoalKind.Fruit)
            {
                foreach (var obj in classicModeObjects)
                    obj.SetActive(false);
                foreach (var obj in adventureFruitModeObjects)
                    obj.SetActive(true);
                foreach (var obj in adventureScoreModeObjects)
                    obj.SetActive(false);
            }
        }

    }

    #region // Test / QA Debug Button Events
    /// <summary>
    /// 디버그용 스테이지 번호 표시
    /// </summary>
    public void Debug_ShowStageNumber()
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
    public void Debug_SetAllStageActive()
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
    public void Debug_SetCurrentActiveStage()
    {
        // 1) InputField에서 숫자 파싱
        if (!int.TryParse(setCurrentStageInputField.text, out int stageNumber))
        {
            if (stageNumber >= generator.stageButtons.Count)
            {
                Debug.LogWarning("[StageManager] Input Stage Number exceeds total stage count");
                setCurrentStageInputField.text = "";
                return;
            }
            Debug.LogWarning("[StageManager] Invalid Stage Number Input");
            return;
        }
        // 2) 현재 활성화 스테이지 설정
        SetCurrentActiveStage(stageNumber);
    }
    #endregion
}

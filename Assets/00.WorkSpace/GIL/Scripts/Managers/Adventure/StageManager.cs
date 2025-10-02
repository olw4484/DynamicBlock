using _00.WorkSpace.GIL.Scripts.Managers;
using _00.WorkSpace.GIL.Scripts.Maps;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.Localization.Settings;
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
    [SerializeField] private int lastPlayedStageIndex = -1; // 0-based
    private bool _initializedFromSave;
    public int GetCurrentStage()
    {
        return currentStage;
    }

    public void SetCurrentStage(int value)
    {
        var prev = currentStage;
        currentStage = value;
        Debug.Log($"[StageManager] Current Stage 변경 {prev} -> {currentStage}");
    }

    private void OnValidate()
    {
        if (generator == null)
        {
            Debug.LogWarning("[StageManager] StageListGenerator is null, Auto Adding");
            generator = FindAnyObjectByType<StageList>();
        }
    }
    private void OnEnable()
    {
        if (!_initializedFromSave) StartCoroutine(InitStageFromSaveNextFrame());
    }

    private IEnumerator SetCurrentActiveStageAfterOneFrame()
    {
        yield return null;
        SetCurrentActiveStage(MapManager.Instance.saveManager.gameData.adventureBestIndex);
    }

    private void Awake()
    {
        Instance = this;
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
        var id = StageFlowTrace.NewSeq("ClearAndNext");
        StageFlowTrace.Log(id, $"Before: currentStage={currentStage}, lastPlayed={lastPlayedStageIndex}, total={generator.stageButtons.Count}");

        if (currentStage >= generator.stageButtons.Count)
        {
            SetAllStagesCleared(true);
            StageFlowTrace.Log(id, "All Cleared A");
            return;
        }

        // 현재 스테이지 Cleared
        generator.stageButtons[currentStage].GetComponent<EnterStageButton>()?.SetButtonState(ButtonState.Cleared);

        // 진행도 저장 (세이브가 1-based면 +1, 0-based면 그대로)
        var save = MapManager.Instance?.saveManager?.gameData;
        if (save != null)
        {
            int cleared1 = currentStage + 1;
            if (cleared1 > save.adventureBestIndex)
                save.adventureBestIndex = cleared1;

            MapManager.Instance.saveManager.SaveGame();
        }

        currentStage++;
        StageFlowTrace.Log(id, $"After++: currentStage={currentStage}");

        if (currentStage >= generator.stageButtons.Count)
        {
            SetAllStagesCleared(true);
            StageFlowTrace.Log(id, "All Cleared B");
            return;
        }

        // 다음 스테이지만 Playable
        generator.stageButtons[currentStage].GetComponent<EnterStageButton>()?.SetButtonState(ButtonState.Playable);

        // 라벨만 1-based로 표기
        SetCurrentStageButton(currentStage + 1);

        StageFlowTrace.Log(id, $"After: currentStage={currentStage}, lastPlayed(keep)={lastPlayedStageIndex}");
        StageFlowTrace.Log(id, $"States: {StageFlowTrace.DumpButtons(generator.stageButtons)}");
    }

    /// <summary>
    /// 현재 활성화 스테이지 설정
    /// </summary>
    /// <param name="stageNumber">활성화할 스테이지 수, 해당 수 이전은 Cleared, 이후는 Locked로 설정</param>
    public void SetCurrentActiveStage(int stageNumber)
    {
        var id = StageFlowTrace.NewSeq("SetCurrentActiveStage");
        StageFlowTrace.Log(id, $"Param stageNumber(1-based)={stageNumber}");

        if (stageNumber < 1)
        {
            StageFlowTrace.Log(id, "stageNumber < 1 → return");
            return;
        }

        currentStage = stageNumber - 1; // 0-based
        StageFlowTrace.Log(id, $"Computed currentStage(0-based)={currentStage}");

        foreach (var button in generator.stageButtons)
        {
            var enterButton = button.GetComponent<EnterStageButton>();
            if (!enterButton) { StageFlowTrace.Log(id, "missing EnterStageButton"); return; }
            enterButton.SetButtonState(ButtonState.Locked);
        }

        for (int i = 0; i < currentStage; i++)
        {
            if (i >= generator.stageButtons.Count) break;
            generator.stageButtons[i].GetComponent<EnterStageButton>()?.SetButtonState(ButtonState.Cleared);
        }

        if (currentStage < generator.stageButtons.Count)
        {
            generator.stageButtons[currentStage].GetComponent<EnterStageButton>()?.SetButtonState(ButtonState.Playable);
        }

        StageFlowTrace.Log(id, $"States: {StageFlowTrace.DumpButtons(generator.stageButtons.ToArray())}");

        SetCurrentStageButton(stageNumber);
    }

    /// <summary>
    /// 현재 스테이지 진입 버튼 설정, 이름과 기능을 변경
    /// </summary>
    /// <param name="stageNumber">진입할 스테이지 번호 및 텍스트 번호</param>
    private void SetCurrentStageButton(int stageNumber)
    {
        enterCurrentStageButton.GetComponentInChildren<TextMeshProUGUI>().text = $"Level {stageNumber}";
        enterCurrentStageButton.onClick.RemoveAllListeners();

        int capturedStage = currentStage;

        var id = StageFlowTrace.NewSeq("BindEnterCurrentStageButton");
        StageFlowTrace.Log(id, $"Bind: stageNumber(label)={stageNumber}, capturedStage(index)={capturedStage}, currentStage(now)={currentStage}, total={generator.stageButtons.Count}", this);

        enterCurrentStageButton.onClick.AddListener(() =>
        {
            StageFlowTrace.Log(id, $"CLICK: capturedStage={capturedStage}, nowCurrent={currentStage}");
            EnterStageByIndex(capturedStage, "EnterByCurrentStageButton");
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
    public void SetObjectsByGameModeNGoalKind(GameMode gameMode, MapGoalKind goalKind)
    {
        void ToggleAll(GameObject[] arr, bool on)
        {
            if (arr == null) return;
            for (int i = 0; i < arr.Length; i++) if (arr[i]) arr[i].SetActive(on);
        }

        // 베이스라인: 모두 OFF
        ToggleAll(classicModeObjects, false);
        ToggleAll(adventureScoreModeObjects, false);
        ToggleAll(adventureFruitModeObjects, false);

        if (gameMode == GameMode.Classic)
        {
            ToggleAll(classicModeObjects, true);
        }
        else if (gameMode == GameMode.Adventure)
        {
            if (goalKind == MapGoalKind.Score) ToggleAll(adventureScoreModeObjects, true);
            else if (goalKind == MapGoalKind.Fruit) ToggleAll(adventureFruitModeObjects, true);
        }

        Debug.Log($"[Stage.Apply] OUT AdvScore={(adventureScoreModeObjects?.Any(go => go && go.activeSelf) == true ? "ON" : "OFF")} " +
                  $"AdvFruit={(adventureFruitModeObjects?.Any(go => go && go.activeSelf) == true ? "ON" : "OFF")} " +
                  $"Classic={(classicModeObjects?.Any(go => go && go.activeSelf) == true ? "ON" : "OFF")}");
    }



    private IEnumerator InitStageFromSaveNextFrame()
    {
        yield return null;
        int saved0 = MapManager.Instance?.saveManager?.gameData?.adventureBestIndex ?? 0; // 0-based
        if (saved0 < 0) saved0 = 0;
        SetCurrentActiveStage(saved0 + 1);
        _initializedFromSave = true;
    }

    public void EnterStageByIndex(int idx, string source = "EnterStageByIndex")
    {
        var id = StageFlowTrace.NewSeq(source);
        StageFlowTrace.Log(id, $"Request idx={idx}, currentStage={currentStage}, lastPlayed={lastPlayedStageIndex}, total={generator.stageButtons.Count}", this);

        if (idx < 0 || idx >= generator.stageButtons.Count)
        {
            StageFlowTrace.Log(id, $"OUT-OF-RANGE idx={idx}");
            return;
        }

        lastPlayedStageIndex = idx;

        SetCurrentStage(idx);

        var btn = generator.stageButtons[idx];
        StageFlowTrace.Log(id, $"Go with button={btn?.name}");


        btn.GetComponent<PanelSwitchOnClick>()?.Invoke();

        // MapManager 쪽 엔트리 호출을 여기서 정식 API
    }

    public void RetryLastStage()
    {
        int idx = (lastPlayedStageIndex >= 0) ? lastPlayedStageIndex : currentStage;
        EnterStageByIndex(idx, "RetryLastStage");
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

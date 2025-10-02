using _00.WorkSpace.GIL.Scripts.Blocks;
using _00.WorkSpace.GIL.Scripts.Managers;
using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections;
using _00.WorkSpace.GIL.Scripts.Maps;

public enum InvokeSfxMode
{
    None,
    Button,
    ClassicStart,
    CustomId
}

public sealed class PanelSwitchOnClick : MonoBehaviour, IPointerClickHandler
{
    [Header("Target")]
    [SerializeField] string targetPanel = "Game";   // "Game" or "Main"
    
    [Header("Modal close")]
    [SerializeField] bool closeModalFirst = true;
    [SerializeField] string[] modalsToClose = { "GameOver", "Option" };
    [SerializeField] bool clearRunStateOnClick = false; // 메인 갈 때 진행상태 비우기

    [Header("SFX")]
    [SerializeField] InvokeSfxMode sfxMode = InvokeSfxMode.Button;
    [SerializeField] SfxId customId = SfxId.ButtonClick;

    [Header("Others")]
    [SerializeField] float cooldown = 0.12f;

    [Header("Game Enter Mode")]
    [SerializeField] GameMode enterMode = GameMode.Classic;
    [Tooltip("어드벤쳐일 경우 어떤 모드로 진입하냐?")]
    [SerializeField] MapGoalKind goalKind = MapGoalKind.Score;
    float _cool;
    bool _invoking; // 재진입 방지

    void Update()
    {
        if (_cool > 0f) _cool -= Time.unscaledDeltaTime;
    }

    void OnDisable()
    {
        _invoking = false;
        _cool = 0f;
    }
    /// <summary>
    /// 외부에서 타겟 패널을 바꿀 수 있게 함.
    /// </summary>    
    // 스테이지 버튼을 생성할 때 타겟을 바꿔야 하므로 public Setter 제공
    public void SetGoalKind(MapGoalKind kind)
    {
        goalKind = kind;
    }

    public void OnPointerClick(PointerEventData _) => Invoke();

    public void Invoke()
    {
        Debug.Log($"[Home] Invoke clicked. clear={clearRunStateOnClick} target={targetPanel} cool={_cool} bound={Game.IsBound}");
        if (_invoking || _cool > 0f || !Game.IsBound) return;

        _invoking = true;
        _cool = cooldown;
        try
        {
            PlayInvokeSfx();

            switch (targetPanel)
            {
                case "Game":
                case "Score":
                case "Fruit":
                    {
                        // 공통: 게임 화면으로 전환
                        Game.Bus.PublishImmediate(new GameResetRequest(targetPanel, ResetReason.ToGame));
                        StartCoroutine(EnterGameNextFrame());
                        break;
                    }

                case "Main":
                    {
                        if (!clearRunStateOnClick)
                            MapManager.Instance?.saveManager?.SaveRunSnapshot(saveBlocksToo: true, src: SaveManager.SnapshotSource.Manual);
                        else
                        {
                            MapManager.Instance?.saveManager?.ClearRunState(true);
                            GridManager.Instance?.ResetBoardToEmpty();
                            ScoreManager.Instance?.ResetRuntime();
                            UnityEngine.Object.FindFirstObjectByType<BlockStorage>()?.ClearHand();
                        }

                        GridManager.Instance?.HealBoardFromStates();
                        Game.Bus.PublishImmediate(new GameResetRequest(targetPanel, ResetReason.ToMain));
                        break;
                    }

                case "Stage":
                    {
                        Debug.Log("스테이지 선택창 이동");
                        Game.Bus.PublishImmediate(new PanelToggle(targetPanel, true));
                        break;
                    }

                default:
                    {
                        Game.Bus.PublishImmediate(new GameResetRequest(targetPanel, ResetReason.None));
                        break;
                    }
            }

            // 모달 정리(공통)
            if (closeModalFirst && modalsToClose != null)
            {
                var bus2 = Game.Bus;
                foreach (var k in modalsToClose)
                {
                    if (string.IsNullOrEmpty(k)) continue;
                    var off = new PanelToggle(k, false);
                    bus2.PublishSticky(off, alsoEnqueue: false);
                    bus2.PublishImmediate(off);
                    if (k == "GameOver") bus2.ClearSticky<GameOver>();
                }
            }
        }
        finally { _invoking = false; }
    }


    private IEnumerator EnterGameNextFrame()
    {
        yield return null;

        var map = MapManager.Instance;
        var stage = StageManager.Instance;
        if (!map) { Debug.LogError("[Home] MapManager missing on EnterGameNextFrame"); yield break; }

        switch (enterMode)
        {
            case GameMode.Tutorial:
                {
                    // 클래식/튜토리얼 진입: 어드벤처 잔재 정리
                    map.ClearAdventureListeners();
                    map.DisableAdventureObjects();

                    map.SetGameMode(GameMode.Tutorial);
                    map.SetGoalKind(MapGoalKind.None);
                    stage?.SetObjectsByGameModeNGoalKind(GameMode.Tutorial, MapGoalKind.None);
                    map.RequestTutorialApply();
                    break;
                }

            case GameMode.Classic:
                {
                    // 클래식 진입: 어드벤처 잔재 정리 (가장 중요)
                    map.ClearAdventureListeners();
                    map.DisableAdventureObjects();

                    map.SetGameMode(GameMode.Classic);
                    map.SetGoalKind(MapGoalKind.None);
                    stage?.SetObjectsByGameModeNGoalKind(GameMode.Classic, MapGoalKind.None);
                    map.RequestClassicEnter(MapManager.ClassicEnterPolicy.ForceLoadSave);
                    break;
                }

            case GameMode.Adventure:
                {
                    Debug.Log($"[BTN] Enter Adventure goal={goalKind}");

                    if (goalKind == MapGoalKind.Tutorial || goalKind == MapGoalKind.None)
                    {
                        var fallback = MapManager.Instance?.CurrentMapData?.goalKind ?? MapGoalKind.Score;
                        Debug.LogWarning($"[BTN] goalKind was {goalKind}, fallback => {fallback}");
                        goalKind = fallback;
                    }

                    map.SetGameMode(GameMode.Adventure);
                    map.SetGoalKind(goalKind);

                    if (goalKind == MapGoalKind.Score) map.SetAdvScoreObjects();
                    else if (goalKind == MapGoalKind.Fruit) map.SetAdvFruitObjects();

                    stage?.SetObjectsByGameModeNGoalKind(GameMode.Adventure, goalKind);
                    Debug.Log("[BTN] Stage.Apply done");
                    break;
                }
        }
    }

    void PlayInvokeSfx()
    {
        switch (sfxMode)
        {
            case InvokeSfxMode.None: return;
            case InvokeSfxMode.Button: Sfx.Button(); return;
            case InvokeSfxMode.ClassicStart: Sfx.StageEnter(); return;
            case InvokeSfxMode.CustomId: Sfx.PlayId((int)customId); return;
        }
    }
}

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
        if (_invoking) return;
        if (_cool > 0f || !Game.IsBound) return;

        _invoking = true;
        _cool = cooldown;

        try
        {
            PlayInvokeSfx();
            var bus = Game.Bus;

            if (targetPanel == "Game")
            {
                // 1) UI 리셋/전환을 먼저 요청
                var reason = ResetReason.ToGame;
                bus.PublishImmediate(new GameResetRequest(targetPanel, reason));
                Debug.Log($"입장 모드 : {enterMode}, 어드벤쳐 종류 {goalKind}");

                // 1.5) 입장하는 게임 오브젝트의 종류에 따라 다른 오브젝트 활성화
                StageManager.Instance.SetObjectsByGameModeNGoalKind(enterMode, goalKind);

                // 2) 다음 프레임에 입장 로직 적용 (리셋 완료 후)
                StartCoroutine(EnterGameNextFrame());

            }
            else if (targetPanel == "Main")
            {
                // 이어하기를 원하면 Inspector에서 clearRunStateOnClick = false 유지!
                if (!clearRunStateOnClick)
                {
                    MapManager.Instance?.saveManager?.SaveRunSnapshot(saveBlocksToo: true, src: SaveManager.SnapshotSource.Manual);
                }
                else
                {
                    MapManager.Instance?.saveManager?.ClearRunState(true);
                    GridManager.Instance?.ResetBoardToEmpty();
                    ScoreManager.Instance?.ResetRuntime();
                    var storage = UnityEngine.Object.FindFirstObjectByType<BlockStorage>();
                    storage?.ClearHand();
                }

                GridManager.Instance?.HealBoardFromStates();

                var reason = ResetReason.ToMain;
                bus.PublishImmediate(new GameResetRequest(targetPanel, reason));
            }
            // TODO : [PanelSwitchOnClick] swtich문으로 바꿔야 할 것으로 생각됨
            else if (targetPanel == "Score")
            {
                // 1) UI 리셋/전환을 먼저 요청
                var reason = ResetReason.ToGame;
                bus.PublishImmediate(new GameResetRequest(targetPanel, reason));

                // 2) 다음 프레임에 입장 로직 적용 (리셋 완료 후)
                StartCoroutine(EnterGameNextFrame());
            }
            else if (targetPanel == "Fruit")
            {
                // 1) UI 리셋/전환을 먼저 요청
                var reason = ResetReason.ToGame;
                bus.PublishImmediate(new GameResetRequest(targetPanel, reason));

                // 2) 다음 프레임에 입장 로직 적용 (리셋 완료 후)
                StartCoroutine(EnterGameNextFrame());
            }
            else if (targetPanel == "Stage")
            {
                Debug.Log("스테이지 선택창 이동");
                bus.PublishImmediate(new PanelToggle(targetPanel, true));
            }
            else
            {
                var reason = ResetReason.None;
                bus.PublishImmediate(new GameResetRequest(targetPanel, reason));
            }

            // 2) 모달 정리 (기존 유지)
            if (closeModalFirst && modalsToClose != null)
            {
                var bus2 = Game.Bus;
                for (int i = 0; i < modalsToClose.Length; i++)
                {
                    var k = modalsToClose[i];
                    if (string.IsNullOrEmpty(k)) continue;
                    var off = new PanelToggle(k, false);
                    bus2.PublishSticky(off, alsoEnqueue: false);
                    bus2.PublishImmediate(off);
                    if (k == "GameOver") bus2.ClearSticky<GameOver>();
                }
            }
        }
        finally
        {
            _invoking = false;
        }
    }

    private IEnumerator EnterGameNextFrame()
    {
        yield return null;

        var map = MapManager.Instance;
        if (!map) { Debug.LogError("[Home] MapManager missing on EnterGameNextFrame"); yield break; }

        var bus = Game.Bus;
        // 튜토리얼 입장
        if (enterMode == GameMode.Tutorial)
        {
            map.SetGameMode(GameMode.Tutorial);
            map.RequestTutorialApply();
            Debug.Log("[Home] Tutorial apply (after reset)");
        }
        // 클래식 모드 입장
        else if (enterMode == GameMode.Classic)
        {
            map.SetGameMode(GameMode.Classic);
            map.RequestClassicEnter(MapManager.ClassicEnterPolicy.ForceLoadSave);
            Debug.Log("[BTN] EnterGameNextFrame: mode=Classic policy=ForceLoadSave");
        }
        // 어드벤쳐 모드 입장
        else if (enterMode == GameMode.Adventure)
        {
            // 점수제일 경우 MapManager를 통해 데이터를 바꾸기
            if (goalKind == MapGoalKind.Score)
            {
                map.SetAdvScoreObjects();
            }
            // 과일제일 경우 어딘가 저장된 MapGoal정보를 통해 데이터를 바꾸기
            else if (goalKind == MapGoalKind.Fruit)
            {
                map.SetAdvFruitObjects();
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

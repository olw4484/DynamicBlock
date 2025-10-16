using _00.WorkSpace.GIL.Scripts.Blocks;
using _00.WorkSpace.GIL.Scripts.Managers;
using _00.WorkSpace.GIL.Scripts.Maps;
using _00.WorkSpace.GIL.Scripts.Messages;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;

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

    /// <summary>스테이지 버튼 생성 시 목표 종류를 외부에서 주입</summary>
    public void SetGoalKind(MapGoalKind kind) => goalKind = kind;

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

            // 0) 공통: 모달/스티키 정리
            if (closeModalFirst && modalsToClose != null)
            {
                var bus = Game.Bus;
                foreach (var k in modalsToClose)
                {
                    if (string.IsNullOrEmpty(k)) continue;
                    var off = new PanelToggle(k, false);
                    bus.PublishSticky(off, alsoEnqueue: false);
                    bus.PublishImmediate(off);
                    if (k == "GameOver") bus.ClearSticky<GameOver>();
                }
                bus.ClearSticky<AdventureStageCleared>();
                bus.ClearSticky<AdventureStageFailed>();
                bus.ClearSticky<GameOverConfirmed>();
                bus.ClearSticky<GameOver>();
            }

            // 1) 게임으로 진입
            if (targetPanel == "Game" || targetPanel == "Score" || targetPanel == "Fruit")
            {
                var requested = enterMode;
                var chosenMode = ResolveEffectiveMode(requested);

                bool fired = false;
                System.Action<GameResetDone> handler = null;
                handler = _ =>
                {
                    if (fired) return;
                    fired = true;
                    Game.Bus.Unsubscribe(handler);
                    EnterGameImmediately(chosenMode);
                };

                Game.Bus.Subscribe(handler, replaySticky: false);
                Game.Bus.PublishImmediate(new GameResetRequest(targetPanel, ResetReason.ToGame));
                return;
            }

            // 2) 메인으로 복귀
            if (targetPanel == "Main")
            {
                if (MapManager.Instance?.CurrentMode == GameMode.Adventure)
                {
                    var last = ScoreManager.Instance ? ScoreManager.Instance.Score : 0;
                    MapManager.Instance.saveManager?.UpdateAdventureScore(last);
                }

                if (!clearRunStateOnClick)
                {
                    bool saveBlocks = MapManager.Instance?.CurrentMode != GameMode.Classic;
                    MapManager.Instance?.saveManager?.SaveRunSnapshot(
                        saveBlocksToo: saveBlocks,
                        src: SaveManager.SnapshotSource.Manual);
                }
                else
                {
                    MapManager.Instance?.saveManager?.ClearRunState(true);
                    GridManager.Instance?.ResetBoardToEmpty();
                    ScoreManager.Instance?.ResetRuntime();
                    UnityEngine.Object.FindFirstObjectByType<BlockStorage>()?.ClearHand();
                }

                GridManager.Instance?.HealBoardFromStates();
                Game.Bus.PublishImmediate(new GameResetRequest(targetPanel, ResetReason.ToMain));
                return;
            }

            // 3) 스테이지 선택 열기
            if (targetPanel == "Stage")
            {
                Debug.Log("스테이지 선택창 이동");
                Game.Bus.PublishImmediate(new PanelToggle(targetPanel, true));
                return;
            }

            // 4) 그 외: 일반 패널 전환
            Game.Bus.PublishImmediate(new GameResetRequest(targetPanel, ResetReason.None));
        }
        finally
        {
            _invoking = false;
        }
    }


    /// <summary>리셋 완료 후 즉시 모드별 초기화</summary>
    private void EnterGameImmediately(GameMode mode)
    {
        mode = ResolveEffectiveMode(mode);

        var map = MapManager.Instance;
        var stage = StageManager.Instance;
        if (!map)
        {
            Debug.LogError("[Home] MapManager missing");
            return;
        }

        switch (mode)
        {
            case GameMode.Classic:
                map.ClearAdventureListeners();
                map.DisableAdventureObjects();
                map.SetGameMode(GameMode.Classic);
                map.SetGoalKind(MapGoalKind.None);
                stage?.SetObjectsByGameModeNGoalKind(GameMode.Classic, MapGoalKind.None);

                var policy = MapManager.ClassicEnterPolicy.ResumeIfAliveElseLoadSaveElseNew;
                map.RequestClassicEnter(policy);
                break;

            case GameMode.Adventure:
                map.SetGameMode(GameMode.Adventure);

                var idx0 = StageManager.Instance ? StageManager.Instance.GetCurrentStage() : 0;
                map.EnterAdventureByIndex0(idx0);

                var md = map.CurrentMapData;
                var kind = md ? md.goalKind : MapGoalKind.Score;
                map.SetGoalKind(kind);

                if (kind == MapGoalKind.Score) map.SetAdvScoreObjects();
                else map.SetAdvFruitObjects();

                stage?.SetObjectsByGameModeNGoalKind(GameMode.Adventure, kind);
                break;

            case GameMode.Tutorial:
                map.ClearAdventureListeners();
                map.DisableAdventureObjects();
                map.SetGameMode(GameMode.Tutorial);
                map.SetGoalKind(MapGoalKind.None);
                stage?.SetObjectsByGameModeNGoalKind(GameMode.Tutorial, MapGoalKind.None);
                map.RequestTutorialApply();
                break;
        }
    }

    private static GameMode ResolveEffectiveMode(GameMode requested)
    {
        return (requested == GameMode.Tutorial && TutorialFlags.WasFirstPlacement())
            ? GameMode.Classic
            : requested;
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

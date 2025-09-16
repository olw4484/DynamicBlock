using _00.WorkSpace.GIL.Scripts.Blocks;
using _00.WorkSpace.GIL.Scripts.Managers;
using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections;

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
                // 🔸 1) UI 리셋/전환을 먼저 요청
                var reason = ResetReason.ToGame;
                bus.PublishImmediate(new GameResetRequest(targetPanel, reason));

                // 🔸 2) 다음 프레임에 입장 로직 적용 (리셋 완료 후)
                StartCoroutine(EnterGameNextFrame());
            }
            else if (targetPanel == "Main")
            {
                // 이어하기를 원하면 Inspector에서 clearRunStateOnClick = false 유지!
                if (!clearRunStateOnClick)
                {
                    MapManager.Instance?.saveManager?.SaveRunSnapshot(saveBlocksToo: true);
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
        // UI 토글/리셋 구독자들이 모두 처리할 시간을 한 프레임 줌
        yield return null;

        var map = MapManager.Instance;
        if (!map)
        {
            Debug.LogError("[Home] MapManager missing on EnterGameNextFrame");
            yield break;
        }

        // 모드 세팅
        if (enterMode == GameMode.Tutorial)
        {
            map.SetGameMode(GameMode.Tutorial);
            map.RequestTutorialApply();
            Debug.Log("[Home] Tutorial apply (after reset)");
        }
        else
        {
            map.SetGameMode(GameMode.Classic);
            map.RequestClassicEnter(MapManager.ClassicEnterPolicy.ForceLoadSave);
            Debug.Log("[Home] Classic enter ForceLoadSave (after reset)");
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

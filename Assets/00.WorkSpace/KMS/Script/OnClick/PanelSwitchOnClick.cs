using _00.WorkSpace.GIL.Scripts.Blocks;
using _00.WorkSpace.GIL.Scripts.Managers;
using _00.WorkSpace.GIL.Scripts.Utils;
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
        // 0) 기본 가드
        Debug.Log($"[Home] Invoke clicked. clear={clearRunStateOnClick} target={targetPanel} cool={_cool} bound={Game.IsBound}");
        if (_invoking) return;
        if (_cool > 0f || !Game.IsBound) return;

        _invoking = true;
        _cool = cooldown;

        try
        {
            // 1) SFX
            PlayInvokeSfx();

            var bus = Game.Bus;
            var map = MapManager.Instance;

            // 2) (핵심) 게임 진입/적용 요청을 먼저 MapManager로 위임
            if (targetPanel == "Game")
            {
                if (!map)
                {
                    Debug.LogError("[Home] MapManager missing. Abort.");
                    return;
                }

                if (map.GameMode == GameMode.Tutorial)
                {
                    // 프레임 지연 코루틴 대신 '요청'으로 안전 진입
                    map.RequestTutorialApply();
                    Debug.Log("[Home] Requested Tutorial Apply");
                }
                else
                {
                    map.RequestClassicEnter(); // GridReady Sticky + 타임아웃 가드 내장
                    Debug.Log("[Home] Requested Classic Enter");
                }
            }

            // 3) 런 상태 정리 — 보통 Main으로 갈 때만 권장
            if (targetPanel == "Main")
            {
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
            }

            // 4) 모달 먼저 정리(필요 시)
            if (closeModalFirst && modalsToClose != null)
            {
                for (int i = 0; i < modalsToClose.Length; i++)
                {
                    var k = modalsToClose[i];
                    if (string.IsNullOrEmpty(k)) continue;
                    var off = new PanelToggle(k, false);
                    bus.PublishSticky(off, alsoEnqueue: false);
                    bus.PublishImmediate(off);
                    if (k == "GameOver") bus.ClearSticky<GameOver>();
                }
            }

            // 5) UI 전환 + 게임 리셋 요청 (마지막에 수행)
            bus.PublishImmediate(new GameResetRequest(targetPanel));
            Debug.Log($"[Home] Published GameResetRequest({targetPanel})");

            // 6) Main으로 이동하는 케이스라면 저장/정리(필요 시)
            if (targetPanel == "Main")
            {
                // 리셋 버튼이 아닌 경우: 현재 런 상태 저장
                if (!clearRunStateOnClick)
                {
                    MapManager.Instance?.saveManager?.SaveRunSnapshot(saveBlocksToo: true);
                }
                else
                {
                    // 완전 리셋 홈 버튼인 경우에만 런 상태 파기
                    MapManager.Instance?.saveManager?.ClearRunState(true);
                    GridManager.Instance?.ResetBoardToEmpty();
                    ScoreManager.Instance?.ResetRuntime();
                    var storage = UnityEngine.Object.FindFirstObjectByType<_00.WorkSpace.GIL.Scripts.Blocks.BlockStorage>();
                    storage?.ClearHand();
                    Debug.Log("[Home] Cleared run state (reset home).");
                }

                // 보정
                GridManager.Instance?.HealBoardFromStates();
            }
        }
        finally
        {
            _invoking = false;
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

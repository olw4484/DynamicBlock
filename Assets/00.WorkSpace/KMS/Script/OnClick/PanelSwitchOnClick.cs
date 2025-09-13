using System.Collections;
using System.Collections.Generic;
using _00.WorkSpace.GIL.Scripts.Managers;
using _00.WorkSpace.GIL.Scripts.Utils;
using UnityEngine;
using UnityEngine.EventSystems;

public enum InvokeSfxMode
{
    None,
    Button,          // 일반 버튼 클릭음
    ClassicStart,    // 클래식 시작 효과음
    CustomId         // 필요시 SfxId로 직접 지정
}

public sealed class PanelSwitchOnClick : MonoBehaviour, IPointerClickHandler
{
    [Header("Target")]
    [SerializeField] string targetPanel = "Game";   // "Game" or "Main"

    [Header("Modal close")]
    [SerializeField] bool closeModalFirst = true;
    [SerializeField] string[] modalsToClose = { "GameOver", "Option" };

    [Header("SFX")]
    [SerializeField] InvokeSfxMode sfxMode = InvokeSfxMode.Button; // ← 인스펙터에서 선택
    [SerializeField] SfxId customId = SfxId.ButtonClick;           // sfxMode=CustomId일 때 사용

    [Header("Others")]
    [SerializeField] bool generateClassicMap = false; // ← 인스펙터에서 On/Off
    [SerializeField] float cooldown = 0.12f;

    float _cool;

    void Update() { if (_cool > 0f) _cool -= Time.unscaledDeltaTime; }

    public void OnPointerClick(PointerEventData _) => Invoke();

    public void Invoke()
    {
        if (_cool > 0f || !Game.IsBound) return;
        _cool = cooldown;

        // 1) SFX (상황별)
        PlayInvokeSfx();

        // 2) 모달 먼저 정리
        var bus = Game.Bus;
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

        // 3) 게임 리셋 + UI전환 요청
        bus.PublishImmediate(new GameResetRequest(targetPanel));
        
        // GIL Add
        var map = MapManager.Instance;

        if (targetPanel == "Game")
        {
            if (!map) return;

            // 튜토리얼은 기존 로직 유지, 그 외는 클래식 진입
            if (map.GameMode == GameMode.Tutorial)
            {
                StartCoroutine(map.EnterTutorial());
                //map.RequestTutorialApply( /* index 필요시 */ );
            }
            else
            {
                StartCoroutine(map.EnterClassicAfterOneFrame());
                //map.EnterClassic();
            }
        }
        else if (targetPanel == "Main")
        {
            // 1) 먼저 상태 동기화(화면 -> 상태)

            // 2) 그 다음 저장 
            GameSnapShot.SaveGridSnapshot();

            // 3) 저장 이후에 유령 인덱스 정리
            GridManager.Instance?.HealBoardFromStates();
        }
    }
    void PlayInvokeSfx()
    {
        switch (sfxMode)
        {
            case InvokeSfxMode.None:
                return;
            case InvokeSfxMode.Button:
                Sfx.Button();
                return;
            case InvokeSfxMode.ClassicStart:
                Sfx.StageEnter();
                return;
            case InvokeSfxMode.CustomId:
                Sfx.PlayId((int)customId);
                return;
        }
    }
}
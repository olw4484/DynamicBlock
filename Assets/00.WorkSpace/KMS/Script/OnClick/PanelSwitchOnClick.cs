using System.Collections;
using System.Collections.Generic;
using _00.WorkSpace.GIL.Scripts.Managers;
using _00.WorkSpace.GIL.Scripts.Utils;
using UnityEngine;

public sealed class PanelSwitchOnClick : MonoBehaviour
{
    [SerializeField] string targetPanel = "Game"; // "Game" or "Main"
    [SerializeField] bool closeModalFirst = true; // GameOver ���� ��� ���� ����
    [SerializeField] string[] modalsToClose = { "GameOver", "Option" };
    [SerializeField] float cooldown = 0.12f;

    float _cool;

    void Update() { if (_cool > 0f) _cool -= Time.unscaledDeltaTime; }

    public void Invoke()
    {
        if (_cool > 0f || !Game.IsBound) return;
        _cool = cooldown;

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

        // ���� ���� + UI ��ȯ ��û(������)
        bus.PublishImmediate(new GameResetRequest(targetPanel));
        
        // GIL Add
        var map = MapManager.Instance;

        if (targetPanel == "Game")
        {
            if (!map) return;

            // 튜토리얼은 기존 로직 유지, 그 외는 클래식 진입
            if (map.GameMode == GameMode.Tutorial)
            {
                map.SetMapDataToGrid();
            }
            else
            {
                map.EnterClassic();
            }
        }
        else if (targetPanel == "Main")
        {
            // 1) 먼저 상태 동기화(화면 -> 상태)
            GridManager.Instance?.SyncStatesFromSquares();

            // 2) 그 다음 저장 
            GameSnapShot.SaveGridSnapshot();

            // 3) 저장 이후에 유령 인덱스 정리
            GridManager.Instance?.HealBoardFromStates();
        }
    }
}

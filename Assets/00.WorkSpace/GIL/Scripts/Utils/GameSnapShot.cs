using System.Collections.Generic;
using _00.WorkSpace.GIL.Scripts.Managers;
using UnityEngine;

namespace _00.WorkSpace.GIL.Scripts.Utils
{
    public static class GameSnapShot
    {
        /// <summary>
        /// 그리드 스냅샷 저장.
        /// - 저장 직전 화면→상태 동기화
        /// - ExportLayoutCodes는 비점유=0 강제
        /// - 기본: 클래식 모드일 때만 isClassicModePlaying 플래그를 세움
        /// - 점수/콤보도 함께 기록
        /// </summary>
        public static void SaveGridSnapshot(bool alsoScoreCombo = true)
        {
            var gm = GridManager.Instance;
            var map = MapManager.Instance;
            var sm = map?.saveManager;

            if (gm == null || sm?.gameData == null)
            {
                Debug.LogWarning("[GameSnapShot] GridManager/SaveManager missing");
                return;
            }

            // 1) 저장 직전 화면→상태 동기화
            gm.SyncStatesFromSquares();

            // 2) 레이아웃 산출
            var layout = gm.ExportLayoutCodes();

            // 3) GameData 반영
            if (map != null && map.GameMode == GameMode.Classic)
                sm.gameData.isClassicModePlaying = true; // 튜토리얼인 경우 플래그 세우지 않음

            sm.gameData.currentMapLayout = layout;

            if (alsoScoreCombo)
            {
                var sc = ScoreManager.Instance;
                if (sc != null)
                {
                    sm.gameData.currentScore = sc.Score;
                    sm.gameData.currentCombo = sc.Combo;
                }
            }

            // 4) 저장
            sm.SaveGame();
            Debug.Log($"[GameSnapShot] Grid snapshot saved. cells={layout?.Count ?? 0}");
        }
    }
}
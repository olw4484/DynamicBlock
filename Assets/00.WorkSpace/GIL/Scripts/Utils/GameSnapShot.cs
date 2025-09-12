using System.Collections.Generic;
using _00.WorkSpace.GIL.Scripts.Managers;
using UnityEngine;

namespace _00.WorkSpace.GIL.Scripts.Utils
{
    public static class GameSnapShot
    {
        public static void SaveGridSnapshot()
        {
            var gm = GridManager.Instance;
            var sm = MapManager.Instance?.saveManager;
            if (gm == null || sm == null)
            {
                Debug.LogWarning("[GameSnapShot] GridManager/SaveManager 없음");
                return;
            }

            // 1) 현재 그리드 상태 갱신
            List<int> layout = gm.ExportLayoutCodes();

            // 2) GameData에 기록
            sm.gameData.isClassicModePlaying = true;
            sm.gameData.currentMapLayout     = layout;

            // (임시) 점수/콤보 기록
            sm.gameData.currentScore = ScoreManager.Instance?.Score ?? sm.gameData.currentScore;
            sm.gameData.currentCombo = ScoreManager.Instance?.Combo ?? sm.gameData.currentCombo;

            // 3) 저장
            sm.SaveGame();
            Debug.Log("[GameSnapShot] Grid snapshot saved.");
        }
    }
}
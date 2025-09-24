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
            if (gm == null || sm == null) { return; }

            if (sm?.gameData?.classicDownedPending == true)
            {
                Debug.Log("[Snap] Skip SaveGridSnapshot: DownedPending");
                return;
            }


            // 1회 스킵: 홈/리트라이 등에서 의도적으로 비웠을 때
            if (sm.skipNextGridSnapshot)
            {
                sm.skipNextGridSnapshot = false; // 한번만
                Debug.Log("[GameSnapShot] Skipped snapshot due to skip flag.");
                return;
            }

            // 현재 보드 상태 스냅샷
            var layout = gm.ExportLayoutCodes();

            // 점수 + 빈보드 가드
            bool boardEmpty = true;
            for (int i = 0; i < layout.Count; i++)
                if (layout[i] != 0) { boardEmpty = false; break; }

            // 점수는 세이브 캐시/라이브 중 큰 값 사용
            int cached = sm.gameData?.currentScore ?? 0;               // SaveManager.gameData.currentScore
            int live   = ScoreManager.Instance?.Score ?? 0;            // 라이브 점수(이미 0으로 초기화됐을 수 있음)
            int scoreNow = (cached > live) ? cached : live;

            // 플레이를 했던 상태(=점수>0)인데 보드가 빈 경우 → 홈/리셋으로 비운 것으로 간주하고 저장 스킵
            if (boardEmpty && scoreNow > 0)
            {
                Debug.Log("[GameSnapShot] Empty board while score>0 → skip snapshot.");
                return;
            }

            // 정상 저장
            sm.gameData.currentMapLayout = layout;
            sm.gameData.currentScore     = live != 0 ? live : cached;
            sm.gameData.currentCombo     = ScoreManager.Instance?.Combo ?? sm.gameData.currentCombo;

            sm.SaveGame();
            Debug.Log("[GameSnapShot] Grid snapshot saved.");
        }
    }
}
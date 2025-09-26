using UnityEngine;
using System.Collections.Generic;
using _00.WorkSpace.GIL.Scripts.Shapes;

namespace _00.WorkSpace.GIL.Scripts.Managers
{
    public partial class BlockSpawnManager
    {
#if UNITY_EDITOR
        // 라인 보정 시작/후보/평가/채택, 한 줄씩 간단 호출용 훅
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        private void LC_Begin() => TDo("라인 보정 후보 계산 시작");

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        private void LC_Stat(int candLines) => TDo2($"후보 라인={candLines}");

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        private void LC_RunSample(string axis, int index, int length)
            => TSampled("LineCorr.run", 20, $"run: axis={axis}, index={index}, length={length}");

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        private void LC_EvalSample(string lineKey, ShapeData s, float gain)
            => TSampled("LineCorr.eval", 50, $"line={lineKey}, shape={s?.Id}, gain={gain}");

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        private void LC_Pick(ShapeData s, float gain, int row, int col)
            => TDo($"라인 보정 채택: shape={s?.Id}, gain={gain}, origin=({row},{col})");

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        private void LC_Fail() => TDo("라인 보정 실패: 채택 불가");
#endif
    }
}
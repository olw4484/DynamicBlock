using System.Collections.Generic;
using _00.WorkSpace.GIL.Scripts.Shapes;

namespace _00.WorkSpace.GIL.Scripts.Managers
{
    public partial class BlockSpawnManager
    {
        // 웨이브 구성(순서 무시 멀티셋) : ID 정렬 후 "A|B|C"
        private string MakeWaveHistory(List<ShapeData> wave)
        {
            var ids = new List<string>(wave.Count);
            for (int i = 0; i < wave.Count; i++) ids.Add(wave[i]?.Id ?? "");
            ids.Sort();
            return string.Join("|", ids);
        }

        // 이번 기록이 들어오면 N연속이 되는가?
        private bool WouldBecomeNStreak(string currentWave, int n)
        {
            if (n <= 1) return true;
            if (_lastWaves.Count < n - 1) return false;
            foreach (var prev in _lastWaves)
                if (prev != currentWave) return false;
            return true;
        }

        // 이력 갱신: 최근 (N-1)개만 유지
        private void RegisterWaveHistory(string wave)
        {
            _lastWaves.Enqueue(wave);
            while (_lastWaves.Count > (maxSameWaveStreak - 1))
                _lastWaves.Dequeue();
        }
        
        public void ResetWaveHistory() => _lastWaves.Clear();
    }
}
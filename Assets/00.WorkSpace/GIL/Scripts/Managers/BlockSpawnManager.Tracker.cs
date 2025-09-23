using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace _00.WorkSpace.GIL.Scripts.Managers
{
    /// <summary>
    /// Editor 전용 스폰 로깅 유틸 v2 (압축/카운트/슬롯묶음/청크 출력/문서 넘버링 + 계획/하위단계 포맷)
    /// - TBegin/TDump : 웨이브 단위 시작/종료
    /// - T/Tn : 일반/넘버링 라인
    /// - TPlan(key, title) : "a) 블럭을 고르기" 같은 상위 계획 라인
    /// - TDo/TDo2 : 하위 단계(코드 실제 발동) "ㄴ …", "   · …"
    /// - TCount/TSampled : 반복 카운트/샘플링 로그
    /// - TSnapshot/TSlotEnd : 슬롯별 내부 과정 묶기
    /// 런타임 빌드에서는 빈 스텁.
    /// </summary>
    public partial class BlockSpawnManager
    {
#if UNITY_EDITOR
        // === State ===
        private StringBuilder _spawnTracker;
        private StringBuilder _slotBuffer;
        private string _tPrev;
        private int _tPrevCount;
        private readonly Dictionary<string,int> _tally = new();
        private const int T_CHUNK = 8000; // 콘솔 잘림 방지용 분할 크기

        // === Doc Numbering (기획서 넘버링 매핑표) ===
        // key: 논리 키, value: 넘버링 접두어(예: "a", "b-i", "b-iv-2")
        private static readonly Dictionary<string,string> _docNum = new()
        {
            {"Start",                              "a"},
            {"ComputeBByVacancy",                 "b-i"},
            {"ComputeAForGate",                   "b-ii"},

            {"Plan.PickBlock",                    "a"},   // 슬롯 단계 예시: a) 블럭을 고르기
            {"Plan.Failure",                      "b"},   // 슬롯 실패/보정: b) 무슨 일이 발생함

            {"GroupByTileCount",                  "c-i"},
            {"ComputeGroupWeights",               "c-ii"},
            {"PickGroup",                         "c-iii"},

            {"ComputeInGroupWeightsByDifficulty", "d-i"},
            {"WeightedPickIndex",                 "d-ii"},

            {"TryPickPlaceable",                   "e-i"},
            {"ReservePlacement",                   "e-ii"},

            {"TryApplyLineCorrectionOnce",         "f-i"},
            {"PickLineCorrection",                 "f-ii"},

            {"End",                                "g"}
        };

        /// <summary> 외부에서 런타임으로 부분 매핑을 덮어쓸 수 있도록 제공(선택사항). </summary>
        public void SetDocNumbering(Dictionary<string,string> overrides)
        {
            if (overrides == null) return;
            foreach (var kv in overrides) _docNum[kv.Key] = kv.Value;
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        private void TBegin(string header = null)
        {
            _spawnTracker ??= new StringBuilder(4096);
            _slotBuffer   ??= new StringBuilder(2048);
            _spawnTracker.Clear();
            _slotBuffer.Clear();
            _tally.Clear();
            _tPrev = null; _tPrevCount = 0;
            if (!string.IsNullOrEmpty(header)) _spawnTracker.AppendLine(header);
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        private void T(string msg)
        {
            if (msg == _tPrev) { _tPrevCount++; return; } // 연속 동일 메시지 압축
            FlushPrevLine();
            _tPrev = msg; _tPrevCount = 1;
        }

        /// <summary> 넘버링 프리픽스를 자동으로 붙여서 출력 </summary>
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        private void Tn(string key, string line)
        {
            if (string.IsNullOrEmpty(line)) return;
            if (_docNum != null && _docNum.TryGetValue(key, out var num) && !string.IsNullOrEmpty(num))
                T($"{num}) {line}");
            else
                T(line);
        }

        /// <summary> 계획(기획서 상위 항목) 라인: 예) a) 블럭을 고르기 </summary>
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        private void TPlan(string key, string title)
        {
            Tn(key, title);
        }

        /// <summary> 하위 단계(코드 실제 발동): "ㄴ ..." </summary>
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        private void TDo(string line)
        {
            if (!string.IsNullOrEmpty(line)) T($"ㄴ {line}");
        }

        /// <summary> 하위-하위 단계: "   · ..." </summary>
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        private void TDo2(string line)
        {
            if (!string.IsNullOrEmpty(line)) T($"   · {line}");
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        private void TCount(string key, int n = 1)
        {
            if (_tally.TryGetValue(key, out var v)) _tally[key] = v + n; else _tally[key] = n;
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        private void TSampled(string key, int period, string verboseLine)
        {
            TCount(key);
            if (_tally[key] % Mathf.Max(1, period) == 0) T(verboseLine);
        }

        // === 슬롯 로깅 유틸 ===
        [System.Diagnostics.Conditional("UNITY_EDITOR")] private void TSlotBegin(int slotIdx) { /* 헤더는 TSlotEnd에서 출력 */ }

        private int TSnapshot()
        {
#if UNITY_EDITOR
            FlushPrevLine();
            return _spawnTracker?.Length ?? 0;
#else
            return 0;
#endif
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        private void TSlotEnd(int slotIdx, int snapshotIndex, string endLine)
        {
            FlushPrevLine();
            if (_spawnTracker == null) return;
            int start = Mathf.Clamp(snapshotIndex, 0, _spawnTracker.Length);
            int len   = _spawnTracker.Length - start;
            string inner = len > 0 ? _spawnTracker.ToString(start, len).TrimEnd() : string.Empty;
            _spawnTracker.Length = start; // 잘라내기

            _slotBuffer.AppendLine($"Slot{slotIdx} : 선택 시작");
            if (!string.IsNullOrEmpty(inner)) _slotBuffer.AppendLine(inner);
            if (!string.IsNullOrEmpty(endLine)) _slotBuffer.AppendLine(endLine);
            _slotBuffer.AppendLine();
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        private void TDump(bool chunked = true, string title = null)
        {
            if (!string.IsNullOrEmpty(title)) _spawnTracker.AppendLine(title);
            FlushPrevLine();

            // 최종 출력 조립: 전역 → 슬롯 상세 → 반복 요약
            var final = new StringBuilder(_spawnTracker.Length + _slotBuffer.Length + 256);
            final.Append(_spawnTracker.ToString());
            if (_slotBuffer.Length > 0)
            {
                final.AppendLine();
                final.AppendLine("--- 슬롯 상세 ---");
                final.Append(_slotBuffer.ToString());
            }
            if (_tally.Count > 0)
            {
                final.AppendLine();
                final.AppendLine("--- 반복 요약 ---");
                foreach (var kv in _tally) final.AppendLine($"{kv.Key} : {kv.Value}회");
            }

            var text = final.ToString();
            if (chunked && text.Length > T_CHUNK)
            {
                int idx = 0, chunkIdx = 1;
                while (idx < text.Length)
                {
                    int len = Mathf.Min(T_CHUNK, text.Length - idx);
                    UnityEngine.Debug.Log($"[SpawnLog Chunk {chunkIdx}] " + text.Substring(idx, len));
                    idx += len; chunkIdx++;
                }
            }
            else
            {
                UnityEngine.Debug.Log(text);
            }

            // 다음 라운드 준비
            _spawnTracker.Clear();
            _slotBuffer.Clear();
            _tally.Clear();
            _tPrev = null; _tPrevCount = 0;
        }

        private void FlushPrevLine()
        {
            if (_tPrevCount <= 0) return;
            if (_tPrevCount == 1) _spawnTracker.AppendLine(_tPrev);
            else _spawnTracker.AppendLine($"{_tPrev} x{_tPrevCount}회");
            _tPrev = null; _tPrevCount = 0;
        }
#else
        // ===== Build(런타임) 스텁 =====
        private void TBegin(string header = null) {}
        private void T(string msg) {}
        private void Tn(string key, string line) {}
        private void TPlan(string key, string title) {}
        private void TDo(string line) {}
        private void TDo2(string line) {}
        private void TCount(string key, int n = 1) {}
        private void TSampled(string key, int period, string verboseLine) {}
        private void TSlotBegin(int slotIdx) {}
        private int  TSnapshot() => 0;
        private void TSlotEnd(int slotIdx, int snapshotIndex, string endLine) {}
        private void TDump(bool chunked = true, string title = null) {}
#endif
    }
}

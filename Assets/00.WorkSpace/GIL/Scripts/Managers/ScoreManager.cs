using System;
using TMPro;
using UnityEngine;
using _00.WorkSpace.GIL.Scripts.Messages;
using _00.WorkSpace.GIL.Scripts.Maps;

namespace _00.WorkSpace.GIL.Scripts.Managers
{
    public class ScoreManager : MonoBehaviour, IRuntimeReset
    {
        public static ScoreManager Instance;

        [Header("Score Text")]
        [SerializeField] private TMP_Text scoreText;
        [SerializeField] private TMP_Text comboText;

        [SerializeField] public int baseScroe = 30;

        private EventQueue _bus;
        private int _score = 0;
        public int Score => _score;
        public int Combo { get; private set; }

        private bool _handHadClear = false; // ??? ???(3??) ???? ?? ????? ?? ????? ????��?

        public int comboCount
        {
            get => Combo;
            set => SetCombo(value);
        }

        private void Awake()
        {
            if (Instance == null)
                Instance = this;
            else
                Destroy(gameObject);
            Debug.Log("ScoreManager: Awake");
            UpdateScoreUI();
        }

        void OnEnable()
        {
            StartCoroutine(GameBindingUtil.WaitAndRun(() => SetDependencies(Game.Bus)));
        }
        private void OnDisable()
        {
            _bus?.Unsubscribe<AllClear>(OnAllClear);
            _bus?.Unsubscribe<GameResetRequest>(OnGameResetReq);
        }

        private void OnAllClear(AllClear e)
        {
            AddScore(e.bonus);
        }

        public void SetDependencies(EventQueue bus)
        {
            _bus = bus;
            _bus.Unsubscribe<AllClear>(OnAllClear);
            _bus.Subscribe<AllClear>(OnAllClear, replaySticky: false);

            _bus.Unsubscribe<GameResetRequest>(OnGameResetReq);
            _bus.Subscribe<GameResetRequest>(OnGameResetReq, replaySticky: false);

            // ?? ???? ???? 0 ???��? HUD?? ????
            PublishScore();
            PublishCombo();
        }

        // =============== ????/??? API ===============
        private void OnGameResetReq(GameResetRequest _)
        {
            ResetRuntime();
        }

        public void ResetRuntime()
        {
            _score = 0;
            Combo = 0;
            _handHadClear = false; // ??? ????
            PublishScore();
            PublishCombo();
        }

        public void AddScore(int amount)
        {
            int before = _score;
            _score += amount;
            PublishScore();
        }

        public void SetCombo(int value)
        {
            Combo = Mathf.Max(0, value);

            if (Combo > 0)
                Sfx.Combo(Combo); // ????? 1~8?? ?????

            PublishCombo();
        }

        public void ApplyMoveScore(int blockUnits, int clearedLines)
        {
            if (blockUnits < 0) blockUnits = 0;
            if (clearedLines < 0) clearedLines = 0;

            int comboAtStart = Combo;
            int baseScore = (comboAtStart + 1) * baseScroe; // ????? ????

            if (clearedLines == 0)
            {
                AddScore(blockUnits);
                // SetCombo(0); // ??? ?????? ??? ???? ??? ??? ????
                return;
            }

            int tier = (comboAtStart <= 4) ? 0 : (comboAtStart <= 9 ? 1 : 2); // 0,1,2
            int w = 2 + tier; // 2,3,4

            int bonus = CalcBonus(baseScore, comboAtStart, clearedLines);

            AddScore(blockUnits + bonus);
            SetCombo(comboAtStart + 1);

            // GIL_ADD - 어드벤쳐 모드일 경우 점수 체크까지 하기
            if (MapManager.Instance.CurrentMode == GameMode.Adventure)
            {
                var mM = MapManager.Instance;
                if (mM.CurrentMapData != null && mM.CurrentMapData.goalKind == MapGoalKind.Score)
                {
                    if (_score >= mM.CurrentMapData.scoreGoal)
                    {
                        Debug.Log($"[ScoreManager] 점수 목표 달성! 현재 점수: {_score}, 목표 점수: {mM.CurrentMapData.scoreGoal}");
                        // TODO: 점수 목표 달성 시 처리 (예: 맵 클리어 이벤트 발송)

                    }
                }
            }
            _handHadClear = true;
        }

        // ???? API?? ????
        [Obsolete("Use ApplyMoveScore(blockUnits, clearedLines).")]
        public void CalculateLineClearScore(int lineCount)
        {
            ApplyMoveScore(0, lineCount);
        }

        public void OnHandRefilled()
        {
            if (!_handHadClear)
                SetCombo(0);   // ??? ???(3??) ???? ?? ???? ?? ????? ???????? ??? ????

            _handHadClear = false; // ???? ????? ???? ?��??? ????
        }

        // =============== ???? API ===============
        void PublishScore()
        {
            if (scoreText) scoreText.text = _score.ToString();
            if (_bus == null) return;
            var e = new ScoreChanged(_score);
            _bus.PublishSticky(e, alsoEnqueue: false);
            _bus.PublishImmediate(e);
        }

        void PublishCombo()
        {
            if (comboText) comboText.text = $"Combo : {Combo}";
            if (_bus == null) return;
            var e = new ComboChanged(Combo);
            _bus.PublishSticky(e, alsoEnqueue: false);
            _bus.PublishImmediate(e);
        }
        // 어드벤쳐, 점수 모드일 경우 콤보수 마다 추가될 점수
        // 변경 가능성을 대비해서 별도 배열로 둠
        private readonly int[] ScoreByLines = { 0, 10, 30, 60, 100, 150, 210 };

        private int CalcBonus(int baseScore, int comboAtStart, int clearedLines)
        {
            // Adventure 모드 && 점수 목표일 경우 별도의 모드 적용
            var mM = MapManager.Instance;
            if (mM.CurrentMode == GameMode.Adventure && mM.CurrentMapData.goalKind == MapGoalKind.Score)
            {
                if (mM.CurrentMapData != null && mM.CurrentMapData.goalKind == MapGoalKind.Score)
                {
                    int idx = Mathf.Clamp(clearedLines, 0, ScoreByLines.Length - 1);
                    return ScoreByLines[idx];
                }
            }
            int tier = (comboAtStart <= 4) ? 0 : (comboAtStart <= 9 ? 1 : 2); // 0,1,2
            int w = 2 + tier; // 2,3,4

            if (clearedLines == 1)
            {
                int[] num = { 1, 3, 2 }; // 1, 1.5, 2
                int[] den = { 1, 2, 1 };
                return baseScore * num[tier] / den[tier];
            }
            else if (clearedLines == 2)
            {
                int[] mul = { 2, 3, 4 };
                return baseScore * mul[tier];
            }
            else
            {
                return baseScore * w * 3 * (clearedLines - 2);
            }
        }

        private void UpdateScoreUI()
        {
            if (scoreText) scoreText.text = _score.ToString();
        }
        
        public void SetFromSave(int score, int combo, bool publish = true)
        {
            _score = Mathf.Max(0, score);
            comboCount = Mathf.Max(0, combo);

            if (!publish) return;

            // 1) ��� �� �� (�̹� ���� ���� �����ʿ�)
            PublishScore();
            PublishCombo();
        }

        public void ResetAll(bool publish = true) => SetFromSave(0, 0, publish);
        public void RestoreScoreState(int score, int combo, bool silent = false)
        {
            _score = Mathf.Max(0, score);
            Combo = Mathf.Max(0, combo);
            if (!silent)
            {
                Game.Bus.PublishImmediate(new ScoreChanged(_score));
                Game.Bus.PublishImmediate(new ComboChanged(Combo));
            }
        }
    }
}

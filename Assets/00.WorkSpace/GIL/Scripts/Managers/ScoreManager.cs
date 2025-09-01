using System;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;

namespace _00.WorkSpace.GIL.Scripts.Managers
{
    public class ScoreManager : MonoBehaviour, IRuntimeReset
    {
        public static ScoreManager Instance;

        [Header("Score Text")]
        [SerializeField] private TMP_Text scoreText;
        private int _score = 0;
        public int Score => _score;
        public int Combo { get; private set; }

        public int comboCount
        {
            get => Combo;
            set => SetCombo(value);
        }

        EventQueue _bus;

        private void Awake()
        {
            if (Instance == null)
                Instance = this;
            else
                Destroy(gameObject);
            Debug.Log("ScoreManager: Awake");
            UpdateScoreUI();
        }

        void OnEnable() { StartCoroutine(GameBindingUtil.WaitAndRun(() => SetDependencies(Game.Bus))); }

        public void SetDependencies(EventQueue bus)
        {
            _bus = bus;
            _bus.Subscribe<GameResetRequest>(_ => ResetRuntime(), replaySticky: false);

            // 씬 진입 직후 0 상태를 HUD에 보장
            PublishScore();
            PublishCombo();
        }

        // =============== 점수/콤보 API ===============
        public void ResetRuntime()
        {
            _score = 0;
            Combo = 0;
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
            PublishCombo();
        }

        public void ApplyMoveScore(int blockUnits, int clearedLines)
        {
            if (blockUnits < 0) blockUnits = 0;
            if (clearedLines < 0) clearedLines = 0;

            int comboAtStart = Combo;
            int baseScore = (comboAtStart + 1) * 10;

            if (clearedLines == 0)
            {
                AddScore(blockUnits);
                SetCombo(0);
                return;
            }

            int tier = (comboAtStart <= 4) ? 0 : (comboAtStart <= 9 ? 1 : 2); // 0,1,2
            int w = 2 + tier; // 2,3,4

            int bonus;
            if (clearedLines == 1)
            {
                int[] oneNum = { 1, 3, 2 };
                int[] oneDen = { 1, 2, 1 };
                bonus = baseScore * oneNum[tier] / oneDen[tier];
            }
            else if (clearedLines == 2)
            {
                int[] twoMul = { 2, 3, 4 };
                bonus = baseScore * twoMul[tier];
            }
            else
            {
                bonus = baseScore * w * 3 * (clearedLines - 2);
            }

            AddScore(blockUnits + bonus);
            SetCombo(comboAtStart + 1);
        }

        // 기존 API는 위임
        [Obsolete("Use ApplyMoveScore(blockUnits, clearedLines).")]
        public void CalculateLineClearScore(int lineCount)
        {
            ApplyMoveScore(0, lineCount);
        }


        // =============== 내부 API ===============
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
            if (_bus == null) return;
            var e = new ComboChanged(Combo);
            _bus.PublishSticky(e, alsoEnqueue: false);
            _bus.PublishImmediate(e);
        }

        private void UpdateScoreUI()
        {
            if (scoreText) scoreText.text = _score.ToString();
        }
    }
}

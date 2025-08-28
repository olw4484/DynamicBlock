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


        public void CalculateLineClearScore(int lineCount)
        {
            if (lineCount <= 0) return;

            int combo = Combo;
            int mul = combo <= 4 ? 2 : (combo <= 9 ? 3 : 4);
            int baseLines = Mathf.Clamp(lineCount - 2, 1, Int32.MaxValue);

            int clearScore = (combo + 1) * 10 * (mul * baseLines * 3);
            AddScore(clearScore);
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

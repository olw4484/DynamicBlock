using System;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;

namespace _00.WorkSpace.GIL.Scripts.Managers
{
    public class ScoreManager : MonoBehaviour
    {
        public static ScoreManager Instance;
        
        
        [Header("Combo Debugger")]
        [SerializeField] private TMP_Text comboText;
        [SerializeField] private TMP_Text scoreText;
        private int score = 0;
        private int _comboCount = 0;
        public int ComboCount { get; set; }
        private void Awake()
        {
            if (Instance == null)
                Instance = this;
            else
                Destroy(gameObject);

            UpdateScoreUI();
        }
        
        public void CalculateLineClearScore()
        {
            int combo = _comboCount;
            int lineCount = GridManager.Instance.LineCount;
            
            if (lineCount <= 0) return;
            int clearScore = 0;

            if (combo >= 0 && combo <= 4)
            {
                clearScore += (combo + 1) * 10 * (2 * (Mathf.Clamp(lineCount - 2, 1, Int32.MaxValue)) * 3);
            }
            else if (combo >= 5 && combo <= 9)
            {
                clearScore += (combo + 1) * 10 * (3 * (Mathf.Clamp(lineCount - 2, 1, Int32.MaxValue)) * 3);
            }
            else if (combo >= 10)
            {
                clearScore += (combo + 1) * 10 * (4 * (Mathf.Clamp(lineCount - 2, 1, Int32.MaxValue)) * 3);
            }

            AddScore(clearScore);
        }
        
        
        public void AddScore(int amount)
        {
            score += amount;
            UpdateScoreUI();
        }

        private void UpdateScoreUI()
        {
            if (scoreText != null && comboText != null)
            {
                scoreText.text = score.ToString();
                comboText.text = _comboCount.ToString();
            }
        }
    }
}

using TMPro;
using UnityEngine;

namespace _00.WorkSpace.GIL.Scripts.Managers
{
    public class ScoreManager : MonoBehaviour
    {
        public static ScoreManager Instance;

        [SerializeField] private TMP_Text scoreText;
        private int score = 0;

        private void Awake()
        {
            if (Instance == null)
                Instance = this;
            else
                Destroy(gameObject);

            UpdateScoreUI();
        }

        public void AddScore(int amount)
        {
            score += amount;
            UpdateScoreUI();
        }

        private void UpdateScoreUI()
        {
            if (scoreText != null)
                scoreText.text = score.ToString();
        }
    }
}

using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ReviveScreen : MonoBehaviour
{
    [SerializeField] private Button _reviveBtn;
    [SerializeField] private TMP_Text _countText;
    [SerializeField] private Image _countImage;
    [SerializeField] private float _windowSeconds = 5f;

    float _remain;
    Coroutine _timerRoutine;
    bool _adRunning;

    void OnEnable()
    {
        _adRunning = false;
        _remain = _windowSeconds;
        UpdateUI();
        _reviveBtn.onClick.AddListener(OnClickReviveReward);
        _timerRoutine = StartCoroutine(CoTimer());
    }

    void OnDisable()
    {
        if (_timerRoutine != null) { StopCoroutine(_timerRoutine); _timerRoutine = null; }
        _reviveBtn.onClick.RemoveAllListeners();
    }

    IEnumerator CoTimer()
    {
        while (_remain > 0f)
        {
            _remain -= Time.unscaledDeltaTime;
            if (_remain < 0f) _remain = 0f;
            UpdateUI();
            yield return null;

            if (_adRunning) yield break; // 광고 시작되면 타이머 종료
        }

        // 시간 만료 → 전면 광고 시도 + 게임오버 모달 오픈
        TryShowInterstitial();
        CloseSelf();
        // Revive 실패 분기: 바로 결과 모달
        PublishPanelToggle("GameOver", true);
    }

    void UpdateUI()
    {
        if (_countText) _countText.text = ((int)Mathf.Ceil(_remain)).ToString();
        if (_countImage) _countImage.fillAmount = Mathf.Clamp01(_remain / _windowSeconds);
        // _reviveBtn.interactable = AdManager.Instance?.Reward?.IsReady ?? true;
    }

    void OnClickReviveReward()
    {
        if (_adRunning) return;
        _adRunning = true;
        if (_timerRoutine != null) { StopCoroutine(_timerRoutine); _timerRoutine = null; }
        _reviveBtn.interactable = false;

        bool rewarded = false;

        var reward = AdManager.Instance?.Reward;
        if (reward == null)
        {
            Debug.LogWarning("[Revive] Reward controller is null");
            FailToGameOver();
            return;
        }

        void Cleanup()
        {
            reward.Closed -= OnClosed;
            reward.Failed -= OnFailed;
            _reviveBtn.interactable = true;
            _adRunning = false;
        }

        void OnClosed()
        {
            Cleanup();
            CloseSelf();

            if (rewarded)
            {
                // 부활 성공: 이벤트 발행 → BlockStorage/게임 흐름이 이어서 처리
                Game.Bus.PublishImmediate(new ContinueGranted());
                Time.timeScale = 1f; // 재개
            }
            else
            {
                // 보상 실패/스킵: 결과 모달
                PublishPanelToggle("GameOver", true);
            }
        }

        void OnFailed()
        {
            Cleanup();
            FailToGameOver();
        }

        reward.Closed += OnClosed;
        reward.Failed += OnFailed;

        Debug.Log("[Revive] Try reward ad");
        reward.ShowAd(() => rewarded = true);
    }

    void TryShowInterstitial()
    {
        var inter = AdManager.Instance?.Interstitial;
        if (inter == null) return;
        inter.ShowAd(); // 콜백 없어도 UX상 즉시 모달로 넘어가면 충분
    }

    void FailToGameOver()
    {
        CloseSelf();
        PublishPanelToggle("GameOver", true);
    }

    void CloseSelf() => gameObject.SetActive(false);

    // UIManager와 직접 참조를 끊고 이벤트로 패널 토글
    void PublishPanelToggle(string key, bool on)
    {
        Game.Bus?.PublishImmediate(new PanelToggle(key, on));
    }
}

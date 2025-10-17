using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ReviveScreen : MonoBehaviour
{
    [SerializeField] private Button _reviveBtn;
    [SerializeField] private Button _giveUpBtn;
    [SerializeField] private TMP_Text _countText;
    [SerializeField] private Image _countImage;
    [SerializeField] private float _windowSeconds = 5f;

    float _remain;
    Coroutine _timerRoutine;
    bool _adRunning;
    bool _reviveGranted;

    IAdService Ads => Game.Ads;

    Coroutine _watchReadyRoutine;

#if UNITY_EDITOR
    [SerializeField] bool debugMockReward;
#endif

    void Awake()
    {
        if (!_reviveBtn) _reviveBtn = transform.GetComponentInChildren<Button>(true);
        if (!_giveUpBtn) _giveUpBtn = transform.Find("GiveUpButton")?.GetComponent<Button>();
        Debug.Log($"[Revive] Wire buttons: revive={_reviveBtn}, giveUp={_giveUpBtn}");
    }

    void OnEnable()
    {
        if (_reviveBtn) { _reviveBtn.onClick.RemoveListener(OnClickReviveReward); _reviveBtn.onClick.AddListener(OnClickReviveReward); }
        if (_giveUpBtn) { _giveUpBtn.onClick.RemoveListener(OnClickGiveUp); _giveUpBtn.onClick.AddListener(OnClickGiveUp); }

        _adRunning = false;
        _reviveGranted = false;

        _remain = _windowSeconds;
        if (_reviveBtn) _reviveBtn.interactable = false;
        UpdateUI();

        Ads?.Refresh();
        _watchReadyRoutine = StartCoroutine(Co_WatchReady(3f));
        _timerRoutine = StartCoroutine(CoTimer());
    }

    void OnDisable()
    {
        if (_watchReadyRoutine != null) { StopCoroutine(_watchReadyRoutine); _watchReadyRoutine = null; }
        if (_timerRoutine != null) { StopCoroutine(_timerRoutine); _timerRoutine = null; }

        if (_reviveBtn) _reviveBtn.onClick.RemoveListener(OnClickReviveReward);
        if (_giveUpBtn) _giveUpBtn.onClick.RemoveListener(OnClickGiveUp);
    }

    // 포기/타임아웃은 Router에 위임
    void ForceGiveUp(string reason)
    {
        _adRunning = false;
        if (_timerRoutine != null) { StopCoroutine(_timerRoutine); _timerRoutine = null; }
        ReviveRouter.I?.ForceGiveUp(reason);
        // 패널 닫기/게이트 해제/버스 발행은 전부 Router가 처리
    }

    IEnumerator CoTimer()
    {
        while (_remain > 0f)
        {
            _remain -= Time.unscaledDeltaTime;
            if (_remain < 0f) _remain = 0f;
            UpdateUI();
            if (_adRunning) yield break;
            yield return null;
        }

        if (_adRunning || _reviveGranted) yield break;

        // 대기/가드 고려는 Router가 하므로 바로 위임
        ForceGiveUp("revive_timeout");
    }

    IEnumerator Co_WatchReady(float duration)
    {
        float t = 0f;
        while (t < duration && !_adRunning)
        {
            bool ready = Ads?.IsRewardedReady() ?? false;
            if (_reviveBtn) _reviveBtn.interactable = ready;
            if (ready) yield break;
            yield return new WaitForSecondsRealtime(0.2f);
            t += 0.2f;
        }
        Ads?.Refresh();
    }

    void UpdateUI()
    {
        if (_countText) _countText.text = ((int)Mathf.Ceil(_remain)).ToString();
        if (_countImage) _countImage.fillAmount = Mathf.Clamp01(_remain / _windowSeconds);

        if (_reviveBtn) _reviveBtn.interactable = Ads?.IsRewardedReady() ?? false;
    }

    void OnClickGiveUp()
    {
        if (_adRunning || _reviveGranted) return;
        ForceGiveUp("giveup_clicked");
    }

    void OnClickReviveReward()
    {
#if UNITY_EDITOR
        if (debugMockReward)
        {
            // 디버그도 Router 경유
            ReviveRouter.I?.OnAdStart();
            _reviveGranted = true;
            ReviveRouter.I?.OnAdRewardGranted();
            ReviveRouter.I?.OnAdClosed(true);
            return;
        }
#endif
        if (_adRunning) return;
        _adRunning = true; _reviveGranted = false;
        if (_timerRoutine != null) { StopCoroutine(_timerRoutine); _timerRoutine = null; }

        if (Ads == null) { _adRunning = false; return; }
        if (!Ads.IsRewardedReady()) { StartCoroutine(Co_TryOnceAfterRefresh()); return; }

        // 광고 경로: Router와 동기화
        ReviveRouter.I?.OnAdStart();
        Ads.ShowRewarded(
            onReward: () => { _reviveGranted = true; ReviveRouter.I?.OnAdRewardGranted(); },
            onClosed: () =>
            {
                _adRunning = false;
                Ads.Refresh();
                ReviveRouter.I?.OnAdClosed(_reviveGranted);
            },
            onFailed: () =>
            {
                _adRunning = false;
                ReviveRouter.I?.OnAdClosed(false);
            }
        );
    }

    IEnumerator Co_TryOnceAfterRefresh()
    {
        Ads?.Refresh();
        yield return new WaitForSecondsRealtime(1.2f);

        if (Ads != null && Ads.IsRewardedReady())
        {
            ReviveRouter.I?.OnAdStart();                                                      // ★
            Ads.ShowRewarded(
                onReward: () => { _reviveGranted = true; ReviveRouter.I?.OnAdRewardGranted(); }, // ★
                onClosed: () => { _adRunning = false; Ads.Refresh(); ReviveRouter.I?.OnAdClosed(_reviveGranted); }, // ★
                onFailed: () => { _adRunning = false; ReviveRouter.I?.OnAdClosed(false); }    // ★
            );
        }
        else
        {
            Debug.LogWarning("[Revive] Rewarded not ready after retry. Keeping panel.");
            _adRunning = false;
            UpdateUI();
        }
    }

    // CloseSelf는 Router가 닫기를 놓쳤을 때만 사용 (최후 수단)
    void CloseSelf()
    {
        var ui = FindFirstObjectByType<UIManager>(FindObjectsInactive.Include);
        if (ui != null)
        {
            ui.ClosePanelImmediate("Revive");
#if UNITY_EDITOR
            ui.LogModalStack("after-close");
#endif
        }
        else
        {
            gameObject.SetActive(false);
        }
    }
}

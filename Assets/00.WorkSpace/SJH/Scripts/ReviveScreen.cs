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
    bool _giveUpFired;

    IAdService Ads => Game.Ads;
    bool BusReady => Game.IsBound && Game.Bus != null;

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
        _giveUpFired = false;

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

        if (_adRunning || _reviveGranted || ReviveGate.IsArmed) yield break;

        if (!_giveUpFired && BusReady)
        {
            _giveUpFired = true;
            Game.Bus.PublishImmediate(new GiveUpRequest());
        }

        var ui = FindFirstObjectByType<UIManager>(FindObjectsInactive.Include);
        if (ui) ui.OpenResultNowBecauseNoRevive();

        CloseSelf();

        if (_remain <= 0f) AdManager.Instance.ShowInterstitial();
    }

    IEnumerator Co_WatchReady(float duration)
    {
        float t = 0f;
        while (t < duration && !_adRunning)
        {
            bool ready = Ads?.IsRewardedReady() ?? false;
            if (_reviveBtn) _reviveBtn.interactable = ready;
            // 필요하면 텍스트 갱신: _reviveBtn.GetComponentInChildren<TMP_Text>().text = ready ? "Revive" : "Loading...";
            if (ready) yield break;

            yield return new WaitForSecondsRealtime(0.2f);
            t += 0.2f;
        }
        // 아직도 미준비면 추가 로드 시도
        Ads?.Refresh();
    }

    void UpdateUI()
    {
        if (_countText) _countText.text = ((int)Mathf.Ceil(_remain)).ToString();
        if (_countImage) _countImage.fillAmount = Mathf.Clamp01(_remain / _windowSeconds);

        // 준비됐을 때만 버튼 활성화 (즉시 포기로 새지 않도록)
        if (_reviveBtn) _reviveBtn.interactable = Ads?.IsRewardedReady() ?? false;
    }

    void OnClickGiveUp()
    {
        if (_adRunning || _reviveGranted || ReviveGate.IsArmed) return;

        if (!_giveUpFired && BusReady)
        {
            _giveUpFired = true;
            Game.Bus.PublishImmediate(new GiveUpRequest());
        }

        var ui = FindFirstObjectByType<UIManager>(FindObjectsInactive.Include);
        if (ui) ui.OpenResultNowBecauseNoRevive();

        CloseSelf();
    }

    void OnClickReviveReward()
    {
#if UNITY_EDITOR
        if (debugMockReward)
        {
            if (BusReady) { Game.Bus.PublishImmediate(new ContinueGranted()); Game.Bus.PublishImmediate(new RevivePerformed()); }
            CloseSelf(); return;
        }
#endif
        if (_adRunning) return;
        _adRunning = true; _reviveGranted = false;
        if (_timerRoutine != null) { StopCoroutine(_timerRoutine); _timerRoutine = null; }

        if (Ads == null) { _adRunning = false; return; }
        if (!Ads.IsRewardedReady()) { StartCoroutine(Co_TryOnceAfterRefresh()); return; }

        Ads.ShowRewarded(
    onReward: () => { _reviveGranted = true; },
    onClosed: () =>
    {
        _adRunning = false;
        Ads.Refresh();

        if (_reviveGranted && BusReady)
        {
            Game.Bus.PublishImmediate(new ContinueGranted());
            Game.Bus.PublishImmediate(new RevivePerformed());
        }
        else if (!_giveUpFired && BusReady)
        {
            _giveUpFired = true;
            Game.Bus.PublishImmediate(new GiveUpRequest());
        }

        CloseSelf();
    },
    onFailed: () =>
    {
        _adRunning = false;
        if (!_giveUpFired && BusReady)
        {
            _giveUpFired = true;
            Game.Bus.PublishImmediate(new GiveUpRequest());
        }
        CloseSelf();
    }
);
    }

    IEnumerator Co_TryOnceAfterRefresh()
    {
        Ads?.Refresh();
        yield return new WaitForSecondsRealtime(1.2f);

        if (Ads != null && Ads.IsRewardedReady())
        {
            Ads.ShowRewarded(
                onReward: () => { _reviveGranted = true; },
                onClosed: () => { _adRunning = false; Ads.Refresh(); CloseSelf(); },
                onFailed: () =>
                {
                    _adRunning = false;
                    if (!_reviveGranted && !_giveUpFired && BusReady)
                    {
                        _giveUpFired = true;
                        Game.Bus.PublishImmediate(new GiveUpRequest());
                    }
                    CloseSelf();
                }
            );
        }
        else
        {
            Debug.LogWarning("[Revive] Rewarded not ready after retry. Keeping panel.");
            _adRunning = false;
            UpdateUI();
        }
    }

    void ShowRewarded()
    {
        Ads.ShowRewarded(
            onReward: () =>
            {
                // 보상 수령 시 리바이브 요청
                if (BusReady) Game.Bus.PublishImmediate(new ReviveRequest());
            },
            onClosed: () =>
            {
                _adRunning = false;
                Ads.Refresh();  // 다음 로드를 위해
                CloseSelf();
            },
            onFailed: () =>
            {
                Debug.LogError("[Revive] Rewarded failed → give up.");
                _adRunning = false;
                if (BusReady) Game.Bus.PublishImmediate(new GiveUpRequest());
                CloseSelf();
            }
        );
    }

    void CloseSelf() => gameObject.SetActive(false);
}
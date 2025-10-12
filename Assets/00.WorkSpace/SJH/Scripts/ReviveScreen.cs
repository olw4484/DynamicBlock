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

    IAdService Ads => Game.Ads as IAdService;
    bool BusReady => Game.IsBound && Game.Bus != null;

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
        // 중복 등록 방지 후 등록
        if (_reviveBtn)
        {
            _reviveBtn.onClick.RemoveListener(OnClickReviveReward);
            _reviveBtn.onClick.AddListener(OnClickReviveReward);
        }
        if (_giveUpBtn)
        {
            _giveUpBtn.onClick.RemoveListener(OnClickGiveUp);
            _giveUpBtn.onClick.AddListener(OnClickGiveUp);
        }

        _adRunning = false;
        _remain = _windowSeconds;
        if (_reviveBtn) _reviveBtn.interactable = false; // 초기 비활성
        UpdateUI();

        // 1) 패널 열릴 때 로드
        Ads?.Refresh();

        // 2) 준비감시 코루틴 (0.2s 간격으로 3초 감시)
        StartCoroutine(Co_WatchReady(3f));

        if (_reviveBtn) _reviveBtn.onClick.AddListener(OnClickReviveReward);
        if (_giveUpBtn) _giveUpBtn.onClick.AddListener(OnClickGiveUp);

        _timerRoutine = StartCoroutine(CoTimer());
    }

    void OnDisable()
    {
        if (_reviveBtn) _reviveBtn.onClick.RemoveListener(OnClickReviveReward);
        if (_giveUpBtn) _giveUpBtn.onClick.RemoveListener(OnClickGiveUp);

        if (_timerRoutine != null) { StopCoroutine(_timerRoutine); _timerRoutine = null; }
        if (_reviveBtn) _reviveBtn.onClick.RemoveAllListeners();
        if (_giveUpBtn) _giveUpBtn.onClick.RemoveAllListeners();
    }

    IEnumerator CoTimer()
    {
        while (_remain > 0f)
        {
            _remain -= Time.unscaledDeltaTime;
            if (_remain < 0f) _remain = 0f;
            UpdateUI();
            if (_adRunning) yield break; // 광고 시작되면 타이머 종료
            yield return null;
        }

        // 시간 만료 → 포기 이벤트만 발행
        if (BusReady) Game.Bus.PublishImmediate(new GiveUpRequest());

        CloseSelf();

        // 0초 되면 전면 광고 실행
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
        if (BusReady) Game.Bus.PublishImmediate(new GiveUpRequest());
        CloseSelf();
    }

    void OnClickReviveReward()
    {
#if UNITY_EDITOR
        if (debugMockReward)
        {
            // 광고 없이 리바이브 흐름만 테스트
            if (BusReady) Game.Bus.PublishImmediate(new ReviveRequest());
            Debug.Log("[Revive] OnClickReviveReward");
            CloseSelf();
            return;
        }
#endif
        if (_adRunning) return;
        _adRunning = true;
        if (_timerRoutine != null) { StopCoroutine(_timerRoutine); _timerRoutine = null; }

        if (Ads == null)
        {
            Debug.LogWarning("[Revive] Ads facade is null");
            _adRunning = false;
            return;
        }

        if (!Ads.IsRewardedReady())
        {
            Debug.LogWarning("[Revive] Not ready. Refresh and retry.");
            StartCoroutine(Co_TryOnceAfterRefresh());
            return;
        }

        Debug.Log("[Revive] ShowRewarded()");
        Ads.ShowRewarded(
            onReward: () => Debug.Log("[Revive] onReward"),
            onClosed: () => { Debug.Log("[Revive] onClosed"); _adRunning = false; Ads.Refresh(); CloseSelf(); },
            onFailed: () => { Debug.LogError("[Revive] onFailed"); _adRunning = false; if (BusReady) Game.Bus.PublishImmediate(new GiveUpRequest()); CloseSelf(); }
        );

        // 보상 수령 이벤트는 ReviveRequest로 이벤트 분리(이전 답변대로)
        // -> IAdService 구현에서 onReward 시 Game.Bus.Publish(new ReviveRequest()); 호출해도 됨
    }

    IEnumerator Co_TryOnceAfterRefresh()
    {
        Ads?.Refresh();
        yield return new WaitForSecondsRealtime(1.2f);

        if (Ads != null && Ads.IsRewardedReady())
        {
            ShowRewarded();
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
using GoogleMobileAds.Api;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RewardAdController
{
#if UNITY_ANDROID
    private const string TEST_REWARDED = "ca-app-pub-3940256099942544/5224354917";
#elif UNITY_IOS
    private const string TEST_REWARDED = "ca-app-pub-3940256099942544/1712485313";
#else
    private const string TEST_REWARDED = "unexpected_platform";
#endif

    private string RewardId =>
#if TEST_ADS || DEVELOPMENT_BUILD
        TEST_REWARDED;
#else
        AdIds.Rewarded;
#endif

    private RewardedAd _ad;
    private Action _externalOnReward;

    public bool IsReady { get; private set; }
    public bool IsLoading { get; private set; }

    public event Action Opened;
    public event Action Closed;
    public event Action Failed;
    public event Action Rewarded;

    private bool _granted;           // 보상 수령 여부
    private bool _closedHandled;     // 닫힘 처리 중복 방지
    private Coroutine _fallbackJob;  // 폴백 코루틴 핸들

    public static void ConfigureTestDevices(params string[] testDeviceIds)
    {
#if TEST_ADS || DEVELOPMENT_BUILD
        var list = (testDeviceIds == null) ? null : new List<string>(testDeviceIds);
        var conf = new RequestConfiguration { TestDeviceIds = list };
        MobileAds.SetRequestConfiguration(conf);
#endif
    }

    public void Init()
    {
        if (IsLoading || IsReady) return;

        DestroyAd();
        IsReady = false;
        IsLoading = true;
        _closedHandled = false;

        Debug.Log("[Rewarded] Loading start. isFocused=" + Application.isFocused);
        RewardedAd.Load(RewardId, new AdRequest(), (ad, error) =>
        {
            IsLoading = false;
            if (error != null || ad == null)
            {
                Debug.LogError($"[Rewarded] Load failed: {(error == null ? "null" : error)}");
                IsReady = false;
                if (Application.isFocused && AdManager.Instance != null)
                    AdManager.Instance.StartCoroutine(RetryAfter(10f));
                return;
            }

            Debug.Log("[Rewarded] Load success");
            _ad = ad;
            IsReady = true;
            HookEvents(_ad);
        });
    }

    public void DestroyAd()
    {
        if (_fallbackJob != null && AdManager.Instance != null)
        {
            AdManager.Instance.StopCoroutine(_fallbackJob);
            _fallbackJob = null;
        }

        if (_ad != null)
        {
            _ad.Destroy();
            _ad = null;
        }

        _externalOnReward = null;
        _granted = false;
        _closedHandled = false;
        IsReady = false;
        IsLoading = false;
    }

    public bool ShowAd(Action onReward = null, bool ignoreCooldown = false)
    {
        Debug.Log($"[Rewarded] Show; set revivePending=true (full={AdStateProbe.IsFullscreenShowing})");

        if (!ignoreCooldown && AdManager.Instance != null &&
            AdManager.Instance.NextRewardTime > DateTime.UtcNow)
        {
            Debug.Log("[Rewarded] Skipped due to cooldown");
            return false;
        }

        if (_ad == null || !_ad.CanShowAd() || !IsReady)
        {
            Debug.Log("[Rewarded] Not ready → Load");
            Init();
            return false;
        }

        _externalOnReward = onReward;
        _granted = false;
        _closedHandled = false;
        IsReady = false;

        // 광고 진입 전부터 ‘부활 대기’ 신호: GameOver 라우팅/발행 보류
        AdStateProbe.IsRevivePending = true;

        _ad.Show(reward =>
        {
            Debug.Log($"[Rewarded] Granted via Show(): {reward.Type} x{reward.Amount}");
            _granted = true;
            try { Rewarded?.Invoke(); } catch { }
            try { onReward?.Invoke(); } catch { }

            if (AdManager.Instance != null)
            {
                if (_fallbackJob != null) { AdManager.Instance.StopCoroutine(_fallbackJob); _fallbackJob = null; }
                _fallbackJob = AdManager.Instance.StartCoroutine(Co_FallbackCloseAfterGrant(5f));
            }
        });

        return true;
    }

    private void HookEvents(RewardedAd ad)
    {
        if (ad == null) return;

        ad.OnAdFullScreenContentOpened += () =>
        {
            ReviveGate.Arm(10f);
            AdStateProbe.IsFullscreenShowing = true;
            AdStateProbe.IsRevivePending = true;   // 광고 중엔 true 유지
            Debug.Log($"[Rewarded] Opened; fullscreen={AdStateProbe.IsFullscreenShowing}, revive={AdStateProbe.IsRevivePending}");
            try { Opened?.Invoke(); } catch { }
            if (Game.IsBound) Game.Bus.PublishImmediate(new AdPlaying());
        };

        ad.OnAdFullScreenContentClosed += () =>
        {
            HandleClosed("sdk_closed");
        };

        ad.OnAdFullScreenContentFailed += (AdError e) =>
        {
            Debug.LogError($"[Rewarded] Show error: {e}");
            HandleClosed($"sdk_failed:{e}");
        };
    }

    // === 폴백: 보상 수령 후 일정 시간 Closed 미도착 시 강제 닫힘 처리 ===
    private IEnumerator Co_FallbackCloseAfterGrant(float timeoutSec)
    {
        float end = Time.realtimeSinceStartup + Mathf.Max(1f, timeoutSec);
        while (Time.realtimeSinceStartup < end)
        {
            if (_closedHandled) yield break; // 이미 닫힘 처리됨
            yield return null;
        }

        if (!_closedHandled)
        {
            Debug.LogWarning("[Rewarded] Fallback close fired (Closed not received).");
            HandleClosed("fallback_after_grant");
        }
    }

    // === 닫힘 공통 루틴(정상/실패/폴백 모두 여기로 수렴) ===
    private void HandleClosed(string reason)
    {
        if (_closedHandled) return;
        _closedHandled = true;

        Debug.Log($"[Rewarded] Closed ({reason})");

        // 폴백 중단
        if (_fallbackJob != null && AdManager.Instance != null)
        {
            AdManager.Instance.StopCoroutine(_fallbackJob);
            _fallbackJob = null;
        }

        // 상태/게이트 정리
        AdStateProbe.IsFullscreenShowing = false;
        AdStateProbe.IsRevivePending = false;
        ReviveGate.Disarm();
        GameOverGate.Reset("ad closed");

        try { Closed?.Invoke(); } catch { }

        _externalOnReward = null;
        IsReady = false;
        Init();
    }

    private IEnumerator RetryAfter(float sec)
    {
        float until = Time.realtimeSinceStartup + sec;
        while (Time.realtimeSinceStartup < until) yield return null;
        if (!IsReady && !IsLoading && Application.isFocused) Init();
    }
}

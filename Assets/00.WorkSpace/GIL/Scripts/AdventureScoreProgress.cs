using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class AdventureScoreProgress : MonoBehaviour
{
    [Header("Reference")]
    [SerializeField] private Slider scoreProgressBar;
    [SerializeField] private TMP_Text scoreProgressText;

    [Header("Options")]
    [Tooltip("슬라이더를 0~1 정규화로 사용할지 여부")]
    [SerializeField] private bool useNormalized = true;

    [Tooltip("값 변경 시 부드럽게 보간(초) — 0이면 즉시 갱신")]
    [SerializeField] private float tweenSeconds = 0f;

    private int  _target;
    private int  _current;
    private Coroutine _tweenCo;

    void Reset() => AutoWire();
    void OnValidate() => AutoWire();

    void AutoWire()
    {
        if (!scoreProgressBar)  scoreProgressBar  = GetComponentInChildren<Slider>(true);
        if (!scoreProgressText) scoreProgressText = transform.GetComponentInChildren<TMP_Text>(true);
        if (scoreProgressBar)
        {
            if (useNormalized)
            {
                scoreProgressBar.wholeNumbers = false;
                scoreProgressBar.minValue = 0f;
                scoreProgressBar.maxValue = 1f;
            }
            else
            {
                scoreProgressBar.wholeNumbers = true; // 0~목표점수
            }
        }
    }

    public void Initialize(int target, int startValue)
    {
        _target = Mathf.Max(1, target);
        _current = Mathf.Clamp(startValue, 0, _target);

        if (!scoreProgressBar)  return;

        if (useNormalized)
        {
            scoreProgressBar.minValue = 0f;
            scoreProgressBar.maxValue = 1f;
            scoreProgressBar.value    = _current / (float)_target;
        }
        else
        {
            scoreProgressBar.minValue = 0f;
            scoreProgressBar.maxValue = _target;
            scoreProgressBar.value    = _current;
        }

        SetLabel(_current, _target);
    }

    public void UpdateCurrent(int newScore)
    {
        _current = Mathf.Clamp(newScore, 0, _target);
        float v = useNormalized ? _current / (float)_target : _current;

        if (!scoreProgressBar) return;

        if (_tweenCo != null) StopCoroutine(_tweenCo);
        if (tweenSeconds > 0f)
            _tweenCo = StartCoroutine(TweenValue(scoreProgressBar.value, v, tweenSeconds));
        else
            scoreProgressBar.value = v;

        SetLabel(_current, _target);
    }

    public void UpdateTarget(int newTarget, bool keepCurrent = true)
    {
        int cur = keepCurrent ? _current : 0;
        Initialize(newTarget, cur);
    }

    IEnumerator TweenValue(float from, float to, float dur)
    {
        float t = 0f;
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / dur);
            scoreProgressBar.value = Mathf.Lerp(from, to, k);
            yield return null;
        }
        scoreProgressBar.value = to;
        _tweenCo = null;
    }

    void SetLabel(int curr, int tgt)
    {
        if (!scoreProgressText) return;
        // 1,234 / 5,000 형식
        scoreProgressText.text = $"{curr:N0} / {tgt:N0}";
    }
}

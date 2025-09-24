using UnityEngine;

public sealed class AchievementsButtonNotifier : MonoBehaviour
{
    [Header("FX (�ڽ� ������Ʈ��)")]
    [SerializeField] GameObject[] sparkles;  // Sparkle-Small/Medium/Big
    [SerializeField] GameObject redDot;      // ���� �� ������
    [SerializeField] float autoHideSec = 1.1f;
    [SerializeField] bool unscaledTime = true;

    bool _queued;

    void Awake()
    {
        if (redDot) redDot.SetActive(false);
        foreach (var go in sparkles) if (go) go.SetActive(false);
    }

    void OnEnable()
    {
        if (_queued)
        {
            _queued = false;
            PlaySparkles();
        }
    }

    public void NotifyUnlocked()
    {
        if (redDot) redDot.SetActive(true);

        if (isActiveAndEnabled)         // Ȱ�� ���¸� ��� ���
            PlaySparkles();
        else
            _queued = true;             // ��Ȱ�� ���¸� ���߿� ���
    }

    public void ClearNotification()
    {
        _queued = false;
        if (redDot) redDot.SetActive(false);
        StopAllCoroutines();
        foreach (var go in sparkles) if (go) go.SetActive(false);
    }

    void PlaySparkles()
    {
        float longest = 0f;

        foreach (var go in sparkles)
        {
            if (!go) continue;
            go.SetActive(true);

            var animator = go.GetComponent<Animator>();
            if (animator)
            {
                animator.updateMode = unscaledTime ? AnimatorUpdateMode.UnscaledTime : AnimatorUpdateMode.Normal;
                animator.Play(0, 0, 0f);
                animator.Update(0f);
            }

            var anim = go.GetComponent<Animation>();
            if (anim && anim.clip)
            {
                anim.clip.wrapMode = WrapMode.Once;
                anim.Stop();
                anim.Play(anim.clip.name);
                longest = Mathf.Max(longest, anim.clip.length);
            }
        }

        if (autoHideSec > 0f)
        {
            float t = Mathf.Max(autoHideSec, longest);
            if (unscaledTime) StartCoroutine(WaitRealtimeThenHide(t));
            else StartCoroutine(WaitThenHide(t));
        }
    }

    System.Collections.IEnumerator WaitThenHide(float t)
    {
        yield return new WaitForSeconds(t);
        foreach (var go in sparkles) if (go) go.SetActive(false);
    }
    System.Collections.IEnumerator WaitRealtimeThenHide(float t)
    {
        yield return new WaitForSecondsRealtime(t);
        foreach (var go in sparkles) if (go) go.SetActive(false);
    }
}

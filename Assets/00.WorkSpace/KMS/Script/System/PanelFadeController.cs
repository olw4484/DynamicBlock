using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public sealed class PanelFadeController : MonoBehaviour
{
    [SerializeField] string panelName = "Splash"; // 예: "Game_Open", "Main"
    [SerializeField] CanvasGroup cg;
    [SerializeField] float fadeDur = 0.3f;
    [SerializeField] bool manageActive = true;   // 끝에서만 SetActive(false)

    private EventQueue _bus;
    float _targetAlpha = -1f;

    void Awake()
    {
        if (!cg) cg = GetComponent<CanvasGroup>();
        if (!cg) cg = gameObject.AddComponent<CanvasGroup>(); // 안전망
    }

    void OnEnable()
    {
        StartCoroutine(GameBindingUtil.WaitAndRun(() => Bind(Game.Bus)));
    }

    void Bind(EventQueue bus)
    {
        _bus = bus;

        _bus.Subscribe<PanelToggle>(e =>
        {
            if (e.key != panelName) return;
            float want = e.on ? 1f : 0f;
            if (Mathf.Approximately(_targetAlpha, want)) return;
            _targetAlpha = want;

            // 디버그
            Debug.Log($"[Fade] {panelName} on={e.on} start={cg.alpha}");

            StopAllCoroutines();
            StartCoroutine(FadeTo(want));
        }, replaySticky: true);
    }

    IEnumerator FadeTo(float target)
    {
        // 켤 때는 먼저 보이도록
        if (manageActive && target > 0f && !gameObject.activeSelf)
            gameObject.SetActive(true);

        // 초기 alpha=1이면 페이드인이 안 보이므로 보정
        if (target > 0f && cg.alpha > 0.999f) cg.alpha = 0f;

        float start = cg.alpha;
        float t = 0f;
        while (t < fadeDur)
        {
            t += Time.unscaledDeltaTime;
            cg.alpha = Mathf.Lerp(start, target, t / fadeDur);
            yield return null;
        }
        cg.alpha = target;
        cg.blocksRaycasts = target >= 1f;
        cg.interactable = target >= 1f;

        // 끌 때는 끝에서만 비활성화 (다른 토글러가 즉시 꺼버리는 문제 회피)
        if (manageActive && target <= 0f)
        {
            // 한 프레임 양보해 다른 처리 끝나도록
            yield return null;
            if (Mathf.Approximately(cg.alpha, 0f))
                gameObject.SetActive(false);
        }
    }
}

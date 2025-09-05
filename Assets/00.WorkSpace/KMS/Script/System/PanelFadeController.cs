using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public sealed class PanelFadeController : MonoBehaviour
{
    [SerializeField] string panelName = "Splash"; // ��: "Game_Open", "Main"
    [SerializeField] CanvasGroup cg;
    [SerializeField] float fadeDur = 0.3f;
    [SerializeField] bool manageActive = true;   // �������� SetActive(false)

    private EventQueue _bus;
    float _targetAlpha = -1f;

    void Awake()
    {
        if (!cg) cg = GetComponent<CanvasGroup>();
        if (!cg) cg = gameObject.AddComponent<CanvasGroup>(); // ������
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

            // �����
            Debug.Log($"[Fade] {panelName} on={e.on} start={cg.alpha}");

            StopAllCoroutines();
            StartCoroutine(FadeTo(want));
        }, replaySticky: true);
    }

    IEnumerator FadeTo(float target)
    {
        // �� ���� ���� ���̵���
        if (manageActive && target > 0f && !gameObject.activeSelf)
            gameObject.SetActive(true);

        // �ʱ� alpha=1�̸� ���̵����� �� ���̹Ƿ� ����
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

        // �� ���� �������� ��Ȱ��ȭ (�ٸ� ��۷��� ��� �������� ���� ȸ��)
        if (manageActive && target <= 0f)
        {
            // �� ������ �纸�� �ٸ� ó�� ��������
            yield return null;
            if (Mathf.Approximately(cg.alpha, 0f))
                gameObject.SetActive(false);
        }
    }
}

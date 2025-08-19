using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// ================================
// Project : DynamicBlock
// Script  : SceneFlowManager.cs
// Desc    : �� ��ȯ(�񵿱�) + ���̵� + �̺�Ʈ ����
// ================================

[DisallowMultipleComponent]
[AddComponentMenu("System/SceneFlowManager")]
public class SceneFlowManager : MonoBehaviour, IManager
{
    public int Order => 20;

    [Header("Fade")]
    [SerializeField] private float _fadeTime = 0.25f;
    [SerializeField] private Color _fadeColor = Color.black;

    private EventQueue _bus;
    private Canvas _fadeCanvas;
    private CanvasGroup _fadeGroup;
    private bool _isLoading;

    // ����
    public void SetDependencies(EventQueue bus) { _bus = bus; }

    public void PreInit()
    {
        if (_bus == null) Debug.LogError("[SceneFlow] EventQueue ���� �ʿ�");
        EnsureFadeCanvas();
    }

    public void Init()
    {
        // �̺�Ʈ ���� �غ�� PostInit����
    }

    public void PostInit()
    {
        _bus.Subscribe<SceneChangeRequest>(OnSceneChangeRequest, replaySticky: false);
    }

    // === �ܺ� API ===
    public void LoadScene(string sceneName, LoadSceneMode mode = LoadSceneMode.Single, float? fadeSec = null)
    {
        if (_isLoading) return;
        StartCoroutine(LoadRoutine(sceneName, mode, fadeSec ?? _fadeTime));
    }

    // === �̺�Ʈ �ڵ鷯 ===
    private void OnSceneChangeRequest(SceneChangeRequest req)
        => LoadScene(req.sceneName, req.mode, req.fadeSec);

    // === �ڷ�ƾ ===
    private IEnumerator LoadRoutine(string sceneName, LoadSceneMode mode, float fadeSec)
    {
        _isLoading = true;

        // �˸� (���ϸ� ���)
        _bus.PublishImmediate(new SceneWillChange(sceneName, mode));

        // Fade-Out
        yield return Fade(1f, fadeSec);

        // �ε�
        var op = SceneManager.LoadSceneAsync(sceneName, mode);
        if (op == null)
        {
            Debug.LogError($"[SceneFlow] Scene '{sceneName}' �ε� ����");
            yield return Fade(0f, fadeSec * 0.5f);
            _isLoading = false;
            yield break;
        }
        if (mode == LoadSceneMode.Single) op.allowSceneActivation = true;
        while (!op.isDone) yield return null;

        // Fade-In
        yield return Fade(0f, fadeSec);

        _bus.PublishImmediate(new SceneChanged(sceneName, mode));
        _isLoading = false;
    }

    private IEnumerator Fade(float target, float dur)
    {
        float t = 0f; float start = _fadeGroup.alpha;
        _fadeCanvas.enabled = true;
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            _fadeGroup.alpha = Mathf.Lerp(start, target, t / dur);
            yield return null;
        }
        _fadeGroup.alpha = target;
        if (Mathf.Approximately(target, 0f)) _fadeCanvas.enabled = false;
    }

    private void EnsureFadeCanvas()
    {
        // �������� ĵ���� �غ�
        var go = new GameObject("SceneFadeCanvas");
        DontDestroyOnLoad(go);
        _fadeCanvas = go.AddComponent<Canvas>();
        _fadeCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _fadeCanvas.sortingOrder = 32760;

        var blocker = new GameObject("Blocker");
        blocker.transform.SetParent(go.transform);
        var rect = blocker.AddComponent<RectTransform>();
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;

        var img = blocker.AddComponent<UnityEngine.UI.Image>();
        img.color = _fadeColor;

        _fadeGroup = go.AddComponent<CanvasGroup>();
        _fadeGroup.alpha = 0f;
        _fadeCanvas.enabled = false;
    }
}

// �� ��ȯ �̺�Ʈ��
public readonly struct SceneChangeRequest
{
    public readonly string sceneName; public readonly LoadSceneMode mode; public readonly float fadeSec;
    public SceneChangeRequest(string name, LoadSceneMode mode = LoadSceneMode.Single, float fadeSec = 0.25f)
    { sceneName = name; this.mode = mode; this.fadeSec = fadeSec; }
}
public readonly struct SceneWillChange
{
    public readonly string sceneName; public readonly LoadSceneMode mode;
    public SceneWillChange(string name, LoadSceneMode mode) { sceneName = name; this.mode = mode; }
}
public readonly struct SceneChanged
{
    public readonly string sceneName; public readonly LoadSceneMode mode;
    public SceneChanged(string name, LoadSceneMode mode) { sceneName = name; this.mode = mode; }
}

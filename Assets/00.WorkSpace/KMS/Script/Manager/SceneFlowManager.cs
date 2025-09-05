using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System.Threading.Tasks;

// ================================
// Project : DynamicBlock
// Script  : SceneFlowManager.cs
// Desc    : �� ��ȯ(�񵿱�) + ���̵� + �̺�Ʈ ����
// ================================
public sealed class SceneFlowManager : IManager
{
    public int Order => 20;

    private EventQueue _bus;
    private Canvas _fadeCanvas;
    private CanvasGroup _fadeGroup;
    private bool _isLoading;

    private float _fadeTime = 0.25f;
    private Color _fadeColor = Color.black;

    // ����
    public void SetDependencies(EventQueue bus) { _bus = bus; }

    public void PreInit()
    {
        if (_bus == null) Debug.LogError("[SceneFlow] EventQueue ���� �ʿ�");
        EnsureFadeCanvas();
    }

    public void Init() { }
    public void PostInit()
    {
        _bus.Subscribe<SceneChangeRequest>(OnSceneChangeRequest, replaySticky: false);

        _bus.Subscribe<GameResetRequest>(_ => {
            _bus.Publish(new GameResetting());        // �Է� ���/��� �ݱ� ��
            ManagerGroup.Instance.SoftReset();        // �� �Ŵ��� ResetRuntime()
            _bus.Publish(new GameResetDone());        // �Է� ����/���� �簳 ��
        }, replaySticky: false);
    }

    // === �ܺ� API ===
    public void LoadScene(string sceneName, LoadSceneMode mode = LoadSceneMode.Single, float? fadeSec = null)
    {
        if (_isLoading) return;
        _ = LoadRoutine(sceneName, mode, fadeSec ?? _fadeTime);
    }

    // === �̺�Ʈ �ڵ鷯 ===
    private void OnSceneChangeRequest(SceneChangeRequest req)
        => LoadScene(req.sceneName, req.mode, req.fadeSec);

    // === async �� ��ü�� �ε� ��ƾ ===
    private async Task LoadRoutine(string sceneName, LoadSceneMode mode, float fadeSec)
    {
        _isLoading = true;
        _bus.PublishImmediate(new SceneWillChange(sceneName, mode));

        // Fade-Out
        await Fade(1f, fadeSec);

        // �� �ε�
        var op = SceneManager.LoadSceneAsync(sceneName, mode);
        if (op == null)
        {
            Debug.LogError($"[SceneFlow] Scene '{sceneName}' �ε� ����");
            await Fade(0f, fadeSec * 0.5f);
            _isLoading = false;
            return;
        }
        if (mode == LoadSceneMode.Single) op.allowSceneActivation = true;
        while (!op.isDone) await Task.Yield();

        // Fade-In
        await Fade(0f, fadeSec);

        _bus.PublishImmediate(new SceneChanged(sceneName, mode));
        _isLoading = false;
    }

    private async Task Fade(float target, float dur)
    {
        float t = 0f; float start = _fadeGroup.alpha;
        _fadeCanvas.enabled = true;
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            _fadeGroup.alpha = Mathf.Lerp(start, target, t / dur);
            await Task.Yield();
        }
        _fadeGroup.alpha = target;
        if (Mathf.Approximately(target, 0f)) _fadeCanvas.enabled = false;
    }

    private void EnsureFadeCanvas()
    {
        var go = new GameObject("SceneFadeCanvas");
        Object.DontDestroyOnLoad(go);

        _fadeCanvas = go.AddComponent<Canvas>();
        _fadeCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _fadeCanvas.sortingOrder = 32760;

        var blocker = new GameObject("Blocker");
        blocker.transform.SetParent(go.transform);
        var rect = blocker.AddComponent<RectTransform>();
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;

        var img = blocker.AddComponent<Image>();
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

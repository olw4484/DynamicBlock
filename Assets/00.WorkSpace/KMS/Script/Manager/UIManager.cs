using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// ================================
// Project : DynamicBlock
// Script  : UIManager.cs
// Desc    : HUD ���� + �г� ��/���� ����
// ================================

[DisallowMultipleComponent]
[AddComponentMenu("Game/UIManager")]
public class UIManager : MonoBehaviour, IManager, IRuntimeReset
{
    [System.Serializable]
    public class PanelEntry
    {
        public string key;                   // "Pause", "Result" ��
        public GameObject root;              // �г� ��Ʈ ������Ʈ
        public bool defaultActive = false;
        public bool useCanvasGroup = true;   // ���̵��
        public bool isModal = false;         // ��� ����
        public bool closeOnEscape = true;    // ESC�� �ݱ� ���
        public int baseSorting = 1000;       // ��� �⺻ ����
    }

    [Header("HUD")]
    [SerializeField] private TMP_Text _scoreText;
    [SerializeField] private TMP_Text _comboText;

    [Header("Panels")]
    [SerializeField] private List<PanelEntry> _panels = new();

    private readonly Dictionary<string, PanelEntry> _panelMap = new();
    private readonly List<string> _modalOrder = new();

    // �гκ� ���̵� �ڷ�ƾ ����
    private readonly Dictionary<string, Coroutine> _fadeJobs = new();

    private EventQueue _bus;
    private GameManager _game;

    public int Order => 100;

    public void SetDependencies(EventQueue bus, GameManager game) { _bus = bus; _game = game; }

    public void PreInit()
    {
        if (_bus == null || _game == null)
            Debug.LogError("[UIManager] SetDependencies �ʿ�");

        foreach (var p in _panels)
        {
            if (!p.root) continue;

            if (p.isModal)
            {
                var cv = p.root.GetComponent<Canvas>() ?? p.root.AddComponent<Canvas>();
                cv.overrideSorting = true;

                if (!p.root.GetComponent<GraphicRaycaster>())
                    p.root.AddComponent<GraphicRaycaster>();

                // ����� ������ CanvasGroup ���� (���̵� �� �ᵵ ����ĳ��Ʈ �����)
                var cg = EnsureCanvasGroup(p.root);
                cg.blocksRaycasts = false;
                cg.interactable = false;
            }

            // ���̵�� CanvasGroup �ʿ� ��(�Ϲ� �г�)
            if (p.useCanvasGroup && !p.isModal)
                _ = EnsureCanvasGroup(p.root);
        }
    }

    // Init: �� ���� + �ʱ� Ȱ��ȭ
    public void Init()
    {
        _panelMap.Clear();
        _modalOrder.Clear();

        foreach (var p in _panels)
        {
            if (!p.root) continue;

            if (_panelMap.ContainsKey(p.key))
                Debug.LogWarning($"[UIManager] Duplicate panel key: {p.key}");
            else
                _panelMap.Add(p.key, p);

            p.root.SetActive(p.defaultActive);
        }
    }

    public void PostInit()
    {
        // HUD ���ε� (Sticky ��� ���)
        _bus.Subscribe<ScoreChanged>(e => { if (_scoreText) _scoreText.text = e.value.ToString(); }, replaySticky: true);
        _bus.Subscribe<ComboChanged>(e => { if (_comboText) _comboText.text = $"x{e.value}"; }, replaySticky: true);

        // GameOver ������ ��� ����(Sticky �� �ʱ⿡�� ��� ����)
        _bus.Subscribe<GameOver>(_ => SetPanel("GameOver", true), replaySticky: true);

        // ���� ���� �� ��� �ݱ�
        _bus.Subscribe<ContinueGranted>(_ => SetPanel("GameOver", false), replaySticky: false);

        // �г� ��� �̺�Ʈ ���� (Sticky ��� �ѵ�)
        _bus.Subscribe<PanelToggle>(OnPanelToggle, replaySticky: true);
    }

    private void OnPanelToggle(PanelToggle e) => SetPanel(e.key, e.on);

    // === �ܺ� API ===
    public void SetPanel(string key, bool on)
    {
        Debug.Log($"SetPanel {key} -> {on}");
        if (!_panelMap.TryGetValue(key, out var p) || p.root == null) return;

        // ����̸� LIFO ��η�
        if (p.isModal)
        {
            if (on) PushModalInternal(key);
            else PopModalInternal(key);
            return;
        }

        // �Ϲ� �г�: ���̵�/�¿���
        if (!p.useCanvasGroup)
        {
            p.root.SetActive(on);
            return;
        }

        // �̹� ��Ȱ�� �гο� OFF ��û�̸� ����(������ ����)
        if (!on && !p.root.activeSelf) return;

        var cg = EnsureCanvasGroup(p.root);

        // �ѱ� �����̸� ���� ���̰�
        if (!p.root.activeSelf)
        {
            p.root.SetActive(true);
            if (on) cg.alpha = 0f; // �� ���� 0��1
        }

        // �� �� �г��� ���̵常 ���� �� �����
        StopFade(key);
        _fadeJobs[key] = StartCoroutine(FadeRoutine(cg, on ? 1f : 0f, 0.15f, on, key));
    }

    private IEnumerator FadeRoutine(CanvasGroup cg, float target, float dur, bool finalActive, string key)
    {
        float t = 0f, start = cg.alpha;
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            cg.alpha = Mathf.Lerp(start, target, t / dur);
            yield return null;
        }

        cg.alpha = target;
        cg.blocksRaycasts = finalActive;
        cg.interactable = finalActive;

        if (!finalActive)
            cg.gameObject.SetActive(false);

        // �ڷ�ƾ �ڵ� ����
        _fadeJobs.Remove(key);
    }

    public void CloseTopModal()
    {
        if (_modalOrder.Count == 0) return;
        PopModalInternal(_modalOrder[^1]);
    }

    public bool TryCloseTopByEscape()
    {
        if (_modalOrder.Count == 0) return false;
        var topKey = _modalOrder[^1];
        if (_panelMap.TryGetValue(topKey, out var p) && !p.closeOnEscape) return false;
        PopModalInternal(topKey);
        return true;
    }

    // === ����: ��� LIFO ===
    private void PushModalInternal(string key)
    {
        if (!_panelMap.TryGetValue(key, out var p) || p.root == null) return;

        // �ߺ� ���� �� �� ����
        int idx = _modalOrder.IndexOf(key);
        if (idx >= 0) _modalOrder.RemoveAt(idx);
        _modalOrder.Add(key);

        // �ѱ� (��޵� ���̵� ���� ������ CanvasGroup Ȱ��)
        if (p.useCanvasGroup)
        {
            var cg = EnsureCanvasGroup(p.root);
            if (!p.root.activeSelf) { p.root.SetActive(true); cg.alpha = 0f; }
            StopFade(key);
            _fadeJobs[key] = StartCoroutine(FadeRoutine(cg, 1f, 0.12f, true, key));
        }
        else p.root.SetActive(true);

        ReorderModals();
    }

    private void PopModalInternal(string key)
    {
        int idx = _modalOrder.LastIndexOf(key);
        if (idx < 0) return;

        _modalOrder.RemoveAt(idx);

        if (_panelMap.TryGetValue(key, out var p) && p.root)
        {
            if (p.useCanvasGroup)
            {
                var cg = EnsureCanvasGroup(p.root);
                StopFade(key);
                _fadeJobs[key] = StartCoroutine(FadeRoutine(cg, 0f, 0.12f, false, key));
            }
            else p.root.SetActive(false);
        }

        ReorderModals();
    }

    private void ReorderModals()
    {
        // �ֻ�ܸ� �Է�/Raycast, ������ baseSorting + depth*10
        for (int i = 0; i < _modalOrder.Count; i++)
        {
            var k = _modalOrder[i];
            var p = _panelMap[k];
            var cv = p.root.GetComponent<Canvas>() ?? p.root.AddComponent<Canvas>();
            cv.overrideSorting = true;
            cv.sortingOrder = p.baseSorting + ((i + 1) * 10);

            var cg = EnsureCanvasGroup(p.root);
            bool top = (i == _modalOrder.Count - 1);
            cg.blocksRaycasts = top;
            cg.interactable = top;
        }
    }

    private void StopFade(string key)
    {
        if (_fadeJobs.TryGetValue(key, out var job) && job != null)
        {
            StopCoroutine(job);
            _fadeJobs.Remove(key);
        }
    }

    // CanvasGroup ����
    private static CanvasGroup EnsureCanvasGroup(GameObject go)
    {
        var cg = go.GetComponent<CanvasGroup>();
        return cg != null ? cg : go.AddComponent<CanvasGroup>();
    }

    public void ResetRuntime()
    {
        // ���ӿ��� �ݰ�, ���� �г� ����
        SetPanel("GameOver", false);
        SetPanel("Game", true);
    }
}

// �г� ��� �̺�Ʈ
public readonly struct PanelToggle
{
    public readonly string key; public readonly bool on;
    public PanelToggle(string key, bool on) { this.key = key; this.on = on; }
}
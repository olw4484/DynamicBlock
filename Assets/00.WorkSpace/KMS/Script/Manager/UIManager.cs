using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

// ================================
// Project : DynamicBlock
// Script  : UIManager.cs
// Desc    : HUD ���� + �г� ��/���� ����
// ================================

[DisallowMultipleComponent]
[AddComponentMenu("Game/UIManager")]
public class UIManager : MonoBehaviour, IManager
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
            if (p.isModal && p.root)
            {
                var cv = p.root.GetComponent<Canvas>() ?? p.root.AddComponent<Canvas>();
                cv.overrideSorting = true;

                var cg = p.root.GetComponent<CanvasGroup>() ?? p.root.AddComponent<CanvasGroup>();
                cg.blocksRaycasts = false;
                cg.interactable = false;

                // ����ĳ���� ������ �߰� (Ŭ��/������ ���� �ʿ�)
                if (!p.root.GetComponent<UnityEngine.UI.GraphicRaycaster>())
                    p.root.AddComponent<UnityEngine.UI.GraphicRaycaster>();
            }
        }
    }

    public void Init()
    {
        _panelMap.Clear();
        _modalOrder.Clear();

        foreach (var p in _panels)
        {
            if (p.root) p.root.SetActive(p.defaultActive);
            if (!_panelMap.ContainsKey(p.key)) _panelMap.Add(p.key, p);
        }
    }

    public void PostInit()
    {
        // HUD ���ε� (Sticky ��� ��� ����)
        _bus.Subscribe<ScoreChanged>(e => { if (_scoreText) _scoreText.text = e.value.ToString(); }, replaySticky: true);
        _bus.Subscribe<ComboChanged>(e => { if (_comboText) _comboText.text = $"x{e.value}"; }, replaySticky: true);

        // GameOver ������ ��� ����(Sticky �� �ʱ⿡�� ��� ����)
        _bus.Subscribe<GameOver>(_ => SetPanel("GameOver", true), replaySticky: true);

        // ���� ���� �� ��� �ݱ�
        _bus.Subscribe<ContinueGranted>(_ => SetPanel("GameOver", false), replaySticky: false);

        // �г� ��� �̺�Ʈ ���� (���ϸ� ��ư���� ���� API ȣ���ص� ��)
        _bus.Subscribe<PanelToggle>(OnPanelToggle, replaySticky: false);
    }

    private void OnPanelToggle(PanelToggle e) => SetPanel(e.key, e.on);

    // === �ܺ� API ===
    public void SetPanel(string key, bool on)
    {
        if (!_panelMap.TryGetValue(key, out var p) || p.root == null) return;

        // ����̸� LIFO ��η�
        if (p.isModal)
        {
            if (on) PushModalInternal(key);
            else PopModalInternal(key);
            return;
        }

        // �Ϲ� �г�: ����ó�� ���̵�/�¿���
        if (!p.useCanvasGroup)
        {
            p.root.SetActive(on);
            return;
        }

        var cg = p.root.GetComponent<CanvasGroup>() ?? p.root.AddComponent<CanvasGroup>();
        if (!p.root.activeSelf) { p.root.SetActive(true); cg.alpha = 0f; }
        StopAllCoroutines();
        StartCoroutine(FadeRoutine(cg, on ? 1f : 0f, 0.15f, on));
    }

    private IEnumerator FadeRoutine(CanvasGroup cg, float target, float dur, bool finalActive)
    {
        float t = 0f; float start = cg.alpha;
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            cg.alpha = Mathf.Lerp(start, target, t / dur);
            yield return null;
        }
        cg.alpha = target;
        if (!finalActive) cg.gameObject.SetActive(false);
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
            var cg = p.root.GetComponent<CanvasGroup>() ?? p.root.AddComponent<CanvasGroup>();
            if (!p.root.activeSelf) { p.root.SetActive(true); cg.alpha = 0f; }
            StopAllCoroutines();
            StartCoroutine(FadeRoutine(cg, 1f, 0.12f, true));
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
                var cg = p.root.GetComponent<CanvasGroup>() ?? p.root.AddComponent<CanvasGroup>();
                StopAllCoroutines();
                StartCoroutine(FadeRoutine(cg, 0f, 0.12f, false));
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
            cv.sortingOrder = p.baseSorting + (i + 1) * 10;

            var cg = p.root.GetComponent<CanvasGroup>() ?? p.root.AddComponent<CanvasGroup>();
            bool top = (i == _modalOrder.Count - 1);
            cg.blocksRaycasts = top;
            cg.interactable = top;
        }
    }
}

// �г� ��� �̺�Ʈ
public readonly struct PanelToggle
{
    public readonly string key; public readonly bool on;
    public PanelToggle(string key, bool on) { this.key = key; this.on = on; }
}

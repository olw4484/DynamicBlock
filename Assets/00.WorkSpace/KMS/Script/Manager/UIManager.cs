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
        public string key;          // "Pause", "Result" ��
        public GameObject root;     // �г� ��Ʈ ������Ʈ
        public bool defaultActive = false;
        public bool useCanvasGroup = true;   // ���̵��
    }

    [Header("HUD")]
    [SerializeField] private TMP_Text _scoreText;
    [SerializeField] private TMP_Text _comboText;

    [Header("Panels")]
    [SerializeField] private List<PanelEntry> _panels = new();

    private readonly Dictionary<string, PanelEntry> _panelMap = new();
    private EventQueue _bus;
    private GameManager _game;

    public int Order => 100;

    public void SetDependencies(EventQueue bus, GameManager game) { _bus = bus; _game = game; }

    public void PreInit()
    {
        if (_bus == null || _game == null)
            Debug.LogError("[UIManager] SetDependencies �ʿ�");
    }

    public void Init()
    {
        _panelMap.Clear();
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

        // �г� ��� �̺�Ʈ ���� (���ϸ� ��ư���� ���� API ȣ���ص� ��)
        _bus.Subscribe<PanelToggle>(OnPanelToggle, replaySticky: false);
    }

    private void OnPanelToggle(PanelToggle e) => SetPanel(e.key, e.on);

    // === �ܺ� API ===
    public void SetPanel(string key, bool on)
    {
        if (!_panelMap.TryGetValue(key, out var p) || p.root == null) return;

        if (!p.useCanvasGroup)
        {
            p.root.SetActive(on);
            return;
        }

        // CanvasGroup ���̵� (����)
        var cg = p.root.GetComponent<CanvasGroup>() ?? p.root.AddComponent<CanvasGroup>();
        if (!p.root.activeSelf) { p.root.SetActive(true); cg.alpha = 0f; }
        StopAllCoroutines();
        StartCoroutine(FadeRoutine(cg, on ? 1f : 0f, 0.15f, on));
    }

    private System.Collections.IEnumerator FadeRoutine(CanvasGroup cg, float target, float dur, bool finalActive)
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
}

// �г� ��� �̺�Ʈ
public readonly struct PanelToggle
{
    public readonly string key; public readonly bool on;
    public PanelToggle(string key, bool on) { this.key = key; this.on = on; }
}

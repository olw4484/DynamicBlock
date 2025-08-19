using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

// ================================
// Project : DynamicBlock
// Script  : UIManager.cs
// Desc    : HUD 갱신 + 패널 온/오프 관리
// ================================

[DisallowMultipleComponent]
[AddComponentMenu("Game/UIManager")]
public class UIManager : MonoBehaviour, IManager
{
    [System.Serializable]
    public class PanelEntry
    {
        public string key;          // "Pause", "Result" 등
        public GameObject root;     // 패널 루트 오브젝트
        public bool defaultActive = false;
        public bool useCanvasGroup = true;   // 페이드용
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
            Debug.LogError("[UIManager] SetDependencies 필요");
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
        // HUD 바인딩 (Sticky 즉시 재생 가정)
        _bus.Subscribe<ScoreChanged>(e => { if (_scoreText) _scoreText.text = e.value.ToString(); }, replaySticky: true);
        _bus.Subscribe<ComboChanged>(e => { if (_comboText) _comboText.text = $"x{e.value}"; }, replaySticky: true);

        // 패널 토글 이벤트 구독 (원하면 버튼에서 직접 API 호출해도 됨)
        _bus.Subscribe<PanelToggle>(OnPanelToggle, replaySticky: false);
    }

    private void OnPanelToggle(PanelToggle e) => SetPanel(e.key, e.on);

    // === 외부 API ===
    public void SetPanel(string key, bool on)
    {
        if (!_panelMap.TryGetValue(key, out var p) || p.root == null) return;

        if (!p.useCanvasGroup)
        {
            p.root.SetActive(on);
            return;
        }

        // CanvasGroup 페이드 (선택)
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

// 패널 토글 이벤트
public readonly struct PanelToggle
{
    public readonly string key; public readonly bool on;
    public PanelToggle(string key, bool on) { this.key = key; this.on = on; }
}

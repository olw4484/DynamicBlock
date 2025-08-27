using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// ================================
// Project : DynamicBlock
// Script  : UIManager.cs
// Desc    : HUD 갱신 + 패널 온/오프 관리
// ================================

[DisallowMultipleComponent]
[AddComponentMenu("Game/UIManager")]
public class UIManager : MonoBehaviour, IManager, IRuntimeReset
{
    [System.Serializable]
    public class PanelEntry
    {
        public string key;                   // "Pause", "Result" 등
        public GameObject root;              // 패널 루트 오브젝트
        public bool defaultActive = false;
        public bool useCanvasGroup = true;   // 페이드용
        public bool isModal = false;         // 모달 여부
        public bool closeOnEscape = true;    // ESC로 닫기 허용
        public int baseSorting = 1000;       // 모달 기본 정렬
    }

    [Header("HUD")]
    [SerializeField] private TMP_Text _scoreText;
    [SerializeField] private TMP_Text _comboText;

    [Header("Panels")]
    [SerializeField] private List<PanelEntry> _panels = new();

    private readonly Dictionary<string, PanelEntry> _panelMap = new();
    private readonly List<string> _modalOrder = new();

    // 패널별 페이드 코루틴 관리
    private readonly Dictionary<string, Coroutine> _fadeJobs = new();

    private EventQueue _bus;
    private GameManager _game;

    public int Order => 100;

    public void SetDependencies(EventQueue bus, GameManager game) { _bus = bus; _game = game; }

    public void PreInit()
    {
        if (_bus == null || _game == null)
            Debug.LogError("[UIManager] SetDependencies 필요");

        foreach (var p in _panels)
        {
            if (!p.root) continue;

            if (p.isModal)
            {
                var cv = p.root.GetComponent<Canvas>() ?? p.root.AddComponent<Canvas>();
                cv.overrideSorting = true;

                if (!p.root.GetComponent<GraphicRaycaster>())
                    p.root.AddComponent<GraphicRaycaster>();

                // 모달은 무조건 CanvasGroup 보장 (페이드 안 써도 레이캐스트 제어용)
                var cg = EnsureCanvasGroup(p.root);
                cg.blocksRaycasts = false;
                cg.interactable = false;
            }

            // 페이드용 CanvasGroup 필요 시(일반 패널)
            if (p.useCanvasGroup && !p.isModal)
                _ = EnsureCanvasGroup(p.root);
        }
    }

    // Init: 맵 구축 + 초기 활성화
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
        // HUD 바인딩 (Sticky 즉시 재생)
        _bus.Subscribe<ScoreChanged>(e => { if (_scoreText) _scoreText.text = e.value.ToString(); }, replaySticky: true);
        _bus.Subscribe<ComboChanged>(e => { if (_comboText) _comboText.text = $"x{e.value}"; }, replaySticky: true);

        // GameOver 들어오면 모달 오픈(Sticky → 초기에도 즉시 반응)
        _bus.Subscribe<GameOver>(_ => SetPanel("GameOver", true), replaySticky: true);

        // 광고 성공 시 모달 닫기
        _bus.Subscribe<ContinueGranted>(_ => SetPanel("GameOver", false), replaySticky: false);

        // 패널 토글 이벤트 구독 (Sticky 재생 켜둠)
        _bus.Subscribe<PanelToggle>(OnPanelToggle, replaySticky: true);
    }

    private void OnPanelToggle(PanelToggle e) => SetPanel(e.key, e.on);

    // === 외부 API ===
    public void SetPanel(string key, bool on)
    {
        Debug.Log($"SetPanel {key} -> {on}");
        if (!_panelMap.TryGetValue(key, out var p) || p.root == null) return;

        // 모달이면 LIFO 경로로
        if (p.isModal)
        {
            if (on) PushModalInternal(key);
            else PopModalInternal(key);
            return;
        }

        // 일반 패널: 페이드/온오프
        if (!p.useCanvasGroup)
        {
            p.root.SetActive(on);
            return;
        }

        // 이미 비활성 패널에 OFF 요청이면 무시(깜빡임 방지)
        if (!on && !p.root.activeSelf) return;

        var cg = EnsureCanvasGroup(p.root);

        // 켜기 시작이면 먼저 보이게
        if (!p.root.activeSelf)
        {
            p.root.SetActive(true);
            if (on) cg.alpha = 0f; // 켤 때만 0→1
        }

        // ★ 이 패널의 페이드만 중지 후 재시작
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

        // 코루틴 핸들 정리
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

    // === 내부: 모달 LIFO ===
    private void PushModalInternal(string key)
    {
        if (!_panelMap.TryGetValue(key, out var p) || p.root == null) return;

        // 중복 제거 후 맨 위로
        int idx = _modalOrder.IndexOf(key);
        if (idx >= 0) _modalOrder.RemoveAt(idx);
        _modalOrder.Add(key);

        // 켜기 (모달도 페이드 쓰고 싶으면 CanvasGroup 활용)
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
        // 최상단만 입력/Raycast, 정렬은 baseSorting + depth*10
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

    // CanvasGroup 보장
    private static CanvasGroup EnsureCanvasGroup(GameObject go)
    {
        var cg = go.GetComponent<CanvasGroup>();
        return cg != null ? cg : go.AddComponent<CanvasGroup>();
    }

    public void ResetRuntime()
    {
        // 게임오버 닫고, 게임 패널 열기
        SetPanel("GameOver", false);
        SetPanel("Game", true);
    }
}

// 패널 토글 이벤트
public readonly struct PanelToggle
{
    public readonly string key; public readonly bool on;
    public PanelToggle(string key, bool on) { this.key = key; this.on = on; }
}
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
        public string fallbackKey = null;    // 닫힐 때 자동으로 켜줄 패널
    }

    [Header("HUD")]
    [SerializeField] private TMP_Text _scoreText;
    [SerializeField] private TMP_Text _comboText;
    [SerializeField] private TMP_Text _hudBestText;
    [SerializeField] private TMP_Text _gameOverTotalText;
    [SerializeField] private TMP_Text _gameOverBestText;

    [Header("Panels")]
    [SerializeField] private List<PanelEntry> _panels = new();

    [Header("Fade")]
    [SerializeField] CanvasGroup mainGroup;   // 메인 루트 그룹(강제 원복용)
    [SerializeField] Image dimOverlay;        // 모달 DIM

    private readonly Dictionary<string, PanelEntry> _panelMap = new();
    private readonly List<string> _modalOrder = new();

    // 패널별 페이드 코루틴 관리
    private readonly Dictionary<string, Coroutine> _fadeJobs = new();
    // DIM 페이드 코루틴
    private Coroutine _dimJob;

    // HUD state
    private int _lastHighScore = 0;

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

                // 모달은 CanvasGroup 보장 (레이캐스트 제어용)
                var cg = EnsureCanvasGroup(p.root);
                cg.blocksRaycasts = false;
                cg.interactable = false;
            }

            // 일반 패널의 페이드용 CanvasGroup 보장
            if (p.useCanvasGroup && !p.isModal)
                _ = EnsureCanvasGroup(p.root);
        }

        // DIM 초기화
        if (dimOverlay)
        {
            var c = dimOverlay.color; c.a = 0f;
            dimOverlay.color = c;
            dimOverlay.enabled = false;
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
        foreach (var kv in _panelMap)
            Debug.Log($"[UI] Map: {kv.Key} -> root={kv.Value.root?.name}");
    }

    public void PostInit()
    {
        // HUD 바인딩 (Sticky 즉시 재생)
        _bus.Subscribe<ScoreChanged>(e =>
        {
            if (_scoreText) _scoreText.text = FormatScore(e.value);
        }, replaySticky: true);

        _bus.Subscribe<ComboChanged>(e =>
        {
            if (_comboText) _comboText.text = $"x{e.value}";
        }, replaySticky: true);

        _bus.Subscribe<GameDataChanged>(e =>
        {
            _lastHighScore = e.data.highScore;
            if (_hudBestText) _hudBestText.text = $"Best: {_lastHighScore:#,0}";
            Debug.Log($"[UI] Best HUD update -> {_lastHighScore}");
        }, replaySticky: true);

        _bus.Subscribe<PlayerDowned>(e =>
        {
            if (_gameOverTotalText) _gameOverTotalText.text = $"TotalScore : {FormatScore(e.score)}";
            int best = Mathf.Max(e.score, _lastHighScore);
            if (_gameOverBestText) _gameOverBestText.text = $"Best : {FormatScore(best)}";
            SetPanel("Revive", true);
        }, replaySticky: false);

        // 광고 성공 시 모달 닫기
        _bus.Subscribe<ContinueGranted>(_ =>
        {
            SetPanel("Revive", false);
            SetPanel("GameOver", false);
        }, replaySticky: false);

        // 패널 토글 이벤트 구독 (Sticky 재생 켜둠)
        _bus.Subscribe<PanelToggle>(OnPanelToggle, replaySticky: true);

        _bus.Subscribe<GameResetRequest>(OnGameResetRequest, replaySticky: false);
    }

    private void OnPanelToggle(PanelToggle e) => SetPanel(e.key, e.on);

    // === 외부 API ===
    public void SetPanel(string key, bool on)
    {
        Debug.Log($"SetPanel {key} -> {on}");
        if (!_panelMap.TryGetValue(key, out var p) || p.root == null) return;

        // 모달: 스택 기반 처리
        if (p.isModal)
        {
            if (on) PushModalInternal(key, on);
            else PopModalInternal(key);
            return;
        }

        // 일반 패널
        if (!p.useCanvasGroup)
        {
            p.root.SetActive(on);
            return;
        }

        if (!on && !p.root.activeSelf) return;

        var cg = EnsureCanvasGroup(p.root);

        if (on)
            ResetChildCanvasGroupsAlpha(p.root);

        if (!p.root.activeSelf)
        {
            p.root.SetActive(true);
            if (on) cg.alpha = 0f;
        }

        StopFade(key);
        _fadeJobs[key] = StartCoroutine(FadeRoutine(cg, on ? 1f : 0f, 0.15f, on, key));
        if (on) StartCoroutine(FailsafeOpenSnap(key, p.root, cg));
    }
    private IEnumerator FadeRoutine(CanvasGroup cg, float target, float dur, bool finalActive, string key)
    {
        // 페이드 중 입력 막기
        cg.blocksRaycasts = false;
        cg.interactable = false;

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
        if (!finalActive) cg.gameObject.SetActive(false);

        _fadeJobs.Remove(key);
    }
    private IEnumerator FailsafeOpenSnap(string key, GameObject root, CanvasGroup cg)
    {
        yield return null; // 다음 프레임
        if (!root || !root.activeInHierarchy) yield break;

        // 여전히 0(또는 거의 0)이고 입력도 막혀 있으면 강제 정상화
        if (cg && cg.alpha <= 0.01f && !cg.interactable)
            StopFadeAndSnap(key, cg, true);
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

    // 메인 UI 강제 원복(DIM/메인 그룹)
    public void ForceMainUIClean()
    {
        if (dimOverlay)
        {
            if (_dimJob != null) { StopCoroutine(_dimJob); _dimJob = null; }
            var c = dimOverlay.color; c.a = 0f;
            dimOverlay.color = c;
            dimOverlay.enabled = false;
        }
        if (mainGroup)
        {
            mainGroup.alpha = 1f;
            mainGroup.interactable = true;
            mainGroup.blocksRaycasts = true;
        }
    }

    // === 내부: 모달 LIFO ===
    private void PushModalInternal(string key, bool on)
    {
        if (!_panelMap.TryGetValue(key, out var p) || p.root == null) return;

        // 중복 제거 후 맨 위로
        int idx = _modalOrder.IndexOf(key);
        if (idx >= 0) _modalOrder.RemoveAt(idx);
        _modalOrder.Add(key);

        // 켜기
        if (p.useCanvasGroup)
        {
            var cg = EnsureCanvasGroup(p.root);
            ResetChildCanvasGroupsAlpha(p.root);
            if (!p.root.activeSelf) { p.root.SetActive(true); cg.alpha = 0f; }
            StopFade(key);
            _fadeJobs[key] = StartCoroutine(FadeRoutine(cg, 1f, 0.12f, true, key));
        }
        else p.root.SetActive(true);

        ReorderModals();
        UpdateDimByStack(); // DIM 업데이트
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

            if (!string.IsNullOrEmpty(p.fallbackKey))
            {
                SetPanel(p.fallbackKey, true);
            }
        }

        ReorderModals();
        UpdateDimByStack(); // DIM 업데이트
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
            bool top = i == _modalOrder.Count - 1;
            cg.blocksRaycasts = top;
            cg.interactable = top;
        }
    }

    private void StopFade(string key)
    {
        if (_fadeJobs.TryGetValue(key, out var job) && job != null)
            StopCoroutine(job);
        _fadeJobs.Remove(key);
    }

    private void StopFadeAndSnap(string key, CanvasGroup cg, bool on)
    {
        StopFade(key);
        if (!cg) return;

        cg.alpha = on ? 1f : 0f;
        cg.blocksRaycasts = on;
        cg.interactable = on;
        if (!on) cg.gameObject.SetActive(false);
    }

    // DIM 페이드/설정
    private void FadeDim(float target, float dur = 0.12f)
    {
        if (!dimOverlay) return;
        if (_dimJob != null) { StopCoroutine(_dimJob); _dimJob = null; }
        if (dur <= 0f) { SetDim(target); return; }
        _dimJob = StartCoroutine(CoDim(target, dur));
    }

    private IEnumerator CoDim(float target, float dur)
    {
        dimOverlay.enabled = true;
        float t = 0f, start = dimOverlay.color.a;
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            var c = dimOverlay.color; c.a = Mathf.Lerp(start, target, t / dur);
            dimOverlay.color = c;
            yield return null;
        }
        SetDim(target);
    }

    private void SetDim(float a)
    {
        var c = dimOverlay.color; c.a = a; dimOverlay.color = c;
        dimOverlay.enabled = a > 0f;
    }

    private void UpdateDimByStack()
    {
        // 모달이 하나라도 있으면 dim 0.6, 없으면 0
        FadeDim(_modalOrder.Count > 0 ? 0.6f : 0f);
    }

    // CanvasGroup 보장
    private static CanvasGroup EnsureCanvasGroup(GameObject go)
    {
        var cg = go.GetComponent<CanvasGroup>();
        return cg != null ? cg : go.AddComponent<CanvasGroup>();
    }

    // 점수 포맷
    private static string FormatScore(int v) => v.ToString("#,0");

    // === 리셋 흐름 ===
    public void ResetRuntime()
    {
        // 모달/Dim/메인 강제 정리 후 기본 패널 상태로
        ForceCloseAllModals();
        SetPanel("GameOver", false);
        SetPanel("Game", true);

        NormalizeAllPanelsAlpha();
        ForceMainUIClean();
    }

    private void OnGameResetRequest(GameResetRequest req)
    {
        // 1) 보장: 시간 재개
        Time.timeScale = 1f;

        // 모달/Dim/메인 강제 정리 (옵션 등 잔여 제거)
        ForceCloseAllModals();

        // 2) 엔진 쪽 리셋 이벤트
        _bus.PublishImmediate(new GameResetting());
        _bus.PublishImmediate(new ComboChanged(0));
        _bus.PublishImmediate(new ScoreChanged(0));

        // 3) UI 전환(원자적)
        SetPanel("GameOver", false);
        if (req.targetPanel == "Game")
        {
            SetPanel("Main", false);
            SetPanel("Game", true);
        }
        else // "Main"
        {
            SetPanel("Game", false);
            SetPanel("Main", true);
        }

        NormalizeAllPanelsAlpha();
        ForceMainUIClean();

        // 4) 완료 알림
        _bus.PublishImmediate(new GameResetDone());
    }

    // 모든 모달 강제 종료 + DIM/메인 원복
    private void ForceCloseAllModals()
    {
        while (_modalOrder.Count > 0)
        {
            var key = _modalOrder[^1];
            _modalOrder.RemoveAt(_modalOrder.Count - 1);

            if (_panelMap.TryGetValue(key, out var p) && p.root)
            {
                var cg = EnsureCanvasGroup(p.root);
                StopFadeAndSnap(key, cg, false);
                ResetChildCanvasGroupsAlpha(p.root);
            }
        }

        UpdateDimByStack();
        ForceMainUIClean();
    }
    private void NormalizeAllPanelsAlpha()
    {
        foreach (var kv in _panelMap)
        {
            var p = kv.Value; if (!p.root) continue;
            var cg = EnsureCanvasGroup(p.root);
            bool on = p.root.activeSelf;

            // 루트 확정
            cg.alpha = on ? 1f : 0f;
            cg.interactable = on;
            cg.blocksRaycasts = on;

            // 하위 확정
            ResetChildCanvasGroupsAlpha(p.root);
        }
    }
    private void ResetChildCanvasGroupsAlpha(GameObject root)
    {
        if (!root) return;
        var groups = root.GetComponentsInChildren<CanvasGroup>(includeInactive: true);
        var rootCg = root.GetComponent<CanvasGroup>();
        foreach (var g in groups)
        {
            if (!g || g == rootCg) continue;                          // 루트 제외
            if (dimOverlay && g.gameObject == dimOverlay.gameObject) continue; // DIM 제외
            g.alpha = 1f;   // 시각만 1로 정규화 (입력은 루트에서 관리)
        }
    }
    public bool TryGetPanelRoot(string key, out GameObject root)
    {
        root = null;
        if (!_panelMap.TryGetValue(key, out var p) || p.root == null) return false;
        root = p.root;
        return true;
    }
}
public readonly struct PanelToggle
{
    public readonly string key; public readonly bool on;
    public PanelToggle(string key, bool on) { this.key = key; this.on = on; }
}

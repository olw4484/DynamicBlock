using _00.WorkSpace.GIL.Scripts.Managers;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
[AddComponentMenu("Game/UIManager")]
public class UIManager : MonoBehaviour, IManager, IRuntimeReset
{
    // Treat these as "result" panels that must retry after ReviveLatch
    static readonly HashSet<string> __ResultKeys =
        new() { "GameOver", "NewRecord", "Adventure_Result" };

    private IEnumerator CoRetryOpenAfterLatch(string key)
    {
        float deadline = Time.realtimeSinceStartup + 6f;
        while (ReviveLatch.Active && Time.realtimeSinceStartup < deadline) yield return null;
        if (!ReviveLatch.Active)
        {
            SetPanel(key, true, ignoreDelay: true);
        }
    }


    [System.Serializable]
    public class PanelEntry
    {
        public string key;
        public GameObject root;
        public bool defaultActive = false;
        public bool useCanvasGroup = true;
        public bool isModal = false;
        public bool closeOnEscape = true;
        public int baseSorting = 1000;
        public string fallbackKey = null;
        public float closeDelaySeconds = 1f;

        public bool restoreScaleOnClose = true;
    }

    [Header("HUD")]
    [SerializeField] private TMP_Text _scoreText;
    [SerializeField] private TMP_Text _hudBestText;

    [Header("GameOver Texts (All Canvases)")]
    [SerializeField] private TMP_Text[] _goTotalTexts;
    [SerializeField] private TMP_Text[] _goBestTexts;

    [Header("Panels")]
    [SerializeField] private List<PanelEntry> _panels = new();

    [Header("Fade")]
    [SerializeField] private CanvasGroup mainGroup;
    [SerializeField] private Image dimOverlay;

    [Header("Combo UI")]
    [SerializeField] private GameObject _rainbowIcon;
    [SerializeField] private CanvasGroup _comboGroup;
    [SerializeField] private TMP_Text _comboText;
    [SerializeField] private float _comboHoldTime = 0.8f;
    [SerializeField] private float _comboFadeTime = 0.2f;
    [SerializeField] private int _comboVisibleThreshold = 2;
    [SerializeField] private int[] _comboTierStarts = new int[] { 0, 2, 3, 5, 8 };

    [Header("Revive Settings")]
    [SerializeField] private float _reviveDelaySec = 1.0f;
    private Coroutine _reviveDelayJob;
    [SerializeField] private CanvasGroup _preReviveBlocker;

    [Header("Revive Limits")]
    [SerializeField] private bool _reviveOncePerRun = true;
    private bool _reviveConsumedThisRun = false;

    [Header("Result Suppress After Revive")]
    [SerializeField] private float _resultSuppressAfterReviveSec = 1.0f;
    private float _resultSuppressUntil = -999f;

    private Coroutine _comboFadeJob;
    private readonly Dictionary<string, PanelEntry> _panelMap = new();
    private readonly List<string> _modalOrder = new();

    private readonly Dictionary<string, Coroutine> _fadeJobs = new();
    private readonly Dictionary<string, Coroutine> _closeDelayJobs = new();
    private Coroutine _dimJob;

    private int _lastBestClassic = 0;
    private int _lastBestAdventure = 0;
    private int _pendingDownedScore;

    private int _lastLoggedClassicBest = -1;
    private int _bestShown = -1;
    private EventQueue _bus;
    private GameManager _game;

    private bool _runStartLogged = false;
    public int Order => 100;

    readonly Dictionary<RectTransform, Vector3> _initScale = new();

    // Result stick / Re-assert
    private readonly Dictionary<string, float> _resultStickUntil = new();
    [SerializeField] private float _resultStickSec = 0.45f;
    private string _lastResultKey;

    public void SetDependencies(EventQueue bus, GameManager game) { _bus = bus; _game = game; }

    private static void SetAll(TMP_Text[] arr, string value)
    {
        if (arr == null) return;
        for (int i = 0; i < arr.Length; i++)
            if (arr[i]) arr[i].text = value;
    }
    private static string FormatScore(int v) => v.ToString("#0");

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
                if (!p.root.TryGetComponent<GraphicRaycaster>(out _))
                    p.root.AddComponent<GraphicRaycaster>();

                var cg = EnsureCanvasGroup(p.root);
                cg.blocksRaycasts = false;
                cg.interactable = false;
            }

            foreach (var a in p.root.GetComponentsInChildren<Animator>(true))
            {
                a.updateMode = AnimatorUpdateMode.UnscaledTime;
                a.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                a.keepAnimatorStateOnDisable = false;
            }

            if (p.useCanvasGroup && !p.isModal)
                _ = EnsureCanvasGroup(p.root);

            CaptureInitialScales(p.root);
        }

        if (dimOverlay)
        {
            var c = dimOverlay.color; c.a = 0f;
            dimOverlay.color = c;
            dimOverlay.enabled = false;
        }
    }

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
        _bus.Subscribe<ScoreChanged>(e =>
        {
            if (_scoreText) _scoreText.text = FormatScore(e.value);
            UpdateBestHUD();
        }, replaySticky: true);

        _bus.Subscribe<ComboChanged>(e =>
        {
            int tier = MapToTier(e.value);
            if (tier <= 0) { HideComboImmediate(); return; }
            if (_comboText) _comboText.SetText($"x{e.value - 1}");
            ApplyComboTierVisuals(tier);
            ShowComboStartHold();
        }, replaySticky: true);

        _bus.Subscribe<GameDataChanged>(e =>
        {
            _lastBestClassic = e.data.classicHighScore;
            _lastBestAdventure = e.data.adventureHighScore;
            UpdateBestHUD();
        }, replaySticky: true);

        // ReviveRouter가 PlayerDowned를 처리하므로 UIManager는 더 이상 관여하지 않음

        _bus.Subscribe<GameOverConfirmed>(OnGameOverConfirmed_Classic, replaySticky: false);

        _bus.Subscribe<ContinueGranted>(_ =>
        {
            GameOverGate.Reset("ContinueGranted");
            GameOverUtil.ResetAll("continue_granted");
        
            CancelReviveDelay();
            AdPauseGuard.OnAdClosedOrFailed();
            AdStateProbe.IsFullscreenShowing = false;
            AdStateProbe.IsRevivePending = false;
        
            Time.timeScale = 1f;
            _bus?.PublishImmediate(new InputLock(false, "ContinueGranted"));
        
            SetPanel("Revive", false, true);
            SetPanel("GameOver", false, true);
            SetPanel("NewRecord", false, true);
        
            ForceCloseAllModals();
            NormalizeAllPanelsAlpha();
            ForceMainUIClean();
        
            // 결과 억제/그레이스
            _resultSuppressUntil = Time.realtimeSinceStartup + _resultSuppressAfterReviveSec;
            UIStateProbe.ArmResultGuard(_resultSuppressAfterReviveSec);
            UIStateProbe.ArmReviveGrace(2.0f);
            StartCoroutine(CoReviveGrace(2.0f));
        
            StartCoroutine(CoPostContinueSanity());
        }, replaySticky: false);

        _bus.Subscribe<RevivePerformed>(_ =>
        {
            _reviveConsumedThisRun = true;
            GameOverGate.Reset("RevivePerformed");
            Debug.Log("[UI] RevivePerformed → reviveConsumed = true");
        }, replaySticky: false);
        
        _bus.Subscribe<PanelToggle>(OnPanelToggle, replaySticky: true);
        _bus.Subscribe<GameResetRequest>(OnGameResetRequest, replaySticky: false);

        var data = Game.Save?.Data;
        if (data != null)
        {
            _lastBestClassic = data.classicHighScore;
            _lastBestAdventure = data.adventureHighScore;
            UpdateBestHUD();
        }
        _bus.Subscribe<GameResetDone>(_ => ResetReviveFlags("GameResetDone"), replaySticky: false);

        _bus.Subscribe<AdFinished>(_ => { StartCoroutine(CoReassertResultAfterAd()); }, replaySticky: false);

        _bus.Subscribe<ResultDelay>(e =>
        {
            _resultSuppressUntil = Time.realtimeSinceStartup + Mathf.Max(0.05f, e.seconds);
            UIStateProbe.ArmResultGuard(e.seconds);
            Debug.Log($"[UI] ResultDelay armed: {e.seconds}s");
        }, replaySticky: false);
    }

    private void OnPanelToggle(PanelToggle e)
    {
        SetPanel(e.key, e.on);
        if (e.key == "Game" && e.on) ResetReviveFlags("PanelToggle(Game on)");
    }

    private void UpdateBestHUD()
    {
        var mm = _00.WorkSpace.GIL.Scripts.Managers.MapManager.Instance;
        var mode = mm?.CurrentMode ?? GameMode.Classic;

        int best = (mode == GameMode.Adventure) ? _lastBestAdventure : _lastBestClassic;
        int cur = ScoreManager.Instance ? ScoreManager.Instance.Score : 0;

        int display = (mode == GameMode.Classic) ? Mathf.Max(best, cur) : best;
        if (display == _bestShown) return;
        _bestShown = display;

        if (_hudBestText) _hudBestText.text = $"{display:#0}";
    }

    public void SetPanel(string key, bool on, bool ignoreDelay = false)
    {
        // 1) 스틱 윈도우, ReviveLatch 가드(현재 코드 유지)
        if (!on && (key == "GameOver" || key == "NewRecord" || key == "Adventure_Result"))
        {
            if (_resultStickUntil.TryGetValue(key, out var until) &&
                Time.realtimeSinceStartup < until)
            {
                Debug.Log($"[UI] Ignore close of {key} during stick window");
                return;
            }
        }
        if (on && (key == "GameOver" || key == "NewRecord" || key == "Adventure_Result"))
        {
            _resultStickUntil[key] = Time.realtimeSinceStartup + _resultStickSec;
            _lastResultKey = key;
        }
        if (__ResultKeys.Contains(key) && on && ReviveLatch.Active)
        {
            var mm = _00.WorkSpace.GIL.Scripts.Managers.MapManager.Instance;
            if (mm != null && mm.CurrentMode == GameMode.Adventure)
            {
                Debug.Log("[UI] Ignore ReviveLatch for Adventure result");
            }
            else
            {
                Debug.Log($"[UI] Block '{key}' open while ReviveLatch active — will retry");
                StartCoroutine(CoRetryOpenAfterLatch(key));
                UpdateProbes();
                return;
            }
        }

        Debug.Log($"SetPanel {key} -> {on}");

        if (key == "Revive" && on && _reviveOncePerRun && _reviveConsumedThisRun)
        {
            Debug.Log("[UI] Block Revive open (revive consumed) → publish GiveUpRequest");
            _bus?.PublishImmediate(new GiveUpRequest("revive_consumed"));
            return;
        }

        // 2) p 확보 (이전과 동일)
        if (!_panelMap.TryGetValue(key, out var p) || p.root == null)
        {
            Debug.LogError($"[UI] SetPanel: unknown panel key='{key}' or root missing. Check mapping.");
            return;
        }

        // 3) 공통 준비
        CancelCloseDelay(key);
        StopFade(key);

        if (on && key == "Adventure_Result")
        {
            EnableAllCanvasRecursively(p.root);
            EnforceSingleScaler(p.root);
            ForceCanvasGroupVisible(p.root);

            var rt = p.root.GetComponent<RectTransform>();
            if (rt)
            {
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
                rt.localScale = Vector3.one;
            }
            Canvas.ForceUpdateCanvases();
            DumpCanvasTree("Adventure_Result AFTER FIX", p.root);
            LogPanelState(key, p.root);
        }


        // 4) 모달 패널 처리 (on/off 모두 커버)
        if (p.isModal)
        {
            if (on)
            {
                PushModalInternal(key, on);
            }
            else
            {
                if (ignoreDelay) ClosePanelImmediate(key);
                else if (p.closeDelaySeconds > 0f && p.root.activeSelf) StartCloseDelayForModal(key, p);
                else PopModalInternal(key);
            }
            UpdateProbes();
            return;
        }

        // 5) CanvasGroup 안 쓰는 패널 (on/off 모두 커버)
        if (!p.useCanvasGroup)
        {
            if (!on && !ignoreDelay && p.closeDelaySeconds > 0f && p.root.activeSelf)
            {
                StartCloseDelay(key, p);
                return;
            }
            p.root.SetActive(on);
            UpdateProbes();
            return;
        }

        // 6) CanvasGroup 쓰는 패널 (on/off 분기)
        var cg = EnsureCanvasGroup(p.root);

        if (!on)
        {
            // 닫기 경로
            cg.blocksRaycasts = false;
            cg.interactable = false;
            cg.alpha = 0f;

            float timeout = (!ignoreDelay && p.root.activeSelf)
                ? Mathf.Max(0.05f, p.closeDelaySeconds)
                : 0.05f;

            bool useCanvasToggle = true;
            _closeDelayJobs[key] = StartCoroutine(GraceClosePanel(key, p.root, timeout, useCanvasToggle));
            return;
        }

        // === 여기부터 열기(on) 경로 ===
        EnsureAllCanvasEnabled(p.root);
        ResetChildCanvasGroupsAlpha(p.root);
        if (!p.root.activeSelf) { p.root.SetActive(true); cg.alpha = 0f; }
        cg.blocksRaycasts = true;
        cg.interactable = true;

        StopFade(key);
        _fadeJobs[key] = StartCoroutine(FadeRoutine(key, p, cg, 1f, 0.15f, true));

        if (key == "Revive") Game.Ads?.Refresh();
        UpdateProbes();
    }

    private void StartCloseDelay(string key, PanelEntry p)
    {
        if (_closeDelayJobs.TryGetValue(key, out var job) && job != null)
            StopCoroutine(job);

        _closeDelayJobs[key] = StartCoroutine(CoCloseAfterDelay(key, p));
    }

    static bool Approximately(Vector3 a, Vector3 b)
    {
        const float eps = 0.0001f;
        return Mathf.Abs(a.x - b.x) < eps && Mathf.Abs(a.y - b.y) < eps && Mathf.Abs(a.z - b.z) < eps;
    }
    void CaptureInitialScales(GameObject root)
    {
        if (!root) return;
        foreach (var rt in root.GetComponentsInChildren<RectTransform>(includeInactive: true))
            if (rt && !_initScale.ContainsKey(rt))
                _initScale[rt] = rt.localScale;
    }

    bool AreChildScalesAtInitial(GameObject root)
    {
        if (!root) return true;
        foreach (var rt in root.GetComponentsInChildren<RectTransform>(includeInactive: true))
            if (rt && _initScale.TryGetValue(rt, out var s0) && !Approximately(rt.localScale, s0))
                return false;
        return true;
    }

    void RestoreInitialScales(GameObject root)
    {
        if (!root) return;
        foreach (var rt in root.GetComponentsInChildren<RectTransform>(includeInactive: true))
            if (rt && _initScale.TryGetValue(rt, out var s0))
                rt.localScale = s0;
    }

    static bool AllChildScalesAreOne(GameObject root)
    {
        if (!root) return true;
        var rts = root.GetComponentsInChildren<RectTransform>(true);
        for (int i = 0; i < rts.Length; i++)
        {
            var s = rts[i].localScale;
            if (Mathf.Abs(s.x - 1f) > 0.001f || Mathf.Abs(s.y - 1f) > 0.001f || Mathf.Abs(s.z - 1f) > 0.001f)
                return false;
        }
        return true;
    }


    private IEnumerator CoCloseAfterDelay(string key, PanelEntry p)
    {
        var cg = EnsureCanvasGroup(p.root);
        cg.blocksRaycasts = false;
        cg.interactable = false;

        // 지연 동안 스케일 복귀 감시 (unscaled 기준)
        float wait = Mathf.Max(0f, p.closeDelaySeconds);
        float t = 0f;
        while (t < wait)
        {
            if (!_closeDelayJobs.ContainsKey(key) || !p.root.activeSelf)
                yield break;

            // 스케일이 모두 1.0 근처로 돌아왔으면 즉시 닫기
            if (AreChildScalesAtInitial(p.root)) break;

            t += Time.unscaledDeltaTime;
            yield return null;
        }

        if (!_closeDelayJobs.ContainsKey(key) || !p.root) yield break;

        StopFade(key);
        if (p.restoreScaleOnClose && !AreChildScalesAtInitial(p.root))
            RestoreInitialScales(p.root);

        yield return GraceClosePanel(key, p.root, 0.0f, true);
        _closeDelayJobs.Remove(key);
    }

    private IEnumerator FadeRoutine(string key, PanelEntry p, CanvasGroup cg, float target, float dur, bool finalActive)
    {
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

        if (!finalActive)
        {
            if (p.restoreScaleOnClose)
                RestoreInitialScales(p.root);
            cg.gameObject.SetActive(false);
        }

        _fadeJobs.Remove(key);
    }

    private IEnumerator FadeOutCombo(CanvasGroup cg, float hold, float fade)
    {
        cg.alpha = 1f;

        float t = 0f;
        while (t < hold) { t += Time.unscaledDeltaTime; yield return null; }

        float start = cg.alpha;
        t = 0f;
        while (t < fade)
        {
            t += Time.unscaledDeltaTime;
            cg.alpha = Mathf.Lerp(start, 0f, t / fade);
            yield return null;
        }
        cg.alpha = 0f;
        cg.gameObject.SetActive(false);
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

    private void PushModalInternal(string key, bool on)
    {
        if (!_panelMap.TryGetValue(key, out var p) || p.root == null) return;

        int idx = _modalOrder.IndexOf(key);
        if (idx >= 0) _modalOrder.RemoveAt(idx);
        _modalOrder.Add(key);

        if (p.useCanvasGroup)
        {
            var cg = EnsureCanvasGroup(p.root);
            ResetChildCanvasGroupsAlpha(p.root);
            if (!p.root.activeSelf) { p.root.SetActive(true); cg.alpha = 0f; }
            StopFade(key);
            _fadeJobs[key] = StartCoroutine(FadeRoutine(key, p, cg, 1f, 0.12f, true));
        }
        else p.root.SetActive(true);

        ReorderModals();
        UpdateDimByStack();
        UpdateProbes();
    }

    private void PopModalInternal(string key)
    {
        Debug.Log($"[UI] PopModalInternal: {key}");

        int idx = _modalOrder.LastIndexOf(key);
        if (idx < 0) return;

        _modalOrder.RemoveAt(idx);

        if (_panelMap.TryGetValue(key, out var p) && p.root)
        {
            if (p.useCanvasGroup)
            {
                var cg = EnsureCanvasGroup(p.root);
                StopFade(key);
                _fadeJobs[key] = StartCoroutine(FadeRoutine(key, p, cg, 0f, 0.12f, false));
            }
            else p.root.SetActive(false);

            if (!string.IsNullOrEmpty(p.fallbackKey))
                SetPanel(p.fallbackKey, true);
        }

        ReorderModals();
        UpdateDimByStack();
        UpdateProbes();
    }

    private void ReorderModals()
    {
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
        FadeDim(_modalOrder.Count > 0 ? 0.6f : 0f);
    }

    private static CanvasGroup EnsureCanvasGroup(GameObject go)
    {
        var cg = go.GetComponent<CanvasGroup>();
        return cg != null ? cg : go.AddComponent<CanvasGroup>();
    }

    public void ResetRuntime()
    {
        _reviveConsumedThisRun = false;
        ForceCloseAllModals();
        SetPanel("GameOver", false);
        SetPanel("NewRecord", false);
        SetPanel("Game", true);

        NormalizeAllPanelsAlpha();
        ForceMainUIClean();
    }

    private void OnGameResetRequest(GameResetRequest req)
    {
        _reviveConsumedThisRun = false;
        AdStateProbe.IsRevivePending = false;
        ReviveGate.Disarm();
        UIStateProbe.IsResultOpen = false;
        UIStateProbe.IsReviveOpen = false;
    
        CancelReviveDelay();
    
        Time.timeScale = 1f;
        Game.Audio.StopContinueTimeCheckSE();
        Game.Audio.StopAllSe();
        Game.Audio.ResumeAll();
        ForceCloseAllModals();
    
        bool toGame = req.targetPanel == "Game";
        string onKey = toGame ? "Game" : "Main";
        string offKey = toGame ? "Main" : "Game";
    
        if (!toGame)
        {
            _bus.PublishImmediate(new GameResetting());
            _bus.PublishImmediate(new ComboChanged(0));
            _bus.PublishImmediate(new ScoreChanged(0));
        }
    
        ClosePanelImmediate("GameOver");
        ClosePanelImmediate("NewRecord");
        SetPanel(offKey, false, ignoreDelay: true);

        SetPanel(onKey, true, ignoreDelay: true);
        if (TryGetPanelRoot(onKey, out var onRoot))
            EnforceSingleScaler(onRoot);
    
        var onEvt = new PanelToggle(onKey, true);
        _bus.PublishSticky(onEvt, alsoEnqueue: false);
        _bus.PublishImmediate(onEvt);
    
        var mm = _00.WorkSpace.GIL.Scripts.Managers.MapManager.Instance;
        bool isAdventure = mm?.CurrentMode == GameMode.Adventure;
        int stageIndex = mm?.CurrentMapData?.mapIndex ?? 0;
    
        // 모드별 리바이브 정책 주입
        var mode = mm?.CurrentMode ?? GameMode.Classic;
        ReviveRouter.I?.SetPolicyFromMode(mode);
    
        // 게임 화면으로 진입할 때 라우터 런 상태 리셋
        if (toGame)
            ReviveRouter.I?.ResetRun();
    
        if (toGame && !_runStartLogged)
        {
            AnalyticsManager.Instance?.GameStartLog(!isAdventure, stageIndex);
            _runStartLogged = true;
        }
        if (!toGame) _runStartLogged = false;
    
        if (dimOverlay)
        {
            if (_dimJob != null) { StopCoroutine(_dimJob); _dimJob = null; }
            var c = dimOverlay.color; c.a = 0f; dimOverlay.color = c;
            dimOverlay.enabled = false;
        }
    
        if (TryGetPanelRoot("Main", out var mainRoot) && !mainRoot.activeSelf)
        {
            if (mainGroup)
            {
                mainGroup.alpha = 0f;
                mainGroup.interactable = false;
                mainGroup.blocksRaycasts = false;
            }
        }
        else
        {
            if (mainGroup)
            {
                mainGroup.alpha = 1f;
                mainGroup.interactable = true;
                mainGroup.blocksRaycasts = true;
            }
        }
    
        _bus.PublishImmediate(new GameResetDone());
    }


    private void ForceCloseAllModals()
    {
        while (_modalOrder.Count > 0)
        {
            var key = _modalOrder[^1];
            _modalOrder.RemoveAt(_modalOrder.Count - 1);

            if (_panelMap.TryGetValue(key, out var p) && p.root)
            {
                if (_closeDelayJobs.TryGetValue(key, out var job) && job != null)
                {
                    StopCoroutine(job);
                    _closeDelayJobs.Remove(key);
                }

                var cg = EnsureCanvasGroup(p.root);
                StopFade(key);
                // ���� ���� ����
                if (p.restoreScaleOnClose) RestoreInitialScales(p.root);
                cg.alpha = 0f;
                cg.blocksRaycasts = false;
                cg.interactable = false;
                p.root.SetActive(false);
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

            cg.alpha = on ? 1f : 0f;
            cg.interactable = on;
            cg.blocksRaycasts = on;

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
            if (!g || g == rootCg) continue;
            if (dimOverlay && g.gameObject == dimOverlay.gameObject) continue;
            g.alpha = 1f;
        }
    }

    public bool TryGetPanelRoot(string key, out GameObject root)
    {
        root = null;
        if (!_panelMap.TryGetValue(key, out var p) || p.root == null) return false;
        root = p.root;
        return true;
    }

    void HideComboImmediate()
    {
        if (_comboFadeJob != null) { StopCoroutine(_comboFadeJob); _comboFadeJob = null; }
        if (_rainbowIcon) _rainbowIcon.SetActive(false);
        if (_comboGroup)
        {
            _comboGroup.alpha = 0f;
            _comboGroup.gameObject.SetActive(false);
            _comboGroup.blocksRaycasts = false;
            _comboGroup.interactable = false;
        }
    }

    void ShowComboStartHold()
    {
        if (_rainbowIcon && !_rainbowIcon.activeSelf) _rainbowIcon.SetActive(true);
        if (_comboGroup && !_comboGroup.gameObject.activeSelf) _comboGroup.gameObject.SetActive(true);
        if (_comboGroup) _comboGroup.alpha = 1f;
        if (_comboFadeJob != null) StopCoroutine(_comboFadeJob);
        if (_comboGroup) _comboFadeJob = StartCoroutine(FadeOutCombo(_comboGroup, _comboHoldTime, _comboFadeTime));
    }

    private int MapToTier(int actual)
    {
        if (_comboTierStarts == null || _comboTierStarts.Length == 0) return 0;
        int tier = 0;
        for (int i = 1; i < _comboTierStarts.Length && i < 5; i++)
            if (actual >= _comboTierStarts[i]) tier = i;
        return Mathf.Clamp(tier, 0, 4);
    }

    private void ApplyComboTierVisuals(int tier)
    {
        if (_comboGroup)
        {
            float scale = tier switch { 1 => 1.00f, 2 => 1.05f, 3 => 1.10f, 4 => 1.15f, _ => 1f };
            _comboGroup.transform.localScale = Vector3.one * scale;
        }
    }

    void CancelReviveDelay()
    {
        if (_reviveDelayJob != null) { StopCoroutine(_reviveDelayJob); _reviveDelayJob = null; }
        SetPreReviveBlock(false);
        Time.timeScale = 1f;
        _bus?.PublishImmediate(new InputLock(false, "PreRevive"));
        AdStateProbe.IsRevivePending = false;
        ReviveGate.Disarm();
    }

    void SetPreReviveBlock(bool on)
    {
        if (!_preReviveBlocker) return;

        if (on)
        {
            _preReviveBlocker.gameObject.SetActive(true);
            _preReviveBlocker.alpha = 0f;
            _preReviveBlocker.blocksRaycasts = true;
            _preReviveBlocker.interactable = false;
            _preReviveBlocker.transform.SetAsLastSibling();
        }
        else
        {
            _preReviveBlocker.blocksRaycasts = false;
            _preReviveBlocker.interactable = false;
            _preReviveBlocker.gameObject.SetActive(false);
        }
    }

    IEnumerator Co_OpenReviveAfterDelay(float delay)
    {
        SetPreReviveBlock(true);
        CancelCloseDelay("Revive");
        _bus?.PublishImmediate(new InputLock(true, "PreRevive"));

        Time.timeScale = 0f;
        yield return new WaitForSecondsRealtime(Mathf.Max(0f, delay));

        if (_reviveOncePerRun && _reviveConsumedThisRun)
        {
            OpenResultNowBecauseNoRevive();
            yield break;
        }

        SetPreReviveBlock(false);
        SetPanel("Revive", true, ignoreDelay: true);
        Game.Audio.PlayContinueTimeCheckSE();

        _bus?.PublishImmediate(new InputLock(false, "PreRevive"));

        _reviveDelayJob = null;
    }

    IEnumerator CoPostContinueSanity()
    {
        yield return null;
        Debug.Log($"[Sanity] modals={_modalOrder.Count} dim.enabled={dimOverlay && dimOverlay.enabled}");
        SetPreReviveBlock(false);
    }

    public void ClosePanelImmediate(string key)
    {
        Debug.Log($"[UI] ClosePanelImmediate: {key}");

        if (!_panelMap.TryGetValue(key, out var p) || p.root == null) return;

        if (_closeDelayJobs.TryGetValue(key, out var job) && job != null)
        {
            StopCoroutine(job);
            _closeDelayJobs.Remove(key);
        }
        StopFade(key);

        int idx = _modalOrder.LastIndexOf(key);
        if (idx >= 0) _modalOrder.RemoveAt(idx);
        UpdateDimByStack();

        var cg = EnsureCanvasGroup(p.root);
        if (p.restoreScaleOnClose) RestoreInitialScales(p.root);
        cg.alpha = 0f;
        cg.blocksRaycasts = false;
        cg.interactable = false;
        p.root.SetActive(false);

        UpdateProbes();
    }

    private void StartCloseDelayForModal(string key, PanelEntry p)
    {
        if (_closeDelayJobs.TryGetValue(key, out var job) && job != null)
            StopCoroutine(job);

        _closeDelayJobs[key] = StartCoroutine(CoCloseModalAfterDelay(key, p));
    }

    private IEnumerator CoCloseModalAfterDelay(string key, PanelEntry p)
    {
        int idx = _modalOrder.LastIndexOf(key);
        if (idx >= 0) _modalOrder.RemoveAt(idx);
        UpdateDimByStack();

        var cg = EnsureCanvasGroup(p.root);
        cg.blocksRaycasts = false;
        cg.interactable = false;

        if (p.closeDelaySeconds > 0f)
            yield return new WaitForSecondsRealtime(p.closeDelaySeconds);

        if (!_closeDelayJobs.ContainsKey(key)) yield break;

        StopFade(key);
        if (p.useCanvasGroup)
            _fadeJobs[key] = StartCoroutine(FadeRoutine(key, p, cg, 0f, 0.12f, false));
        else
        {
            if (p.restoreScaleOnClose) RestoreInitialScales(p.root);
            p.root.SetActive(false);
        }

        _closeDelayJobs.Remove(key);
    }

    private void CancelCloseDelay(string key)
    {
        if (_closeDelayJobs.TryGetValue(key, out var job) && job != null)
            StopCoroutine(job);
        _closeDelayJobs.Remove(key);
    }

    public void OpenResultNowBecauseNoRevive()
    {
        AdStateProbe.IsRevivePending = false;
        ReviveGate.Disarm();

        Debug.Log("[UI] OpenResultNowBecauseNoRevive → redirect to GiveUpRequest");
        _bus?.PublishImmediate(new GiveUpRequest("ui_bypass"));
    }

    public bool TryResolvePanelKeyByRoot(GameObject go, out string key)
    {
        var t = go ? go.transform : null;
        while (t)
        {
            foreach (var kv in _panelMap)
            {
                if (kv.Value.root == t.gameObject) { key = kv.Key; return true; }
            }
            t = t.parent;
        }
        key = null;
        return false;
    }

    // === Utilities ===

    private IEnumerator CoReviveGrace(float sec)
    {
        ReviveGate.Arm(sec);
        yield return new WaitForSecondsRealtime(Mathf.Max(0.1f, sec));
    }

    private bool IsPanelActuallyOpen(string key)
    {
        if (!_panelMap.TryGetValue(key, out var p) || p.root == null) return false;

        // GameObject가 켜져있고(계층 반영) + (CanvasGroup를 쓰면 알파 1 근처/인터랙티브)
        bool active = p.root.activeInHierarchy;
        if (!active) return false;

        if (!p.useCanvasGroup) return true;

        var cg = p.root.GetComponent<CanvasGroup>();
        if (!cg) return true; // CG 없으면 on으로 간주
                              // 알파와 Raycast/Interactable로 "실사용 가능" 상태 판정
        return cg.alpha > 0.99f && cg.blocksRaycasts && cg.interactable;
    }

    private void UpdateProbes()
    {
        SanitizeModalStack();

        UIStateProbe.IsReviveOpen = IsPanelActuallyOpen("Revive");
        UIStateProbe.IsResultOpen =
            IsPanelActuallyOpen("GameOver") ||
            IsPanelActuallyOpen("NewRecord") ||
            IsPanelActuallyOpen("Adventure_Result");

        UIStateProbe.IsAnyModalOpen = _modalOrder.Count > 0;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[UI.Probes] resultOpen={UIStateProbe.IsResultOpen} reviveOpen={UIStateProbe.IsReviveOpen} " +
                  $"anyModal={UIStateProbe.IsAnyModalOpen} grace={UIStateProbe.ReviveGraceActive} guard={UIStateProbe.ResultGuardActive}");
#endif
    }

    private IEnumerator GraceClosePanel(string key, GameObject root, float timeout, bool useCanvasToggle)
    {
        if (!root) yield break;
        var cg = root.GetComponent<CanvasGroup>() ?? root.AddComponent<CanvasGroup>();
        cg.blocksRaycasts = false;
        cg.interactable = false;

        float t = 0f;
        while (t < timeout)
        {
            if (cg.interactable || cg.blocksRaycasts)
            {
                _closeDelayJobs.Remove(key);
                yield break;
            }
            if (AreChildScalesAtInitial(root)) break;
            t += Time.unscaledDeltaTime;
            yield return null;
        }

        if (cg.interactable || cg.blocksRaycasts)
        { _closeDelayJobs.Remove(key); yield break; }

        if (!AreChildScalesAtInitial(root)) RestoreInitialScales(root);

        cg.alpha = 0f;
        if (useCanvasToggle && root.TryGetComponent(out Canvas canvas))
            canvas.enabled = false;

        root.SetActive(false);
        _closeDelayJobs.Remove(key);
    }


    static void EnsureAllCanvasEnabled(GameObject root)
    {
        if (!root) return;
        if (root.TryGetComponent<Canvas>(out var cv))
            cv.enabled = true;
    }
    private void OnGameOverConfirmed_Classic(GameOverConfirmed e)
    {
        if (Time.realtimeSinceStartup < _resultSuppressUntil)
        {
            float wait = _resultSuppressUntil - Time.realtimeSinceStartup;
            StartCoroutine(Co_OpenClassicResultAfterDelay(e, wait));
            return;
        }

        if (AdStateProbe.IsRevivePending || ReviveGate.IsArmed || ReviveLatch.Active)
        {
            Debug.Log("[UI] Suppress GameOverConfirmed (router/gate state)");
            GameOverUtil.CancelPending("ui_suppress_router_gate");
            GameOverGate.Reset("ui_suppress_router_gate");
            UpdateProbes();
            return;
        }

        var mm = _00.WorkSpace.GIL.Scripts.Managers.MapManager.Instance;
        bool isAdventure = (mm?.CurrentMode == GameMode.Adventure);

        Debug.Log($"[UI] GameOverConfirmed score={e.score} isNewBest={e.isNewBest} mode={(isAdventure ? "Adventure" : "Classic")}");

        // 공통: 리바이브 패널은 닫아두는 편이 안전
        CancelReviveDelay();
        Game.Audio.StopContinueTimeCheckSE();
        SetPanel("Revive", false, ignoreDelay: true);

        // Adventure면 클래식 결과 로직 금지(즉시 반환)
        if (isAdventure)
        {
            SetPanel("GameOver", false, ignoreDelay: true);
            SetPanel("NewRecord", false, ignoreDelay: true);
            _lastResultKey = null;  // 인터스티셜 후 재오픈 방지
            AnalyticsManager.Instance?.AdventureBestLog(e.score);
            return;
        }

        // 여기부터는 '클래식'만 처리
        int bestNow = _lastBestClassic;
        if (e.isNewBest)
        {
            bestNow = e.score;
            if (_lastLoggedClassicBest != e.score)
            {
                AnalyticsManager.Instance?.ClassicBestLog(e.score);
                _lastLoggedClassicBest = e.score;
            }
            _lastBestClassic = Mathf.Max(_lastBestClassic, e.score);
            UpdateBestHUD();
        }

        SetAll(_goTotalTexts, $"{FormatScore(e.score)}");
        SetAll(_goBestTexts, $"{FormatScore(bestNow)}");

        SetPanel("GameOver", !e.isNewBest);
        SetPanel("NewRecord", e.isNewBest);

        if (Game.Ads?.IsInterstitialReady() == true)
        {
            StartCoroutine(Co_ShowInterstitialAfterResultOpen(e.isNewBest ? "NewRecord" : "GameOver"));
        }
    }

    private void OpenClassicResultNow(GameOverConfirmed e)
    {
        Debug.Log($"[UI] GameOverConfirmed score={e.score} isNewBest={e.isNewBest} mode=Classic");

        CancelReviveDelay();
        Game.Audio.StopContinueTimeCheckSE();
        SetPanel("Revive", false, ignoreDelay: true);

        int bestNow = _lastBestClassic;
        if (e.isNewBest)
        {
            bestNow = e.score;
            if (_lastLoggedClassicBest != e.score)
            {
                AnalyticsManager.Instance?.ClassicBestLog(e.score);
                _lastLoggedClassicBest = e.score;
            }
            _lastBestClassic = Mathf.Max(_lastBestClassic, e.score);
            UpdateBestHUD();
        }

        SetAll(_goTotalTexts, $"{FormatScore(e.score)}");
        SetAll(_goBestTexts, $"{FormatScore(bestNow)}");

        SetPanel("GameOver", !e.isNewBest);
        SetPanel("NewRecord", e.isNewBest);
    }

    private IEnumerator Co_OpenClassicResultAfterDelay(GameOverConfirmed e, float wait)
    {
        yield return new WaitForSecondsRealtime(Mathf.Max(0.05f, wait));

        if (AdStateProbe.IsRevivePending || ReviveGate.IsArmed || ReviveLatch.Active)
            yield break;

        var mm = _00.WorkSpace.GIL.Scripts.Managers.MapManager.Instance;
        if (mm && mm.CurrentMode == GameMode.Adventure) yield break;

        OpenClassicResultNow(e);
    }

    private IEnumerator Co_ShowInterstitialAfterResultOpen(string key)
    {
        // 패널이 "실사용" 상태가 될 때까지 대기
        float t = 0f;
        while (!IsPanelActuallyOpen(key) && t < 1.0f)
        {
            t += Time.unscaledDeltaTime;
            yield return null;
        }
        // 열렸으면 광고
        if (IsPanelActuallyOpen(key))
            AdManager.Instance.ShowInterstitial();
    }

    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    public void LogModalStack(string tag)
    {
        Debug.Log($"[ModalStack:{tag}] count={_modalOrder.Count} keys=[{string.Join(",", _modalOrder)}]");
    }

    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    void DumpCanvasTree(string tag, GameObject root)
    {
        if (!root) { Debug.Log($"[Dump] {tag}: root=null"); return; }

        var cvs = root.GetComponentsInChildren<Canvas>(true);
        var scals = root.GetComponentsInChildren<CanvasScaler>(true);

        int en = 0; for (int i = 0; i < cvs.Length; i++) if (cvs[i].enabled) en++;
        Debug.Log($"[Dump] {tag}: canvases={cvs.Length} (enabled={en}), scalers={scals.Length}");

        foreach (var cv in cvs)
        {
            var rt = cv.GetComponent<RectTransform>();
            Debug.Log(
              $"  - {cv.gameObject.name}  overrideSorting={cv.overrideSorting}  order={cv.sortingOrder}  " +
              $"renderMode={cv.renderMode}  localScale={rt.localScale}  size={rt.rect.size}"
            );
        }
    }
    static void EnforceSingleScaler(GameObject root)
    {
        if (!root) return;

        var scalers = root.GetComponentsInChildren<CanvasScaler>(true);
        if (scalers == null || scalers.Length == 0) return;

        var rootScaler = root.GetComponent<CanvasScaler>();
        if (!rootScaler) rootScaler = scalers[0];

        for (int i = 0; i < scalers.Length; i++)
            scalers[i].enabled = scalers[i] == rootScaler;
    }
    void ResetReviveFlags(string why)
    {
        _reviveConsumedThisRun = false;
        _resultSuppressUntil = -999f;
        UIStateProbe.ReviveGraceActive = false;

        // 혹시 남아있을 수 있는 라우터 상태 정리
        AdStateProbe.IsRevivePending = false;
        ReviveGate.Disarm();

        Debug.Log($"[UI] ResetReviveFlags: {why}");
    }

    private void SanitizeModalStack()
    {
        for (int i = _modalOrder.Count - 1; i >= 0; i--)
        {
            var key = _modalOrder[i];
            if (!_panelMap.TryGetValue(key, out var p) || p.root == null)
            {
                _modalOrder.RemoveAt(i);
                continue;
            }
            if (!p.root.activeInHierarchy)
            {
                _modalOrder.RemoveAt(i);
                continue;
            }

            if (p.useCanvasGroup)
            {
                var cg = p.root.GetComponent<CanvasGroup>();
                if (cg == null || cg.alpha < 0.99f || !cg.blocksRaycasts || !cg.interactable)
                {
                    _modalOrder.RemoveAt(i);
                }
            }
        }
    }
    IEnumerator CoReassertResultAfterAd()
    {
        if (Game.Ads is AdManager m && m.DisableInterstitials) yield break;

        var mm = MapManager.Instance;
        if (_lastResultKey == "Adventure_Result" && (mm?.CurrentMode != GameMode.Adventure))
            yield break;


        if (string.IsNullOrEmpty(_lastResultKey)) yield break;

        var mode = mm?.CurrentMode ?? GameMode.Classic;

        // 1) 모드-결과키 매칭 가드
        bool isAdventureResult = _lastResultKey == "Adventure_Result";
        bool isClassicResult = _lastResultKey == "GameOver" || _lastResultKey == "NewRecord";

        if ((isAdventureResult && mode != GameMode.Adventure) ||
            (isClassicResult && mode == GameMode.Adventure))
            yield break;

        // 2) 모달/게이트/래치 잠잠해질 때까지 잠깐 대기
        float deadline = Time.realtimeSinceStartup + 0.5f;
        yield return null;
        while ((ReviveGate.IsArmed || UIStateProbe.IsAnyModalOpen || ReviveLatch.Active || UIStateProbe.ResultGuardActive)
               && Time.realtimeSinceStartup < deadline)
            yield return null;

        // 3) 매핑/열림 상태 최종 확인 후 재오픈
        if (!IsPanelActuallyOpen(_lastResultKey) && TryGetPanelRoot(_lastResultKey, out _))
        {
            Debug.Log("[UI] Reopen result after interstitial");
            SetPanel(_lastResultKey, true, ignoreDelay: true);
        }
    }

    static void EnableAllCanvasRecursively(GameObject root)
    {
        if (!root) return;
        var cvs = root.GetComponentsInChildren<Canvas>(includeInactive: true);
        for (int i = 0; i < cvs.Length; i++)
            cvs[i].enabled = true;
    }

    static void ForceCanvasGroupVisible(GameObject root)
    {
        if (!root) return;
        var cg = root.GetComponent<CanvasGroup>() ?? root.AddComponent<CanvasGroup>();
        cg.alpha = 1f;
        cg.blocksRaycasts = true;
        cg.interactable = true;
    }

    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    void LogPanelState(string key, GameObject root)
    {
        var cg = root.GetComponent<CanvasGroup>();
        var cv = root.GetComponent<Canvas>();
        var rt = root.GetComponent<RectTransform>();
        Debug.Log($"[UI.State] {key} active={root.activeInHierarchy} cg?={(cg != null)} alpha={(cg ? cg.alpha : -1)} " +
                  $"ray={(cg ? cg.blocksRaycasts : false)} itact={(cg ? cg.interactable : false)} " +
                  $"ovrSort={(cv ? cv.overrideSorting : false)} order={(cv ? cv.sortingOrder : -1)} " +
                  $"scale={rt.localScale} size={rt.rect.size}");
    }
}

// Events
public readonly struct PanelToggle { public readonly string key; public readonly bool on; public PanelToggle(string key, bool on) { this.key = key; this.on = on; } }
public readonly struct InputLock { public readonly bool on; public readonly string reason; public InputLock(bool on, string reason) { this.on = on; this.reason = reason; } }

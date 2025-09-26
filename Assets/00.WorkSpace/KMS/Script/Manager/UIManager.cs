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
    [SerializeField] private TMP_Text _hudBestText;

    [Header("GameOver Texts (All Canvases)")]
    [SerializeField] private TMP_Text[] _goTotalTexts; // 모든 Canvas의 TotalScore 라벨들
    [SerializeField] private TMP_Text[] _goBestTexts;  // 모든 Canvas의 Best 라벨들

    [Header("Panels")]
    [SerializeField] private List<PanelEntry> _panels = new();

    [Header("Fade")]
    [SerializeField] CanvasGroup mainGroup;   // 메인 루트 그룹(강제 원복용)
    [SerializeField] Image dimOverlay;        // 모달 DIM

    [Header("Combo UI")]
    [SerializeField] private GameObject _rainbowIcon;   // GameCanvas
    [SerializeField] private CanvasGroup _comboGroup;   // UICanvas (Combo 이미지+텍스트 묶음)
    [SerializeField] private TMP_Text _comboText;       // Combo 숫자
    [SerializeField] private float _comboHoldTime = 0.8f; // 유지시간
    [SerializeField] private float _comboFadeTime = 0.2f; // 페이드아웃 시간
    [SerializeField] private int _comboVisibleThreshold = 2;
    [SerializeField] private int[] _comboTierStarts = new int[] { 0, 2, 3, 5, 8 };

    [Header("Revive Settings")]
    [SerializeField] private float _reviveDelaySec = 1.0f;
    private Coroutine _reviveDelayJob;
    [SerializeField] private CanvasGroup _preReviveBlocker; // 풀스크린 Image+CanvasGroup, 투명 OK

    private Coroutine _comboFadeJob;
    private readonly Dictionary<string, PanelEntry> _panelMap = new();
    private readonly List<string> _modalOrder = new();

    // 패널별 페이드 코루틴 관리
    private readonly Dictionary<string, Coroutine> _fadeJobs = new();
    // DIM 페이드 코루틴
    private Coroutine _dimJob;

    // HUD state
    private int _lastHighScore = 0;
    int _pendingDownedScore;

    private int _lastLoggedClassicBest = -1;

    private EventQueue _bus;
    private GameManager _game;

    // 집계 재시작용
    private bool _runStartLogged = false;
    public int Order => 100;

    public void SetDependencies(EventQueue bus, GameManager game) { _bus = bus; _game = game; }

    private static void SetAll(TMP_Text[] arr, string value)
    {
        if (arr == null) return;
        for (int i = 0; i < arr.Length; i++)
            if (arr[i]) arr[i].text = value;
    }
    private static string FormatScore(int v) => v.ToString("#,0");

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
            int tier = MapToTier(e.value);

            if (tier <= 0)
            {
                HideComboImmediate();
                return;
            }

            if (_comboText) _comboText.SetText($"x{e.value - 1}");

            ApplyComboTierVisuals(tier);

            ShowComboStartHold();
        }, replaySticky: true);

        _bus.Subscribe<GameDataChanged>(e =>
        {
            _lastHighScore = e.data.highScore;
            if (_hudBestText) _hudBestText.text = $"{_lastHighScore:#,0}";
            // 필요 시 GO 화면의 Best도 최신으로 동기화
            SetAll(_goBestTexts, $"{FormatScore(_lastHighScore)}");
            Debug.Log($"[UI] Best HUD update -> {_lastHighScore}");
        }, replaySticky: true);

        // 리바이브 패널 ON (저장/FX 금지)
        _bus.Subscribe<PlayerDowned>(e =>
        {
            _pendingDownedScore = e.score;
            if (_reviveDelayJob != null) StopCoroutine(_reviveDelayJob);
            _reviveDelayJob = StartCoroutine(Co_OpenReviveAfterDelay(_reviveDelaySec));
        }, replaySticky: false);


        // 리바이브 패널 OFF
        void CancelReviveDelay()
        {
            if (_reviveDelayJob != null) { StopCoroutine(_reviveDelayJob); _reviveDelayJob = null; }
            SetPreReviveBlock(false);
            _bus?.PublishImmediate(new InputLock(false, "PreRevive"));
            Time.timeScale = 1f;
        }

        // 리바이브 패널 OFF + 결과 패널 ON (신기록 여부에 따라 분기)
        _bus.Subscribe<GameOverConfirmed>(e =>
        {
            CancelReviveDelay();
            Game.Audio.StopContinueTimeCheckSE();

            SetAll(_goTotalTexts, $"{FormatScore(e.score)}");
            int best = e.isNewBest ? e.score : _lastHighScore;
            SetAll(_goBestTexts, $"{FormatScore(best)}");

            SetPanel("Revive", false);

            // Classic 신기록 로깅
            var mm = _00.WorkSpace.GIL.Scripts.Managers.MapManager.Instance;
            bool isAdventure = (mm?.CurrentMode == GameMode.Adventure);
            if (!isAdventure && e.isNewBest)
            {
                // 간단 중복 방지 가드: 동일 점수 중복 로그 방지
                if (_lastLoggedClassicBest != e.score)
                {
                    AnalyticsManager.Instance?.ClassicBestLog(e.score);
                    _lastLoggedClassicBest = e.score;
                }
            }

            if (isAdventure) return;

            SetPanel("GameOver", !e.isNewBest);
            SetPanel("NewRecord", e.isNewBest);

        }, replaySticky: false);


        // 광고 성공 시 모달/패널 닫기
        _bus.Subscribe<ContinueGranted>(_ =>
        {
            CancelReviveDelay();
            Game.Audio.StopContinueTimeCheckSE();

            SetPanel("Revive", false);
            SetPanel("GameOver", false);
            SetPanel("NewRecord", false);

            ForceCloseAllModals();
            NormalizeAllPanelsAlpha();
            ForceMainUIClean();

            // 한 프레임 뒤 재검증(혹시 비동기 꼬임 대비)
            StartCoroutine(CoPostContinueSanity());
        }, replaySticky: false);

        // 패널 토글 이벤트 구독 (Sticky 재생 켜둠)
        _bus.Subscribe<PanelToggle>(OnPanelToggle, replaySticky: true);

        _bus.Subscribe<GameResetRequest>(OnGameResetRequest, replaySticky: false);

        var svc = Game.Save as ISaveService;
        var data = svc?.Data;
        if (data != null)
        {
            _lastHighScore = data.highScore;
            if (_hudBestText) _hudBestText.text = $"{_lastHighScore:#,0}";
            SetAll(_goBestTexts, $"{_lastHighScore:#,0}");
            Debug.Log($"[UI] Seed Best from Save: {_lastHighScore}");
        }
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
        if (on) StartCoroutine(FailsafeOpenSnap(key, p.root, EnsureCanvasGroup(p.root)));
        if (on && key == "Revive")
        {
            Game.Ads?.Refresh();   // Reward/Interstitial 준비 안됐으면 바로 로드 시작
        }
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

    private IEnumerator FadeOutCombo(CanvasGroup cg, float hold, float fade)
    {
        // 즉시 보이게
        cg.alpha = 1f;

        // 일정 시간 유지
        float t = 0f;
        while (t < hold) { t += Time.unscaledDeltaTime; yield return null; }

        // 페이드 아웃
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

    // === 리셋 흐름 ===
    public void ResetRuntime()
    {
        // 모달/Dim/메인 강제 정리 후 기본 패널 상태로
        ForceCloseAllModals();
        SetPanel("GameOver", false);
        SetPanel("NewRecord", false);
        SetPanel("Game", true);

        NormalizeAllPanelsAlpha();
        ForceMainUIClean();
    }

    private void OnGameResetRequest(GameResetRequest req)
    {
        CancelReviveDelay();

        // 1) 보장: 시간/오디오/모달 정리
        Time.timeScale = 1f;
        Game.Audio.StopContinueTimeCheckSE();
        Game.Audio.StopAllSe();
        Game.Audio.ResumeAll();
        ForceCloseAllModals();

        bool toGame = (req.targetPanel == "Game");
        string onKey = toGame ? "Game" : "Main";
        string offKey = toGame ? "Main" : "Game";

        // 2) 엔진 리셋 이벤트 (목적지에 따라)
        if (!toGame)
        {
            // Main으로 나갈 때만 런 정리
            _bus.PublishImmediate(new GameResetting());
            _bus.PublishImmediate(new ComboChanged(0));
            _bus.PublishImmediate(new ScoreChanged(0));
        }
        else
        {
            // Game으로 들어갈 때는 런 유지 (원하면 Heal 요청만)
            // _bus.PublishImmediate(new HealBoardRequest(), alsoEnqueue:false);
        }

        // 3) UI 전환(원자적)
        SetPanel("GameOver", false);
        SetPanel("NewRecord", false);
        SetPanel(offKey, false);
        SetPanel(onKey, true);

        _bus.PublishImmediate(new PanelToggle(offKey, false));
        var onEvt = new PanelToggle(onKey, true);
        _bus.PublishSticky(onEvt, alsoEnqueue: false);
        _bus.PublishImmediate(onEvt);

        var mm = _00.WorkSpace.GIL.Scripts.Managers.MapManager.Instance;
        bool isAdventure = (mm?.CurrentMode == GameMode.Adventure);
        int stageIndex = mm?.CurrentMapData?.mapIndex ?? 0;

        if (toGame && !_runStartLogged)
        {
            AnalyticsManager.Instance?.GameStartLog(!isAdventure, stageIndex);
            _runStartLogged = true;
        }

        if (!toGame) _runStartLogged = false;

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
        // 1~4 티어만 검사
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
        if (_reviveDelayJob != null)
        {
            StopCoroutine(_reviveDelayJob);
            _reviveDelayJob = null;
        }
        SetPreReviveBlock(false);
        Time.timeScale = 1f;
    }

    void SetPreReviveBlock(bool on)
    {
        if (!_preReviveBlocker) return;

        if (on)
        {
            _preReviveBlocker.gameObject.SetActive(true);
            _preReviveBlocker.alpha = 0f;            // 완전 투명
            _preReviveBlocker.blocksRaycasts = true; // 클릭 완전 차단
            _preReviveBlocker.interactable = false;
            _preReviveBlocker.transform.SetAsLastSibling(); // 항상 맨 위
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
        // 1) UI 터치 차단(오버레이)
        SetPreReviveBlock(true);

        // 2) 전역 입력 락 (직접 Input 읽는 스크립트용)
        _bus?.PublishImmediate(new InputLock(true, "PreRevive"));

        Time.timeScale = 0f;
        yield return new WaitForSecondsRealtime(Mathf.Max(0f, delay));

        SetPreReviveBlock(false);
        SetPanel("Revive", true);
        Game.Audio.PlayContinueTimeCheckSE();

        // 3) 전역 입력락 해제
        _bus?.PublishImmediate(new InputLock(false, "PreRevive"));

        _reviveDelayJob = null;
    }
    IEnumerator CoPostContinueSanity()
    {
        yield return null;
        Debug.Log($"[Sanity] modals={_modalOrder.Count} dim.enabled={(dimOverlay && dimOverlay.enabled)}");
        SetPreReviveBlock(false);
    }
}
public readonly struct PanelToggle
{
    public readonly string key; public readonly bool on;
    public PanelToggle(string key, bool on) { this.key = key; this.on = on; }
}
public readonly struct InputLock
{
    public readonly bool on; public readonly string reason;
    public InputLock(bool on, string reason) { this.on = on; this.reason = reason; }
}
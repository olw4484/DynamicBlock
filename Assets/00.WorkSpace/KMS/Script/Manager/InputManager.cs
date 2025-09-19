using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

// ================================
// Script  : InputManager.cs
// Desc    : UI 버튼 브리지 + 입력 잠금/쿨다운
// ================================

[DisallowMultipleComponent]
[AddComponentMenu("System/InputManager")]
public sealed class InputManager : MonoBehaviour, IManager, ITickable
{
    public int Order => 30;

    //[Header("Click Cooldown")]
    //[SerializeField] private float clickCooldown = 0.12f;

    private EventQueue _bus;
    private bool _inputEnabled;
    private float _cool;

    public void SetDependencies(EventQueue bus) => _bus = bus;

    // Lifecycle
    public void PreInit()
    {
        if (_bus == null) Debug.LogError("[InputManager] EventQueue 주입 필요");

    }

    public void Init()
    {
        _inputEnabled = true;
        _cool = 0f;
    }

    public void PostInit()
    {
        _bus.Subscribe<SceneWillChange>(_ => { _inputEnabled = false; Debug.Log("[Input] lock: SceneWillChange"); }, false);
        _bus.Subscribe<SceneChanged>(_ => { _inputEnabled = true; _cool = 0f; Debug.Log("[Input] unlock: SceneChanged"); }, false);

        _bus.Subscribe<AdPlaying>(_ => { _inputEnabled = false; Debug.Log("[Input] lock: AdPlaying"); }, false);
        _bus.Subscribe<AdFinished>(_ => { _inputEnabled = true; _cool = 0f; Debug.Log("[Input] unlock: AdFinished"); }, false);

        _bus.Subscribe<GameResetting>(_ => { _inputEnabled = false; Debug.Log("[Input] lock: GameResetting"); }, false);
        _bus.Subscribe<GameResetDone>(_ => { _inputEnabled = true; _cool = 0f; Debug.Log("[Input] unlock: GameResetDone"); }, false);
    }

    // New Input System
    public void Tick(float dt)
    {
        if (_cool > 0f) _cool -= dt;

        bool esc = false;

        // New Input System (있을 때만)
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current?.escapeKey.wasPressedThisFrame ?? false)
            esc = true;
        if (Gamepad.current?.startButton.wasPressedThisFrame ?? false)
            esc = true;
#endif

        // Old Input System
        if (Input.GetKeyDown(KeyCode.Escape))
            esc = true;

        if (!esc || !Ready()) return;

        if (Game.UI.TryCloseTopByEscape()) { Consume(); return; }

        // 기본 동작(모달 없을 때)
        if (Game.UI.TryGetPanelRoot("Game", out var gameRoot) && gameRoot.activeInHierarchy)
            Game.UI.SetPanel("Game_Options", true);
        else
            Game.UI.SetPanel("ExitConfirm", true);

        Consume();
    }


    // === 내부 유틸 ===
    private bool Ready()
    {
        return _inputEnabled && _cool <= 0f;
    }
    private void Consume()
    {
        //_cool = clickCooldown;
    }

    // === 외부 API ===
    // ──────────────────────────────────────────────────────────────
    // # UI Button → Direct API (즉시 실행)
    // 버튼 인스펙터에서 파라미터 입력 가능
    // ──────────────────────────────────────────────────────────────
    public void OnClick_LoadScene(string sceneName)
    {
        if (!Ready()) return;
        Consume();
        Game.Scene.LoadScene(sceneName);
    }

    public void OnClick_SetPanel(string key)                // 켜기 전용
    {
        if (!Ready()) return;
        Consume();
        Game.UI.SetPanel(key, true);
    }

    public void OnClick_SetPanelOff(string key)             // 끄기 전용
    {
        if (!Ready()) return;
        Consume();
        Game.UI.SetPanel(key, false);
    }
    public void OnClick_WatchAdToContinue()
    {
        if (!Ready()) return; Consume();
        _bus.Publish(new RewardedContinueRequest());   // 명령 이벤트
    }

    public void OnClick_Restart()
    {
        if (!Ready()) return; Consume();

        // 패널 닫기
        _bus.Publish(new PanelToggle("Options", false));
        _bus.Publish(new PanelToggle("GameOver", false));

        Time.timeScale = 1f;

        // 씬 리로드(이벤트 경로)
        _bus.Publish(new SceneChangeRequest("Gameplay"));
        // 또는 Game.Scene.LoadScene("Gameplay");
    }

    // ──────────────────────────────────────────────────────────────
    // # UI Button → Event 경로 (디커플링/여러 시스템 동시 반응)
    // ──────────────────────────────────────────────────────────────
    public void OnClick_RequestScene(string sceneName)
    {
        if (!Ready()) return;
        Consume();
        _bus.Publish(new SceneChangeRequest(sceneName));
    }

    public void OnClick_TogglePanelEvent(string key, bool on)
    {
        if (!Ready()) return;
        Consume();
        _bus.Publish(new PanelToggle(key, on));
    }

    public void OnClick_SwitchPanels(string offKey, string onKey)
    {
        if (!Ready()) return;
        Consume();

        // 이벤트 경로(디커플링)
        _bus.Publish(new PanelToggle(offKey, false));
        _bus.Publish(new PanelToggle(onKey, true));

        // 또는 직접 경로(2줄로 대체가능)
        // Game.UI.SetPanel(offKey, false);
        // Game.UI.SetPanel(onKey,  true);
    }

    // 매개변수 없는 프리셋(인스펙터에서 편리)
    [Header("Presets (Optional)")]
    [SerializeField] private string gameplayScene = "Gameplay";
    public void OnClick_StartGame() => OnClick_RequestScene(gameplayScene);
    public void OnClick_PauseOn() => OnClick_TogglePanelEvent("Pause", true);
    public void OnClick_PauseOff() => OnClick_TogglePanelEvent("Pause", false);
}

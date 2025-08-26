using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

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
        // 씬 전환 동안 입력 잠금
        _bus.Subscribe<SceneWillChange>(_ => _inputEnabled = false, replaySticky: false);
        _bus.Subscribe<SceneChanged>(_ => { _inputEnabled = true; _cool = 0f; }, replaySticky: false);
    }

    public void Tick(float dt)
    {
        if (_cool > 0f) _cool -= dt;

        if (Input.GetKeyDown(KeyCode.Escape) && Ready())
        {
            if (Game.UI.TryCloseTopByEscape())
                Consume();
        }
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
        Game.Scene.LoadScene("Gameplay");              // 또는 Reset 이벤트 발행
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

        // 또는 직접 경로(2줄로 대체가능 )
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

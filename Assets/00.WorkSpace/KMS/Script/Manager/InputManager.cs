using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// ================================
// Script  : InputManager.cs
// Desc    : UI ��ư �긮�� + �Է� ���/��ٿ�
// ================================

[DisallowMultipleComponent]
[AddComponentMenu("System/InputManager")]
public sealed class InputManager : MonoBehaviour, IManager, ITickable
{
    public int Order => 30;

    [Header("Click Cooldown")]
    [SerializeField] private float clickCooldown = 0.12f;

    private EventQueue _bus;
    private bool _inputEnabled;
    private float _cool;

    public void SetDependencies(EventQueue bus) => _bus = bus;

    // ���� Lifecycle
    public void PreInit()
    {
        if (_bus == null) Debug.LogError("[InputManager] EventQueue ���� �ʿ�");
    }

    public void Init()
    {
        _inputEnabled = true;
        _cool = 0f;
    }

    public void PostInit()
    {
        // �� ��ȯ ���� �Է� ���
        _bus.Subscribe<SceneWillChange>(_ => _inputEnabled = false, replaySticky: false);
        _bus.Subscribe<SceneChanged>(_ => { _inputEnabled = true; _cool = 0f; }, replaySticky: false);
    }

    public void Tick(float dt)
    {
        if (_cool > 0f) _cool -= dt;
    }

    // ���� ���� ��ƿ
    private bool Ready()
    {
        return _inputEnabled && _cool <= 0f;
    }
    private void Consume()
    {
        _cool = clickCooldown;
    }

    // ����������������������������������������������������������������������������������������������������������������������������
    // # UI Button �� Direct API (��� ����)
    // ��ư �ν����Ϳ��� �Ķ���� �Է� ����
    // ����������������������������������������������������������������������������������������������������������������������������
    public void OnClick_LoadScene(string sceneName)
    {
        if (!Ready()) return;
        Consume();
        Game.Scene.LoadScene(sceneName);
    }

    public void OnClick_SetPanel(string key)                // �ѱ� ����
    {
        if (!Ready()) return;
        Consume();
        Game.UI.SetPanel(key, true);
    }

    public void OnClick_SetPanelOff(string key)             // ���� ����
    {
        if (!Ready()) return;
        Consume();
        Game.UI.SetPanel(key, false);
    }

    // ����������������������������������������������������������������������������������������������������������������������������
    // # UI Button �� Event ��� (��Ŀ�ø�/���� �ý��� ���� ����)
    // ����������������������������������������������������������������������������������������������������������������������������
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

    // ���� �Ű����� ���� ������(�ν����Ϳ��� ��)
    [Header("Presets (Optional)")]
    [SerializeField] private string gameplayScene = "Gameplay";
    public void OnClick_StartGame() => OnClick_RequestScene(gameplayScene);
    public void OnClick_PauseOn() => OnClick_TogglePanelEvent("Pause", true);
    public void OnClick_PauseOff() => OnClick_TogglePanelEvent("Pause", false);
}

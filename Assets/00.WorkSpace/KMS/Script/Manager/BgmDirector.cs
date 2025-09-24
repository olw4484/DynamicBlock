using System.Collections.Generic;
using UnityEngine;

// ================================
// Script  : BgmDirector.cs
// Desc    : ĵ���� �г�/�� �̺�Ʈ�� ������ BGM �����
// ================================
public sealed class BgmDirector : IManager
{
    public int Order => 25;

    private EventQueue _bus;
    private IAudioService _audio;
    private Dictionary<string, AudioClip> _bgmByKey;

    public void SetDependencies(EventQueue bus, IAudioService audio)
    {
        _bus = bus; _audio = audio;
    }

    public void PreInit()
    {
        if (_bus == null) Debug.LogError("[BgmDirector] EventQueue ���� ����");
        if (_audio == null) Debug.LogError("[BgmDirector] IAudioService ���� ����");
    }

    public void Init() { }

    public void PostInit()
    {
        _bus.Subscribe<AppSplashFinished>(_ => OnSplashDone(), replaySticky: true);
        _bus.Subscribe<PanelToggle>(OnPanelToggle, replaySticky: true);
        // _bus.Subscribe<SceneChanged>(OnSceneChanged, replaySticky: true);
    }

    // ���÷��� ���� ��: ����� ������ ����
    private void OnSplashDone()
    {
        EnsureMap();
        var am = AudioManager.Instance;
        if (!am) return;

        if (am.IsBgmOn && am.BGM_Main)
        {
            // �ߺ� ��� ������ IAudioService ���ο��� ���ִ� ���� ���
            _audio?.PlayBgm(am.BGM_Main);
        }
        else
        {
            _audio?.StopBgm();
        }
    }

    // �г� ���� �� ����� (���� ���� ����)
    private void OnPanelToggle(PanelToggle e)
    {
        if (!e.on) return;

        EnsureMap();
        var am = AudioManager.Instance;
        if (!am) return;

        // ����� ��� OFF�� ���� ������� ����
        if (!am.IsBgmOn) { _audio?.StopBgm(); return; }

        if (_bgmByKey != null && _bgmByKey.TryGetValue(e.key, out var clip) && clip)
        {
            _audio?.PlayBgm(clip);
            // Debug.Log($"[BGM] Panel='{e.key}' -> {clip.name}");
        }
        // ���� ������ ����(�Ǵ� ��å�� ���� _audio.StopBgm();)
    }

    // �ʿ� �� �� ü�������� ���� ��å ����
    private void OnSceneChanged(SceneChanged e)
    {
        EnsureMap();
        var am = AudioManager.Instance;
        if (!am || !am.IsBgmOn) { _audio?.StopBgm(); return; }

        if (_bgmByKey != null && _bgmByKey.TryGetValue(e.sceneName, out var clip) && clip)
            _audio?.PlayBgm(clip);
        // else _audio?.StopBgm();
    }

    // �г�/�� Ű �� BGM ����
    private void EnsureMap()
    {
        if (_bgmByKey != null) return;

        var am = AudioManager.Instance ?? Object.FindFirstObjectByType<AudioManager>();
        if (!am)
        {
            Debug.LogWarning("[BgmDirector] AudioManager not found. ��õ� ����.");
            return;
        }

        _bgmByKey = new Dictionary<string, AudioClip>
        {
            ["Main"] = am.BGM_Main,
            ["Game"] = am.BGM_Adventure,
            ["Classic"] = am.BGM_Main,       // �ʿ信 ���� ����
            ["Adventure"] = am.BGM_Adventure,
        };
    }
}

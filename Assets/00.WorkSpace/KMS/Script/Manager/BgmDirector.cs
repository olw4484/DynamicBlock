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

        // �� ü������ ���� Ȱ��ȭ
        // _bus.Subscribe<SceneChanged>(OnSceneChanged, replaySticky: true);
    }

    private void OnPanelToggle(PanelToggle e)
    {
        if (!e.on) return; // ���� ���� ����

        EnsureMap();
        if (_bgmByKey == null) return;

        if (_bgmByKey.TryGetValue(e.key, out var clip) && clip)
        {
            _audio.PlayBgm(clip);
            // Debug.Log($"[BGM] Panel='{e.key}' -> {clip.name}");
        }
        else
        {
            // ��å ����: ���� ������ ���� or ����
            // _audio.StopBgm();
        }
    }

    private void OnSceneChanged(SceneChanged e)
    {
        EnsureMap();
        if (_bgmByKey == null) return;

        if (_bgmByKey.TryGetValue(e.sceneName, out var clip) && clip)
            _audio.PlayBgm(clip);
        // else _audio.StopBgm();
    }

    private void OnSplashDone()
    {
        EnsureMap();

        var am = AudioManager.Instance;
        if (am != null && !am.IsBgmOn)
            am.SetBgmOn(true);

        if (am?.BGM_Main) _audio.PlayBgm(am.BGM_Main);
    }

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
            ["Classic"] = am.BGM_Main,      
            ["Adventure"] = am.BGM_Adventure,
        };
    }
}

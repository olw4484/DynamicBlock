using System.Collections.Generic;
using UnityEngine;

// ================================
// Script  : BgmDirector.cs
// Desc    : ��/���� �̺�Ʈ�� ������ BGM�� �����
// ================================
public sealed class BgmDirector : IManager
{
    public int Order => 25;

    private EventQueue _bus;
    private IAudioService _audio;

    private Dictionary<string, AudioClip> _sceneBgm;

    public void SetDependencies(EventQueue bus, IAudioService audio)
    {
        _bus = bus; _audio = audio;
    }

    public void PreInit()
    {
        if (_bus == null) Debug.LogError("[BgmDirector] EventQueue ���� ����");
        if (_audio == null) Debug.LogError("[BgmDirector] IAudioService ���� ����");
        // ���⼭�� AudioManager.Instance�� �ǵ帮�� ����!
    }

    public void Init() { }

    public void PostInit()
    {
        _bus.Subscribe<AppSplashFinished>(_ => OnSplashDone(), replaySticky: true);
    }

    private void EnsureMap()
    {
        if (_sceneBgm != null) return;

        var am = AudioManager.Instance ?? Object.FindFirstObjectByType<AudioManager>();
        Debug.Log($"[BGM] AM={(am != null)}, Main={(am?.BGM_Main != null)}, Adv={(am?.BGM_Adventure != null)}");
        if (!am)
        {
            Debug.LogWarning("[BgmDirector] AudioManager ���� ����. ���� �����ӿ� ��õ�.");
            return;
        }

        _sceneBgm = new Dictionary<string, AudioClip>
        {
            ["Title"] = am.BGM_Main,
            ["Classic"] = am.BGM_Main,
            ["Adventure"] = am.BGM_Adventure
        };
    }

    private void OnSceneChanged(SceneChanged e)
    {
        Debug.Log($"[BGM] SceneChanged: {e.sceneName}");
        EnsureMap();
        if (_sceneBgm == null)
        {
            Debug.Log($"[BGM] Has key? {_sceneBgm.ContainsKey(e.sceneName)}");
            // ���� AudioManager�� ���ٸ� ���� �����ӿ� �ٽ� �õ�
            // (Ȥ�� ���⼭ �ٷ� am�� ���� ��� Ŭ���� ���� �� ���� ����)
            return;
        }

        if (_sceneBgm.TryGetValue(e.sceneName, out var clip) && clip)
        {
            _audio.PlayBgm(clip);
        }
        else
        {
            _audio.StopBgm(); // ��å: ���� ������ �ϴ� ����(���ϸ� ������ ����)
        }
    }
    private void OnSplashDone()
    {
        var am = AudioManager.Instance ?? Object.FindFirstObjectByType<AudioManager>();
        if (am && am.BGM_Main != null)
            _audio.PlayBgm(am.BGM_Main);
        else
            Debug.LogWarning("[BGM] BGM_Main ���Ҵ� �Ǵ� AudioManager ����");
    }
}

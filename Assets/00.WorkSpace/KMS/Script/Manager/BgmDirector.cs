using System.Collections.Generic;
using UnityEngine;

// ================================
// Script  : BgmDirector.cs
// Desc    : 씬/상태 이벤트를 구독해 BGM을 라우팅
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
        if (_bus == null) Debug.LogError("[BgmDirector] EventQueue 주입 누락");
        if (_audio == null) Debug.LogError("[BgmDirector] IAudioService 주입 누락");
        // 여기서는 AudioManager.Instance를 건드리지 않음!
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
            Debug.LogWarning("[BgmDirector] AudioManager 아직 없음. 다음 프레임에 재시도.");
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
            // 아직 AudioManager가 없다면 다음 프레임에 다시 시도
            // (혹은 여기서 바로 am를 새로 잡고 클립을 직접 고를 수도 있음)
            return;
        }

        if (_sceneBgm.TryGetValue(e.sceneName, out var clip) && clip)
        {
            _audio.PlayBgm(clip);
        }
        else
        {
            _audio.StopBgm(); // 정책: 매핑 없으면 일단 정지(원하면 유지로 변경)
        }
    }
    private void OnSplashDone()
    {
        var am = AudioManager.Instance ?? Object.FindFirstObjectByType<AudioManager>();
        if (am && am.BGM_Main != null)
            _audio.PlayBgm(am.BGM_Main);
        else
            Debug.LogWarning("[BGM] BGM_Main 미할당 또는 AudioManager 없음");
    }
}

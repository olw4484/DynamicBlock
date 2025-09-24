using System.Collections.Generic;
using UnityEngine;

// ================================
// Script  : BgmDirector.cs
// Desc    : 캔버스 패널/씬 이벤트를 구독해 BGM 라우팅
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
        if (_bus == null) Debug.LogError("[BgmDirector] EventQueue 주입 누락");
        if (_audio == null) Debug.LogError("[BgmDirector] IAudioService 주입 누락");
    }

    public void Init() { }

    public void PostInit()
    {
        _bus.Subscribe<AppSplashFinished>(_ => OnSplashDone(), replaySticky: true);
        _bus.Subscribe<PanelToggle>(OnPanelToggle, replaySticky: true);
        // _bus.Subscribe<SceneChanged>(OnSceneChanged, replaySticky: true);
    }

    // 스플래시 종료 시: 사용자 설정을 존중
    private void OnSplashDone()
    {
        EnsureMap();
        var am = AudioManager.Instance;
        if (!am) return;

        if (am.IsBgmOn && am.BGM_Main)
        {
            // 중복 재생 방지는 IAudioService 내부에서 해주는 편이 깔끔
            _audio?.PlayBgm(am.BGM_Main);
        }
        else
        {
            _audio?.StopBgm();
        }
    }

    // 패널 열릴 때 라우팅 (닫힐 때는 무시)
    private void OnPanelToggle(PanelToggle e)
    {
        if (!e.on) return;

        EnsureMap();
        var am = AudioManager.Instance;
        if (!am) return;

        // 사용자 토글 OFF면 절대 재생하지 않음
        if (!am.IsBgmOn) { _audio?.StopBgm(); return; }

        if (_bgmByKey != null && _bgmByKey.TryGetValue(e.key, out var clip) && clip)
        {
            _audio?.PlayBgm(clip);
            // Debug.Log($"[BGM] Panel='{e.key}' -> {clip.name}");
        }
        // 매핑 없으면 유지(또는 정책에 따라 _audio.StopBgm();)
    }

    // 필요 시 씬 체인지에도 같은 정책 적용
    private void OnSceneChanged(SceneChanged e)
    {
        EnsureMap();
        var am = AudioManager.Instance;
        if (!am || !am.IsBgmOn) { _audio?.StopBgm(); return; }

        if (_bgmByKey != null && _bgmByKey.TryGetValue(e.sceneName, out var clip) && clip)
            _audio?.PlayBgm(clip);
        // else _audio?.StopBgm();
    }

    // 패널/씬 키 → BGM 매핑
    private void EnsureMap()
    {
        if (_bgmByKey != null) return;

        var am = AudioManager.Instance ?? Object.FindFirstObjectByType<AudioManager>();
        if (!am)
        {
            Debug.LogWarning("[BgmDirector] AudioManager not found. 재시도 예정.");
            return;
        }

        _bgmByKey = new Dictionary<string, AudioClip>
        {
            ["Main"] = am.BGM_Main,
            ["Game"] = am.BGM_Adventure,
            ["Classic"] = am.BGM_Main,       // 필요에 따라 조정
            ["Adventure"] = am.BGM_Adventure,
        };
    }
}

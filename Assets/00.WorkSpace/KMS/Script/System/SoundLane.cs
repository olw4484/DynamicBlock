using System.Collections.Generic;
using UnityEngine;
using Unity.Profiling;

public sealed class SoundLane : MonoBehaviour
{
    [Header("Budget / Cooldown")]
    [SerializeField] int budgetPerFrame = 6;

    [Tooltip("Cooldown / 예: 0.06 = 60ms")]
    [SerializeField] double defaultCooldownSec = 0.06;

    // 초기 용량 미리 잡기 (필요에 따라 조절)
    readonly Queue<SoundEvent> _q = new(32);
    readonly Dictionary<int, double> _cool = new(32);

    System.Func<double> _time;
    IAudioService _audio;

    public void SetDependencies(IAudioService audio) => _audio = audio;

    // Profiler Marker (SFX 처리 구간만 보고 싶을 때)
    static readonly ProfilerMarker MarkerConsume = new("SoundLane.Consume");

    void Awake()
    {
        _time = () => Time.realtimeSinceStartupAsDouble;
    }

    void OnEnable() => TryRebindAudio();

    void TryRebindAudio()
    {
        if (_audio != null) return;

        // 1) Game.Audio 우선
        if (Game.IsBound && Game.Audio != null)
        {
            _audio = Game.Audio;
#if UNITY_EDITOR
            Debug.Log("[SoundLane] IAudioService bound via Game.Audio");
#endif
            return;
        }

        // 2) ManagerGroup fallback
        var mg = ManagerGroup.Instance;
        if (mg != null)
        {
            var resolved = mg.Resolve<IAudioService>();
            if (resolved != null)
            {
                _audio = resolved;
#if UNITY_EDITOR
                Debug.Log("[SoundLane] IAudioService resolved from ManagerGroup");
#endif
                return;
            }
        }

#if UNITY_EDITOR
        Debug.LogWarning("[SoundLane] IAudioService not found. Will retry in Consume().");
#endif
    }

    public void Enqueue(SoundEvent e)
    {
#if UNITY_EDITOR && SOUNDLANE_DEBUG
        Debug.Log($"[SoundLane] Enqueue id={e.id}");
#endif
        _q.Enqueue(e);
    }

    public void TickBegin() { }

    public void Consume()
    {
        using (MarkerConsume.Auto())
        {
            if (_audio == null)
            {
                TryRebindAudio();
                if (_audio == null)
                {
#if UNITY_EDITOR && SOUNDLANE_DEBUG
                    Debug.LogWarning("[SoundLane] _audio NULL");
#endif
                    return;
                }
            }

            int played = 0;
            double now = _time();

            while (_q.Count > 0 && played < budgetPerFrame)
            {
                var e = _q.Peek();

                if (_cool.TryGetValue(e.id, out var nextAllowed) && now < nextAllowed)
                {
                    _q.Dequeue();
                    continue;
                }

                _q.Dequeue();

                if (!TryRoute(e.id))
                {
#if UNITY_EDITOR && SOUNDLANE_DEBUG
                    Debug.LogWarning($"[SoundLane] Unmapped SFX id={e.id}");
#endif
                    continue;
                }

                _cool[e.id] = now + defaultCooldownSec;
                played++;
            }
        }
    }

    bool TryRoute(int id)
    {
        if (_audio == null) return false;

        var sfxId = (SfxId)id;

        // 딕셔너리 + 람다 대신 switch로 직접 호출
        switch (sfxId)
        {
            case SfxId.ButtonClick:
                _audio.PlayButtonClick();
                return true;

            case SfxId.ClassicStageEnter:
                _audio.PlayClassicStageEnter();
                return true;

            case SfxId.AdvenStageEnter:
                _audio.PlayStageEnter();
                return true;

            case SfxId.ClassicGameOver:
                _audio.PlayClassicGameOver();
                return true;

            case SfxId.ClassicNewRecord:
                _audio.PlayClassicNewRecord();
                return true;

            case SfxId.AdvenFail:
                _audio.PlayAdvenFail();
                return true;

            case SfxId.AdvenClear:
                _audio.PlayAdvenClear();
                return true;

            case SfxId.BlockSelect:
                _audio.PlayBlockSelect();
                return true;

            case SfxId.BlockPlace:
                _audio.PlayBlockPlace();
                return true;

            case SfxId.ContinueTimeCheck:
                _audio.PlayContinueTimeCheck();
                return true;

            case SfxId.ClearAllBlock:
                _audio.PlayClearAllBlock();
                return true;

            default:
                return TryHeuristicRoute(id);
        }
    }

    bool TryHeuristicRoute(int id)
    {
        // Combo 1~8
        if (id >= (int)SfxId.Combo1 && id <= (int)SfxId.Combo8)
        {
            int combo = 1 + (id - (int)SfxId.Combo1);
            _audio.PlayClearCombo(combo);
            return true;
        }

        // LineClear 1~6
        if (id >= (int)SfxId.LineClear1 && id <= (int)SfxId.LineClear6)
        {
            int lines = 1 + (id - (int)SfxId.LineClear1);
            _audio.PlayLineClear(lines);
            return true;
        }

        return false;
    }
}

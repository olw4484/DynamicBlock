using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// ===== 사운드 레인 =====
public sealed class SoundLane : MonoBehaviour
{
    [Header("Budget / Cooldown")]
    [SerializeField] private int budgetPerFrame = 6;

    [Tooltip("Coldown / 예: 0.06 = 60ms")]
    [SerializeField] private double defaultCooldownSec = 0.06;

    private readonly Queue<SoundEvent> _q = new();
    private readonly Dictionary<int, double> _cool = new();
    private System.Func<double> _time;
    private IAudioService _audio;

    const int ComboBase = (int)SfxId.Combo1;     // 1011
    const int ComboMax = (int)SfxId.Combo8;     // 1018
    const int LineBase = (int)SfxId.LineClear1; // 1020
    const int LineMax = (int)SfxId.LineClear6; // 1025
    public void SetDependencies(IAudioService audio) => _audio = audio;

    void Awake()
    {
        _time = () => Time.realtimeSinceStartupAsDouble;
        BuildRoute();
    }
    void OnEnable() => TryRebindAudio();

    private Dictionary<int, System.Action> _route;

    private void TryRebindAudio()
    {
        if (_audio != null) return;

        // 1) Game.Bind로 이미 묶인 전역 우선
        if (Game.IsBound && Game.Audio != null)
        {
            _audio = Game.Audio;
            Debug.Log("[SoundLane] IAudioService bound via Game.Audio");
            return;
        }

        // 2) 예비: ManagerGroup에서 Resolve
        var mg = ManagerGroup.Instance;
        if (mg != null)
        {
            var resolved = mg.Resolve<IAudioService>();
            if (resolved != null)
            {
                _audio = resolved;
                Debug.Log("[SoundLane] IAudioService resolved from ManagerGroup");
                return;
            }
        }

        Debug.LogWarning("[SoundLane] IAudioService not found. Will retry in Consume().");
    }

    void BuildRoute()
    {
        _route = new Dictionary<int, System.Action>
    {
        { (int)SfxId.ButtonClick,        () => _audio.PlayButtonClick() },
        { (int)SfxId.ClassicStageEnter,  () => _audio.PlayClassicStageEnter() },
        { (int)SfxId.AdvenStageEnter,    () => _audio.PlayStageEnter() },

        { (int)SfxId.ClassicGameOver,    () => _audio.PlayClassicGameOver() },
        { (int)SfxId.ClassicNewRecord,   () => _audio.PlayClassicNewRecord() },

        { (int)SfxId.AdvenFail,          () => _audio.PlayAdvenFail() },
        { (int)SfxId.AdvenClear,         () => _audio.PlayAdvenClear() },

        { (int)SfxId.BlockSelect,        () => _audio.PlayBlockSelect() },
        { (int)SfxId.BlockPlace,         () => _audio.PlayBlockPlace() },

        { (int)SfxId.ContinueTimeCheck,  () => _audio.PlayContinueTimeCheck() },
        { (int)SfxId.ClearAllBlock,      () => _audio.PlayClearAllBlock() },
    };
    }

    public void Enqueue(SoundEvent e)
    {
        Debug.Log($"[SoundLane] Enqueue id={e.id}");
        _q.Enqueue(e);
    }

    public void TickBegin() { }

    public void Consume()
    {
        if (_audio == null)
        {
            TryRebindAudio();                   
            if (_audio == null)
            {              
                Debug.LogWarning("[SoundLane] _audio NULL");
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
                Debug.LogWarning($"[SoundLane] Unmapped SFX id={e.id}");
                continue;
            }

            _cool[e.id] = now + defaultCooldownSec;
            played++;
        }
    }
    private bool TryRoute(int id)
    {
        Debug.Log($"[SoundLane] Route id={id}");
        if (_route != null && _route.TryGetValue(id, out var act))
        {
            act?.Invoke();
            return true;
        }
        return TryHeuristicRoute(id);
    }
    private bool TryHeuristicRoute(int id)
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
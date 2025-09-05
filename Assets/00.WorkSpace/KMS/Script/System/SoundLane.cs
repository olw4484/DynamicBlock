using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// ===== 사운드 레인 =====
public sealed class SoundLane : MonoBehaviour
{
    [Header("Budget / Cooldown")]
    [SerializeField] private int budgetPerFrame = 6;
    [SerializeField] private int defaultCooldownMs = 60;

    private readonly Queue<SoundEvent> _q = new();
    private readonly Dictionary<int, double> _cool = new();
    private System.Func<double> _time;
    private IAudioService _audio;
    public void SetDependencies(IAudioService audio)
    {
        _audio = audio;
    }

    void Awake()
    {
        _time = () => Time.realtimeSinceStartupAsDouble;
        BuildRoute();
    }

    private Dictionary<int, System.Action> _route;
    void BuildRoute()
    {
        _route = new Dictionary<int, System.Action>
    {
        { (int)SfxId.ButtonClick,        () => _audio?.PlayButtonClick() },
        { (int)SfxId.ClassicStageEnter,  () => _audio?.PlayStageEnter()  },
        { (int)SfxId.ClassicGameOver,    () => _audio?.PlayClassicGameOver() },
        { (int)SfxId.ClassicNewRecord,   () => _audio?.PlayClassicNewRecord() },

        { (int)SfxId.AdvenStageEnter,    () => _audio?.PlayStageEnter()  },
        { (int)SfxId.AdvenFail,          () => _audio?.PlayAdvenFail()   },
        { (int)SfxId.AdvenClear,         () => _audio?.PlayAdvenClear()  },

        { (int)SfxId.BlockSelect,        () => _audio?.PlayBlockSelect() },
        { (int)SfxId.BlockPlace,         () => _audio?.PlayBlockPlace()  },

        { (int)SfxId.ContinueTimeCheck,  () => _audio?.PlayContinueTimeCheck() },
        { (int)SfxId.ClearAllBlock,      () => _audio?.PlayClearAllBlock() },
    };
    }


    public void Enqueue(SoundEvent e) => _q.Enqueue(e);

    public void TickBegin() { /* delay 필요 시 확장 */ }

    public void Consume()
    {
        if (_audio == null) return;

        int budget = budgetPerFrame;
        double now = _time();

        while (_q.Count > 0 && budget-- > 0)
        {
            var e = _q.Dequeue();

            if (_cool.TryGetValue(e.id, out var nextAllowed) && now < nextAllowed)
                continue;

            if (_route != null && _route.TryGetValue(e.id, out var act))
                act?.Invoke();
            else
                TryHeuristicRoute(e.id);

            _cool[e.id] = now + defaultCooldownMs / 1000.0;
        }
    }

    private void TryHeuristicRoute(int id)
    {
        // Combo 1~8 → 1011~1018
        if (id >= 1011 && id <= 1018) { _audio?.PlayClearCombo(id - 1010); return; }

        // LineClear 1~6 → 1020~1025
        if (id >= 1020 && id <= 1025) { _audio?.PlayLineClear(id - 1019); return; }
    }
}
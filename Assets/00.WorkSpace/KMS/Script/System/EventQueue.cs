using _00.WorkSpace.GIL.Scripts.Managers;
using _00.WorkSpace.GIL.Scripts.Maps;
using _00.WorkSpace.GIL.Scripts.Messages;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using Unity.Profiling;
using UnityEngine;

// ================================
// Project : DynamicBlock
// Script  : EventQueue.cs
// Desc    : 이벤트 버스/큐 (즉시/지연/스티키/타이머/스레드세이프)
// ================================

public sealed class EventQueue : IManager, ITickable, ITeardown
{

#if UNITY_EDITOR || DEVELOPMENT_BUILD

    static readonly ProfilerMarker MK_Tick = new("EventQueue.Tick");
    static readonly ProfilerMarker MK_MergeThread = new("EventQueue.MergeThread");
    static readonly ProfilerMarker MK_Timers = new("EventQueue.Timers");
    static readonly ProfilerMarker MK_Dispatch = new("EventQueue.Dispatch");
    static readonly ProfilerMarker MK_Handlers = new("EventQueue.Handlers");
    static readonly ProfilerMarker MK_Taps = new("EventQueue.Taps");
#endif
    public int Order => 0;

    // 타입별 핸들러
    private readonly Dictionary<Type, List<Delegate>> _handlers = new(64);
    // 프레임 처리용 큐 (메인스레드)
    private readonly Queue<object> _queue = new(128);
    // 워커 스레드 입력 큐
    private readonly ConcurrentQueue<object> _threadQueue = new();
    // 스티키(마지막 상태 캐시)
    private readonly Dictionary<Type, object> _sticky = new(32);

    // 지연 발행(타이머) 관리
    private struct Scheduled
    {
        public float due;   // 만기 시간(Time.time + delay)
        public object evt;
    }
    private readonly List<Scheduled> _scheduled = new(64);

    public void PreInit()
    {
        _handlers.Clear();
        _queue.Clear();
        while (_threadQueue.TryDequeue(out _)) { }
        _sticky.Clear();
        _scheduled.Clear();
    }

    public void Init() { /* 필요 시 예약 초기화 */ }
    public void PostInit() { /* 소비자가 구독함 */ }

    public void Tick(float dt)
    {
       // UnityEngine.Debug.Log($"[EQ] Tick, q={_queue.Count}");
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        using (MK_Tick.Auto())
#endif
        {
            // 1) 워커 병합
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            using (MK_MergeThread.Auto())
#endif
                while (_threadQueue.TryDequeue(out var fromWorker))
                    _queue.Enqueue(fromWorker);

            // 2) 타이머 처리
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            using (MK_Timers.Auto())
#endif
            {
                float now = Time.time;
                for (int i = _scheduled.Count - 1; i >= 0; i--)
                    if (_scheduled[i].due <= now) { _queue.Enqueue(_scheduled[i].evt); _scheduled.RemoveAt(i); }
            }

            // 3) 큐 디스패치
            int count = _queue.Count;
            for (int i = 0; i < count; i++)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                using (MK_Dispatch.Auto())
#endif
                    Dispatch(_queue.Dequeue());
            }
        }
    }

    public void Teardown()
    {
        _handlers.Clear();
        _queue.Clear();
        while (_threadQueue.TryDequeue(out _)) { }
        _sticky.Clear();
        _scheduled.Clear();
    }

    // ===============================
    // # API - 발행
    // ===============================
    public void Publish<T>(T evt) => _queue.Enqueue(evt);
    public void PublishImmediate<T>(T evt) => Dispatch(evt);

    public void PublishSticky<T>(T evt, bool alsoEnqueue = true)
    {
        _sticky[typeof(T)] = evt!;
        if (alsoEnqueue) _queue.Enqueue(evt!);
    }

    public void PublishAfter<T>(T evt, float delaySec)
    {
        _scheduled.Add(new Scheduled { due = Time.time + Mathf.Max(0f, delaySec), evt = evt! });
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD

    private readonly List<System.Action<object>> _taps = new();

    [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
    public void AddTap(System.Action<object> tap)
    {
        if (tap != null) _taps.Add(tap);
    }

    [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
    public void RemoveTap(System.Action<object> tap)
    {
        _taps.Remove(tap);
    }
#endif

    // 워커 스레드에서 안전
    public void EnqueueFromAnyThread<T>(T evt) => _threadQueue.Enqueue(evt!);

    // ===============================
    // # API - 구독
    // ===============================
    public void Subscribe<T>(Action<T> handler, bool replaySticky = true)
    {
        var t = typeof(T);
        if (!_handlers.TryGetValue(t, out var list))
            _handlers[t] = list = new List<Delegate>(8);

        list.Add(handler);

        // Sticky 즉시 재생
        if (replaySticky && _sticky.TryGetValue(t, out var last))
            handler((T)last);
    }

    public void Unsubscribe<T>(Action<T> handler)
    {
        var t = typeof(T);
        if (_handlers.TryGetValue(t, out var list))
            list.Remove(handler);
    }

    // ===============================
    // # 내부
    // ===============================
    private void Dispatch(object evt)
    {
        if (evt is GameResetRequest) UnityEngine.Debug.Log("[EQ] Dispatch(GameResetRequest)");
        var t = evt.GetType();
        if (_handlers.TryGetValue(t, out var list))
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            using (MK_Handlers.Auto())
#endif
                for (int i = 0; i < list.Count; i++)
                {
                    if (list[i] is Action<object> obj) obj(evt);
                    else list[i].DynamicInvoke(evt);
                }
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        using (MK_Taps.Auto())
            for (int i = 0; i < _taps.Count; i++)
            {
                try { _taps[i]?.Invoke(evt); }
                catch (Exception ex) { UnityEngine.Debug.LogException(ex); }
            }
#endif
    }

    // Sticky 초기화
    public void ClearSticky<T>() => _sticky.Remove(typeof(T));
    public void ClearAllSticky() => _sticky.Clear();
}

// 샘플 이벤트
public readonly struct ScoreChanged { public readonly int value; public ScoreChanged(int v) => value = v; }
public readonly struct ComboChanged { public readonly int value; public ComboChanged(int v) => value = v; }
public readonly struct GameOver
{
    public readonly int score; public readonly string reason;
    public GameOver(int score, string reason = null) { this.score = score; this.reason = reason; }
}
public readonly struct RewardedContinueRequest { }      // 명령(Non-Sticky)
public readonly struct AdPlaying { }                    // 광고 시작(입력 잠금)
public readonly struct AdFinished { }                   // 광고 종료(입력 해제)
public readonly struct PlayerDowned
{
    public readonly int score; public readonly string reason;
    public PlayerDowned(int s, string r) { score = s; reason = r; }
}
public readonly struct GameOverConfirmed
{
    public readonly int score;
    public readonly bool isNewBest;
    public readonly string reason;
    public GameOverConfirmed(int s, bool nb, string r) { score = s; isNewBest = nb; reason = r; }
}
public readonly struct RevivePerformed { }
public readonly struct ContinueGranted { }              // 명령 결과
public readonly struct SaveRequested { }
public readonly struct LoadRequested { }
public readonly struct ResetRequested { }
public readonly struct GameDataChanged
{
    public readonly GameData data;
    public GameDataChanged(GameData d) { data = d; }
}


public readonly struct GridReady { public readonly int rows, cols; public GridReady(int r, int c) { rows = r; cols = c; } }
public readonly struct GameResetRequest
{
    public readonly string targetPanel;
    public readonly ResetReason reason;
    public GameResetRequest(string target, ResetReason reason)
    {
        this.targetPanel = target; this.reason = reason;
    }
}


public enum ResetReason { ToMain, Restart, GameOver, ToGame, None }
public readonly struct GameResetting
{
    public readonly string targetPanel;
    public readonly ResetReason reason;
    public GameResetting(string target, ResetReason reason)
    {
        this.targetPanel = target; this.reason = reason;
    }
}
public readonly struct GameResetDone { }      // 리셋 완료(입력해제/패널 복구 등)
public readonly struct SplashFinish { }        // 스플래시 종료 트리거
public readonly struct AppSplashFinished { }
public readonly struct SplashSkipRequest { }   // 사용자가 탭으로 스킵 요청
public readonly struct PreloadDone { }         // 에셋 프리로드가 끝났을 때 발행
public readonly struct SoundEvent
{
    public readonly int id;
    public readonly int delayMs;
    public SoundEvent(int id, int delayMs = 0) { this.id = id; this.delayMs = delayMs; }
}
public readonly struct EffectEvent
{
    public readonly int id;
    public readonly Vector3 pos;
    public readonly bool hasColor;
    public readonly Color color;

    public EffectEvent(int id, Vector3 pos)
    { this.id = id; this.pos = pos; this.hasColor = false; this.color = Color.white; }

    public EffectEvent(int id, Vector3 pos, Color color)
    { this.id = id; this.pos = pos; this.hasColor = true; this.color = color; }
}
public readonly struct RowClearFxEvent
{
    public readonly int row;
    public readonly Color color;

    public RowClearFxEvent(int row, Color color)
    {
        this.row = row;
        this.color = color;
    }
}
public readonly struct ColClearFxEvent
{
    public readonly int col;
    public readonly Color color;

    public ColClearFxEvent(int col, Color color)
    {
        this.col = col;
        this.color = color;
    }
}
public readonly struct LanguageChangeRequested
{
    public readonly int index;
    public LanguageChangeRequested(int index) { this.index = index; }
}
namespace _00.WorkSpace.GIL.Scripts.Messages
{
    public readonly struct LinesWillClear
    {
        public readonly int[] rows;
        public readonly int[] cols;
        public readonly Sprite destroySprite;
        public int Total => (rows?.Length ?? 0) + (cols?.Length ?? 0);

        public LinesWillClear(int[] rows, int[] cols, Sprite sprite)
        {
            this.rows = rows; this.cols = cols; this.destroySprite = sprite;
        }
    }

    public readonly struct AllClear
    {
        public readonly int bonus;
        public readonly int combo;
        public readonly Vector3? fxWorld;
        public AllClear(int bonus = 50, int combo = 0, Vector3? fxWorld = null)
        {
            this.bonus = bonus; this.combo = combo; this.fxWorld = fxWorld;
        }
    }

    public readonly struct LinesCleared
    {
        public readonly int[] rows;
        public readonly int[] cols;
        public readonly int combo;
        public readonly Sprite destroySprite;
        public int Total => (rows?.Length ?? 0) + (cols?.Length ?? 0);

        public LinesCleared(int[] rows, int[] cols, int combo, Sprite sprite)
        {
            this.rows = rows; this.cols = cols; this.combo = combo; this.destroySprite = sprite;
        }
    }

    public readonly struct AdventureStageCleared
    {
        public readonly MapGoalKind kind;
        public readonly int finalScore;
        public AdventureStageCleared(MapGoalKind kind, int finalScore)
        { this.kind = kind; this.finalScore = finalScore; }
    }

    public readonly struct AdventureStageFailed
    {
        public readonly MapGoalKind kind;
        public readonly int finalScore;
        public AdventureStageFailed(MapGoalKind kind, int finalScore)
        { this.kind = kind; this.finalScore = finalScore; }
    }
}


public readonly struct ReviveRequest { }              // 리바이브 버튼 클릭/보상 수령
public readonly struct GiveUpRequest { }              // 포기 버튼/타임아웃

public readonly struct GridCleared { public readonly int rows, cols; public GridCleared(int r,int c){rows=r;cols=c;} }

public readonly struct GameEnterRequest
{
    public readonly GameMode mode;
    public readonly MapManager.ClassicEnterPolicy policy;
    public GameEnterRequest(GameMode mode, MapManager.ClassicEnterPolicy policy)
    {
        this.mode = mode;
        this.policy = policy;
    }
}

public readonly struct GameEnterIntent
{
    public readonly GameMode mode;
    public readonly bool forceLoadSave; // Classic일 때 저장 강제 로드할지

    public GameEnterIntent(GameMode mode, bool forceLoadSave)
    {
        this.mode = mode;
        this.forceLoadSave = forceLoadSave;
    }
}
public readonly struct BoardReady
{
    public readonly int rows, cols;
    public BoardReady(int r, int c) { rows = r; cols = c; }
}

public readonly struct GameEntered
{
    public readonly GameMode mode;
    public GameEntered(GameMode m) { mode = m; }
}

public readonly struct BlockCommitted { }
using _00.WorkSpace.GIL.Scripts.Messages;
using UnityEngine;

[AddComponentMenu("FX/ClearEventResponder")]
public sealed class ClearEventResponder : MonoBehaviour, IManager
{
    [Header("Preview")]
    [SerializeField] private bool enablePerimeterPreview = true;

    [Header("Line FX Colors")]
    [SerializeField] private Color normalLineColor = Color.white;

    private EventQueue _bus;
    private bool _subscribed;

    // IManager
    public int Order => 60;
    public void PreInit() { }
    public void Init() { EnsureSubscribed(); }
    public void PostInit() { }

    public void SetDependencies(EventQueue bus)
    {
        _bus = bus;
        _subscribed = false;
        Debug.Log("[Responder] SetDependencies done");
    }

    private void EnsureSubscribed()
    {
        if (_subscribed || _bus == null) return;

        Debug.Log("[Responder] Subscribing to events (after Init)");
        _bus.Subscribe<_00.WorkSpace.GIL.Scripts.Messages.LinesWillClear>(OnLinesWillClear, replaySticky: false);
        _bus.Subscribe<_00.WorkSpace.GIL.Scripts.Messages.LinesCleared>(OnLinesCleared, replaySticky: false);
        _bus.Subscribe<_00.WorkSpace.GIL.Scripts.Messages.AllClear>(OnAllClear, replaySticky: false);

        _subscribed = true;
    }


    void OnEnable() { StartCoroutine(GameBindingUtil.WaitAndRun(() => TryBindBus())); }
    void Start() { TryBindBus(); }
    private void TryBindBus()
    {
        if (_bus != null || !Game.IsBound) return;
        SetDependencies(Game.Bus);
    }

    private void OnLinesWillClear(LinesWillClear e)
    {
        if (!enablePerimeterPreview) return;
        if (Game.Fx == null) return;

        if (e.rows != null) foreach (var r in e.rows) Game.Fx.PlayRowPerimeter(r, e.destroySprite);
        if (e.cols != null) foreach (var c in e.cols) Game.Fx.PlayColPerimeter(c, e.destroySprite);
    }

    private void OnLinesCleared(LinesCleared e)
    {
        // === FX ===
        if (Game.Fx != null)
        {
            bool isCombo = e.combo > 1;
            if (e.rows != null)
                foreach (var r in e.rows)
                    if (isCombo) Game.Fx.PlayComboRow(r, null);
                    else Game.Fx.PlayRow(r, normalLineColor);

            if (e.cols != null)
                foreach (var c in e.cols)
                    if (isCombo) Game.Fx.PlayComboCol(c, null);
                    else Game.Fx.PlayCol(c, normalLineColor);
        }

        int total = (e.rows?.Length ?? 0) + (e.cols?.Length ?? 0);
        Game.Audio?.PlayLineClear(Mathf.Clamp(total, 1, 6)); // 1~6 단계 재생
        if (e.combo > 1)
            Game.Audio?.PlayClearCombo(Mathf.Min(e.combo, 8)); // 콤보 보강음 (선택)
    }

    private void OnAllClear(AllClear e)
    {
        Debug.Log($"[Responder] OnAllClear recv combo={e.combo} world?={e.fxWorld.HasValue}");

        if (e.fxWorld.HasValue) Game.BlockFx?.PlayAllClearAtWorld(e.fxWorld.Value);
        else Game.BlockFx?.PlayAllClear();

        Game.Audio?.PlayLineClear(6);
        Game.Audio?.PlayClearCombo(Mathf.Min(e.combo + 1, 8));
    }
}

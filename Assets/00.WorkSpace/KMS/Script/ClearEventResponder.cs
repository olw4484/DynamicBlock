using _00.WorkSpace.GIL.Scripts.Messages;
using UnityEngine;

[AddComponentMenu("FX/ClearEventResponder")]
public sealed class ClearEventResponder : MonoBehaviour, IManager
{
    [Header("Preview")]
    [SerializeField] private bool enablePerimeterPreview = true;
    [SerializeField] private Color perimeterColor = new Color(1f, 1f, 1f, 1f);
    [SerializeField] private float perimeterTTL = 0.4f;

    [Header("Line FX Colors")]
    [SerializeField] private Color normalLineColor = Color.white;

    private Coroutine _perimeterTimeout;
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
    }

    private void EnsureSubscribed()
    {
        if (_subscribed || _bus == null) return;

        _bus.Subscribe<_00.WorkSpace.GIL.Scripts.Messages.LinesWillClear>(OnLinesWillClear, replaySticky: false);
        _bus.Subscribe<_00.WorkSpace.GIL.Scripts.Messages.LinesCleared>(OnLinesCleared, replaySticky: false);
        _bus.Subscribe<_00.WorkSpace.GIL.Scripts.Messages.AllClear>(OnAllClear, replaySticky: false);

        _subscribed = true;
    }
    private void OnLinesWillClear(_00.WorkSpace.GIL.Scripts.Messages.LinesWillClear e)
    {
        if (!enablePerimeterPreview) return;

        // ���� ���� ���� ����
        Game.BlockFx?.StopAllLoop();

        // ����� �׵θ� ���� ����
        if (e.rows != null) foreach (var r in e.rows)
                Game.BlockFx?.PlayRowPerimeter(r, perimeterColor);
        if (e.cols != null) foreach (var c in e.cols)
                Game.BlockFx?.PlayColPerimeter(c, perimeterColor);

        // ������ġ: TTL ������ �ڵ� ���� (LinesCleared�� �� ���� ��� ���)
        if (_perimeterTimeout != null) StopCoroutine(_perimeterTimeout);
        if (perimeterTTL > 0f) _perimeterTimeout = StartCoroutine(PerimeterTimeout());
    }

    private System.Collections.IEnumerator PerimeterTimeout()
    {
        yield return new WaitForSeconds(perimeterTTL);
        Game.BlockFx?.StopAllLoop();
    }

    void OnEnable() { StartCoroutine(GameBindingUtil.WaitAndRun(() => TryBindBus())); }
    void Start() { TryBindBus(); }
    private void TryBindBus()
    {
        if (_bus != null || !Game.IsBound) return;
        SetDependencies(Game.Bus);
    }

    private void OnLinesCleared(_00.WorkSpace.GIL.Scripts.Messages.LinesCleared e)
    {
        // 1) ���� ���� ����
        Game.BlockFx?.StopAllLoop();

        // 2) ���� ���� Ŭ���� �ܹ� FX
        bool combo = e.combo > 1;
        if (Game.Fx != null)
        {
            if (e.rows != null) foreach (var r in e.rows)
                    if (combo) Game.Fx.PlayComboRow(r, null);
                    else Game.Fx.PlayRow(r, Color.white);
            if (e.cols != null) foreach (var c in e.cols)
                    if (combo) Game.Fx.PlayComboCol(c, null);
                    else Game.Fx.PlayCol(c, Color.white);
        }

        // 3) SFX
        int total = (e.rows?.Length ?? 0) + (e.cols?.Length ?? 0);
        Game.Audio?.PlayLineClear(Mathf.Clamp(total, 1, 6));
        if (e.combo > 1) Game.Audio?.PlayClearCombo(Mathf.Min(e.combo, 8));
    }

    private void OnAllClear(_00.WorkSpace.GIL.Scripts.Messages.AllClear e)
    {
        Game.BlockFx?.StopAllLoop();
        if (e.fxWorld.HasValue) Game.BlockFx?.PlayAllClearAtWorld(e.fxWorld.Value);
        else Game.BlockFx?.PlayAllClear();

        Game.Audio?.PlayLineClear(6);
        Game.Audio?.PlayClearCombo(Mathf.Min(e.combo + 1, 8));
    }
}

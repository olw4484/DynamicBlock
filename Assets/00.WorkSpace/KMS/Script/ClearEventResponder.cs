using _00.WorkSpace.GIL.Scripts.Messages;
using UnityEngine;
using System.Collections;

public enum SfxPolicy { Layered, ComboOverridesLine, Staggered } // ��ħ, �޺��켱, ����

[AddComponentMenu("FX/ClearEventResponder")]
public sealed class ClearEventResponder : MonoBehaviour, IManager
{


    [Header("Preview")]
    [SerializeField] private bool enablePerimeterPreview = true;
    [SerializeField] private Color perimeterColor = new Color(1f, 1f, 1f, 1f);
    [SerializeField] private float perimeterTTL = 0.4f;

    [Header("Line FX Colors")]
    [SerializeField] private Color normalLineColor = Color.white;

    [Header("SFX Policy")]
    [SerializeField] private SfxPolicy sfxPolicy = SfxPolicy.Staggered;
    [SerializeField, Range(0f, 0.5f)] private float comboDelay = 0.12f; // ���� ��å�� ��

    private const int ComboStartThreshold = 2;

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
                Game.BlockFx?.PlayRowPerimeter(r, e.destroySprite);
        if (e.cols != null) foreach (var c in e.cols)
                Game.BlockFx?.PlayColPerimeter(c, e.destroySprite);

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

        // 2) ���� ��ƼŬ
        bool hasCombo = e.combo >= ComboStartThreshold;
        if (Game.Fx != null)
        {
            if (e.rows != null)
            {
                foreach (var r in e.rows)
                    if (hasCombo) Game.Fx.PlayComboRow(r, e.destroySprite);
                    else Game.Fx.PlayRow(r, Color.white);
            }
            if (e.cols != null)
            {
                foreach (var c in e.cols)
                    if (hasCombo) Game.Fx.PlayComboCol(c, e.destroySprite);
                    else Game.Fx.PlayCol(c, Color.white);
            }
        }

        // 3) SFX
        int total = Mathf.Clamp((e.rows?.Length ?? 0) + (e.cols?.Length ?? 0), 1, 6);

        switch (sfxPolicy)
        {
            case SfxPolicy.Layered:
                Game.Audio?.PlayLineClear(total);
                if (hasCombo) Game.Audio?.PlayClearCombo(Mathf.Min(e.combo, 8));
                break;

            case SfxPolicy.ComboOverridesLine:
                if (hasCombo) Game.Audio?.PlayClearCombo(Mathf.Min(e.combo, 8));
                else Game.Audio?.PlayLineClear(total);
                break;

            case SfxPolicy.Staggered:
                Game.Audio?.PlayLineClear(total);
                if (hasCombo) StartCoroutine(PlayComboSfxAfter(comboDelay, e.combo));
                break;
        }
    }

    private IEnumerator PlayComboSfxAfter(float delay, int combo)
    {
        if (delay > 0f) yield return new WaitForSeconds(delay);
        Game.Audio?.PlayClearCombo(Mathf.Min(combo, 8));
    }

    private IEnumerator PlayComboSfxAfter(float delay, int combo)
    {
        if (delay > 0f) yield return new WaitForSeconds(delay);
        Game.Audio?.PlayClearCombo(Mathf.Min(combo, 8));
    }

    // AllClear�� ����
    private void OnAllClear(_00.WorkSpace.GIL.Scripts.Messages.AllClear e)
    {
        Game.BlockFx?.StopAllLoop();
        if (e.fxWorld.HasValue) Game.BlockFx?.PlayAllClearAtWorld(e.fxWorld.Value);
        else Game.BlockFx?.PlayAllClear();

        // ��Ŭ�� �� �� ����ص� �̵�(��� �̺�Ʈ)
        Game.Audio?.PlayLineClear(6);
        Game.Audio?.PlayClearCombo(Mathf.Min(e.combo + 1, 8));
    }
}

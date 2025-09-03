using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public sealed class BlockFxFacade : MonoBehaviour
{
    [Header("Bridged Managers")]
    [SerializeField] private ParticleManager particleManager;
    [SerializeField] private AudioFxFacade audioFx;
    [SerializeField] private EffectLane effectLane;

    [Header("Optional Data")]
    [SerializeField] private FxTheme theme;

    [Header("IDs")]
    [SerializeField] private int rowClearSfxId = (int)SfxId.RowClear;
    [SerializeField] private int colClearSfxId = (int)SfxId.ColClear;
    [SerializeField] private int rowClearFxId = 2000;
    [SerializeField] private int colClearFxId = 2001;

    bool _subscribed;

    private void Awake()
    {
        if (!particleManager) Debug.LogWarning("[BlockFxFacade] ParticleManager missing.");
        if (!audioFx) Debug.LogWarning("[BlockFxFacade] AudioFxFacade missing.");
    }

    private void OnEnable()
    {
        StartCoroutine(DelaySubscribe());
    }

    private void OnDisable()
    {
        if (_subscribed && Game.IsBound)
        {
            Game.Bus.Unsubscribe<RowClearFxEvent>(OnRowFx);
            Game.Bus.Unsubscribe<ColClearFxEvent>(OnColFx);
            _subscribed = false;
        }
    }

    private IEnumerator DelaySubscribe()
    {
        // Game.Bind 이후 구독 보장
        yield return null;
        if (!_subscribed && Game.IsBound)
        {
            Game.Bus.Subscribe<RowClearFxEvent>(OnRowFx, replaySticky: false);
            Game.Bus.Subscribe<ColClearFxEvent>(OnColFx, replaySticky: false);
            _subscribed = true;
        }
    }

    // ========== 표준 API ==========
    public void PlayRowClear(int row, Color c)
    {
        var color = ResolveRowColor(c);
        PlayRowParticle(row, color);
        EnqueueRowLane(row);
        PlayRowSound();
    }

    public void PlayColClear(int col, Color c)
    {
        var color = ResolveColColor(c);
        PlayColParticle(col, color);
        EnqueueColLane(col);
        PlayColSound();
    }

    public void PlayBlockPlace(int sfxId = (int)SfxId.BlockPlace)
    {
        if (audioFx) audioFx.EnqueueSound(sfxId);
    }

    public void PlayBlockSelect(int sfxId = (int)SfxId.BlockSelect)
    {
        if (audioFx) audioFx.EnqueueSound(sfxId);
    }

    // ========== 이벤트 핸들러 ==========
    private void OnRowFx(RowClearFxEvent e) => PlayRowClear(e.row, e.color);
    private void OnColFx(ColClearFxEvent e) => PlayColClear(e.col, e.color);

    // ========== 내부 헬퍼 ==========
    private Color ResolveRowColor(Color candidate)
    {
        // 호출 인자 우선, 없으면 테마, 없으면 기본
        if (candidate.a > 0f) return candidate;
        if (theme) return theme.rowClearColor;
        return Color.cyan;
    }

    private Color ResolveColColor(Color candidate)
    {
        if (candidate.a > 0f) return candidate;
        if (theme) return theme.colClearColor;
        return Color.magenta;
    }

    private void PlayRowParticle(int row, Color color)
    {
        if (!particleManager) return;
        particleManager.PlayRowParticle(row, color);
    }

    private void PlayColParticle(int col, Color color)
    {
        if (!particleManager) return;
        particleManager.PlayColParticle(col, color);
    }

    private void EnqueueRowLane(int row)
    {
        // 선택: 레인에 “마커”로도 남겨 스로틀/분석/리플레이 활용
        if (!effectLane) return;
        // row를 pos.y에, col은 0으로 인코딩(현 구조에서 색상은 lane으로 전달 불가)
        effectLane.Enqueue(new EffectEvent(id: rowClearFxId, pos: new Vector3(0, row, 0)));
    }

    private void EnqueueColLane(int col)
    {
        if (!effectLane) return;
        effectLane.Enqueue(new EffectEvent(id: colClearFxId, pos: new Vector3(col, 0, 0)));
    }

    private void PlayRowSound()
    {
        if (audioFx) audioFx.EnqueueSound(rowClearSfxId);
        else if (Game.Audio != null) Game.Audio.PlayLineClear(1);
    }

    private void PlayColSound()
    {
        if (audioFx) audioFx.EnqueueSound(colClearSfxId);
        else if (Game.Audio != null) Game.Audio.PlayLineClear(1);
    }
}

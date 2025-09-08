using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public sealed class BlockFxFacade : MonoBehaviour
{
    [Header("Bridged")]
    [SerializeField] private ParticleManager particle;
    [SerializeField] private EffectLane effectLane; 
    [SerializeField] private AudioFxFacade audioFx; 

    [Header("IDs")]
    [SerializeField] private int rowClearFxId = 2000;
    [SerializeField] private int colClearFxId = 2001;
    [SerializeField] public int rowClearSfxId = (int)SfxId.LineClear1;
    [SerializeField] public int colClearSfxId = (int)SfxId.LineClear1;

    [Header("Optional")]
    [SerializeField] private FxTheme theme; 
    [SerializeField] private Transform fxSpawnParent;

    // 외부 API
    public void PlayRowClear(int row, Color color)
    {
        if (particle != null)
        {
            particle.PlayRowParticle(row, ResolveRowColor(color));
        }
        audioFx?.EnqueueSound(rowClearSfxId);
    }

    public void PlayColClear(int col, Color color)
    {
        if (particle != null)
        {
            particle.PlayColParticle(col, ResolveColColor(color));
        }
        audioFx?.EnqueueSound(colClearSfxId);
    }

    // 이벤트 핸들러(사용안함)
    private void OnRowFx(RowClearFxEvent e) => PlayRowClear(e.row, e.color);
    private void OnColFx(ColClearFxEvent e) => PlayColClear(e.col, e.color);

    // 내부 헬퍼
    private Color ResolveRowColor(Color candidate)
    => candidate.a > 0f ? candidate : (theme ? theme.rowClearColor : Color.cyan);
    private Color ResolveColColor(Color candidate)
      => candidate.a > 0f ? candidate : (theme ? theme.colClearColor : Color.magenta);
}
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public sealed class BlockFxFacade : MonoBehaviour
{
    [Header("Bridged Managers")]
    [SerializeField] private ParticleManager particleManager;
    [SerializeField] private AudioFxFacade audioFx; // 기존 파사드

    [Header("Optional Data")]
    [SerializeField] private FxTheme theme;

    // === 표준 API ===
    public void PlayRowClear(int rowIndex, Color? color = null, int sfxId = -1)
    {
        var c = color ?? (theme ? theme.rowClearColor : Color.cyan);
        particleManager.PlayRowParticle(rowIndex, c);

        int id = sfxId >= 0 ? sfxId : (theme ? theme.rowClearSfxId : (int)SfxId.RowClear);
        audioFx.EnqueueSound(id);
    }

    public void PlayColClear(int colIndex, Color? color = null, int sfxId = -1)
    {
        var c = color ?? (theme ? theme.colClearColor : Color.magenta);
        particleManager.PlayColParticle(colIndex, c);

        int id = sfxId >= 0 ? sfxId : (theme ? theme.colClearSfxId : (int)SfxId.ColClear);
        audioFx.EnqueueSound(id);
    }

    // 필요시: 블록 배치 등 묶음 호출
    public void PlayBlockPlace(int sfxId = (int)SfxId.BlockPlace)
    {
        audioFx.EnqueueSound(sfxId);
        // 파티클 연출 추가하고 싶으면 여기서 호출
    }

    public void PlayBlockSelect(int sfxId = (int)SfxId.BlockSelect)
    {
        audioFx.EnqueueSound(sfxId);
    }
}
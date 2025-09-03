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

    // === 표준 API ===
    public void PlayRowClear(int row, Color c)
    {
        // 이펙트
        effectLane.Enqueue(new EffectEvent(id: 2000, pos: new Vector3(0, row, 0)));
        // 사운드
        audioFx.EnqueueSound((int)SfxId.RowClear);
    }

    public void PlayColClear(int col, Color c)
    {
        effectLane.Enqueue(new EffectEvent(id: 2001, pos: new Vector3(col, 0, 0)));
        audioFx.EnqueueSound((int)SfxId.ColClear);
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
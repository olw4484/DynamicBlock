using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// ===== 사운드 레인 =====
public sealed class SoundLane : LaneBase<SoundEvent>
{
    [SerializeField] private AudioManager audioMgr;
    [SerializeField] private AudioClip[] idTable; // 인덱스=ID, 또는 Dictionary로 교체

    protected override bool TryConsume(SoundEvent e)
    {
        if (!IsCooledDown(e.id)) return false;
        var clip = ResolveClip(e.id);
        if (!clip) return true; // 소비는 했지만 재생할 건 없음(로그만)
        // delayMs 필요하면 코루틴으로 지연
        audioMgr.PlaySE(clip);
        return true;
    }

    private AudioClip ResolveClip(int id)
    {
        if (id < 0 || id >= idTable.Length) return null;
        return idTable[id];
    }
}
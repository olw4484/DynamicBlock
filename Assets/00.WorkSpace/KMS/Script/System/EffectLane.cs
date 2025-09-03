using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// ===== ����Ʈ ���� =====
public sealed class EffectLane : LaneBase<EffectEvent>
{
    [SerializeField] private ParticleManager particleMgr;
    // ¦��=Row, Ȧ��=Col
    [SerializeField] private Color defaultRowColor = Color.cyan;
    [SerializeField] private Color defaultColColor = Color.magenta;

    protected override bool TryConsume(EffectEvent e)
    {
        if (!IsCooledDown(e.id)) return false;

        if ((e.id % 2) == 0)
            particleMgr.PlayRowParticle(Mathf.RoundToInt(e.pos.y), defaultRowColor);
        else
            particleMgr.PlayColParticle(Mathf.RoundToInt(e.pos.x), defaultColColor);

        return true;
    }
}
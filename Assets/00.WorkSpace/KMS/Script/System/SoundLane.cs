using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// ===== ���� ���� =====
public sealed class SoundLane : LaneBase<SoundEvent>
{
    [SerializeField] private AudioManager audioMgr;
    [SerializeField] private AudioClip[] idTable; // �ε���=ID, �Ǵ� Dictionary�� ��ü

    protected override bool TryConsume(SoundEvent e)
    {
        if (!IsCooledDown(e.id)) return false;
        var clip = ResolveClip(e.id);
        if (!clip) return true; // �Һ�� ������ ����� �� ����(�α׸�)
        // delayMs �ʿ��ϸ� �ڷ�ƾ���� ����
        audioMgr.PlaySE(clip);
        return true;
    }

    private AudioClip ResolveClip(int id)
    {
        if (id < 0 || id >= idTable.Length) return null;
        return idTable[id];
    }
}
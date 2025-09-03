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

    // === ǥ�� API ===
    public void PlayRowClear(int row, Color c)
    {
        // ����Ʈ
        effectLane.Enqueue(new EffectEvent(id: 2000, pos: new Vector3(0, row, 0)));
        // ����
        audioFx.EnqueueSound((int)SfxId.RowClear);
    }

    public void PlayColClear(int col, Color c)
    {
        effectLane.Enqueue(new EffectEvent(id: 2001, pos: new Vector3(col, 0, 0)));
        audioFx.EnqueueSound((int)SfxId.ColClear);
    }

    // �ʿ��: ��� ��ġ �� ���� ȣ��
    public void PlayBlockPlace(int sfxId = (int)SfxId.BlockPlace)
    {
        audioFx.EnqueueSound(sfxId);
        // ��ƼŬ ���� �߰��ϰ� ������ ���⼭ ȣ��
    }

    public void PlayBlockSelect(int sfxId = (int)SfxId.BlockSelect)
    {
        audioFx.EnqueueSound(sfxId);
    }
}
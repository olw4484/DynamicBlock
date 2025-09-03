using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public sealed class BlockFxFacade : MonoBehaviour
{
    [Header("Bridged Managers")]
    [SerializeField] private ParticleManager particleManager;
    [SerializeField] private AudioFxFacade audioFx; // ���� �Ļ��

    [Header("Optional Data")]
    [SerializeField] private FxTheme theme;

    // === ǥ�� API ===
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
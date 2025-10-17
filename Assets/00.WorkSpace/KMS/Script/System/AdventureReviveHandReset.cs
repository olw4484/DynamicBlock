using _00.WorkSpace.GIL.Scripts.Blocks;
using _00.WorkSpace.GIL.Scripts.Managers;
using _00.WorkSpace.GIL.Scripts.Messages;
using System.Collections;
using UnityEngine;

public sealed class AdventureReviveHandReset : MonoBehaviour
{
    EventQueue _bus;

    void OnEnable()
    {
        StartCoroutine(GameBindingUtil.WaitAndRun(() =>
        {
            _bus = Game.Bus;
            // ��Ȱ Ȯ�� ����(ContinueGranted) ����
            _bus.Subscribe<ContinueGranted>(OnContinueGranted, replaySticky: false);
            // ����) BlockStorage�� ��Ȱ ���̺긦 �̹� ���������� �˸��� ��ȣ
            _bus.Subscribe<RevivePerformed>(_ => { /* �ʿ��ϸ� ���⿡�� ��ó�� */ }, replaySticky: false);
        }));
    }

    void OnDisable()
    {
        if (_bus == null) return;
        _bus.Unsubscribe<ContinueGranted>(OnContinueGranted);
        _bus.Unsubscribe<RevivePerformed>(_ => { });
    }

    private void OnContinueGranted(ContinueGranted _)
    {
        var mm = MapManager.Instance;
        if (!mm || mm.CurrentMode != GameMode.Adventure) return;

        // BlockStorage�� ���� �����ӿ� ���̺긦 ���� �� ������ �� ������ �纸 �� Ȯ��
        StartCoroutine(CoResetIfNeededNextFrame());
    }

    IEnumerator CoResetIfNeededNextFrame()
    {
        yield return null; // BlockStorage.OnContinueGranted�� ���� �� ��ȸ ����

        var mm = MapManager.Instance;
        if (!mm || mm.CurrentMode != GameMode.Adventure) yield break;

        var bs = FindFirstObjectByType<BlockStorage>(FindObjectsInactive.Include);
        if (!bs) yield break;

        // �̹� BlockStorage�� ��Ȱ ���̺긦 �����ؼ� ���а� �����Ѵٸ� �ƹ� �͵� ���� ����(�ߺ� ����)
        if (bs.CurrentBlocks != null && bs.CurrentBlocks.Count > 0) yield break;

        // ������� �Դٴ� �� ���а� ����ְų� ������ ���� ���̽� �� ���� ����

        // 1) ���� ����
        bs.ClearHand();

        // 2) ���� �������� ��� ������ �ǵ��������� �� ����
        mm.saveManager?.SkipNextSnapshot("AdvReviveHandReset");
        mm.saveManager?.SuppressSnapshotsFor(0.5f);

        // 3) ��庥ó ��Ģ�� ���� �� ���� ����
        //    (GenerateAllBlocks�� ��庥ó�� �� ���� ��������/��ǥ �ݿ� + ������� ����)
        bs.GenerateAllBlocks();

        // 4) ��ó��
        ScoreManager.Instance?.OnHandRefilled();
        mm.saveManager?.SaveCurrentBlocksFromStorage(bs);
    }
}

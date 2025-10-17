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
            // 부활 확정 시점(ContinueGranted) 수신
            _bus.Subscribe<ContinueGranted>(OnContinueGranted, replaySticky: false);
            // 선택) BlockStorage가 부활 웨이브를 이미 적용했음을 알리는 신호
            _bus.Subscribe<RevivePerformed>(_ => { /* 필요하면 여기에서 후처리 */ }, replaySticky: false);
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

        // BlockStorage가 같은 프레임에 웨이브를 넣을 수 있으니 한 프레임 양보 후 확인
        StartCoroutine(CoResetIfNeededNextFrame());
    }

    IEnumerator CoResetIfNeededNextFrame()
    {
        yield return null; // BlockStorage.OnContinueGranted가 먼저 돌 기회 제공

        var mm = MapManager.Instance;
        if (!mm || mm.CurrentMode != GameMode.Adventure) yield break;

        var bs = FindFirstObjectByType<BlockStorage>(FindObjectsInactive.Include);
        if (!bs) yield break;

        // 이미 BlockStorage가 부활 웨이브를 적용해서 손패가 존재한다면 아무 것도 하지 않음(중복 방지)
        if (bs.CurrentBlocks != null && bs.CurrentBlocks.Count > 0) yield break;

        // 여기까지 왔다는 건 손패가 비어있거나 복원이 꼬인 케이스 → 강제 리필

        // 1) 손패 비우기
        bs.ClearHand();

        // 2) 저장 스냅샷이 방금 리필을 되돌려버리는 걸 방지
        mm.saveManager?.SkipNextSnapshot("AdvReviveHandReset");
        mm.saveManager?.SuppressSnapshotsFor(0.5f);

        // 3) 어드벤처 규칙에 맞춘 새 손패 생성
        //    (GenerateAllBlocks는 어드벤처일 때 과일 오버레이/목표 반영 + 저장까지 수행)
        bs.GenerateAllBlocks();

        // 4) 후처리
        ScoreManager.Instance?.OnHandRefilled();
        mm.saveManager?.SaveCurrentBlocksFromStorage(bs);
    }
}

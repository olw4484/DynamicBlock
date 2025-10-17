using _00.WorkSpace.GIL.Scripts.Managers;
using _00.WorkSpace.GIL.Scripts.Maps;
using _00.WorkSpace.GIL.Scripts.Messages;
using UnityEngine;

public sealed class ReviveResultProbe : MonoBehaviour
{
    EventQueue _bus;

    void OnEnable()
    {
        StartCoroutine(GameBindingUtil.WaitAndRun(() =>
        {
            _bus = Game.Bus;

            _bus.Subscribe<PlayerDowned>(e => {
                Debug.Log($"[PROBE] PlayerDowned score={e.score} mode={CurMode()} " +
                          $"revivePending={AdStateProbe.IsRevivePending} gate={ReviveGate.IsArmed}");
            }, replaySticky: false);

            _bus.Subscribe<GiveUpRequest>(_ => {
                Debug.Log($"[PROBE] GiveUpRequest recv mode={CurMode()} " +
                          $"revivePending={AdStateProbe.IsRevivePending} gate={ReviveGate.IsArmed}");
            }, replaySticky: false);

            _bus.Subscribe<ReviveRequest>(_ => {
                Debug.Log($"[PROBE] ReviveRequest recv. CanOffer={Game.Ads?.CanOfferReviveNow()}");
            }, replaySticky: false);

            _bus.Subscribe<ContinueGranted>(_ => {
                Debug.Log($"[PROBE] ContinueGranted ¡æ pending={AdStateProbe.IsRevivePending} gate={ReviveGate.IsArmed}");
            }, replaySticky: false);

            _bus.Subscribe<RevivePerformed>(_ => {
                Debug.Log($"[PROBE] RevivePerformed ¡æ pending={AdStateProbe.IsRevivePending} gate={ReviveGate.IsArmed}");
            }, replaySticky: false);

            _bus.Subscribe<GameOverConfirmed>(e => {
                Debug.Log($"[PROBE] GOC recv score={e.score} isNewBest={e.isNewBest} mode={CurMode()} " +
                          $"sup={UIStateProbe.ResultGuardActive} anyModal={UIStateProbe.IsAnyModalOpen} " +
                          $"reviveOpen={UIStateProbe.IsReviveOpen} full={AdStateProbe.IsFullscreenShowing} " +
                          $"revivePending={AdStateProbe.IsRevivePending} gate={ReviveGate.IsArmed}");
            }, replaySticky: false);

            _bus.Subscribe<AdventureStageCleared>(e => {
                Debug.Log($"[PROBE] AD Cleared kind={e.kind} score={e.finalScore}");
            }, replaySticky: false);

            _bus.Subscribe<AdventureStageFailed>(e => {
                Debug.Log($"[PROBE] AD Failed kind={e.kind} score={e.finalScore}");
            }, replaySticky: false);

            _bus.Subscribe<PanelToggle>(e => {
                Debug.Log($"[PROBE] PanelToggle {e.key} -> {e.on}");
            }, replaySticky: true);
        }));
    }

    string CurMode() => (MapManager.Instance?.CurrentMode ?? GameMode.Classic).ToString();
}

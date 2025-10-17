using UnityEngine;

public static class ReviveCleanup
{
    /// <summary>
    /// 부활 실패/포기/광고불가 등 모든 종료 경로에서 안전하게 호출하는 정리 함수
    /// - Revive/Result 관련 가드와 플래그를 일괄 해제
    /// - GameOverUtil의 pending/suppress nonce를 증가시켜 레이스 방지
    /// </summary>
    public static void ResetAll(string why = null)
    {
        ReviveGate.Disarm();
        ReviveLatch.Disarm("cleanup");
        AdStateProbe.IsRevivePending = false;

        UIStateProbe.DisarmReviveGrace();
        UIStateProbe.DisarmResultGuard();
        UIStateProbe.IsReviveOpen = false; // 방어적 해제
        UIStateProbe.IsAnyModalOpen = false; // 방어적 해제

        GameOverUtil.ResetAll(why ?? "revive_cleanup");
        Debug.Log($"[ReviveCleanup] ResetAll ({why ?? "-"})");
    }
}

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

static class GameBindingUtil
{
    public static IEnumerator WaitAndRun(System.Action action)
    {
        while (!Game.IsBound) yield return null; // Game.Bind 완료 대기
        yield return null;                        // 한 프레임 더 대기(순서 안정)
        action?.Invoke();
    }
}

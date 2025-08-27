using System.Collections;
using System.Collections.Generic;
using UnityEngine;

static class GameBindingUtil
{
    public static IEnumerator WaitAndRun(System.Action action)
    {
        while (!Game.IsBound) yield return null; // Game.Bind �Ϸ� ���
        yield return null;                        // �� ������ �� ���(���� ����)
        action?.Invoke();
    }
}

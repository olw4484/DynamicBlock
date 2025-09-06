using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public sealed class CoroutineHost : MonoBehaviour
{
    static CoroutineHost _inst;
    [RuntimeInitializeOnLoadMethod]
    static void Bootstrap()
    {
        if (_inst) return;
        var go = new GameObject("[CoroutineHost]");
        DontDestroyOnLoad(go);
        _inst = go.AddComponent<CoroutineHost>();
    }
    public static Coroutine Run(IEnumerator co)
    {
        if (_inst == null) Bootstrap();
        return _inst.StartCoroutine(co);
    }
}

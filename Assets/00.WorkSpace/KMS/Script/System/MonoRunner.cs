using System.Collections;
using UnityEngine;

/// 傈开 内风凭 角青扁
public sealed class MonoRunner : MonoBehaviour
{
    static MonoRunner _i;
    public static void Run(IEnumerator co)
    {
        if (_i == null)
        {
            var go = new GameObject("~MonoRunner");
            DontDestroyOnLoad(go);
            _i = go.AddComponent<MonoRunner>();
        }
        _i.StartCoroutine(co);
    }
}
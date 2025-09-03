using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "EffectRegistry", menuName = "FX/EffectRegistry")]
public sealed class EffectRegistry : ScriptableObject
{
    [System.Serializable]
    public class Entry
    {
        public int id;                 // 2000(RowClear), 2001(ColClear) 등
        public GameObject prefab;      // 에셋의 파티클 프리팹
        public bool supportsColor = false;
        public Vector3 defaultScale = Vector3.one;
    }

    public List<Entry> entries;
    Dictionary<int, Entry> map;

    public Entry Get(int id)
    {
        map ??= Build();
        return map.TryGetValue(id, out var e) ? e : null;
    }
    Dictionary<int, Entry> Build()
    {
        var d = new Dictionary<int, Entry>();
        foreach (var e in entries) if (e != null) d[e.id] = e;
        return d;
    }
}

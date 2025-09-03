using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// ===== ����Ʈ Ǯ (ID�� 5�� ���� ����) =====
public sealed class EffectPool : MonoBehaviour
{
    [SerializeField] private GameObject[] effectPrefabs; // id = index
    [SerializeField] private int poolPerEffect = 5;

    private readonly Dictionary<int, Stack<GameObject>> _pools = new();

    void Awake()
    {
        for (int id = 0; id < effectPrefabs.Length; id++)
        {
            var prefab = effectPrefabs[id];
            if (!prefab) continue;

            var stack = new Stack<GameObject>(poolPerEffect);
            for (int i = 0; i < poolPerEffect; i++)
            {
                var go = Instantiate(prefab, transform);
                go.SetActive(false);
                stack.Push(go);
            }
            _pools[id] = stack;
        }
    }

    public GameObject Rent(int id)
    {
        if (_pools.TryGetValue(id, out var stack) && stack.Count > 0)
            return stack.Pop();
        return null; // �����ϸ� ���(�Ǵ� Ȯ�� ��å �߰�)
    }

    public void Release(int id, GameObject go)
    {
        go.SetActive(false);
        if (_pools.TryGetValue(id, out var stack)) stack.Push(go);
        else Destroy(go);
    }

    public void ClearAll()
    {
        foreach (var kv in _pools)
            foreach (var go in kv.Value) { go.SetActive(false); }
    }
}

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

// ===== ����Ʈ ���� =====
public sealed class EffectLane : LaneBase<EffectEvent>
{
    [SerializeField] private EffectRegistry registry;

    [Header("Spawn")]
    [SerializeField] private Transform spawnParent;
    [SerializeField] private bool useLocalPosition = true;


    private readonly Dictionary<int, ObjectPool<GameObject>> pools = new();
    public void SetSpawnParent(Transform parent, bool useLocal)
    {
        spawnParent = parent;
        useLocalPosition = useLocal;
    }
    protected override bool TryConsume(EffectEvent e)
    {
        if (!IsCooledDown(e.id)) return false;

        var entry = registry ? registry.Get(e.id) : null;
        if (entry == null || !entry.prefab)
        {
            Debug.LogWarning($"[EffectLane] No mapping for id={e.id}");
            return true; // �Һ� �Ϸ� ó��
        }

        var go = GetPool(e.id, entry.prefab).Get();
        var t = go.transform;

        // 1) �θ� ���� ���̰�(worldPositionStays: false)
        if (spawnParent) t.SetParent(spawnParent, worldPositionStays: false);
        else t.SetParent(null, worldPositionStays: false);

        // 2) ��ǥ/������ ���� (UI/�Ϲ� ��� ����)
        ApplyTransform(t, e.pos, entry.defaultScale, useLocalPosition && spawnParent != null);

        // 3) �÷� ����(�ɼ�)
        if (entry.supportsColor && e.hasColor) TryApplyColor(go, e.color);

        // 4) ���(����/������ �ݿ� ���� ����) + �ڵ� ��ȯ
        PlayAll(go);
        PlayAndReturn(go, e.id);
        return true;
    }

    private void ApplyTransform(Transform t, Vector3 pos, Vector3 scale, bool useLocal)
    {
        // RectTransform�̸� UI ��ǥ���
        var rt = t as RectTransform ?? t.GetComponent<RectTransform>();

        if (useLocal)
        {
            if (rt != null)
            {
                // UI ĵ���� ����: anchoredPosition3D ���
                rt.anchoredPosition3D = pos;
                rt.localRotation = Quaternion.identity;
                rt.localScale = scale;
            }
            else
            {
                t.localPosition = pos;
                t.localRotation = Quaternion.identity;
                t.localScale = scale;
            }
        }
        else
        {
            t.SetPositionAndRotation(pos, Quaternion.identity);
            t.localScale = scale;
        }
    }

    // �ڽ� ��ƼŬ ���� ����/������ �ݿ��ǰ� ���� �� ���
    private void PlayAll(GameObject root)
    {
        var systems = root.GetComponentsInChildren<ParticleSystem>(true);
        for (int i = 0; i < systems.Length; i++)
        {
            var ps = systems[i];
            var main = ps.main;

            // ��ġ/ũ�� �ݿ��ǵ��� ����
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            if (main.loop) main.loop = false;

            ps.Clear(true);
            ps.Play(true);
        }
    }

    ObjectPool<GameObject> GetPool(int id, GameObject prefab)
    {
        if (pools.TryGetValue(id, out var pool)) return pool;

        pool = new ObjectPool<GameObject>(
            createFunc: () => {
                var go = Instantiate(prefab);
                go.SetActive(false);
                return go;
            },
            actionOnGet: go => {
                go.SetActive(true);
            },
            actionOnRelease: go => {
                go.SetActive(false);
            },
            actionOnDestroy: go => Destroy(go),
            collectionCheck: false, defaultCapacity: 8, maxSize: 64
        );
        pools[id] = pool;
        return pool;
    }

    private void PlayAndReturn(GameObject go, int id)
    {
        var systems = go.GetComponentsInChildren<ParticleSystem>(true);
        float longest = 0f;
        for (int i = 0; i < systems.Length; i++)
        {
            var ps = systems[i];
            var main = ps.main;
            if (main.loop) main.loop = false;           
            main.stopAction = ParticleSystemStopAction.None;
            ps.Clear(true);
            ps.Play(true);

            longest = Mathf.Max(longest, main.duration + main.startLifetime.constantMax);
        }

        StartCoroutine(ReturnAfter(go, id, longest));
    }

    private IEnumerator ReturnAfter(GameObject go, int id, float seconds)
    {
        yield return new WaitForSeconds(seconds);
        if (pools.TryGetValue(id, out var pool)) pool.Release(go);
        else go.SetActive(false);
    }

    void TryApplyColor(GameObject root, Color c)
    {
        foreach (var ps in root.GetComponentsInChildren<ParticleSystem>(true))
        {
            var main = ps.main;
            main.startColor = c;
        }
    }
}
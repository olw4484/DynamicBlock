using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// ===== ����Ʈ ���� =====
public sealed class EffectLane : MonoBehaviour
{
    [SerializeField] private int budgetPerFrame = 5;
    [SerializeField] private EffectPool pool;

    private readonly PhaseBuffer<EffectEvent> _buf = new();

    // �ܺο��� ȣ��
    public void Enqueue(in EffectEvent e) => _buf.Enqueue(e, e.delay);

    public void TickBegin() => _buf.TickBegin();

    public void Consume() => _buf.Consume(budgetPerFrame, Spawn);

    private void Spawn(EffectEvent e)
    {
        var go = pool.Rent(e.id);
        if (!go) return; // Ǯ �� á���� �ڿ������� ���

        go.transform.position = e.pos;
        go.SetActive(true);

        var ps = go.GetComponent<ParticleSystem>();
        if (ps != null) StartCoroutine(ReturnWhenStopped(ps, e.id, go));
        else StartCoroutine(ReturnAfter(e.id, go, 1.0f)); // ��ƼŬ ������ �ӽ� Ÿ�Ӿƿ�
    }

    private IEnumerator ReturnWhenStopped(ParticleSystem ps, int id, GameObject go)
    {
        ps.Play(true);
        while (ps.IsAlive(true)) yield return null;
        pool.Release(id, go);
    }

    private IEnumerator ReturnAfter(int id, GameObject go, float seconds)
    {
        yield return new WaitForSeconds(seconds);
        pool.Release(id, go);
    }

    public void ClearAll()
    {
        _buf.Clear();
        pool.ClearAll();
    }
}
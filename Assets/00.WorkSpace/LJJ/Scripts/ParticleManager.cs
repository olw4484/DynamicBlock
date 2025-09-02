using System.Collections.Generic;
using UnityEngine;

public class ParticleManager : MonoBehaviour
{
    [Header("Pool Settings")]
    public GameObject rowParticle;
    public GameObject colParticle;
    public int poolSize = 5;

    private Vector2 startPosition = Vector2.zero;
    private Vector2 spacing = new Vector2(5f, 5f);
    [SerializeField] private GameObject gridSquare;

    private Queue<ParticleSystem> rowPool = new Queue<ParticleSystem>();
    private Queue<ParticleSystem> colPool = new Queue<ParticleSystem>();

    private void Awake()
    {
        InitializePool();
    }

    // Ǯ �ʱ�ȭ
    private void InitializePool()
    {
        for (int i = 0; i < poolSize; i++)
        {
            GameObject obj = Instantiate(rowParticle, transform);
            GameObject obj2 = Instantiate(colParticle, transform);
            obj.SetActive(false);
            obj2.SetActive(false);
            ParticleSystem ps = obj.GetComponent<ParticleSystem>();
            ParticleSystem ps2 = obj2.GetComponent<ParticleSystem>();
            rowPool.Enqueue(ps);
            colPool.Enqueue(ps2);
        }
    }
    // ���� ��ƼŬ ���
    public void PlayRowParticle(int index, Color color)
    {
        if (rowPool.Count == 0)
        {
            Debug.LogWarning("Particle pool exhausted! Consider increasing pool size.");
            return;
        }

        ParticleSystem ps = rowPool.Dequeue();
        ps.transform.position = RowIndexToWorld(index);

        
        // ���� ���� (��: ParticleSystem�� Main ��� ���)
        var main = ps.main;
        main.startColor = color;

        ps.gameObject.SetActive(true);
        Debug.Log($"ps.transform.position: {ps.transform.position}");
        ps.Play();

        StartCoroutine(ReturnToRowPool(ps, main.duration));
    }

    // ���� ��ƼŬ ���
    public void PlayColParticle(int index, Color color)
    {
        if (colPool.Count == 0)
        {
            Debug.LogWarning("Particle pool exhausted! Consider increasing pool size.");
            return;
        }

        ParticleSystem ps = colPool.Dequeue();
        ps.transform.position = ColIndexToWorld(index);
        
        // ���� ���� (��: ParticleSystem�� Main ��� ���)
        var main = ps.main;
        main.startColor = color;

        ps.gameObject.SetActive(true);
        Debug.Log($"Col Particle Position: {ps.transform.position}");
        ps.Play();

        StartCoroutine(ReturnToColPool(ps, main.duration));
    }

    // �׸��� ��ǥ�� ���� ��ǥ�� ��ȯ�ϴ� ���� �Լ�
    private Vector3 RowIndexToWorld(int index)
    {
        // �׸��� ��ǥ�� ���� ��ǥ�� ��ȯ�ϴ� ������ ���⿡ �����ϼ���.
        RectTransform squareRect = gridSquare.GetComponent<RectTransform>();
        Vector2 squareSize = squareRect.sizeDelta;

        float posX = startPosition.x + (squareSize.x + spacing.x) * 3.5f; // �߾� ���� ����
        float posY = startPosition.y - index * (squareSize.y + spacing.y);
        Vector3 worldPos = new Vector3(posX, posY, 0);
        Debug.Log($"Row Particle World Position: {worldPos}");
        return worldPos;
    }

    private Vector3 ColIndexToWorld(int index)
    {
        // �׸��� ��ǥ�� ���� ��ǥ�� ��ȯ�ϴ� ������ ���⿡ �����ϼ���.
        RectTransform squareRect = gridSquare.GetComponent<RectTransform>();
        Vector3 squareSize = squareRect.sizeDelta;

        float posX = startPosition.x + index * (squareSize.x + spacing.x);
        float posY = startPosition.y - (squareSize.y + spacing.y) * 3.5f; // �߾� ���� ����
        Vector3 worldPos = new Vector3(posX, posY, 0);
        Debug.Log($"Col Particle World Position: {worldPos}");
        return worldPos;
    }

    // ���� Return to pool
    private System.Collections.IEnumerator ReturnToRowPool(ParticleSystem ps, float delay)
    {
        yield return new WaitForSeconds(delay);
        ps.Stop();
        ps.gameObject.SetActive(false);
        rowPool.Enqueue(ps);
    }

    // ���� Return to pool
    private System.Collections.IEnumerator ReturnToColPool(ParticleSystem ps, float delay)
    {
        yield return new WaitForSeconds(delay);
        ps.Stop();
        ps.gameObject.SetActive(false);
        colPool.Enqueue(ps);
    }
}

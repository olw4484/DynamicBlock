using System.Collections.Generic;
using UnityEngine;

public class ParticleManager : MonoBehaviour
{
    [Header("Pool Settings")]
    public GameObject rowParticle;
    public GameObject colParticle;
    public int poolSize = 5;

    private Queue<ParticleSystem> rowPool = new Queue<ParticleSystem>();
    private Queue<ParticleSystem> colPool = new Queue<ParticleSystem>();

    [Header("Particle Setting")]
    private Vector2 startPosition = Vector2.zero;
    private Vector2 spacing = new Vector2(5f, 5f);
    [SerializeField] private GameObject gridSquare;
    [SerializeField] private Transform gridParent;
    private Vector2 squareSize;
    private Vector3 squarePos;


    private void Awake()
    {
        InitializePool();
        RectTransform squareRect = gridSquare.GetComponent<RectTransform>();
        squareSize = squareRect.sizeDelta;

       squarePos = squareRect.position;
    }

    // Ǯ �ʱ�ȭ
    private void InitializePool()
    {
        for (int i = 0; i < poolSize; i++)
        {
            GameObject obj = Instantiate(rowParticle, gridParent);
            GameObject obj2 = Instantiate(colParticle, gridParent);
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
        
        //ps.transform.localScale = new Vector3(6f, 6f, 1f); // �ʿ信 ���� ũ�� ����

        // ���� ���� (��: ParticleSystem�� Main ��� ���)
        var main = ps.main;
        main.startColor = color;

        ps.gameObject.SetActive(true);
        ps.Clear();
        ps.transform.localPosition = RowIndexToWorld(index);
        ps.transform.localScale = new Vector3(6f, 6f, 1f);

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
        
        //ps.transform.localScale = new Vector3(5f, 5f, 1f); // �ʿ信 ���� ũ�� ����

        // ���� ���� (��: ParticleSystem�� Main ��� ���)
        var main = ps.main;
        main.startColor = color;

        ps.gameObject.SetActive(true);
        ps.Clear();
        ps.transform.localPosition = ColIndexToWorld(index);
        ps.transform.localScale = new Vector3(5f, 5f, 1f);

        Debug.Log($"Col Particle position: {ps.transform.position}");
        ps.Play();

        StartCoroutine(ReturnToColPool(ps, main.duration));
    }

    private Vector3 RowIndexToWorld(int rowIndex)
    {
        // �߾� �� �������� X ��ġ ��� (��: 8ĭ�̸� 3.5ĭ offset)
        float offsetX = (squareSize.x + spacing.x) * 3.5f;
        float offsetY = -(squareSize.y + spacing.y) * rowIndex;

        Vector3 offset = new Vector3(offsetX, offsetY, 0);
        Vector3 worldPos = squarePos + offset;

        Debug.Log($"Row Particle World Position: {offset}");
        return offset;
    }

    private Vector3 ColIndexToWorld(int colIndex)
    {
        float offsetX = (squareSize.x + spacing.x) * colIndex;
        float offsetY = -(squareSize.y + spacing.y) * 3.5f; // �߾� �� ����

        Vector3 offset = new Vector3(offsetX, offsetY, 0);
        Vector3 worldPos = squarePos + offset;

        Debug.Log($"Col Particle World Position: {offset}");
        return offset;
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

using System.Collections.Generic;
using System.Drawing;
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
    [SerializeField] private Canvas uiCanvas;        // GameCanvas
    [SerializeField] private Camera fxCamera;        // FXCamera
    [SerializeField] private Transform fxRoot;
    public Transform GridParent => gridParent;

    private void Awake()
    {
        InitializePool();
        RectTransform squareRect = gridSquare.GetComponent<RectTransform>();
        squareSize = squareRect.sizeDelta;

        //squarePos = squareRect.position;
        //�θ� ���� ���̾ƿ��̸� localPosition�� �ϰ����̶� ����
        squarePos = squareRect.localPosition;
    }

    // Ǯ �ʱ�ȭ
    private void InitializePool()
    {
        if (!fxRoot) { Debug.LogError("[PM] fxRoot is null. Assign FX_GridRoot."); return; }

        for (int i = 0; i < poolSize; i++)
        {
            var r = Instantiate(rowParticle, fxRoot);
            var c = Instantiate(colParticle, fxRoot);
            r.SetActive(false); c.SetActive(false);

            int fxLayer = LayerMask.NameToLayer("FX");
            if (fxLayer >= 0)
            {
                LayerUtil.SetLayerRecursive(rowParticle, fxLayer); // rowObj ������ �ν��Ͻ�
                LayerUtil.SetLayerRecursive(colParticle, fxLayer);
            }
            else
            {
                Debug.LogWarning("[ParticleManager] 'FX' layer not found. Check Project Settings > Tags and Layers.");
            }

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

    private static float LifetimeMax(ParticleSystem.MainModule main)
    {
        var s = main.startLifetime;
        return s.mode switch
        {
            ParticleSystemCurveMode.TwoConstants => Mathf.Max(s.constantMin, s.constantMax),
            ParticleSystemCurveMode.TwoCurves => Mathf.Max(s.curveMin.keys[^1].time, s.curveMax.keys[^1].time),
            ParticleSystemCurveMode.Curve => s.curve.keys[^1].time,
            _ => s.constant,
        };
    }
    // ���� ��ƼŬ ���
    public void PlayRowParticle(int index, UnityEngine.Color color)
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
        ps.transform.localScale = new Vector3(100f, 50f, 1f);

        Debug.Log($"ps.transform.position: {ps.transform.position}");
        ps.Play();

        StartCoroutine(ReturnToRowPool(ps, main.duration));
    }

    // ���� ��ƼŬ ���
    public void PlayColParticle(int index, UnityEngine.Color color)
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
        ps.transform.localScale = new Vector3(100f, 50f, 1f);

        Debug.Log($"Col Particle position: {ps.transform.position}");
        ps.Play();

        StartCoroutine(ReturnToColPool(ps, main.duration));
    }

    public Vector3 RowIndexToWorld(int rowIndex)
    {
        // �߾� �� �������� X ��ġ ��� (��: 8ĭ�̸� 3.5ĭ offset)
        float offsetX = (squareSize.x + spacing.x) * 3.5f;
        float offsetY = -(squareSize.y + spacing.y) * rowIndex;

        Vector3 offset = new Vector3(offsetX, offsetY, -5f);
        Vector3 worldPos = squarePos + offset;

        Debug.Log($"Row Particle World Position: {offset}");
        // return offset;
        return worldPos;
    }

    public Vector3 ColIndexToWorld(int colIndex)
    {
        float offsetX = (squareSize.x + spacing.x) * colIndex;
        float offsetY = -(squareSize.y + spacing.y) * 3.5f; // �߾� �� ����

        Vector3 offset = new Vector3(offsetX, offsetY, -5f);
        Vector3 worldPos = squarePos + offset;

        Debug.Log($"Col Particle World Position: {offset}");
        // return offset;
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

    private Vector3 UiLocalToFxLocal(Vector3 uiLocal)
    {
        if (!gridParent || !uiCanvas || !fxCamera) return uiLocal;

        var gridRT = gridParent as RectTransform;

        // 1) UI ���� �� UI ����
        Vector3 uiWorld = gridRT.TransformPoint(uiLocal);

        // 2) UI ���� �� ��ũ�� �ȼ�
        var uiCam = uiCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : uiCanvas.worldCamera;
        Vector2 screen = RectTransformUtility.WorldToScreenPoint(uiCam, uiWorld);

        // 3) RT �ػ� ���� ����
        if (fxCamera.targetTexture != null)
        {
            float sx = (float)fxCamera.pixelWidth / Screen.width;
            float sy = (float)fxCamera.pixelHeight / Screen.height;
            screen.x *= sx; screen.y *= sy;
        }

        // 4) ��ũ�� �� FX ���� (����/���� �� �� z�� ī�޶��FX��� �Ÿ�)
        float depth = Mathf.Abs(fxRoot.position.z - fxCamera.transform.position.z);
        Vector3 fxWorld = fxCamera.ScreenToWorldPoint(new Vector3(screen.x, screen.y, depth));

        // 5) FX ���� �� FX ����
        return fxRoot.InverseTransformPoint(fxWorld);
    }
}

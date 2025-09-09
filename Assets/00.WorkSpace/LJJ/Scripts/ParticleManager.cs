using System.Collections.Generic;
using System.Collections;
using UnityEngine;

public class ParticleManager : MonoBehaviour
{
    [Header("Non Pool Particle")]
    public GameObject newScoreParticle;
    private ParticleSystem newScorePS;

    public GameObject gameOverParticle;
    private ParticleSystem gameOverPS;

    public GameObject allClearParticle;
    private List<ParticleSystem> allClearPSs = new List<ParticleSystem>();
    public RectTransform[] allClearPos;

    [SerializeField] GameObject safeArea;
    [SerializeField] GameObject gameOverCanvas;
    [SerializeField] RectTransform gameOverTransform;



    [Header("Pool Settings")]
    public GameObject destroyParticle;
    public GameObject perimeterParticle;
    public int poolSize = 6;

    private Queue<ParticleSystem> destroyPool = new Queue<ParticleSystem>();
    private Queue<ParticleSystem> perimeterPool = new Queue<ParticleSystem>();

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

        NonPoolInit();
    }

    private void NonPoolInit()
    {
        int fxLayer = LayerMask.NameToLayer("FX");

        if (safeArea != null)
        {
            if (allClearParticle != null)
            {
                for (int i = 0; i < allClearPos.Length; i++) 
                {
                    GameObject fx = Instantiate(allClearParticle, fxRoot);
                    fx.SetActive(false);
                    LayerUtil.SetLayerRecursive(fx, fxLayer);

                    GameObject ps = Instantiate(allClearParticle, allClearPos[i].transform);
                    ps.transform.localScale = Vector3.one * 500;
                    bool isLeft = i % 2 == 0 ? true : false;

                    if (isLeft)
                    {
                        ps.transform.rotation = Quaternion.Euler(-120f, -90f, 90f);
                    }
                    else
                    {
                        ps.transform.rotation = Quaternion.Euler(-60f, -90f, 90f);
                    }

                    allClearPSs.Add(ps.GetComponent<ParticleSystem>());
                    ps.SetActive(false);
                }
            }
        }
        if (gameOverCanvas != null)
        {
            if (newScoreParticle != null)
            {
                GameObject newScore = Instantiate(newScoreParticle, gameOverTransform);
                newScore.transform.localScale = Vector3.one * 10;
                newScore.transform.rotation = Quaternion.Euler(180f, 0f, 0f);
                newScorePS = newScore.GetComponent<ParticleSystem>();
                //LayerUtil.SetLayerRecursive(newScore, fxLayer);
                newScore.SetActive(false);
            }
            if (gameOverParticle != null)
            {
                GameObject gameOver = Instantiate(gameOverParticle, gameOverTransform);
                gameOver.transform.localScale = Vector3.one * 10;
                gameOver.transform.rotation = Quaternion.Euler(180f, 0f, 0f);
                gameOverPS = gameOver.GetComponent<ParticleSystem>();
                //LayerUtil.SetLayerRecursive(gameOver, fxLayer);
                gameOver.SetActive(false);
            }
        }
    }

    // Ǯ �ʱ�ȭ
    private void InitializePool()
    {
        //if (!fxRoot) { Debug.LogError("[PM] fxRoot is null. Assign FX_GridRoot."); return; }

        if (perimeterParticle != null && destroyParticle != null)
        {
            for (int i = 0; i < poolSize; i++)
            {
                var d = Instantiate(destroyParticle, fxRoot);
                var p = Instantiate(perimeterParticle, fxRoot);
                d.SetActive(false); p.SetActive(false);

                int fxLayer = LayerMask.NameToLayer("FX");
                if (fxLayer >= 0)
                {
                    LayerUtil.SetLayerRecursive(destroyParticle, fxLayer); // rowObj ������ �ν��Ͻ�
                    LayerUtil.SetLayerRecursive(perimeterParticle, fxLayer);
                }
                else
                {
                    Debug.LogWarning("[ParticleManager] 'FX' layer not found. Check Project Settings > Tags and Layers.");
                }

                GameObject obj = Instantiate(destroyParticle, gridParent);
                GameObject obj2 = Instantiate(perimeterParticle, gridParent);
                obj.SetActive(false);
                obj2.SetActive(false);
                ParticleSystem ps = obj.GetComponent<ParticleSystem>();
                ParticleSystem ps2 = obj2.GetComponent<ParticleSystem>();
                destroyPool.Enqueue(ps);
                perimeterPool.Enqueue(ps2);

            }
        }
        else if(perimeterParticle == null && destroyParticle != null)
        {
            for (int i = 0; i < poolSize; i++)
            {
                var d = Instantiate(destroyParticle, fxRoot);
                d.SetActive(false); 

                int fxLayer = LayerMask.NameToLayer("FX");
                if (fxLayer >= 0)
                {
                    LayerUtil.SetLayerRecursive(destroyParticle, fxLayer); // rowObj ������ �ν��Ͻ�
                }
                else
                {
                    Debug.LogWarning("[ParticleManager] 'FX' layer not found. Check Project Settings > Tags and Layers.");
                }

                GameObject obj = Instantiate(destroyParticle, gridParent);
                obj.SetActive(false);
                ParticleSystem ps = obj.GetComponent<ParticleSystem>();
                destroyPool.Enqueue(ps);

            }
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
        if (destroyPool.Count == 0)
        {
            Debug.LogWarning("Particle pool exhausted! Consider increasing pool size.");
            return;
        }

        ParticleSystem ps = destroyPool.Dequeue();
        
        //ps.transform.localScale = new Vector3(6f, 6f, 1f); // �ʿ信 ���� ũ�� ����

        // ���� ���� (��: ParticleSystem�� Main ��� ���)
        var main = ps.main;
        main.startColor = color;

        ps.gameObject.SetActive(true);
        ps.Clear();
        ps.transform.localPosition = RowIndexToWorld(index);
        ps.transform.localScale = new Vector3(5f, 5f, 1f);
        ps.transform.rotation = Quaternion.Euler(0f, 0f, 90f); // 90�� ȸ��

        Debug.Log($"Row Particle position: {ps.transform.localPosition}");
        ps.Play();

        StartCoroutine(ReturnToDestroyPool(ps, main.duration));
    }

    // ���� ��ƼŬ ���
    public void PlayColParticle(int index, UnityEngine.Color color)
    {
        if (destroyPool.Count == 0)
        {
            Debug.LogWarning("Particle pool exhausted! Consider increasing pool size.");
            return;
        }

        ParticleSystem ps = destroyPool.Dequeue();
        
        //ps.transform.localScale = new Vector3(5f, 5f, 1f); // �ʿ信 ���� ũ�� ����

        // ���� ���� (��: ParticleSystem�� Main ��� ���)
        var main = ps.main;
        main.startColor = color;

        ps.gameObject.SetActive(true);
        ps.Clear();
        ps.transform.localPosition = ColIndexToWorld(index);
        ps.transform.localScale = new Vector3(5f, 5f, 1f);

        Debug.Log($"Col Particle position: {ps.transform.localPosition}");
        ps.Play();

        StartCoroutine(ReturnToDestroyPool(ps, main.duration));
    }

    // ���� �׵θ� ���� ��ƼŬ ���
    public void PlayRowPerimeterParticle(int index, UnityEngine.Color color)
    {
        if (destroyPool.Count == 0)
        {
            Debug.LogWarning("Particle pool exhausted! Consider increasing pool size.");
            return;
        }

        ParticleSystem ps = destroyPool.Dequeue();

        //ps.transform.localScale = new Vector3(6f, 6f, 1f); // �ʿ信 ���� ũ�� ����

        // ���� ���� (��: ParticleSystem�� Main ��� ���)
        var main = ps.main;
        main.startColor = color;

        ps.gameObject.SetActive(true);
        ps.Clear();
        ps.transform.localPosition = RowIndexToWorld(index);
        ps.transform.localScale = new Vector3(5f, 5f, 1f);
        ps.transform.rotation = Quaternion.Euler(0f, 0f, 90f); // 90�� ȸ��

        Debug.Log($"Row Particle position: {ps.transform.localPosition}");
        ps.Play();

        StartCoroutine(ReturnToPerimeterPool(ps, main.duration));
    }

    // ���� �׵θ� ���� ��ƼŬ ���
    public void PlayColPerimeterParticle(int index, UnityEngine.Color color)
    {
        if (perimeterPool.Count == 0)
        {
            Debug.LogWarning("Particle pool exhausted! Consider increasing pool size.");
            return;
        }

        ParticleSystem ps = perimeterPool.Dequeue();

        //ps.transform.localScale = new Vector3(5f, 5f, 1f); // �ʿ信 ���� ũ�� ����

        // ���� ���� (��: ParticleSystem�� Main ��� ���)
        var main = ps.main;
        main.startColor = color;

        ps.gameObject.SetActive(true);
        ps.Clear();
        ps.transform.localPosition = ColIndexToWorld(index);
        ps.transform.localScale = new Vector3(5f, 5f, 1f);

        Debug.Log($"Col Particle position: {ps.transform.localPosition}");
        ps.Play();

        StartCoroutine(ReturnToPerimeterPool(ps, main.duration));
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
    private System.Collections.IEnumerator ReturnToDestroyPool(ParticleSystem ps, float delay)
    {
        yield return new WaitForSeconds(delay);
        ps.Stop();
        ps.transform.rotation = Quaternion.identity; // ȸ�� �ʱ�ȭ
        ps.gameObject.SetActive(false);
        destroyPool.Enqueue(ps);
    }

    // ���� Return to pool
    private System.Collections.IEnumerator ReturnToPerimeterPool(ParticleSystem ps, float delay)
    {
        yield return new WaitForSeconds(delay);
        ps.Stop();
        ps.transform.rotation = Quaternion.identity; // ȸ�� �ʱ�ȭ
        ps.gameObject.SetActive(false);
        destroyPool.Enqueue(ps);
    }

    // ��Ŭ���� ��ƼŬ ���
    public void PlayAllClear()
    {
        foreach (var ps in allClearPSs)
        {
            ps.gameObject.SetActive(true);
            ps.Play();
            StartCoroutine(DisableNonPool(ps, LifetimeMax(ps.main)));
        }
    }

    // �����ھ� ��ƼŬ ���
    public void PlayNewScore()
    { 
        newScorePS.gameObject.SetActive(true);
        newScorePS.Play();
        StartCoroutine(DisableNonPool(newScorePS, 3f));
    }

    // ���ӿ��� ��ƼŬ ���
    public void PlayGameOver()
    { 
        gameOverPS.gameObject.SetActive(true);
        gameOverPS.Play();
        StartCoroutine(DisableNonPool(gameOverPS, 3f));
    }

    // ��Ǯ ��ƼŬ ��Ȱ��ȭ
    private IEnumerator DisableNonPool(ParticleSystem ps, float delay)
    {
        yield return new WaitForSeconds(delay);
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        ps.gameObject.SetActive(false);
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

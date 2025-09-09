using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ParticleManager : MonoBehaviour
{
    // Non-Pool (단발성) 파티클
    [Header("Non Pool Particle")]
    [SerializeField] private GameObject newScoreParticle;
    [SerializeField] private GameObject gameOverParticle;
    [SerializeField] private GameObject allClearParticle;
    [SerializeField] private RectTransform[] allClearPos;   // UI 상 위치(좌/우 등)
    [SerializeField] private RectTransform gameOverTransform; // 게임오버 UI 상 부모

    private ParticleSystem newScorePS;
    private ParticleSystem gameOverPS;
    private readonly List<ParticleSystem> allClearPSs = new();

    [Header("Scene Refs")]
    [SerializeField] private Canvas uiCanvas;     // GameCanvas
    [SerializeField] private Camera fxCamera;     // FXCamera (RenderTexture 등)
    [SerializeField] private Transform fxRoot;    // FX 전용 루트(여기에만 풀 붙이기)
    [SerializeField] private GameObject gridSquare;  // UI의 한 칸(크기/간격 기준용)
    [SerializeField] private Transform gridParent;   // UI Grid 루트 (RectTransform)
    public Transform GridParent => gridParent;

    [Header("Grid & Layout")]
    [SerializeField] private Vector2 spacing = new Vector2(5f, 5f); // UI 간격 픽셀(또는 캔버스 스케일 적용값)
    private Vector2 squareSize;            // UI 칸 크기 (RectTransform sizeDelta 기준)
    private Vector3 squarePosLocal;        // 기준점(UI 로컬)

    // Pool 파티클
    [Header("Pool Settings")]
    [SerializeField] private GameObject destroyParticle;
    [SerializeField] private GameObject perimeterParticle;
    [SerializeField] private int poolSize = 6;

    private readonly Queue<ParticleSystem> destroyPool = new();
    private readonly Queue<ParticleSystem> perimeterPool = new();

    [Header("FX Size Controls (권장: 스케일은 1, 입자 크기로 제어)")]
    [Tooltip("행/열 소거 라인 두께 등을 입자 크기로 맞추세요.")]
    [SerializeField] private float lineStartSize = 5f; // 필요에 따라 조절
    [SerializeField] private ParticleSystemScalingMode scalingMode = ParticleSystemScalingMode.Hierarchy;

    private int fxLayer = -1;

    private void Awake()
    {
        if (!fxRoot) { Debug.LogError("[ParticleManager] fxRoot is null. Assign FX_GridRoot."); return; }
        if (!fxCamera) { Debug.LogError("[ParticleManager] fxCamera is null."); return; }
        if (!uiCanvas) { Debug.LogError("[ParticleManager] uiCanvas is null."); return; }
        if (!gridSquare || !gridParent) { Debug.LogError("[ParticleManager] gridSquare/gridParent missing."); return; }

        fxLayer = LayerMask.NameToLayer("FX");
        if (fxLayer < 0) Debug.LogWarning("[ParticleManager] 'FX' layer not found. Check Project Settings > Tags and Layers.");

        // UI 기준 크기/기점 기록
        var squareRect = gridSquare.GetComponent<RectTransform>();
        squareSize = squareRect.sizeDelta;
        squarePosLocal = squareRect.localPosition;

        // 풀/단발 파티클 생성
        InitializePool();
        InitializeNonPool();
    }

    // Non-Pool 초기화

    private void InitializeNonPool()
    {
        // All Clear: 좌/우 UI 포지션에 붙는 연출 (UI 트리에 생성)
        if (allClearParticle && allClearPos != null)
        {
            foreach (var anchor in allClearPos)
            {
                if (!anchor) continue;
                var go = Instantiate(allClearParticle, anchor);
                go.SetActive(false);

                var ps = go.GetComponent<ParticleSystem>();
                if (ps)
                {
                    var main = ps.main;
                    // 필요 시 scalingMode 조정
                    main.scalingMode = ParticleSystemScalingMode.Hierarchy;
                }
                // UI에서 크게 보이게 쓰고 있으면 여기서만 스케일 조정
                go.transform.localScale = Vector3.one; // 필요 시 수정
                allClearPSs.Add(ps);
            }
        }

        // New Score / GameOver: 게임오버 UI 상 부모에 생성
        if (gameOverTransform)
        {
            if (newScoreParticle)
            {
                var go = Instantiate(newScoreParticle, gameOverTransform);
                go.SetActive(false);
                newScorePS = go.GetComponent<ParticleSystem>();
                if (newScorePS)
                {
                    var m = newScorePS.main;
                    // timeScale=0에서도 재생하고 싶으면 켜기
                    // m.useUnscaledTime = true;
                }
                go.transform.localScale = Vector3.one;
            }
            if (gameOverParticle)
            {
                var go = Instantiate(gameOverParticle, gameOverTransform);
                go.SetActive(false);
                gameOverPS = go.GetComponent<ParticleSystem>();
                if (gameOverPS)
                {
                    var m = gameOverPS.main;
                    // m.useUnscaledTime = true; // 필요 시
                }
                go.transform.localScale = Vector3.one;
            }
        }
    }
    // Pool 초기화
    private void InitializePool()
    {
        // Destroy 라인
        if (destroyParticle)
        {
            for (int i = 0; i < poolSize; i++)
            {
                var go = Instantiate(destroyParticle, fxRoot);
                if (fxLayer >= 0) LayerUtil.SetLayerRecursive(go, fxLayer);
                go.SetActive(false);

                var ps = go.GetComponent<ParticleSystem>();
                if (ps)
                {
                    var m = ps.main;
                    m.scalingMode = scalingMode;
                    // 기본 크기
                    if (lineStartSize > 0f) m.startSize = lineStartSize;
                }
                destroyPool.Enqueue(ps);
            }
        }

        // Perimeter 라인
        if (perimeterParticle)
        {
            for (int i = 0; i < poolSize; i++)
            {
                var go = Instantiate(perimeterParticle, fxRoot);
                if (fxLayer >= 0) LayerUtil.SetLayerRecursive(go, fxLayer);
                go.SetActive(false);

                var ps = go.GetComponent<ParticleSystem>();
                if (ps)
                {
                    var m = ps.main;
                    m.scalingMode = scalingMode;
                    if (lineStartSize > 0f) m.startSize = lineStartSize;
                }
                perimeterPool.Enqueue(ps);
            }
        }
    }

    // 좌표 계산 (UI 로컬 → FX 로컬)
    private Vector3 RowIndexToUiLocal(int rowIndex)
    {
        float offsetX = (squareSize.x + spacing.x) * 3.5f;      // 중앙 열 기준
        float offsetY = -(squareSize.y + spacing.y) * rowIndex; // 아래로 증가
        return squarePosLocal + new Vector3(offsetX, offsetY, 0f);
    }
    private Vector3 ColIndexToUiLocal(int colIndex)
    {
        float offsetX = (squareSize.x + spacing.x) * colIndex;
        float offsetY = -(squareSize.y + spacing.y) * 3.5f;     // 중앙 행 기준
        return squarePosLocal + new Vector3(offsetX, offsetY, 0f);
    }

    private Vector3 UiLocalToFxLocal(Vector3 uiLocal)
    {
        if (!gridParent || !uiCanvas || !fxCamera || !fxRoot) return uiLocal;

        var gridRT = gridParent as RectTransform;

        // 1) UI 로컬 → UI 월드
        Vector3 uiWorld = gridRT.TransformPoint(uiLocal);

        // 2) UI 월드 → 스크린
        var uiCam = uiCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : uiCanvas.worldCamera;
        Vector2 screen = RectTransformUtility.WorldToScreenPoint(uiCam, uiWorld);

        // 3) RT 해상도 보정 (FXCamera가 RT를 쓰면 Screen과 픽셀 스케일 다름)
        if (fxCamera.targetTexture != null)
        {
            float sx = (float)fxCamera.pixelWidth / Screen.width;
            float sy = (float)fxCamera.pixelHeight / Screen.height;
            screen.x *= sx;
            screen.y *= sy;
        }

        // 4) 스크린 → FX 월드
        float depth = Mathf.Abs(fxRoot.position.z - fxCamera.transform.position.z);
        Vector3 fxWorld = fxCamera.ScreenToWorldPoint(new Vector3(screen.x, screen.y, depth));
        // 5) FX 월드 → FX 로컬
        return fxRoot.InverseTransformPoint(fxWorld);
    }

    // 재생 API (라인 파티클)
    public void PlayRowParticle(int rowIndex, Color color)
    {
        if (destroyPool.Count == 0) { Debug.LogWarning("destroyPool exhausted"); return; }
        var ps = destroyPool.Dequeue();

        var main = ps.main;
        main.startColor = color;
        main.scalingMode = scalingMode;      // 일관성
        if (lineStartSize > 0f) main.startSize = lineStartSize;

        ps.transform.localPosition = UiLocalToFxLocal(RowIndexToUiLocal(rowIndex));
        ps.transform.localRotation = Quaternion.Euler(0, 0, 90);
        ps.transform.localScale = Vector3.one;   // 스케일은 1

        ps.gameObject.SetActive(true);
        ps.Clear();
        ps.Play();

        StartCoroutine(ReturnToDestroyPool(ps, main.duration));
    }

    public void PlayColParticle(int colIndex, Color color)
    {
        if (destroyPool.Count == 0) { Debug.LogWarning("destroyPool exhausted"); return; }
        var ps = destroyPool.Dequeue();

        var main = ps.main;
        main.startColor = color;
        main.scalingMode = scalingMode;
        if (lineStartSize > 0f) main.startSize = lineStartSize;

        ps.transform.localPosition = UiLocalToFxLocal(ColIndexToUiLocal(colIndex));
        ps.transform.localRotation = Quaternion.identity;
        ps.transform.localScale = Vector3.one;

        ps.gameObject.SetActive(true);
        ps.Clear();
        ps.Play();

        StartCoroutine(ReturnToDestroyPool(ps, main.duration));
    }

    public void PlayRowPerimeterParticle(int rowIndex, Color color)
    {
        if (perimeterPool.Count == 0) { Debug.LogWarning("perimeterPool exhausted"); return; }
        var ps = perimeterPool.Dequeue();

        var main = ps.main;
        main.startColor = color;
        main.scalingMode = scalingMode;
        if (lineStartSize > 0f) main.startSize = lineStartSize;

        ps.transform.localPosition = UiLocalToFxLocal(RowIndexToUiLocal(rowIndex));
        ps.transform.localRotation = Quaternion.Euler(0, 0, 90);
        ps.transform.localScale = Vector3.one;

        ps.gameObject.SetActive(true);
        ps.Clear();
        ps.Play();

        StartCoroutine(ReturnToPerimeterPool(ps, main.duration));
    }

    public void PlayColPerimeterParticle(int colIndex, Color color)
    {
        if (perimeterPool.Count == 0) { Debug.LogWarning("perimeterPool exhausted"); return; }
        var ps = perimeterPool.Dequeue();

        var main = ps.main;
        main.startColor = color;
        main.scalingMode = scalingMode;
        if (lineStartSize > 0f) main.startSize = lineStartSize;

        ps.transform.localPosition = UiLocalToFxLocal(ColIndexToUiLocal(colIndex));
        ps.transform.localRotation = Quaternion.identity;
        ps.transform.localScale = Vector3.one;

        ps.gameObject.SetActive(true);
        ps.Clear();
        ps.Play();

        StartCoroutine(ReturnToPerimeterPool(ps, main.duration));
    }

    // 풀 반환
    private IEnumerator ReturnToDestroyPool(ParticleSystem ps, float delay)
    {
        yield return new WaitForSeconds(delay);
        ps.Stop();
        ps.transform.localRotation = Quaternion.identity;
        ps.gameObject.SetActive(false);
        destroyPool.Enqueue(ps);
    }

    private IEnumerator ReturnToPerimeterPool(ParticleSystem ps, float delay)
    {
        yield return new WaitForSeconds(delay);
        ps.Stop();
        ps.transform.localRotation = Quaternion.identity;
        ps.gameObject.SetActive(false);
        perimeterPool.Enqueue(ps);
    }

    // 단발 연출
    public void PlayAllClear()
    {
        foreach (var ps in allClearPSs)
        {
            if (!ps) continue;
            ps.gameObject.SetActive(true);
            ps.Clear();
            ps.Play();
            StartCoroutine(DisableNonPool(ps, LifetimeMax(ps.main)));
        }
    }

    public void PlayNewScore()
    {
        if (!newScorePS) return;
        var m = newScorePS.main;
        // m.useUnscaledTime = true; // 필요 시
        newScorePS.gameObject.SetActive(true);
        newScorePS.Clear();
        newScorePS.Play();
        StartCoroutine(DisableNonPool(newScorePS, 3f));
    }

    public void PlayGameOver()
    {
        if (!gameOverPS) return;
        var m = gameOverPS.main;
        // m.useUnscaledTime = true; // 필요 시
        gameOverPS.gameObject.SetActive(true);
        gameOverPS.Clear();
        gameOverPS.Play();
        StartCoroutine(DisableNonPool(gameOverPS, 3f));
    }

    private IEnumerator DisableNonPool(ParticleSystem ps, float delay)
    {
        yield return new WaitForSeconds(delay);
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        ps.gameObject.SetActive(false);
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
}
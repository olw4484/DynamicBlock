using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ParticleManager : MonoBehaviour
{
    // ================================
    // 통합 스폰 파이프라인 지원 타입
    // ================================
    public enum SpawnMode { GridRow, GridCol, AnchorRect, ScreenPoint, WorldPos }

    public readonly struct SpawnTarget
    {
        public readonly SpawnMode mode;
        public readonly int index;                 // row 또는 col
        public readonly RectTransform anchor;      // AnchorRect 모드
        public readonly Vector2 screen;            // ScreenPoint 모드
        public readonly Vector3 world;             // WorldPos 모드
        public readonly Vector2 pixelOffset;       // AnchorRect용 추가 오프셋
        public readonly float rotationY;           // 최종 로컬 Y 회전 (comboParticle 용)
        public readonly float rotationZ;           // 최종 로컬 Z 회전
        public readonly bool unscaled;             // TimeScale 무시 여부
        public readonly float? durationOverride;   // 지속시간 강제 지정

        public SpawnTarget(
            SpawnMode m, int idx = -1, RectTransform a = null,
            Vector2? scr = null, Vector3? w = null, Vector2? px = null,
            float rotY = 0f, float rotZ = 0f, bool unscaledTime = false, float? dur = null)
        {
            mode = m; index = idx; anchor = a;
            screen = scr ?? default;
            world = w ?? default;
            pixelOffset = px ?? default;
            rotationY = rotY;
            rotationZ = rotZ; unscaled = unscaledTime; durationOverride = dur;
        }
    }

    public readonly struct FxParams
    {
        public readonly Color? color;
        public readonly bool isLineFx;
        public readonly bool setScalingMode;
        public readonly bool keepPrefabSize;
        public readonly float uniformScale;

        public FxParams(
            Color? color,
            bool lineFx = false,
            bool applyScalingMode = true,
            bool keepPrefabSize = false,
            float uniformScale = 1f)
        {
            this.color = color;
            isLineFx = lineFx;
            setScalingMode = applyScalingMode;
            this.keepPrefabSize = keepPrefabSize;
            this.uniformScale = uniformScale;
        }
    }

    private readonly Dictionary<ParticleSystem, Vector3> _baseSize3D = new();
    private readonly Dictionary<ParticleSystem, float> _baseSize = new();
    private readonly Dictionary<ParticleSystem, float> _baseStartSize = new();

    // ================================
    // Inspector
    // ================================
    [Header("Non Pool Particle (단발성)")]
    [SerializeField] private GameObject newScoreParticle;
    [SerializeField] private GameObject gameOverParticle;
    [SerializeField] private GameObject allClearParticle;
    [SerializeField] private RectTransform[] allClearPos;   // UI 좌/우 등
    [SerializeField] private RectTransform gameOverTransform; // 게임오버 UI 상 부모
    [SerializeField] private RectTransform go_NewScorePos;   // "ParticlePos_NewScore"
    [SerializeField] private RectTransform go_GameOverPos;   // "ParticlePos_GameOver"

    [Header("Scene Refs")]
    [SerializeField] private Canvas uiCanvas;     // GameCanvas
    [SerializeField] private Camera fxCamera;     // FXCamera (RenderTexture 등)
    [SerializeField] private Transform fxRoot;    // FX 전용 루트
    [SerializeField] private GameObject gridSquare;  // UI의 한 칸(크기/간격 기준용)
    [SerializeField] private Transform gridParent;   // UI Grid 루트 (RectTransform)
    public Transform GridParent => gridParent;

    [Header("Grid & Layout")]
    [SerializeField] private Vector2 spacing = new Vector2(5f, 5f); // UI 간격
    private Vector2 squareSize;            // UI 칸 크기
    private Vector3 squarePosLocal;        // 기준점(UI 로컬)
    [SerializeField] private int rows = 8;
    [SerializeField] private int cols = 8;

    [Header("Pool Settings")]
    [SerializeField] private GameObject destroyParticle;   // 가로/세로 소거 라인
    [SerializeField] private GameObject comboParticle; // 콤보용 파티클 (스프라이트 변경)
    [SerializeField] private GameObject perimeterParticle; // 테두리 라인 등
    [SerializeField] private int poolSize = 6;

    [Header("FX Size Controls (권장: 스케일 1, 입자 크기로 제어)")]
    [Tooltip("행/열 소거 라인 두께 등을 입자 크기로 맞추세요.")]
    [SerializeField] private float lineStartSize = 1f;      // 줄 두께
    [SerializeField] private float fxScaleFactor = 3f;      // 일반 FX 배율
    [SerializeField] private float allClearSizeScale = 1.75f;
    [SerializeField] private ParticleSystemScalingMode scalingMode = ParticleSystemScalingMode.Hierarchy;

    [SerializeField] private ParticleSystem comboPrefab;
    [SerializeField] private Transform comboParent;
    [SerializeField] private int comboPrewarm = 8;

    public int AllClearCount => allClearPSs?.Count ?? 0;

    // ================================
    // Runtime
    // ================================
    private ParticleSystem newScorePS;
    private ParticleSystem gameOverPS;
    private readonly List<ParticleSystem> allClearPSs = new();
    private readonly Queue<ParticleSystem> destroyPool = new();
    private readonly Queue<ParticleSystem> comboPool = new();
    private readonly Queue<ParticleSystem> perimeterPool = new();
    private int fxLayer = -1;
    private readonly Dictionary<(SpawnMode, int), (ParticleSystem ps, Queue<ParticleSystem> pool)> activeLoops
        = new();

    // ================================
    // Unity LifeCycle
    // ================================
    private void Awake()
    {
        if (!fxRoot) { Debug.LogError("[ParticleManager] fxRoot is null. Assign FX_GridRoot."); return; }
        if (!fxCamera) { Debug.LogError("[ParticleManager] fxCamera is null."); return; }
        if (!uiCanvas) { Debug.LogError("[ParticleManager] uiCanvas is null."); return; }
        if (!gridSquare || !gridParent) { Debug.LogError("[ParticleManager] gridSquare/gridParent missing."); return; }

        fxLayer = LayerMask.NameToLayer("FX");
        if (fxLayer < 0) Debug.LogWarning("[ParticleManager] 'FX' layer not found. Check Project Settings > Tags and Layers.");

        var squareRect = gridSquare.GetComponent<RectTransform>();
        squareSize = squareRect.sizeDelta;
        squarePosLocal = squareRect.localPosition;

        InitializePool();
        InitializeNonPool();
    }

    // ================================
    // 초기화
    // ================================
    private void InitializePool()
    {
        if (destroyParticle)
        {
            for (int i = 0; i < poolSize; i++)
            {
                var go = Instantiate(destroyParticle, fxRoot);
                if (fxLayer >= 0) LayerUtil.SetLayerRecursive(go, fxLayer);
                go.SetActive(false);

                var ps = go.GetComponent<ParticleSystem>();
                // 라인 FX: 두께 고정 사용
                var main = ps.main;
                main.scalingMode = scalingMode;
                if (lineStartSize > 0f) main.startSize = lineStartSize;

                destroyPool.Enqueue(ps);
            }
        }

        if (perimeterParticle)
        {
            for (int i = 0; i < poolSize; i++)
            {
                var go = Instantiate(perimeterParticle, fxRoot);
                if (fxLayer >= 0) LayerUtil.SetLayerRecursive(go, fxLayer);
                go.SetActive(false);

                var ps = go.GetComponent<ParticleSystem>();
                var main = ps.main;
                main.scalingMode = scalingMode;
                if (lineStartSize > 0f) main.startSize = lineStartSize;

                perimeterPool.Enqueue(ps);
            }
        }

        if (comboParticle)
        {
            for (int i = 0; i < poolSize; i++)
            {
                var go = Instantiate(comboParticle, fxRoot);
                if (fxLayer >= 0) LayerUtil.SetLayerRecursive(go, fxLayer);
                go.SetActive(false);

                var ps = go.GetComponent<ParticleSystem>();
                var main = ps.main;
                main.scalingMode = scalingMode;
                if (lineStartSize > 0f) main.startSize = lineStartSize;

                comboPool.Enqueue(ps);
            }
        }
    }

    private void InitializeNonPool()
    {
        if (allClearParticle && allClearPos != null)
        {
            foreach (var anchor in allClearPos)
            {
                if (!anchor) continue;
                var go = Instantiate(allClearParticle, fxRoot);
                if (fxLayer >= 0) LayerUtil.SetLayerRecursive(go, fxLayer);
                go.SetActive(false);

                var ps = go.GetComponent<ParticleSystem>();
                // 일반 FX: 배율 사용
                var main = ps.main;
                main.scalingMode = scalingMode;
                if (fxScaleFactor != 1f) main.startSizeMultiplier *= fxScaleFactor;

                go.transform.localScale = Vector3.one;
                allClearPSs.Add(ps);
            }

            foreach (var ps in allClearPSs)
            {
                var m = ps.main;
                if (m.startSize3D)
                    _baseSize3D[ps] = new Vector3(m.startSizeXMultiplier, m.startSizeYMultiplier, m.startSizeZMultiplier);
                else
                    _baseSize[ps] = m.startSizeMultiplier;
            }
        }

        if (newScoreParticle)
        {
            var go = Instantiate(newScoreParticle, fxRoot);
            if (fxLayer >= 0) LayerUtil.SetLayerRecursive(go, fxLayer);
            go.SetActive(false);

            newScorePS = go.GetComponent<ParticleSystem>();
            if (newScorePS) { var m = newScorePS.main; m.useUnscaledTime = true; m.scalingMode = scalingMode; if (fxScaleFactor != 1f) m.startSizeMultiplier *= fxScaleFactor; }
            go.transform.localScale = Vector3.one;
        }

        if (gameOverParticle)
        {
            var go = Instantiate(gameOverParticle, fxRoot);
            if (fxLayer >= 0) LayerUtil.SetLayerRecursive(go, fxLayer);
            go.SetActive(false);

            gameOverPS = go.GetComponent<ParticleSystem>();
            if (gameOverPS) { var m = gameOverPS.main; m.useUnscaledTime = true; m.scalingMode = scalingMode; if (fxScaleFactor != 1f) m.startSizeMultiplier *= fxScaleFactor; }
            go.transform.localScale = Vector3.one;
        }

        Debug.Log($"[Particle] InitializeNonPool done. allClearPSs={allClearPSs.Count}");
    }

    // ================================
    // 좌표 변환 (공통)
    // ================================
    public void ConfigureGrid(int r, int c, Vector2 cellSize, Vector3 firstCellLocalPos)
    {
        rows = r; cols = c; squareSize = cellSize; squarePosLocal = firstCellLocalPos;
    }
    private Vector3 RowIndexToUiLocal(int rowIndex)
    {
        // 첫 칸 기준 + (열 중앙으로 이동)
        float centerCol = (cols - 1) * 0.5f; // 8칸이면 3.5
        float offsetX = (squareSize.x + spacing.x) * centerCol;
        float offsetY = -(squareSize.y + spacing.y) * rowIndex;
        return squarePosLocal + new Vector3(offsetX, offsetY, 0f);
    }

    private Vector3 ColIndexToUiLocal(int colIndex)
    {
        float centerRow = (rows - 1) * 0.5f; // 8칸이면 3.5
        float offsetX = (squareSize.x + spacing.x) * colIndex;
        float offsetY = -(squareSize.y + spacing.y) * centerRow;
        return squarePosLocal + new Vector3(offsetX, offsetY, 0f);
    }
    private Vector3 UiWorldToFxLocal_NoProjection(Vector3 uiWorld)
    {
        if (!fxRoot) return uiWorld;
        // x,y는 UI 월드 그대로, z만 fx 평면에 맞춤
        var fxWorld = new Vector3(uiWorld.x, uiWorld.y, fxRoot.position.z);
        return fxRoot.InverseTransformPoint(fxWorld);
    }
    private Vector3 UiLocalToFxLocal(Vector3 uiLocal)
    {
        if (!gridParent) return uiLocal;
        var gridRT = gridParent as RectTransform;
        var uiWorld = gridRT.TransformPoint(uiLocal);
        return UiWorldToFxLocal_NoProjection(uiWorld);
    }

    private Vector3 UiWorldToFxLocal(Vector3 uiWorld)
    {
        return UiWorldToFxLocal_NoProjection(uiWorld);
    }

    private Vector3 ToFxLocal(in SpawnTarget t)
    {
        switch (t.mode)
        {
            case SpawnMode.GridRow: return UiLocalToFxLocal(RowIndexToUiLocal(t.index));
            case SpawnMode.GridCol: return UiLocalToFxLocal(ColIndexToUiLocal(t.index));
            case SpawnMode.AnchorRect:
                {
                    var uiWorld = GetRectWorldCenter(t.anchor);
                    if (t.pixelOffset != default)
                    {
                        var local = t.anchor.InverseTransformPoint(uiWorld);
                        local += (Vector3)t.pixelOffset;
                        uiWorld = t.anchor.TransformPoint(local);
                    }
                    return UiWorldToFxLocal(uiWorld);
                }
            case SpawnMode.ScreenPoint:
                {
                    var screen = t.screen;
                    if (fxCamera.targetTexture != null)
                    {
                        float sx = (float)fxCamera.pixelWidth / Screen.width;
                        float sy = (float)fxCamera.pixelHeight / Screen.height;
                        screen.x *= sx; screen.y *= sy;
                    }
                    float depth = Mathf.Abs(fxRoot.position.z - fxCamera.transform.position.z);
                    var fxWorld = fxCamera.ScreenToWorldPoint(new Vector3(screen.x, screen.y, depth));
                    return fxRoot.InverseTransformPoint(fxWorld);
                }
            case SpawnMode.WorldPos: return fxRoot.InverseTransformPoint(t.world);
            default: return Vector3.zero;
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

    private Vector3 GetRectWorldCenter(RectTransform rt)
    {
        var rect = rt.rect;
        // pivot 보정된 로컬 중앙
        var localCenter = new Vector3(
            (0.5f - rt.pivot.x) * rect.width,
            (0.5f - rt.pivot.y) * rect.height,
            0f
        );
        return rt.TransformPoint(localCenter);
    }

    // ================================
    // 공통 재생기
    // ================================
    private void CacheIfNeeded(ParticleSystem ps)
    {
        if (!_baseStartSize.ContainsKey(ps))
            _baseStartSize[ps] = ps.main.startSizeMultiplier;
    }

    private void ApplyFxParams(ParticleSystem ps, in FxParams p)
    {
        if (!ps) return;
        var main = ps.main;
        if (p.setScalingMode) main.scalingMode = scalingMode;

        if (p.isLineFx)
        {
            if (lineStartSize > 0f) main.startSize = lineStartSize;
        }
        else
        {
            if (!p.keepPrefabSize)
            {
                CacheIfNeeded(ps);
                if (main.startSize3D)
                {
                    // 3D인 경우도 base를 따로 캐시해서 절대값으로 세팅 권장
                    main.startSizeXMultiplier = p.uniformScale;
                    main.startSizeYMultiplier = p.uniformScale;
                    main.startSizeZMultiplier = p.uniformScale;
                }
                else
                {
                    main.startSizeMultiplier = _baseStartSize[ps] * p.uniformScale;
                }
            }
        }
        if (p.color.HasValue) main.startColor = p.color.Value;
    }

    private void PlayOnceCommon(
        ParticleSystem ps,
        in SpawnTarget target,
        in FxParams p,
        bool returnToPool,
        Queue<ParticleSystem> poolToReturn = null)
    {
        if (!ps) return;

        ApplyFxParams(ps, p);

        var t = ps.transform;
        t.SetParent(fxRoot, false);
        t.localPosition = ToFxLocal(target);
        t.localRotation = Quaternion.Euler(0f, target.rotationY, target.rotationZ);
        t.localScale = Vector3.one;

        if (fxLayer >= 0) LayerUtil.SetLayerRecursive(ps.gameObject, fxLayer);

        ps.gameObject.SetActive(true);
        ps.Clear();
        ps.Play();

        var main = ps.main;
        float dur = target.durationOverride ?? LifetimeMax(main);

        if (returnToPool && poolToReturn != null)
        {
            StartCoroutine(ReturnToPoolAfter(ps, dur, target.unscaled, poolToReturn));
        }
        else
        {
            StartCoroutine(WaitAndStop(ps, dur, target.unscaled));
        }
    }

    private IEnumerator WaitAndStop(ParticleSystem ps, float delay, bool unscaled)
    {
        if (unscaled) yield return new WaitForSecondsRealtime(delay);
        else yield return new WaitForSeconds(delay);

        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        ps.transform.localRotation = Quaternion.identity;
        ps.gameObject.SetActive(false);
    }

    private IEnumerator ReturnToPoolAfter(ParticleSystem ps, float delay, bool unscaled, Queue<ParticleSystem> pool)
    {
        if (unscaled) yield return new WaitForSecondsRealtime(delay);
        else yield return new WaitForSeconds(delay);

        yield return new WaitWhile(() => ps != null && ps.IsAlive(true));

        if (ps == null) yield break;
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        ps.transform.localRotation = Quaternion.identity;
        ps.gameObject.SetActive(false);
        pool.Enqueue(ps);
    }

    public void PlayLoopCommon(ParticleSystem ps, in SpawnTarget target, in FxParams p,
                               bool returnToPool, Queue<ParticleSystem> poolToReturn = null)
    {
        if (ps == null) return;
        ApplyFxParams(ps, p);
        var t = ps.transform;
        t.SetParent(fxRoot, false);
        t.localPosition = ToFxLocal(target);
        t.localRotation = Quaternion.Euler(0f, 0f, target.rotationZ);
        t.localScale = Vector3.one;
        if (fxLayer >= 0) LayerUtil.SetLayerRecursive(ps.gameObject, fxLayer);

        var main = ps.main; main.loop = true;

        ps.gameObject.SetActive(true);
        ps.Clear();
        ps.Play();

        var key = (target.mode, target.index);
        activeLoops[key] = (ps, returnToPool ? poolToReturn : null);
    }


    // 모든 루프 일괄 종료
    // **모든** 재생 중인 루프 파티클 일괄 종료 (파라미터 없음)
    public void StopAllLoopCommon()
    {
        foreach (var kv in activeLoops)
        {
            var (ps, pool) = kv.Value;
            if (!ps) continue;

            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ps.gameObject.SetActive(false);
            if (pool != null) pool.Enqueue(ps); // 풀로 되돌릴 수 있으면
        }
        activeLoops.Clear();
    }

    // ================================
    // 퍼블릭 API (기존 시그니처 유지)
    // ================================
    // 1) 라인 파괴 FX (풀 반환)
    public void PlayRowParticle(int rowIndex, Color color)
    {
        if (destroyPool.Count == 0) { Debug.LogWarning("destroyPool exhausted"); return; }
        var ps = destroyPool.Dequeue();

        var target = new SpawnTarget(
            SpawnMode.GridRow, idx: rowIndex, rotZ: 90f, unscaledTime: false);

        var param = new FxParams(color, lineFx: true, applyScalingMode: true);

        PlayOnceCommon(ps, target, param, returnToPool: true, poolToReturn: destroyPool);
    }

    public void PlayColParticle(int colIndex, Color color)
    {
        if (destroyPool.Count == 0) { Debug.LogWarning("destroyPool exhausted"); return; }
        var ps = destroyPool.Dequeue();

        var target = new SpawnTarget(
            SpawnMode.GridCol, idx: colIndex, rotZ: 0f, unscaledTime: false);

        var param = new FxParams(color, lineFx: true, applyScalingMode: true);

        PlayOnceCommon(ps, target, param, returnToPool: true, poolToReturn: destroyPool);
    }

    // 콤보용 파티클 재생 (스프라이트 변경)
    public void PlayComboRowParticle(int rowIndex, Sprite sprite)
    {
        //EnsureComboPool();
        if (comboPool == null || comboPool.Count == 0)
        {
            Debug.LogWarning("[Particle] comboPool empty (row)");
            return;
        }

        var ps = comboPool.Dequeue();

        var target = new SpawnTarget(
            SpawnMode.GridRow, idx: rowIndex, rotZ: 0f, unscaledTime: false);

        var param = new FxParams(Color.white, lineFx: true, applyScalingMode: true);

        ApplySprite(ps, sprite);
        PlayOnceCommon(ps, target, param, returnToPool: true, poolToReturn: comboPool);
    }

    public void PlayComboColParticle(int colIndex, Sprite sprite)
    {
        //EnsureComboPool();
        if (comboPool == null || comboPool.Count == 0)
        {
            Debug.LogWarning("[Particle] comboPool empty (col)");
            return;
        }

        var ps = comboPool.Dequeue();

        var target = new SpawnTarget(
            SpawnMode.GridCol, idx: colIndex, rotY: 90f, rotZ: 90f, unscaledTime: false);

        var param = new FxParams(Color.white, lineFx: true, applyScalingMode: true);

        ApplySprite(ps, sprite);
        PlayOnceCommon(ps, target, param, returnToPool: true, poolToReturn: comboPool);
    }

    private static void ApplySprite(ParticleSystem ps, Sprite sprite)
    {
        Debug.Log($"ApplySprite : {sprite}");
        if (!sprite) return;

        // 권장: MPB로 텍스처만 바꾸면 머티리얼 인스턴스 증식 방지
        var r = ps.GetComponent<ParticleSystemRenderer>();
        var mpb = new MaterialPropertyBlock();
        r.GetPropertyBlock(mpb);
        mpb.SetTexture("_MainTex", sprite.texture);
        r.SetPropertyBlock(mpb);

        // TextureSheetAnimation을 쓰는 경우 (스프라이트 슬롯 교체)
        var tsa = ps.textureSheetAnimation;
        if (tsa.enabled)
        {
            tsa.mode = ParticleSystemAnimationMode.Sprites;
            tsa.RemoveSprite(0);
            tsa.AddSprite(sprite);
        }
    }

    // 테두리용 파티클 재생 
    public void PlayRowPerimeterParticle(int rowIndex, Color color)
    {
        if (perimeterPool.Count == 0) { Debug.LogWarning("perimeterPool exhausted"); return; }
        var ps = perimeterPool.Dequeue();

        var target = new SpawnTarget(
            SpawnMode.GridRow, idx: rowIndex, rotZ: 90f, unscaledTime: false);

        var param = new FxParams(color, lineFx: true, applyScalingMode: true);

        PlayLoopCommon(ps, target, param, returnToPool: true, poolToReturn: perimeterPool);
    }

    public void PlayColPerimeterParticle(int colIndex, Color color)
    {
        if (perimeterPool.Count == 0) { Debug.LogWarning("perimeterPool exhausted"); return; }
        var ps = perimeterPool.Dequeue();

        var target = new SpawnTarget(
            SpawnMode.GridCol, idx: colIndex, rotZ: 0f, unscaledTime: false);

        var param = new FxParams(color, lineFx: true, applyScalingMode: true);

        PlayLoopCommon(ps, target, param, returnToPool: true, poolToReturn: perimeterPool);
    }

    // 2) 단발 FX (비풀, 자동 비활성)

    private void EnsureAllClearParticlesFallback(int desired = 2)
    {
        if (allClearPSs.Count > 0) return;
        if (!allClearParticle) { Debug.LogError("[Particle] allClearParticle prefab missing"); return; }

        Debug.LogWarning("[Particle] allClearPSs empty. Creating fallback instances under fxRoot.");

        int n = (allClearPos != null && allClearPos.Length > 0) ? allClearPos.Length : desired;
        for (int i = 0; i < n; i++)
        {
            var go = Instantiate(allClearParticle, fxRoot);
            if (fxLayer >= 0) LayerUtil.SetLayerRecursive(go, fxLayer);
            go.SetActive(false);

            var ps = go.GetComponent<ParticleSystem>();
            if (!ps) { Debug.LogError("[Particle] allClearParticle has no ParticleSystem."); continue; }

            var main = ps.main;
            main.scalingMode = scalingMode;
            if (fxScaleFactor != 1f) main.startSizeMultiplier *= fxScaleFactor;

            go.transform.localScale = Vector3.one;
            allClearPSs.Add(ps);
        }
    }
    public void PlayAllClear()
    {
        EnsureAllClearParticlesFallback();

        Vector3[] positions = {
        new Vector3(5f, 4f, 0f),
        new Vector3(-5f, 4f, 0f)
    };
        Vector3[] rotations = {
        new Vector3(330f, 270f, 0f),
        new Vector3(330f, 90f, 0f)
    };

        for (int i = 0; i < positions.Length; i++)
        {
            if (i >= allClearPSs.Count) break;
            var ps = allClearPSs[i];
            if (!ps) continue;

            var go = ps.gameObject;
            go.SetActive(true);

            var t = ps.transform;
            t.SetParent(fxRoot, false);
            t.localPosition = positions[i];
            t.localRotation = Quaternion.Euler(rotations[i]);
            ApplyAllClearSize(ps);

            PlayOnceAndAutoDisable(ps, unscaled: true);
        }
    }

    public void PlayAllClearAtWorld(Vector3 fxWorldCenter)
    {
        EnsureAllClearParticlesFallback();

        if (allClearPSs == null || allClearPSs.Count == 0)
        {
            Debug.LogWarning("[Particle] No AllClear particles prepared.");
            return;
        }

        Vector3 centerLocal = fxRoot.InverseTransformPoint(fxWorldCenter);

        Vector3[] offsets =
        {
        new Vector3( 5f, 4f, 0f),
        new Vector3(-5f, 4f, 0f)
    };
        Vector3[] rotations =
        {
        new Vector3(330f, 270f, 0f),
        new Vector3(330f,  90f, 0f)
    };

        for (int i = 0; i < allClearPSs.Count && i < offsets.Length; i++)
        {
            var ps = allClearPSs[i];
            if (!ps) continue;

            var t = ps.transform;
            t.SetParent(fxRoot, false);
            t.localPosition = centerLocal + offsets[i];
            t.localRotation = Quaternion.Euler(rotations[i]);
            ApplyAllClearSize(ps);
            PlayOnceAndAutoDisable(ps, unscaled: true);
        }
    }

    public void PlayNewScoreAt(RectTransform anchor = null, Vector2 pixelOffset = default)
    {
        if (!newScorePS) return;
        var m = newScorePS.main; m.useUnscaledTime = true;

        var fallback = go_NewScorePos ? go_NewScorePos : gameOverTransform;
        var target = new SpawnTarget(
            SpawnMode.AnchorRect,
            a: (anchor ? anchor : fallback),
            px: pixelOffset,
            rotZ: 0f,
            unscaledTime: true,
            dur: LifetimeMax(m));

        var param = new FxParams(color: null, lineFx: false, applyScalingMode: true);

        PlayOnceCommon(newScorePS, target, param, returnToPool: false);
    }

    public void PlayGameOverAt(RectTransform anchor = null, Vector2 pixelOffset = default)
    {
        if (!gameOverPS) return;
        var m = gameOverPS.main; m.useUnscaledTime = true;

        var fallback = go_GameOverPos ? go_GameOverPos : gameOverTransform;
        var target = new SpawnTarget(
            SpawnMode.AnchorRect,
            a: (anchor ? anchor : fallback),
            px: pixelOffset,
            rotZ: 0f,
            unscaledTime: true,
            dur: LifetimeMax(m));

        var param = new FxParams(color: null, lineFx: false, applyScalingMode: true);

        PlayOnceCommon(gameOverPS, target, param, returnToPool: false);
    }

    // 화면 중앙 고정 오프셋(기존 ScreenToWorldPoint 방식 대체)
    public void PlayGameOverAtScreenCenter(Vector2 screenOffset)
    {
        if (!gameOverPS) return;
        var m = gameOverPS.main; m.useUnscaledTime = true;

        Vector2 screen = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f) + screenOffset;

        if (fxCamera.targetTexture != null)
        {
            float sx = (float)fxCamera.pixelWidth / Screen.width;
            float sy = (float)fxCamera.pixelHeight / Screen.height;
            screen.x *= sx; screen.y *= sy;
        }

        var target = new SpawnTarget(
            SpawnMode.ScreenPoint,
            scr: screen,
            rotZ: 0f,
            unscaledTime: true,
            dur: LifetimeMax(m));

        var param = new FxParams(color: null, lineFx: false, applyScalingMode: true);
        PlayOnceCommon(gameOverPS, target, param, returnToPool: false);
    }

    private static class LayerUtil
    {
        public static void SetLayerRecursive(GameObject root, int layer)
        {
            if (!root) return;
            var ts = root.GetComponentsInChildren<Transform>(true);
            foreach (var t in ts) t.gameObject.layer = layer;
        }
    }
    private void EnsureExplosionFallback(ParticleSystem ps)
    {
        var m = ps.main;
        var emi = ps.emission;
        var sh = ps.shape;

        bool noSpeed = (m.startSpeed.mode == ParticleSystemCurveMode.Constant && m.startSpeed.constant <= 0f);
        bool noRate = (!emi.enabled) || (emi.rateOverTime.mode == ParticleSystemCurveMode.Constant && emi.rateOverTime.constant <= 0f);
        ParticleSystem.Burst[] bursts = new ParticleSystem.Burst[emi.burstCount];
        emi.GetBursts(bursts);
        bool noBurst = bursts == null || bursts.Length == 0;

        if (noSpeed) m.startSpeed = 4f;
        if (noRate && noBurst)
        {
            emi.enabled = true;
            emi.rateOverTime = 0f;
            emi.SetBursts(new[] { new ParticleSystem.Burst(0f, 50, 70, 1, 0.01f) });
        }
        if (!sh.enabled)
        {
            sh.enabled = true;
            sh.shapeType = ParticleSystemShapeType.Sphere;
            sh.radius = 0.15f;
        }
    }
    private void ApplyAllClearSize(ParticleSystem ps)
    {
        var m = ps.main;
        if (m.startSize3D)
        {
            // 기본값 × 배율 (누적 NO)
            if (_baseSize3D.TryGetValue(ps, out var baseMul))
            {
                m.startSizeXMultiplier = baseMul.x * allClearSizeScale;
                m.startSizeYMultiplier = baseMul.y * allClearSizeScale;
                m.startSizeZMultiplier = baseMul.z * allClearSizeScale;
            }
        }
        else
        {
            if (_baseSize.TryGetValue(ps, out var baseMul))
                m.startSizeMultiplier = baseMul * allClearSizeScale;
        }
    }
    //private void EnsureComboPool()
    //{
    //    if (comboPool == null) return;
    //    if (comboPool.Count > 0) return;
    //
    //    if (!comboParticle && !comboPrefab)
    //    {
    //        Debug.LogError("[Particle] Combo prefab is null (comboParticle/comboPrefab).");
    //        return;
    //    }
    //
    //    // 우선순위: comboPrefab > comboParticle
    //    var prefabGO = comboPrefab ? comboPrefab.gameObject : comboParticle;
    //
    //    for (int i = 0; i < Mathf.Max(1, comboPrewarm); i++)
    //    {
    //        var go = Instantiate(prefabGO, comboParent ? comboParent : fxRoot);
    //        if (fxLayer >= 0) LayerUtil.SetLayerRecursive(go, fxLayer);
    //        go.SetActive(false);
    //        var ps = go.GetComponent<ParticleSystem>();
    //        var m = ps.main;
    //        m.scalingMode = scalingMode;
    //        if (lineStartSize > 0f) m.startSize = lineStartSize;
    //        comboPool.Enqueue(ps);
    //    }
    //}

    private void PlayOnceAndAutoDisable(ParticleSystem ps, bool unscaled)
    {
        if (!ps) return;

        // 본체 + 자식(서브 이미터 포함) 모두 1회 재생 세팅
        foreach (var s in ps.GetComponentsInChildren<ParticleSystem>(true))
        {
            var m = s.main;
            m.loop = false;
            m.useUnscaledTime = unscaled;
            m.stopAction = ParticleSystemStopAction.Disable; // 끝나면 자동 비활성

            // 루프 방지: 상시 방출 끄고, 버스트만 사용
            var em = s.emission;
            em.enabled = true;
            em.rateOverTime = 0f;

            // 만약 프리팹에 버스트가 전혀 없다면 1회 폭발 보정(선택)
            EnsureExplosionFallback(s);

            m.scalingMode = scalingMode;
        }

        ps.gameObject.SetActive(true);
        ps.Clear(true);
        ps.Play(true);

        // 수명 끝나면 완전히 정지/비활성 (자식까지 고려)
        var life = LifetimeMax(ps.main);
        StartCoroutine(WaitAndStop(ps, life, unscaled));
    }
}

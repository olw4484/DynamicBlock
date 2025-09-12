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
        public readonly float rotationZ;           // 최종 로컬 Z 회전
        public readonly bool unscaled;             // TimeScale 무시 여부
        public readonly float? durationOverride;   // 지속시간 강제 지정

        public SpawnTarget(
            SpawnMode m, int idx = -1, RectTransform a = null,
            Vector2? scr = null, Vector3? w = null, Vector2? px = null,
            float rotZ = 0f, bool unscaledTime = false, float? dur = null)
        {
            mode = m; index = idx; anchor = a;
            screen = scr ?? default;
            world = w ?? default;
            pixelOffset = px ?? default;
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

    [Header("Pool Settings")]
    [SerializeField] private GameObject destroyParticle;   // 가로/세로 소거 라인
    [SerializeField] private GameObject perimeterParticle; // 테두리 라인 등
    [SerializeField] private int poolSize = 6;

    [Header("FX Size Controls (권장: 스케일 1, 입자 크기로 제어)")]
    [Tooltip("행/열 소거 라인 두께 등을 입자 크기로 맞추세요.")]
    [SerializeField] private float lineStartSize = 1f;      // 줄 두께
    [SerializeField] private float fxScaleFactor = 3f;      // 일반 FX 배율
    [SerializeField] private float allClearSizeScale = 1.75f;
    [SerializeField] private ParticleSystemScalingMode scalingMode = ParticleSystemScalingMode.Hierarchy;

    // ================================
    // Runtime
    // ================================
    private ParticleSystem newScorePS;
    private ParticleSystem gameOverPS;
    private readonly List<ParticleSystem> allClearPSs = new();
    private readonly Queue<ParticleSystem> destroyPool = new();
    private readonly Queue<ParticleSystem> perimeterPool = new();
    private int fxLayer = -1;
    private Dictionary<(SpawnMode, int), ParticleSystem> activeLoops = new();

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
    }

    // ================================
    // 좌표 변환 (공통)
    // ================================
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
        Vector3 uiWorld = gridRT.TransformPoint(uiLocal);

        var uiCam = uiCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : uiCanvas.worldCamera;
        Vector2 screen = RectTransformUtility.WorldToScreenPoint(uiCam, uiWorld);

        if (fxCamera.targetTexture != null)
        {
            float sx = (float)fxCamera.pixelWidth / Screen.width;
            float sy = (float)fxCamera.pixelHeight / Screen.height;
            screen.x *= sx;
            screen.y *= sy;
        }

        float depth = Mathf.Abs(fxRoot.position.z - fxCamera.transform.position.z);
        Vector3 fxWorld = fxCamera.ScreenToWorldPoint(new Vector3(screen.x, screen.y, depth));
        return fxRoot.InverseTransformPoint(fxWorld);
    }

    private Vector3 UiWorldToFxLocal(Vector3 uiWorld)
    {
        var uiCam = uiCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : uiCanvas.worldCamera;
        var screen = RectTransformUtility.WorldToScreenPoint(uiCam, uiWorld);

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
                if (main.startSize3D)
                {
                    main.startSizeXMultiplier *= p.uniformScale;
                    main.startSizeYMultiplier *= p.uniformScale;
                    main.startSizeZMultiplier *= p.uniformScale;
                }
                else
                {
                    main.startSizeMultiplier = p.uniformScale;
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
        t.localRotation = Quaternion.Euler(0f, 0f, target.rotationZ);
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

        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        ps.transform.localRotation = Quaternion.identity;
        ps.gameObject.SetActive(false);
        pool.Enqueue(ps);
    }

    public void PlayLoopCommon(
        ParticleSystem ps,
        in SpawnTarget target,
        in FxParams p,
        bool returnToPool,
        Queue<ParticleSystem> poolToReturn = null)
    {
        if (ps == null) return;

        ApplyFxParams(ps, p);

        var t = ps.transform;
        t.SetParent(fxRoot, false);
        t.localPosition = ToFxLocal(target);
        t.localRotation = Quaternion.Euler(0f, 0f, target.rotationZ);
        t.localScale = Vector3.one;
        if (fxLayer >= 0) LayerUtil.SetLayerRecursive(ps.gameObject, fxLayer);

        var main = ps.main;
        main.loop = true;

        ps.gameObject.SetActive(true);
        ps.Clear();
        ps.Play();

        // 변경: 튜플 키 사용
        var key = (target.mode, target.index);
        activeLoops[key] = ps;
    }


    // 모든 루프 일괄 종료
    // **모든** 재생 중인 루프 파티클 일괄 종료 (파라미터 없음)
    public void StopAllLoopCommon()
    {
        foreach (var kv in activeLoops)
        {
            var ps = kv.Value;
            if (ps == null) continue;

            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ps.gameObject.SetActive(false);
            perimeterPool.Enqueue(ps);
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
    public void PlayAllClear()
    {
        // 고정 위치/회전값 리스트
        Vector3[] positions =
        {
        new Vector3(5f, 4f, 0f),
        new Vector3(-5f, 4f, 0f)
    };

        Vector3[] rotations =
        {
        new Vector3(330f, 270f, 0f),
        new Vector3(330f, 90f, 0f)
    };

        for (int i = 0; i < positions.Length; i++)
        {
            if (i >= allClearPSs.Count) break; // 파티클 개수 초과 방지

            var ps = allClearPSs[i];
            if (!ps) continue;

            var t = ps.transform;
            t.SetParent(fxRoot, false);
            t.localPosition = positions[i];
            t.localRotation = Quaternion.Euler(rotations[i]);
            ApplyAllClearSize(ps);

            var param = new FxParams(
                color: null,
                lineFx: false,
                applyScalingMode: true,
                keepPrefabSize: true,
                uniformScale: 1f
            );

            ps.Play();
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
}

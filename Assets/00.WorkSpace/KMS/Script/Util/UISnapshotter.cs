using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public static class UISnapshotter
{
    // 메모리 캐시 (key -> Texture2D)
    static readonly Dictionary<string, Texture2D> _cache = new();

    // 숨김 코루틴 러너
    static SnapshotRunner _runner;
    static SnapshotRunner Runner
    {
        get
        {
            if (_runner != null) return _runner;
            var go = new GameObject("[UISnapshotter]");
            Object.DontDestroyOnLoad(go);
            _runner = go.AddComponent<SnapshotRunner>();
            return _runner;
        }
    }

    /// <summary>
    /// 화면 전체를 캡처해서 key로 저장.
    /// </summary>
    public static void Capture(string key)
    {
        if (string.IsNullOrEmpty(key)) return;
        Runner.StartCoroutine(CoCaptureFullScreen(key));
    }

    /// <summary>
    /// 특정 UI 루트(RectTransform 영역)를 캡처해서 key(또는 루트명)로 저장.
    /// - 화면에 보이지 않으면(알파 0, 캔버스 비활성 등) 빈 캡처가 될 수 있음.
    /// - 진짜 썸네일이 필요하면 temporarilyShowForCapture=true 로 잠깐 보이게 해서 캡처.
    /// </summary>
    public static void Capture(GameObject root, string key = null, bool temporarilyShowForCapture = false)
    {
        if (!root) return;
        if (string.IsNullOrEmpty(key)) key = root.name;
        Runner.StartCoroutine(CoCaptureRect(root, key, temporarilyShowForCapture));
    }

    public static bool TryGet(string key, out Texture2D tex) => _cache.TryGetValue(key, out tex);
    public static bool Has(string key) => _cache.ContainsKey(key);

    public static void Clear(string key)
    {
        if (_cache.TryGetValue(key, out var tex) && tex) Object.Destroy(tex);
        _cache.Remove(key);
    }

    public static void ClearAll()
    {
        foreach (var kv in _cache) if (kv.Value) Object.Destroy(kv.Value);
        _cache.Clear();
    }

    // --------- 내부 구현 ---------

    static IEnumerator CoCaptureFullScreen(string key)
    {
        yield return new WaitForEndOfFrame();

        int w = Screen.width;
        int h = Screen.height;
        if (w <= 0 || h <= 0) yield break;

        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
        tex.Apply();

        ReplaceCache(key, tex);
        // Debug.Log($"[UISnapshotter] Full screen captured -> {key} ({w}x{h})");
    }

    static IEnumerator CoCaptureRect(GameObject root, string key, bool temporarilyShowForCapture)
    {
        if (!root) yield break;

        // 캔버스/캔버스그룹 상태 백업
        var cg = root.GetComponent<CanvasGroup>();
        float? prevAlpha = cg ? cg.alpha : null;
        bool? prevInteractable = cg ? cg.interactable : null;
        bool? prevBlocks = cg ? cg.blocksRaycasts : null;

        var canvas = root.GetComponentInParent<Canvas>(includeInactive: true);
        bool? prevCanvasEnabled = canvas ? canvas.enabled : null;

        // 필요 시 잠깐 보이게
        if (temporarilyShowForCapture)
        {
            if (canvas) canvas.enabled = true;
            if (cg)
            {
                cg.alpha = 1f;
                cg.interactable = false;     // 입력 차단
                cg.blocksRaycasts = false;
            }
        }

        yield return new WaitForEndOfFrame();

        // RectTransform 기준 화면영역 계산
        var rt = root.GetComponent<RectTransform>() ?? root.GetComponentInChildren<RectTransform>(true);
        if (!rt)
        {
            // 못 찾으면 전체화면
            yield return CoCaptureFullScreen(key);
        }
        else
        {
            // 월드 코너 → 스크린 좌표
            Vector3[] world = new Vector3[4];
            rt.GetWorldCorners(world);

            Camera cam = canvas && canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? (canvas.worldCamera ? canvas.worldCamera : Camera.main)
                : null;

            Vector2 min = new Vector2(float.MaxValue, float.MaxValue);
            Vector2 max = new Vector2(float.MinValue, float.MinValue);

            for (int i = 0; i < 4; i++)
            {
                Vector3 sp = cam ? cam.WorldToScreenPoint(world[i]) : RectTransformUtility.WorldToScreenPoint(null, world[i]);
                if (sp.x < min.x) min.x = sp.x;
                if (sp.y < min.y) min.y = sp.y;
                if (sp.x > max.x) max.x = sp.x;
                if (sp.y > max.y) max.y = sp.y;
            }

            // 스크린 Rect 정리
            var r = new Rect(min.x, min.y, Mathf.Max(2, max.x - min.x), Mathf.Max(2, max.y - min.y));
            // 클램프
            r.x = Mathf.Clamp(r.x, 0, Screen.width);
            r.y = Mathf.Clamp(r.y, 0, Screen.height);
            r.width = Mathf.Clamp(r.width, 1, Screen.width - r.x);
            r.height = Mathf.Clamp(r.height, 1, Screen.height - r.y);

            // ReadPixels은 좌하 원점 → 그대로 사용
            var tex = new Texture2D(Mathf.RoundToInt(r.width), Mathf.RoundToInt(r.height), TextureFormat.RGBA32, false);
            tex.ReadPixels(r, 0, 0);
            tex.Apply();

            ReplaceCache(key, tex);
            // Debug.Log($"[UISnapshotter] Rect captured -> {key} {r}");
        }

        // 상태 복원
        if (temporarilyShowForCapture)
        {
            if (prevCanvasEnabled.HasValue && canvas) canvas.enabled = prevCanvasEnabled.Value;
            if (cg)
            {
                if (prevAlpha.HasValue) cg.alpha = prevAlpha.Value;
                if (prevInteractable.HasValue) cg.interactable = prevInteractable.Value;
                if (prevBlocks.HasValue) cg.blocksRaycasts = prevBlocks.Value;
            }
        }
    }

    static void ReplaceCache(string key, Texture2D tex)
    {
        if (_cache.TryGetValue(key, out var old) && old) Object.Destroy(old);
        _cache[key] = tex;
    }

    // 코루틴 실행용
    class SnapshotRunner : MonoBehaviour { }
}

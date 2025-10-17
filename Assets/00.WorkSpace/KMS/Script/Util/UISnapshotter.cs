using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public static class UISnapshotter
{
    // �޸� ĳ�� (key -> Texture2D)
    static readonly Dictionary<string, Texture2D> _cache = new();

    // ���� �ڷ�ƾ ����
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
    /// ȭ�� ��ü�� ĸó�ؼ� key�� ����.
    /// </summary>
    public static void Capture(string key)
    {
        if (string.IsNullOrEmpty(key)) return;
        Runner.StartCoroutine(CoCaptureFullScreen(key));
    }

    /// <summary>
    /// Ư�� UI ��Ʈ(RectTransform ����)�� ĸó�ؼ� key(�Ǵ� ��Ʈ��)�� ����.
    /// - ȭ�鿡 ������ ������(���� 0, ĵ���� ��Ȱ�� ��) �� ĸó�� �� �� ����.
    /// - ��¥ ������� �ʿ��ϸ� temporarilyShowForCapture=true �� ��� ���̰� �ؼ� ĸó.
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

    // --------- ���� ���� ---------

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

        // ĵ����/ĵ�����׷� ���� ���
        var cg = root.GetComponent<CanvasGroup>();
        float? prevAlpha = cg ? cg.alpha : null;
        bool? prevInteractable = cg ? cg.interactable : null;
        bool? prevBlocks = cg ? cg.blocksRaycasts : null;

        var canvas = root.GetComponentInParent<Canvas>(includeInactive: true);
        bool? prevCanvasEnabled = canvas ? canvas.enabled : null;

        // �ʿ� �� ��� ���̰�
        if (temporarilyShowForCapture)
        {
            if (canvas) canvas.enabled = true;
            if (cg)
            {
                cg.alpha = 1f;
                cg.interactable = false;     // �Է� ����
                cg.blocksRaycasts = false;
            }
        }

        yield return new WaitForEndOfFrame();

        // RectTransform ���� ȭ�鿵�� ���
        var rt = root.GetComponent<RectTransform>() ?? root.GetComponentInChildren<RectTransform>(true);
        if (!rt)
        {
            // �� ã���� ��üȭ��
            yield return CoCaptureFullScreen(key);
        }
        else
        {
            // ���� �ڳ� �� ��ũ�� ��ǥ
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

            // ��ũ�� Rect ����
            var r = new Rect(min.x, min.y, Mathf.Max(2, max.x - min.x), Mathf.Max(2, max.y - min.y));
            // Ŭ����
            r.x = Mathf.Clamp(r.x, 0, Screen.width);
            r.y = Mathf.Clamp(r.y, 0, Screen.height);
            r.width = Mathf.Clamp(r.width, 1, Screen.width - r.x);
            r.height = Mathf.Clamp(r.height, 1, Screen.height - r.y);

            // ReadPixels�� ���� ���� �� �״�� ���
            var tex = new Texture2D(Mathf.RoundToInt(r.width), Mathf.RoundToInt(r.height), TextureFormat.RGBA32, false);
            tex.ReadPixels(r, 0, 0);
            tex.Apply();

            ReplaceCache(key, tex);
            // Debug.Log($"[UISnapshotter] Rect captured -> {key} {r}");
        }

        // ���� ����
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

    // �ڷ�ƾ �����
    class SnapshotRunner : MonoBehaviour { }
}

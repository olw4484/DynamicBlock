using System.Collections;
using System.Collections.Generic;
using UnityEngine;
// ================================
// Script : BusSnifferTap.cs
// Desc  : 모든 이벤트 전역 탭 + 필터/오버레이
// ================================

#if UNITY_EDITOR || DEVELOPMENT_BUILD
[AddComponentMenu("Debug/BusSnifferTap")]
public class BusSnifferTap : MonoBehaviour
{
    [SerializeField] int maxLines = 120;
    [SerializeField] KeyCode toggleKey = KeyCode.F9;
    [SerializeField] string containsFilter = "";  // 예: "Scene" / "Score"
    [SerializeField] bool showOverlay = true;

    readonly Queue<string> _lines = new();
    bool _enabled = true;

    void OnEnable()
    {
        TryAttach();
    }

    void Start()
    {
        if (!_attached) StartCoroutine(WaitAndAttach());
    }

    bool _attached;

    void TryAttach()
    {
        if (_attached) return;
        if (Game.Bus == null) return;
        Game.Bus.AddTap(OnAnyEvent);
        _attached = true;
    }

    IEnumerator WaitAndAttach()
    {
        yield return null;
        TryAttach();
    }

    void OnDisable()
    {
        if (_attached && Game.Bus != null) Game.Bus.RemoveTap(OnAnyEvent);
        _attached = false;
    }

    void Update()
    {
        if (Input.GetKeyDown(toggleKey)) _enabled = !_enabled;
    }

    static string FormatPayload(object e)
    {
        try
        {
            var json = JsonUtility.ToJson(e, false);
            if (!string.IsNullOrEmpty(json) && json != "{}") return json;
        }
        catch { /* ignored */ }
        return e.ToString();
    }

    void OnAnyEvent(object e)
    {
        if (!_enabled) return;

        // payload 직렬화 시도 → 실패/{}면 ToString() 폴백
        string payload = FormatPayload(e);
        string msg = $"{e.GetType().Name} {payload}";

        // 대소문자 무시 필터
        if (!string.IsNullOrEmpty(containsFilter) &&
            msg.IndexOf(containsFilter, System.StringComparison.OrdinalIgnoreCase) < 0)
            return;

        string line = $"[{Time.frameCount:000000} | {Time.time:0.000}] {msg}";
        _lines.Enqueue(line);
        while (_lines.Count > maxLines) _lines.Dequeue();
        Debug.Log("[Evt] " + line);
    }

    void OnGUI()
    {
        if (!_enabled || !showOverlay) return;
        GUILayout.BeginArea(new Rect(8, 8, Screen.width * 0.6f, Screen.height * 0.6f), GUI.skin.box);
        GUILayout.Label($"BusSnifferTap (F9)  Filter:\"{containsFilter}\"  Count:{_lines.Count}");
        foreach (var s in _lines) GUILayout.Label(s);
        GUILayout.EndArea();
    }
}
#endif
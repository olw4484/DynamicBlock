using UnityEngine;
using UnityEngine.EventSystems;

[DefaultExecutionOrder(-10000)]
public class EventSystemRescue : MonoBehaviour
{
    static bool booted;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Boot()
    {
        if (booted) return; booted = true;
        var go = new GameObject("EventSystemRescue");
        DontDestroyOnLoad(go);
        go.AddComponent<EventSystemRescue>();
        EnsureAlive();
    }

    void OnEnable() { StartCoroutine(Ping()); }
    void OnApplicationFocus(bool f) { if (f) EnsureAlive(); } // 홈복귀 시도 역시 보장
    void OnApplicationPause(bool p) { if (!p) EnsureAlive(); }

    System.Collections.IEnumerator Ping()
    {
        var w = new WaitForSecondsRealtime(0.5f);
        while (true) { EnsureAlive(); yield return w; }
    }

    public static void EnsureAlive()
    {
        if (EventSystem.current != null)
        {
            var es = EventSystem.current;
            es.enabled = true;
            var sim = es.GetComponent<StandaloneInputModule>();
            if (!sim) sim = es.gameObject.AddComponent<StandaloneInputModule>();
            // 모듈 재바인딩 토글(광고 복귀 후 먹통 방지)
            sim.enabled = false; sim.enabled = true;
            return;
        }

        // 씬에 비활성 ES 있으면 살림
        var found = Object.FindObjectOfType<EventSystem>(true);
        if (found)
        {
            found.gameObject.SetActive(true);
            found.enabled = true;
            var sim = found.GetComponent<StandaloneInputModule>() ?? found.gameObject.AddComponent<StandaloneInputModule>();
            sim.enabled = true;
            Debug.LogWarning("[ESRescue] revive existing ES");
            return;
        }

        // 아예 없으면 새로 생성
        var go = new GameObject("EventSystem(Auto)");
        go.AddComponent<EventSystem>();
        go.AddComponent<StandaloneInputModule>();
        DontDestroyOnLoad(go);
        Debug.LogWarning("[ESRescue] create new ES");
    }
}

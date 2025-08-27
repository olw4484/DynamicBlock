using _00.WorkSpace.GIL.Scripts.Managers;
using UnityEngine;

// ================================
// Project : DynamicBlock
// Script  : GameBootstrap.cs
// Desc    : Register → Initialize → Bind
// ================================

[AddComponentMenu("System/GameBootstrap")]
public class GameBootstrap : MonoBehaviour
{
    private void Awake()
    {
        var group = ManagerGroup.Instance
              ?? new GameObject("ManagerGroup").AddComponent<ManagerGroup>();
        DontDestroyOnLoad(group.gameObject);

        // 순수 C# 매니저
        var bus = new EventQueue();           // 0
        var game = new GameManager(bus);       // 10
        var scene = new SceneFlowManager();     // 20
        var audio = new NullSoundManager();     // 50

        // 씬 오브젝트 보장 (없으면 생성)
        var input = EnsureInScene<InputManager>("InputManager"); // 30
        var ui = EnsureInScene<UIManager>("UIManager");       // 100

        // 레거시 세이브 매니저 확보(없으면 생성)
        var legacySave = FindFirstObjectByType<SaveManager>()
                      ?? new GameObject("SaveManager").AddComponent<SaveManager>();
        DontDestroyOnLoad(legacySave.gameObject);

        // 어댑터(매니저로 등록)
        var saveAdapter = new SaveServiceAdapter();              // 40
        saveAdapter.SetDependencies(bus, legacySave);

        // GridManager는 씬 상주(선택)
        var grid = FindFirstObjectByType<GridManager>();
        if (grid != null) grid.SetDependencies(bus);

        // DI
        scene.SetDependencies(bus);
        if (ui != null) ui.SetDependencies(bus, game);
        if (input != null) input.SetDependencies(bus);

        // 등록 (Order로 정렬되므로 순서는 크게 무관)
        group.Register(bus);
        group.Register(game);
        group.Register(scene);
        group.Register(input);
        group.Register(saveAdapter);
        group.Register(audio);
        group.Register(ui);

        // 초기화 & 바인딩
        group.Initialize();
        var report = Game.Bind(group);
        Debug.Log(report.Detail);
    }

    static T EnsureInScene<T>(string name = null) where T : Component
    {
        var inst = FindFirstObjectByType<T>();
        if (inst) return inst;

        var go = new GameObject(name ?? typeof(T).Name);
        var comp = go.AddComponent<T>();

        // 멀티씬 운용이면 아래 주석 해제 고려
        // DontDestroyOnLoad(go);

        return comp;
    }
}


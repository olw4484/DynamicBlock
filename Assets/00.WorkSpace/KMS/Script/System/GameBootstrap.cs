using _00.WorkSpace.GIL.Scripts.Grids;
using _00.WorkSpace.GIL.Scripts.Managers;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// ================================
// Project : DynamicBlock
// Script  : GameBootstrap.cs
// Desc    : Register → Initialize → Bind
// ================================

[AddComponentMenu("System/GameBootstrap")]
public class GameBootstrap : MonoBehaviour
{
    [Header("Gird")]
    [SerializeField] private GridManager gridManager;
    [SerializeField] private Transform gridRoot;

    [Header("Scene Facades / Lanes")]
    [SerializeField] private AudioFxFacade audioFx;
    [SerializeField] private BlockFxFacade blockFx;
    [SerializeField] private EffectLane effectLane;
    [SerializeField] private SoundLane soundLane;

    private void Awake()
    {
        var group = ManagerGroup.Instance
              ?? new GameObject("ManagerGroup").AddComponent<ManagerGroup>();
        DontDestroyOnLoad(group.gameObject);

        if (gridManager == null)
            throw new System.Exception("[Bootstrap] GridManager reference missing");
        if (gridRoot == null)
            throw new System.Exception("[Bootstrap] GridRoot reference missing");

        // 순수 C# 매니저
        var bus = new EventQueue();          // 0
        var game = new GameManager(bus);      // 10
        var scene = new SceneFlowManager();    // 20
        var audio = new AudioServiceAdapter(); // 50
        var bgm = new BgmDirector();         // 25

        // 씬 오브젝트 보장 (없으면 생성)
        var ui = EnsureInScene<UIManager>("UIManager");
        var input = EnsureInScene<InputManager>("InputManager");

        var save = FindFirstObjectByType<SaveManager>()
        ?? new GameObject("SaveManager").AddComponent<SaveManager>();
        DontDestroyOnLoad(save.gameObject);

        save.SetDependencies(bus);

        // 브리지 보장 및 생성
        var clearResponder = EnsureInScene<ClearEventResponder>("ClearEventResponder");
        clearResponder.SetDependencies(bus);

        // spawnerManager
        var spawner = EnsureInScene<BlockSpawnManager>("BlockSpawnManager");
        spawner.SetDependencies(bus);

        // DI
        scene.SetDependencies(bus);
        if (ui != null) ui.SetDependencies(bus, game);
        if (input != null) input.SetDependencies(bus);
        bgm.SetDependencies(bus, audio);

        // 등록 (Order로 정렬되므로 순서는 크게 무관)
        group.Register(bus);
        group.Register(game);
        group.Register(spawner);
        group.Register(scene);
        group.Register(bgm);
        group.Register(input);
        group.Register(audio);
        group.Register(save);
        group.Register(ui);
        group.Register(clearResponder);

        // 초기화 & 바인딩
        group.Initialize();
        var report = Game.Bind(group);

        // 씬 파사드/레인 확보 & 바인딩
        if (!audioFx) audioFx = FindFirstObjectByType<AudioFxFacade>();
        if (!blockFx) blockFx = FindFirstObjectByType<BlockFxFacade>();
        if (!effectLane) effectLane = FindFirstObjectByType<EffectLane>();
        if (!soundLane) soundLane = FindFirstObjectByType<SoundLane>();

        Game.BindSceneFacades(audioFx, blockFx, effectLane, soundLane);

        soundLane?.SetDependencies(audio);
        // 그리드 스캔
        var squares = new List<GridSquare>();
        gridRoot.GetComponentsInChildren(includeInactive: true, result: squares);

        int maxR = -1, maxC = -1;
        foreach (var s in squares)
        {
            maxR = Mathf.Max(maxR, s.RowIndex);
            maxC = Mathf.Max(maxC, s.ColIndex);
        }
        gridManager.InitializeGridSquares(squares, maxR + 1, maxC + 1);
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


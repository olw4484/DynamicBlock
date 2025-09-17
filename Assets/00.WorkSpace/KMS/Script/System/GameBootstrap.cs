using _00.WorkSpace.GIL.Scripts.Grids;
using _00.WorkSpace.GIL.Scripts.Managers;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// ================================
// Project : DynamicBlock
// Script  : GameBootstrap.cs
// Desc    : Register �� Initialize �� Bind
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

        // ���� C# �Ŵ���
        var bus = new EventQueue();          // 0
        var game = new GameManager(bus);      // 10
        var scene = new SceneFlowManager();    // 20
        var audio = new AudioServiceAdapter(); // 50
        var bgm = new BgmDirector();         // 25

        // �� ������Ʈ ���� (������ ����)
        var ui = EnsureInScene<UIManager>("UIManager");
        var input = EnsureInScene<InputManager>("InputManager");

        var save = FindFirstObjectByType<SaveManager>()
        ?? new GameObject("SaveManager").AddComponent<SaveManager>();
        DontDestroyOnLoad(save.gameObject);

        save.SetDependencies(bus);

        // �긮�� ���� �� ����
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

        // ��� (Order�� ���ĵǹǷ� ������ ũ�� ����)
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

        // �ʱ�ȭ & ���ε�
        group.Initialize();
        var report = Game.Bind(group);

        // �� �Ļ��/���� Ȯ�� & ���ε�
        if (!audioFx) audioFx = FindFirstObjectByType<AudioFxFacade>();
        if (!blockFx) blockFx = FindFirstObjectByType<BlockFxFacade>();
        if (!effectLane) effectLane = FindFirstObjectByType<EffectLane>();
        if (!soundLane) soundLane = FindFirstObjectByType<SoundLane>();

        Game.BindSceneFacades(audioFx, blockFx, effectLane, soundLane);

        soundLane?.SetDependencies(audio);
        // �׸��� ��ĵ
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

        // ��Ƽ�� ����̸� �Ʒ� �ּ� ���� ���
        // DontDestroyOnLoad(go);

        return comp;
    }
}


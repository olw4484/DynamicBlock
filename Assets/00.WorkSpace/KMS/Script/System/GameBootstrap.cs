using _00.WorkSpace.GIL.Scripts.Managers;
using UnityEngine;

// ================================
// Project : DynamicBlock
// Script  : GameBootstrap.cs
// Desc    : Register �� Initialize �� Bind
// ================================

[AddComponentMenu("System/GameBootstrap")]
public class GameBootstrap : MonoBehaviour
{
    private void Awake()
    {
        var group = ManagerGroup.Instance
              ?? new GameObject("ManagerGroup").AddComponent<ManagerGroup>();
        DontDestroyOnLoad(group.gameObject);

        // ���� C# �Ŵ���
        var bus = new EventQueue();           // 0
        var game = new GameManager(bus);       // 10
        var scene = new SceneFlowManager();     // 20
        var audio = new NullSoundManager();     // 50

        // �� ������Ʈ ���� (������ ����)
        var input = EnsureInScene<InputManager>("InputManager"); // 30
        var ui = EnsureInScene<UIManager>("UIManager");       // 100

        // ���Ž� ���̺� �Ŵ��� Ȯ��(������ ����)
        var legacySave = FindFirstObjectByType<SaveManager>()
                      ?? new GameObject("SaveManager").AddComponent<SaveManager>();
        DontDestroyOnLoad(legacySave.gameObject);

        // �����(�Ŵ����� ���)
        var saveAdapter = new SaveServiceAdapter();              // 40
        saveAdapter.SetDependencies(bus, legacySave);

        // GridManager�� �� ����(����)
        var grid = FindFirstObjectByType<GridManager>();
        if (grid != null) grid.SetDependencies(bus);

        // DI
        scene.SetDependencies(bus);
        if (ui != null) ui.SetDependencies(bus, game);
        if (input != null) input.SetDependencies(bus);

        // ��� (Order�� ���ĵǹǷ� ������ ũ�� ����)
        group.Register(bus);
        group.Register(game);
        group.Register(scene);
        group.Register(input);
        group.Register(saveAdapter);
        group.Register(audio);
        group.Register(ui);

        // �ʱ�ȭ & ���ε�
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

        // ��Ƽ�� ����̸� �Ʒ� �ּ� ���� ���
        // DontDestroyOnLoad(go);

        return comp;
    }
}


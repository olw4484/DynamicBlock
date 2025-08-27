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



        var bus = new EventQueue();            // 0
        var game = new GameManager(bus);       // 10
        var scene = new SceneFlowManager();    // 20
        var audio = new NullSoundManager();    // 50  (IAudioService + IManager)

        var ui = FindFirstObjectByType<UIManager>();

        var input = FindFirstObjectByType<InputManager>() 
            ?? new GameObject("InputManager").AddComponent<InputManager>();
     
        var legacySave = FindFirstObjectByType<SaveManager>()
            ?? new GameObject("SaveManager").AddComponent<SaveManager>();

        var saveAdapter = new SaveServiceAdapter();
                saveAdapter.SetDependencies(bus, legacySave);

        var grid = FindFirstObjectByType<GridManager>();

        // ����
        scene.SetDependencies(bus);
        ui.SetDependencies(bus, game);
        input.SetDependencies(bus);
        grid.SetDependencies(bus);

        // ���
        group.Register(bus);
        group.Register(game);
        group.Register(scene);
        group.Register(audio);
        group.Register(ui);
        group.Register(input);
        group.Register(saveAdapter);

        // �ʱ�ȭ & ���ε�
        group.Initialize();
        Game.Bind(group);
    }
}


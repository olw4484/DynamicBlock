using System.Collections;
using System.Collections.Generic;
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

        var bus = new EventQueue();          // 0
        var game = new GameManager(bus);      // 10
        var scene = new SceneFlowManager();    // 20
        var audio = new NullSoundManager();    // 50  (IAudioService + IManager)

        var ui = FindFirstObjectByType<UIManager>()
              ?? new GameObject("UIManager").AddComponent<UIManager>();

        // ����
        scene.SetDependencies(bus);
        ui.SetDependencies(bus, game);

        // ���
        group.Register(bus);
        group.Register(game);
        group.Register(scene);
        group.Register(audio);   // �� ������� �������̽� ����ü �ϳ���
        group.Register(ui);

        // �ʱ�ȭ & ���ε�
        group.Initialize();
        Game.Bind(group);
    }
}


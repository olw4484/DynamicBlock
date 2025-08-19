using System.Collections;
using System.Collections.Generic;
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

        var bus = new EventQueue();                // 0
        var game = new GameManager(bus);           // 10
        var scene = new SceneFlowManager();        // 20
        var sound = new SoundManager();            // 50

        var ui = FindFirstObjectByType<UIManager>()
              ?? new GameObject("UIManager").AddComponent<UIManager>();

        // 주입
        scene.SetDependencies(bus);
        ui.SetDependencies(bus, game);

        // 등록
        group.Register(bus);
        group.Register(game);
        group.Register(scene);
        group.Register(sound);
        group.Register(ui);

        // 초기화 & 바인딩
        group.Initialize();
        Game.Bind(group);
    }
}


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

        var bus = new EventQueue();          // 0
        var game = new GameManager(bus);      // 10
        var scene = new SceneFlowManager();    // 20
        var audio = new NullSoundManager();    // 50  (IAudioService + IManager)

        var ui = FindFirstObjectByType<UIManager>()
              ?? new GameObject("UIManager").AddComponent<UIManager>();

        // 주입
        scene.SetDependencies(bus);
        ui.SetDependencies(bus, game);

        // 등록
        group.Register(bus);
        group.Register(game);
        group.Register(scene);
        group.Register(audio);   // ← 오디오는 인터페이스 구현체 하나만
        group.Register(ui);

        // 초기화 & 바인딩
        group.Initialize();
        Game.Bind(group);
    }
}


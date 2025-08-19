using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// ================================
// Project : DynamicBlock
// Script  : IManager.cs
// Desc    : 매니저 공통 인터페이스 (3-Phase Bootstrap)
// ================================
public interface IManager
{
    int Order { get; }     // 초기화 우선순위 (낮을수록 먼저)
    void PreInit();        // Config, 참조 연결
    void Init();           // 런타임 초기 상태/객체 생성
    void PostInit();       // UI 갱신, 이벤트 구독
}

// 매 프레임 처리
public interface ITickable { void Tick(float dt); }

// 종료/리소스 해제
public interface ITeardown { void Teardown(); }
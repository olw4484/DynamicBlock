using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// ================================
// Project : DynamicBlock
// Script  : IManager.cs
// Desc    : 매니저 공통 인터페이스
// ================================
public interface IManager
{
    void PreInit();   // Config, 참조 연결
    void Init();      // 런타임 초기 상태 생성
    void PostInit();  // UI 갱신, 이벤트 구독
}

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// ================================
// Project : DynamicBlock
// Script  : ManagerGroup.cs
// Desc    : 초기화 순서 제어
// ================================
public class ManagerGroup : MonoBehaviour
{
    [SerializeField] private List<MonoBehaviour> _managers; // IManager 구현 컴포넌트 등록

    private List<IManager> _ordered = new();

    private void Awake()
    {
        foreach (var mb in _managers)
            if (mb is IManager m) _ordered.Add(m);

        // 순서: PreInit → Init → PostInit
        foreach (var m in _ordered) m.PreInit();
        foreach (var m in _ordered) m.Init();
        foreach (var m in _ordered) m.PostInit();
    }
}

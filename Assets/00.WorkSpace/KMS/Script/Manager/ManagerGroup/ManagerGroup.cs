using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// ================================
// Project : DynamicBlock
// Script  : ManagerGroup.cs
// Desc    : �ʱ�ȭ ���� ����
// ================================
public class ManagerGroup : MonoBehaviour
{
    [SerializeField] private List<MonoBehaviour> _managers; // IManager ���� ������Ʈ ���

    private List<IManager> _ordered = new();

    private void Awake()
    {
        foreach (var mb in _managers)
            if (mb is IManager m) _ordered.Add(m);

        // ����: PreInit �� Init �� PostInit
        foreach (var m in _ordered) m.PreInit();
        foreach (var m in _ordered) m.Init();
        foreach (var m in _ordered) m.PostInit();
    }
}

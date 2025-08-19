using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

// ================================
// Project : DynamicBlock
// Script  : ManagerGroup.cs
// Desc    : 매니저 등록/초기화/조회 + Tick/Teardown
// ================================

[DisallowMultipleComponent]
[AddComponentMenu("System/ManagerGroup")]
public class ManagerGroup : MonoBehaviour
{
    public static ManagerGroup Instance { get; private set; }

    private readonly List<IManager> _managers = new();
    private ITickable[] _tickables;
    private bool _initialized;

    public IReadOnlyList<IManager> Managers => _managers;
    public bool IsInitialized => _initialized;

    private void Awake()
    {
        DontDestroyOnLoad(gameObject);
        Instance = this;
    }

    public void Register(IManager mgr)
    {
        _managers.Add(mgr);
    }

    public void Initialize()
    {
        if (_initialized) return;

        var ordered = _managers.OrderBy(m => m.Order).ToList();

        foreach (var m in ordered) m.PreInit();
        foreach (var m in ordered) m.Init();
        foreach (var m in ordered) m.PostInit();

        _tickables = ordered.OfType<ITickable>().ToArray();
        _initialized = true;
    }

    private void Update()
    {
        if (!_initialized || _tickables == null) return;
        float dt = Time.deltaTime;
        for (int i = 0; i < _tickables.Length; i++)
            _tickables[i].Tick(dt);
    }

    private void OnDestroy()
    {
        if (_initialized)
        {
            foreach (var td in _managers.AsEnumerable().Reverse().OfType<ITeardown>())
                td.Teardown();
        }
        if (Instance == this) Instance = null;
    }

    // 조회 (제네릭/리플렉션)
    public T Resolve<T>() where T : class, IManager
        => _managers.FirstOrDefault(m => m is T) as T;

    public IManager Resolve(System.Type t)
        => _managers.FirstOrDefault(m => t.IsInstanceOfType(m));
}

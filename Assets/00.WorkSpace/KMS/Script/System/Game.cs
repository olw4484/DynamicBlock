using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using System.Text;
using System;

// ================================
// Project : DynamicBlock
// Script  : Game.cs
// Desc    : ���� �Ļ�� (Explicit Bind + Fail-Fast + Report)
// ================================
public static class Game
{
    public static EventQueue Bus { get; private set; }
    public static GameManager GM { get; private set; }
    public static IAudioService Audio { get; private set; }
    public static UIManager UI { get; private set; }
    public static SceneFlowManager Scene { get; private set; }
    public static ISaveService Save { get; private set; }
    public static AudioFxFacade AudioFx { get; private set; }
    public static BlockFxFacade BlockFx { get; private set; }
    public static EffectLane EffectLane { get; private set; }
    public static SoundLane SoundLane { get; private set; }
    public static IAdService Ads { get; private set; }
    public static IFx Fx { get; private set; }


    public static bool IsBound { get; private set; }

    public struct BindOptions
    {
        public bool IncludeUI;        // UIManager�� �ʼ� �˻�����
        public bool IncludeScene;     // SceneFlowManager �ʼ� �˻�����
        public bool ThrowOnMissing;   // ����/�ߺ�/���� �̻� �� ���� ������
        public bool PrintReport;      // �ֿܼ� ����Ʈ �������
    }

    public struct BindReport
    {
        public string Summary;
        public string Detail;
        public int RegisteredCount;
        public string[] Missing;
        public string[] Duplicated;
        public (string type, int order)[] OrderList;
    }

    public static void BindSceneFacades(
    AudioFxFacade audioFx,
    BlockFxFacade blockFx,
    EffectLane effectLane,
    SoundLane soundLane)
    {
        AudioFx = audioFx;
        BlockFx = blockFx;
        EffectLane = effectLane;
        SoundLane = soundLane;
        Fx = blockFx;
        Debug.Log("[Game] Scene facades/lanes bound.");
    }

    public static BindReport Bind(ManagerGroup group, BindOptions? opt = null)
    {
        var options = opt ?? new BindOptions
        {
            IncludeUI = true,
            IncludeScene = true,
            ThrowOnMissing = true,
            PrintReport = true
        };

        // 1) ���� "���� ����"�� Resolve (�˻� ��� �ÿ��� ���� �ʵ忡 ����)
        var bus = group.Resolve<EventQueue>();
        var gm = group.Resolve<GameManager>();
        var audio = group.Resolve<IAudioService>();                 // �������̽� Resolve
        var ui = options.IncludeUI ? group.Resolve<UIManager>() : null;
        var scene = options.IncludeScene ? group.Resolve<SceneFlowManager>() : null;
        var save = group.Resolve<ISaveService>();

        // 2) �ʼ� Ÿ�� ���� ����
        var requiredTypes = new System.Collections.Generic.List<Type>
        {
            typeof(EventQueue), typeof(GameManager), typeof(IAudioService)
        };
        if (options.IncludeUI) requiredTypes.Add(typeof(UIManager));
        if (options.IncludeScene) requiredTypes.Add(typeof(SceneFlowManager));

        // 3) ����(����/�ߺ�/����)
        string[] missing = requiredTypes
            .Where(t => group.Resolve(t) == null)
            .Select(t => t.Name)
            .ToArray();

        var dup = group.Managers
            .GroupBy(m => m.GetType().Name)
            .Where(g => g.Count() > 1)
            .Select(g => $"{g.Key} x{g.Count()}")
            .ToArray();

        var ordered = group.Managers
            .OrderBy(m => m.Order)
            .Select(m => (m.GetType().Name, m.Order))
            .ToArray();

        // 4) ����Ʈ ���
        var sb = new StringBuilder();
        sb.AppendLine("=== Game.Bind Report ===");
        sb.AppendLine($"Registered : {group.Managers.Count}");
        if (missing.Length > 0) sb.AppendLine($"Missing    : {string.Join(", ", missing)}");
        if (dup.Length > 0) sb.AppendLine($"Duplicated : {string.Join(", ", dup)}");
        sb.AppendLine("Order      : " + string.Join(" < ", ordered.Select(x => $"{x.Name}({x.Order})")));

        var report = new BindReport
        {
            Summary = (missing.Length == 0 && dup.Length == 0) ? "OK" : "ISSUES",
            Detail = sb.ToString(),
            RegisteredCount = group.Managers.Count,
            Missing = missing,
            Duplicated = dup,
            OrderList = ordered
        };

        if (options.PrintReport) Debug.Log(report.Detail);
        if (options.ThrowOnMissing && (missing.Length > 0 || dup.Length > 0))
            throw new Exception($"Game.Bind FAILED: {report.Summary}. See console for details.");

        // 5) ����ϸ� "�� ����" ���� (���� ���ε� ���� ����)
        Bus = bus;
        GM = gm;
        Audio = audio;
        UI = ui;
        Scene = scene;

        IsBound = true; 
        return report;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Bus = null; GM = null; Audio = null; UI = null; Scene = null; IsBound = false;
        AudioFx = null; BlockFx = null; EffectLane = null; SoundLane = null; Ads = null;
    }
}

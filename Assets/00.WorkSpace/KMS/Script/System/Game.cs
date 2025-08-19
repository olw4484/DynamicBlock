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
    public static SoundManager Audio { get; private set; }
    public static UIManager UI { get; private set; } // (�ɼ�)

    public static bool IsBound { get; private set; }

    public struct BindOptions
    {
        public bool IncludeUI;        // UIManager�� �ʼ��� �˻�����
        public bool ThrowOnMissing;   // ����/�ߺ�/���� �̻� �� ���� ������
        public bool PrintReport;      // �ֿܼ� ����Ʈ �������
    }

    public struct BindReport
    {
        public string Summary;
        public string Detail;
        public int RegisteredCount;
        public string[] Missing;
        public string[] Duplicated;   // ���� Ÿ�� 2�� �̻�
        public (string type, int order)[] OrderList; // ���ĵ� ���
    }

    public static BindReport Bind(ManagerGroup group, BindOptions? opt = null)
    {
        var options = opt ?? new BindOptions { IncludeUI = true, ThrowOnMissing = true, PrintReport = true };

        // 1) �ʼ� Ÿ�� ���� ����
        var required = options.IncludeUI
            ? new Type[] { typeof(EventQueue), typeof(GameManager), typeof(SoundManager), typeof(UIManager) }
            : new Type[] { typeof(EventQueue), typeof(GameManager), typeof(SoundManager) };

        // 2) Resolve + ĳ��
        Bus = group.Resolve<EventQueue>();
        GM = group.Resolve<GameManager>();
        Audio = group.Resolve<SoundManager>();
        UI = options.IncludeUI ? group.Resolve<UIManager>() : null;

        // 3) ����(����/�ߺ�/����)
        var missing = required
            .Where(t => group.Resolve(t) == null)
            .Select(t => t.Name).ToArray();

        var dup = group.Managers
            .GroupBy(m => m.GetType().Name)
            .Where(g => g.Count() > 1)
            .Select(g => $"{g.Key} x{g.Count()}")
            .ToArray();

        var ordered = group.Managers
            .OrderBy(m => m.Order)
            .Select(m => (m.GetType().Name, m.Order))
            .ToArray();

        // 4) ����Ʈ ���ڿ�
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

        if (options.PrintReport)
            Debug.Log(report.Detail);

        if (options.ThrowOnMissing && (missing.Length > 0 || dup.Length > 0))
            throw new Exception($"Game.Bind FAILED: {report.Summary}. See console for details.");

        IsBound = true;
        return report;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Bus = null; GM = null; Audio = null; UI = null; IsBound = false;
    }
}

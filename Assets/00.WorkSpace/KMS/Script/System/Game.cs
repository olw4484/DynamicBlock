using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using System.Text;
using System;

// ================================
// Project : DynamicBlock
// Script  : Game.cs
// Desc    : 전역 파사드 (Explicit Bind + Fail-Fast + Report)
// ================================

public static class Game
{
    public static EventQueue Bus { get; private set; }
    public static GameManager GM { get; private set; }
    public static SoundManager Audio { get; private set; }
    public static UIManager UI { get; private set; } // (옵션)

    public static bool IsBound { get; private set; }

    public struct BindOptions
    {
        public bool IncludeUI;        // UIManager도 필수로 검사할지
        public bool ThrowOnMissing;   // 누락/중복/순서 이상 시 예외 던질지
        public bool PrintReport;      // 콘솔에 리포트 출력할지
    }

    public struct BindReport
    {
        public string Summary;
        public string Detail;
        public int RegisteredCount;
        public string[] Missing;
        public string[] Duplicated;   // 같은 타입 2개 이상
        public (string type, int order)[] OrderList; // 정렬된 목록
    }

    public static BindReport Bind(ManagerGroup group, BindOptions? opt = null)
    {
        var options = opt ?? new BindOptions { IncludeUI = true, ThrowOnMissing = true, PrintReport = true };

        // 1) 필수 타입 집합 구성
        var required = options.IncludeUI
            ? new Type[] { typeof(EventQueue), typeof(GameManager), typeof(SoundManager), typeof(UIManager) }
            : new Type[] { typeof(EventQueue), typeof(GameManager), typeof(SoundManager) };

        // 2) Resolve + 캐싱
        Bus = group.Resolve<EventQueue>();
        GM = group.Resolve<GameManager>();
        Audio = group.Resolve<SoundManager>();
        UI = options.IncludeUI ? group.Resolve<UIManager>() : null;

        // 3) 진단(누락/중복/순서)
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

        // 4) 리포트 문자열
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

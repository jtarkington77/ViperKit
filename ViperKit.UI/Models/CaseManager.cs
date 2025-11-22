using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace ViperKit.UI.Models;

public static class CaseManager
{
    private static readonly object _lock = new();
    private static readonly List<CaseEvent> _events = new();

    // Multi-target focus list for this case (ConnectWise, Steam, paths, etc.)
    private static readonly List<string> _focusTargets = new();

    public static string CaseId       { get; private set; } = string.Empty;
    public static DateTime StartedAt  { get; private set; }
    public static DateTime? EndedAt   { get; private set; }

    public static string HostName     { get; private set; } = string.Empty;
    public static string UserName     { get; private set; } = string.Empty;
    public static string OsDescription{ get; private set; } = string.Empty;

    public static void StartNewCase()
    {
        lock (_lock)
        {
            _events.Clear();
            _focusTargets.Clear();
            EndedAt = null;

            HostName      = Environment.MachineName;
            UserName      = Environment.UserName;
            OsDescription = GetOsDescription();

            CaseId    = $"{HostName}-{DateTime.Now:yyyyMMdd-HHmmss}";
            StartedAt = DateTime.Now;

            _events.Add(new CaseEvent
            {
                Timestamp = StartedAt,
                Tab       = "Case",
                Action    = "Case started",
                Severity  = "INFO",
                Target    = HostName,
                Details   = $"User: {UserName}; OS: {OsDescription}"
            });
        }
    }

    private static string GetOsDescription()
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return $"{RuntimeInformation.OSDescription} ({RuntimeInformation.OSArchitecture})";
            }

            return RuntimeInformation.OSDescription;
        }
        catch
        {
            return "Unknown OS";
        }
    }

    public static void AddEvent(
        string tab,
        string action,
        string severity,
        string? target,
        string? details)
    {
        lock (_lock)
        {
            _events.Add(new CaseEvent
            {
                Timestamp = DateTime.Now,
                Tab       = tab,
                Action    = action,
                Severity  = severity,
                Target    = target  ?? string.Empty,
                Details   = details ?? string.Empty
            });
        }
    }

    public static IReadOnlyList<CaseEvent> GetSnapshot()
    {
        lock (_lock)
        {
            return _events.ToList();
        }
    }

    // --------------------------------------------------------------------
    // FOCUS API – multi-target case focus for Hunt / Persist / Sweep
    // --------------------------------------------------------------------

    /// <summary>
    /// Backwards-compatible "set focus" – but we now APPEND to a list
    /// instead of overwriting, so you can build up ConnectWise + Steam + etc.
    /// </summary>
    public static void SetFocusTarget(string? value, string sourceTab = "System")
    {
        if (string.IsNullOrWhiteSpace(value))
            return;

        string trimmed = value.Trim();

        lock (_lock)
        {
            if (_focusTargets.Any(t => string.Equals(t, trimmed, StringComparison.OrdinalIgnoreCase)))
            {
                // Already tracked – no need to spam events.
                return;
            }

            _focusTargets.Add(trimmed);

            _events.Add(new CaseEvent
            {
                Timestamp = DateTime.Now,
                Tab       = sourceTab,
                Action    = "Case focus updated",
                Severity  = "INFO",
                Target    = trimmed,
                Details   = $"Focus list now: {string.Join(", ", _focusTargets)}"
            });
        }
    }

    /// <summary>
    /// Return a snapshot of all focus tokens for this case.
    /// </summary>
    public static IReadOnlyList<string> GetFocusTargets()
    {
        lock (_lock)
        {
            return _focusTargets.ToList();
        }
    }

    /// <summary>
    /// Clear all focus tokens (e.g. at the start of a brand-new case).
    /// </summary>
    public static void ClearFocus(string sourceTab = "System")
    {
        lock (_lock)
        {
            _focusTargets.Clear();

            _events.Add(new CaseEvent
            {
                Timestamp = DateTime.Now,
                Tab       = sourceTab,
                Action    = "Case focus cleared",
                Severity  = "INFO",
                Target    = string.Empty,
                Details   = string.Empty
            });
        }
    }

    // --------------------------------------------------------------------
    // EXPORT
    // --------------------------------------------------------------------

    public static string ExportToFile()
    {
        lock (_lock)
        {
            EndedAt ??= DateTime.Now;

            // Drop case logs underneath a predictable folder next to the EXE.
            string baseDir = AppContext.BaseDirectory;
            string caseDir = Path.Combine(baseDir, "logs", "Cases");
            Directory.CreateDirectory(caseDir);

            string fileName = string.IsNullOrWhiteSpace(CaseId)
                ? $"Case-{DateTime.Now:yyyyMMdd-HHmmss}.txt"
                : $"{CaseId}.txt";

            string fullPath = Path.Combine(caseDir, fileName);

            var sb = new StringBuilder();

            sb.AppendLine($"Case ID: {CaseId}");
            sb.AppendLine($"Host:    {HostName}");
            sb.AppendLine($"User:    {UserName}");
            sb.AppendLine($"OS:      {OsDescription}");
            sb.AppendLine($"Started: {StartedAt:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"Ended:   {EndedAt:yyyy-MM-dd HH:mm:ss}");

            if (_focusTargets.Count > 0)
                sb.AppendLine($"Focus:   {string.Join(", ", _focusTargets)}");

            sb.AppendLine();
            sb.AppendLine("Timeline");
            sb.AppendLine(new string('=', 80));

            foreach (var ev in _events.OrderBy(e => e.Timestamp))
            {
                sb.AppendLine($"{ev.Timestamp:yyyy-MM-dd HH:mm:ss} [{ev.Tab}] [{ev.Severity}] {ev.Action}");

                if (!string.IsNullOrWhiteSpace(ev.Target))
                    sb.AppendLine($"  Target:  {ev.Target}");

                if (!string.IsNullOrWhiteSpace(ev.Details))
                    sb.AppendLine($"  Details: {ev.Details}");

                sb.AppendLine();
            }

            File.WriteAllText(fullPath, sb.ToString(), Encoding.UTF8);
            return fullPath;
        }
    }
}

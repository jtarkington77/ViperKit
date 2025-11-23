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

    // Multi-target focus list for this case 
    private static readonly List<string> _focusTargets = new();

    // Cleanup queue for items pending remediation
    private static readonly List<CleanupItem> _cleanupQueue = new();

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
            _cleanupQueue.Clear();
            EndedAt = null;

            HostName      = Environment.MachineName;
            UserName      = Environment.UserName;
            OsDescription = GetOsDescription();

            CaseId    = $"{HostName}-{DateTime.Now:yyyyMMdd-HHmmss}";
            StartedAt = DateTime.Now;

            // Initialize the cleanup journal for this case
            CleanupJournal.Initialize(CaseId);

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
    /// instead of overwriting it. This allows us to track multiple targets
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
    // CLEANUP QUEUE API – items pending remediation
    // --------------------------------------------------------------------

    /// <summary>
    /// Add an item to the cleanup queue.
    /// </summary>
    public static void AddToCleanupQueue(CleanupItem item)
    {
        if (item == null)
            return;

        lock (_lock)
        {
            // Check if already queued (by original path)
            if (_cleanupQueue.Exists(c =>
                string.Equals(c.OriginalPath, item.OriginalPath, StringComparison.OrdinalIgnoreCase)))
            {
                return; // Already queued
            }

            _cleanupQueue.Add(item);

            _events.Add(new CaseEvent
            {
                Timestamp = DateTime.Now,
                Tab       = "Cleanup",
                Action    = "Item added to cleanup queue",
                Severity  = item.Severity,
                Target    = item.Name,
                Details   = $"Type: {item.ItemType}; Path: {item.OriginalPath}; From: {item.SourceTab}"
            });
        }
    }

    /// <summary>
    /// Remove an item from the cleanup queue.
    /// </summary>
    public static bool RemoveFromCleanupQueue(string itemId)
    {
        lock (_lock)
        {
            var item = _cleanupQueue.Find(c => c.Id == itemId);
            if (item != null)
            {
                _cleanupQueue.Remove(item);

                _events.Add(new CaseEvent
                {
                    Timestamp = DateTime.Now,
                    Tab       = "Cleanup",
                    Action    = "Item removed from cleanup queue",
                    Severity  = "INFO",
                    Target    = item.Name,
                    Details   = $"Path: {item.OriginalPath}"
                });

                return true;
            }
            return false;
        }
    }

    /// <summary>
    /// Get all items in the cleanup queue.
    /// </summary>
    public static IReadOnlyList<CleanupItem> GetCleanupQueue()
    {
        lock (_lock)
        {
            return _cleanupQueue.ToList();
        }
    }

    /// <summary>
    /// Get cleanup queue items filtered by status.
    /// </summary>
    public static IReadOnlyList<CleanupItem> GetCleanupQueueByStatus(string status)
    {
        lock (_lock)
        {
            return _cleanupQueue.FindAll(c =>
                string.Equals(c.Status, status, StringComparison.OrdinalIgnoreCase));
        }
    }

    /// <summary>
    /// Update the status of a cleanup item.
    /// </summary>
    public static void UpdateCleanupItemStatus(string itemId, string status, string? errorMessage = null)
    {
        lock (_lock)
        {
            var item = _cleanupQueue.Find(c => c.Id == itemId);
            if (item != null)
            {
                item.Status = status;
                if (status == "Completed" || status == "Failed")
                    item.ExecutedAt = DateTime.Now;
                if (!string.IsNullOrEmpty(errorMessage))
                    item.ErrorMessage = errorMessage;

                _events.Add(new CaseEvent
                {
                    Timestamp = DateTime.Now,
                    Tab       = "Cleanup",
                    Action    = $"Cleanup item {status.ToLower()}",
                    Severity  = status == "Failed" ? "HIGH" : "INFO",
                    Target    = item.Name,
                    Details   = errorMessage ?? $"Action: {item.Action}"
                });
            }
        }
    }

    /// <summary>
    /// Check if an item is already in the cleanup queue.
    /// </summary>
    public static bool IsInCleanupQueue(string originalPath)
    {
        lock (_lock)
        {
            return _cleanupQueue.Exists(c =>
                string.Equals(c.OriginalPath, originalPath, StringComparison.OrdinalIgnoreCase));
        }
    }

    /// <summary>
    /// Get cleanup queue statistics.
    /// </summary>
    public static (int total, int pending, int completed, int failed) GetCleanupStats()
    {
        lock (_lock)
        {
            int total = _cleanupQueue.Count;
            int pending = _cleanupQueue.Count(c => c.Status == "Pending");
            int completed = _cleanupQueue.Count(c => c.Status == "Completed");
            int failed = _cleanupQueue.Count(c => c.Status == "Failed");
            return (total, pending, completed, failed);
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

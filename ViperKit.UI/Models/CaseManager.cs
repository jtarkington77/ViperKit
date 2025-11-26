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

    // Baseline data for this case
    private static BaselineData? _baseline;

    public static string CaseId       { get; private set; } = string.Empty;
    public static string CaseName     { get; private set; } = string.Empty;
    public static DateTime StartedAt  { get; private set; }
    public static DateTime? EndedAt   { get; private set; }

    public static string HostName     { get; private set; } = string.Empty;
    public static string UserName     { get; private set; } = string.Empty;
    public static string OsDescription{ get; private set; } = string.Empty;

    public static bool HasBaseline => _baseline != null;
    public static DateTime? BaselineCapturedAt => _baseline?.CapturedAt;

    /// <summary>
    /// Start a new case with optional custom name.
    /// </summary>
    public static void StartNewCase(string? customName = null)
    {
        lock (_lock)
        {
            _events.Clear();
            _focusTargets.Clear();
            _cleanupQueue.Clear();
            _baseline = null;
            EndedAt = null;

            HostName      = Environment.MachineName;
            UserName      = Environment.UserName;
            OsDescription = GetOsDescription();

            CaseId    = $"{HostName}-{DateTime.Now:yyyyMMdd-HHmmss}";
            CaseName  = customName ?? string.Empty;
            StartedAt = DateTime.Now;

            // Initialize the cleanup journal for this case
            CleanupJournal.Initialize(CaseId);

            // Initialize the harden journal for this case
            HardenJournal.Initialize(CaseId);

            _events.Add(new CaseEvent
            {
                Timestamp = StartedAt,
                Tab       = "Case",
                Action    = "Case started",
                Severity  = "INFO",
                Target    = HostName,
                Details   = string.IsNullOrEmpty(CaseName)
                    ? $"User: {UserName}; OS: {OsDescription}"
                    : $"Name: {CaseName}; User: {UserName}; OS: {OsDescription}"
            });

            // Auto-save to disk
            SaveCurrentCase();
        }
    }

    /// <summary>
    /// Load an existing case from disk.
    /// </summary>
    public static bool LoadCase(string caseId)
    {
        var caseData = CaseStorage.LoadCase(caseId);
        if (caseData == null)
            return false;

        lock (_lock)
        {
            _events.Clear();
            _focusTargets.Clear();
            _cleanupQueue.Clear();

            CaseId        = caseData.CaseId;
            CaseName      = caseData.CaseName;
            HostName      = caseData.HostName;
            UserName      = caseData.UserName;
            OsDescription = caseData.OsDescription;
            StartedAt     = caseData.CreatedAt;
            EndedAt       = caseData.ClosedAt;
            _baseline     = caseData.Baseline;

            if (caseData.FocusTargets != null)
                _focusTargets.AddRange(caseData.FocusTargets);

            if (caseData.Events != null)
                _events.AddRange(caseData.Events);

            if (caseData.CleanupQueue != null)
                _cleanupQueue.AddRange(caseData.CleanupQueue);

            // Initialize journals for this case
            CleanupJournal.Initialize(CaseId);
            HardenJournal.Initialize(CaseId);

            _events.Add(new CaseEvent
            {
                Timestamp = DateTime.Now,
                Tab       = "Case",
                Action    = "Case loaded",
                Severity  = "INFO",
                Target    = CaseId,
                Details   = $"Loaded from disk with {_events.Count - 1} previous events"
            });
        }
        return true;
    }

    /// <summary>
    /// Save the current case to disk.
    /// </summary>
    public static void SaveCurrentCase()
    {
        // Don't save if no case is active
        if (string.IsNullOrEmpty(CaseId))
            return;

        lock (_lock)
        {
            var caseData = new CaseData
            {
                CaseId        = CaseId,
                CaseName      = CaseName,
                HostName      = HostName,
                UserName      = UserName,
                OsDescription = OsDescription,
                CreatedAt     = StartedAt,
                ClosedAt      = EndedAt,
                Status        = EndedAt.HasValue ? "Closed" : "Active",
                FocusTargets  = _focusTargets.ToList(),
                Events        = _events.ToList(),
                CleanupQueue  = _cleanupQueue.ToList(),
                Baseline      = _baseline,
                BaselineCapturedAt = _baseline?.CapturedAt
            };

            CaseStorage.SaveCase(caseData);
        }
    }

    /// <summary>
    /// Capture baseline from current Persist scan results.
    /// </summary>
    public static void CaptureBaseline(List<PersistItem> persistItems)
    {
        lock (_lock)
        {
            _baseline = new BaselineData
            {
                CapturedAt = DateTime.Now,
                HostName = HostName,
                CapturedBy = UserName,
                PersistEntries = new List<BaselinePersistEntry>(),
                HardeningApplied = new List<BaselineHardenEntry>(),
                ConfigEntries = new List<BaselineConfigEntry>()
            };

            // Convert persist items to baseline entries
            foreach (var item in persistItems)
            {
                _baseline.PersistEntries.Add(new BaselinePersistEntry
                {
                    Name = item.Name,
                    Path = item.Path,
                    RegistryPath = item.RegistryPath,
                    LocationType = item.LocationType,
                    Source = item.Source,
                    Risk = item.Risk,
                    Hash = item.FileHash ?? string.Empty,
                    FileModified = item.FileModified
                });
            }

            // Get hardening actions from journal
            var hardenEntries = HardenJournal.GetEntries();
            foreach (var entry in hardenEntries)
            {
                if (!entry.IsRolledBack)
                {
                    _baseline.HardeningApplied.Add(new BaselineHardenEntry
                    {
                        ActionId = entry.ActionId,
                        ActionName = entry.ActionName,
                        Category = entry.Category,
                        PreviousState = entry.PreviousState,
                        NewState = entry.NewState,
                        AppliedAt = entry.Timestamp
                    });
                }
            }

            _events.Add(new CaseEvent
            {
                Timestamp = DateTime.Now,
                Tab = "Baseline",
                Action = "Baseline captured",
                Severity = "INFO",
                Target = CaseId,
                Details = $"Captured {_baseline.PersistEntries.Count} persistence entries, {_baseline.HardeningApplied.Count} hardening actions"
            });

            SaveCurrentCase();
        }
    }

    /// <summary>
    /// Get the current baseline data.
    /// </summary>
    public static BaselineData? GetBaseline()
    {
        lock (_lock)
        {
            return _baseline;
        }
    }

    /// <summary>
    /// Compare current persist items against baseline.
    /// Returns items that are new (not in baseline).
    /// </summary>
    public static List<PersistItem> CompareToBaseline(List<PersistItem> currentItems)
    {
        var newItems = new List<PersistItem>();

        lock (_lock)
        {
            if (_baseline == null)
                return currentItems; // No baseline = all items are "new"

            foreach (var item in currentItems)
            {
                // Check if this item exists in baseline
                bool existsInBaseline = _baseline.PersistEntries.Exists(b =>
                    b.Path == item.Path &&
                    b.RegistryPath == item.RegistryPath &&
                    b.Name == item.Name);

                if (!existsInBaseline)
                {
                    item.IsNewSinceBaseline = true;
                    newItems.Add(item);
                }
            }
        }

        return newItems;
    }

    /// <summary>
    /// Get display name for the case.
    /// </summary>
    public static string GetDisplayName()
    {
        return string.IsNullOrEmpty(CaseName) ? CaseId : $"{CaseName} ({CaseId})";
    }

    /// <summary>
    /// Generate a structured report for PDF export.
    /// </summary>
    public static CaseReport GenerateReport()
    {
        lock (_lock)
        {
            var report = new CaseReport
            {
                CaseId = CaseId,
                CaseName = CaseName,
                HostName = HostName,
                UserName = UserName,
                OsDescription = OsDescription,
                CaseStarted = StartedAt,
                ReportGenerated = DateTime.Now,
                InvestigatorName = Environment.UserName,
                FocusTargets = _focusTargets.ToList(),
                Findings = new FindingsSummary()
            };

            // Extract scan summaries from events
            var scanEvents = _events.Where(e =>
                e.Action.Contains("scan", StringComparison.OrdinalIgnoreCase) ||
                e.Action.Contains("completed", StringComparison.OrdinalIgnoreCase))
                .ToList();

            foreach (var evt in scanEvents)
            {
                var scan = new ScanSummary
                {
                    ScanType = evt.Tab,
                    Timestamp = evt.Timestamp,
                    Status = "Completed"
                };

                // Parse details for counts if available
                if (evt.Details.Contains("entries"))
                {
                    var parts = evt.Details.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (var part in parts)
                    {
                        if (int.TryParse(new string(part.Where(char.IsDigit).ToArray()), out int num))
                        {
                            if (part.Contains("CHECK", StringComparison.OrdinalIgnoreCase))
                                scan.HighRiskFindings = num;
                            else if (part.Contains("NOTE", StringComparison.OrdinalIgnoreCase))
                                scan.MediumRiskFindings = num;
                            else if (part.Contains("entries") || part.Contains("total"))
                                scan.TotalFindings = num;
                        }
                    }
                }

                report.ScansPerformed.Add(scan);
            }

            // Extract remediation actions from events
            var actionEvents = _events.Where(e =>
                e.Action.Contains("cleanup", StringComparison.OrdinalIgnoreCase) ||
                e.Action.Contains("removed", StringComparison.OrdinalIgnoreCase) ||
                e.Action.Contains("deleted", StringComparison.OrdinalIgnoreCase))
                .ToList();

            foreach (var evt in actionEvents)
            {
                report.ActionsTaken.Add(new ActionSummary
                {
                    ActionType = evt.Action,
                    Timestamp = evt.Timestamp,
                    Target = evt.Target,
                    Result = evt.Severity == "ERROR" ? "Failed" : "Success",
                    Details = evt.Details
                });
            }

            // Get hardening actions from journal
            var hardenEntries = HardenJournal.GetEntries();
            foreach (var entry in hardenEntries)
            {
                if (!entry.IsRolledBack)
                {
                    report.HardeningActions.Add(new HardeningApplied
                    {
                        ActionName = entry.ActionName,
                        Category = entry.Category,
                        AppliedAt = entry.Timestamp,
                        PreviousState = entry.PreviousState,
                        NewState = entry.NewState
                    });
                }
            }

            // Add baseline info if exists
            if (_baseline != null)
            {
                report.Baseline = new BaselineInfo
                {
                    CapturedAt = _baseline.CapturedAt,
                    PersistenceEntriesCaptured = _baseline.PersistEntries?.Count ?? 0,
                    HardeningActionsCaptured = _baseline.HardeningApplied?.Count ?? 0
                };
            }

            // Extract key timeline events (filter to important ones)
            var keyEvents = _events.Where(e =>
                e.Severity == "CRITICAL" ||
                e.Severity == "WARNING" ||
                e.Action.Contains("scan", StringComparison.OrdinalIgnoreCase) ||
                e.Action.Contains("cleanup", StringComparison.OrdinalIgnoreCase) ||
                e.Action.Contains("baseline", StringComparison.OrdinalIgnoreCase) ||
                e.Action.Contains("hardening", StringComparison.OrdinalIgnoreCase))
                .Take(20)
                .ToList();

            foreach (var evt in keyEvents)
            {
                report.KeyEvents.Add(new TimelineEvent
                {
                    Timestamp = evt.Timestamp,
                    EventType = evt.Tab,
                    Description = $"[{evt.Tab}] {evt.Action} - {evt.Target}",
                    Severity = evt.Severity
                });
            }

            return report;
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

        // Auto-save after each event (outside lock to avoid deadlock)
        SaveCurrentCase();
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

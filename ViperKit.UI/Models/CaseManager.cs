using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace ViperKit.UI.Models
{
    public static class CaseManager
    {
        private static readonly object _lock = new();

        // Case log
        private static readonly List<CaseEvent> _events = new();

        // Case focus – we store a list, not just a single string
        // Example: ["screenconnect", "logmein", "powershell_ise"]
        private static readonly List<string> _focusTargets = new();

        public static string CaseId { get; private set; } = string.Empty;
        public static DateTime StartedAt { get; private set; }
        public static DateTime? EndedAt { get; private set; }

        public static string HostName { get; private set; } = string.Empty;
        public static string UserName { get; private set; } = string.Empty;
        public static string OsDescription { get; private set; } = string.Empty;

        /// <summary>
        /// Returns the "primary" focus token (the last one that was set/added).
        /// This keeps older code that uses CaseManager.FocusTarget working.
        /// </summary>
        public static string FocusTarget
        {
            get
            {
                lock (_lock)
                {
                    if (_focusTargets.Count == 0)
                        return string.Empty;

                    return _focusTargets[^1];
                }
            }
        }

        /// <summary>
        /// Snapshot of all focus tokens currently in the case.
        /// </summary>
        public static IReadOnlyList<string> GetFocusTargets()
        {
            lock (_lock)
            {
                // Return a copy so callers can't mutate our internal list
                return _focusTargets.ToList();
            }
        }

        /// <summary>
        /// Backwards-compat shim for any code that still calls GetFocusTarget().
        /// </summary>
        public static string GetFocusTarget()
        {
            return FocusTarget;
        }

        public static void StartNewCase()
        {
            lock (_lock)
            {
                _events.Clear();
                _focusTargets.Clear();

                StartedAt = DateTime.Now;
                EndedAt   = null;

                HostName      = Environment.MachineName;
                UserName      = Environment.UserName;
                OsDescription = Environment.OSVersion.VersionString;

                CaseId = $"{HostName}-{StartedAt:yyyyMMddHHmmss}";

                AddEvent("System", "Case started", "INFO", HostName, "New case initialised.");
            }
        }

        public static void AddEvent(
            string tab,
            string action,
            string severity = "INFO",
            string? target  = null,
            string? details = null)
        {
            lock (_lock)
            {
                _events.Add(new CaseEvent
                {
                    Timestamp = DateTime.Now,
                    Tab       = tab,
                    Action    = action,
                    Severity  = severity,
                    Target    = target ?? string.Empty,
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

        public static string ExportToFile()
        {
            lock (_lock)
            {
                EndedAt ??= DateTime.Now;

                string baseDir = AppContext.BaseDirectory;
                string caseDir = Path.Combine(baseDir, "logs", "Cases");
                Directory.CreateDirectory(caseDir);

                string fileName = $"{CaseId}.txt";
                string fullPath = Path.Combine(caseDir, fileName);

                var sb = new StringBuilder();

                sb.AppendLine($"Case ID: {CaseId}");
                sb.AppendLine($"Host:    {HostName}");
                sb.AppendLine($"User:    {UserName}");
                sb.AppendLine($"OS:      {OsDescription}");
                sb.AppendLine($"Started: {StartedAt}");
                sb.AppendLine($"Ended:   {EndedAt}");

                // New: dump case focus list into the header
                if (_focusTargets.Count > 0)
                {
                    sb.AppendLine($"Focus:   {string.Join(", ", _focusTargets)}");
                }
                else
                {
                    sb.AppendLine("Focus:   (none)");
                }

                sb.AppendLine();
                sb.AppendLine($"Events:  {_events.Count}");
                sb.AppendLine(new string('-', 60));

                foreach (var ev in _events)
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

        /// <summary>
        /// Replace the current focus list with a single value.
        /// This is what "Set focus from row" should call.
        /// </summary>
        public static void SetFocusTarget(string? value, string sourceTab = "System")
        {
            lock (_lock)
            {
                _focusTargets.Clear();

                var clean = (value ?? string.Empty).Trim();

                if (!string.IsNullOrEmpty(clean))
                {
                    _focusTargets.Add(clean);
                }

                AddEvent(
                    tab: sourceTab,
                    action: "Focus target updated",
                    severity: "INFO",
                    target: string.IsNullOrEmpty(clean) ? "(none)" : clean,
                    details: string.IsNullOrEmpty(clean)
                        ? "Case focus cleared."
                        : "Case focus set for cross-tab filtering (Hunt, Persist, Sweep).");
            }
        }

        /// <summary>
        /// Add a new focus token without wiping the existing ones.
        /// Example: first "screenconnect", later "logmein".
        /// (We’ll call this from Sweep/Hunt when you want multi-focus.)
        /// </summary>
        public static void AddFocusTarget(string? value, string sourceTab = "System")
        {
            lock (_lock)
            {
                var clean = (value ?? string.Empty).Trim();
                if (string.IsNullOrEmpty(clean))
                    return;

                // Avoid dumb duplicates
                if (!_focusTargets.Contains(clean, StringComparer.OrdinalIgnoreCase))
                {
                    _focusTargets.Add(clean);

                    AddEvent(
                        tab: sourceTab,
                        action: "Focus target appended",
                        severity: "INFO",
                        target: clean,
                        details: "Additional case focus added (multi-target triage).");
                }
            }
        }

        /// <summary>
        /// Clear all focus targets but keep the rest of the case log.
        /// </summary>
        public static void ClearFocusTargets(string sourceTab = "System")
        {
            lock (_lock)
            {
                _focusTargets.Clear();

                AddEvent(
                    tab: sourceTab,
                    action: "Focus targets cleared",
                    severity: "INFO",
                    target: "(none)",
                    details: "All case focus entries cleared for this case.");
            }
        }
    }
}

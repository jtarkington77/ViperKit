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
        private static readonly List<CaseEvent> _events = new();

        public static string CaseId { get; private set; } = string.Empty;
        public static DateTime StartedAt { get; private set; }
        public static DateTime? EndedAt { get; private set; }

        public static string HostName { get; private set; } = string.Empty;
        public static string UserName { get; private set; } = string.Empty;
        public static string OsDescription { get; private set; } = string.Empty;

        public static void StartNewCase()
        {
            lock (_lock)
            {
                _events.Clear();
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
    }
}

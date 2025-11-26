// ViperKit.UI - Models\CaseReport.cs
using System;
using System.Collections.Generic;

namespace ViperKit.UI.Models
{
    /// <summary>
    /// Structured data for professional case report generation.
    /// </summary>
    public class CaseReport
    {
        // Case metadata
        public string CaseId { get; set; } = string.Empty;
        public string CaseName { get; set; } = string.Empty;
        public string HostName { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public string OsDescription { get; set; } = string.Empty;
        public DateTime CaseStarted { get; set; }
        public DateTime ReportGenerated { get; set; }
        public string InvestigatorName { get; set; } = string.Empty;

        // Focus targets
        public List<string> FocusTargets { get; set; } = new();

        // Scans performed
        public List<ScanSummary> ScansPerformed { get; set; } = new();

        // Findings summary
        public FindingsSummary Findings { get; set; } = new();

        // Actions taken
        public List<ActionSummary> ActionsTaken { get; set; } = new();

        // Hardening applied
        public List<HardeningApplied> HardeningActions { get; set; } = new();

        // Baseline info
        public BaselineInfo? Baseline { get; set; }

        // Key timeline events (not all events, just important ones)
        public List<TimelineEvent> KeyEvents { get; set; } = new();
    }

    public class ScanSummary
    {
        public string ScanType { get; set; } = string.Empty; // "Persistence Scan", "Sweep Scan", etc.
        public DateTime Timestamp { get; set; }
        public int TotalFindings { get; set; }
        public int HighRiskFindings { get; set; }
        public int MediumRiskFindings { get; set; }
        public int LowRiskFindings { get; set; }
        public string Status { get; set; } = string.Empty; // "Completed", "Failed", etc.
    }

    public class FindingsSummary
    {
        // Persistence findings
        public int PersistenceTotal { get; set; }
        public int PersistenceCheck { get; set; }
        public int PersistenceNote { get; set; }
        public int PersistenceOk { get; set; }
        public List<string> TopPersistenceFindings { get; set; } = new(); // Top 10

        // Sweep findings
        public int SweepTotal { get; set; }
        public int SweepSuspicious { get; set; }
        public List<string> TopSweepFindings { get; set; } = new(); // Top 10

        // PowerShell history
        public int PowerShellCommandsAnalyzed { get; set; }
        public int PowerShellHighRisk { get; set; }
        public List<string> TopPowerShellCommands { get; set; } = new(); // Top 5

        // Hunt findings
        public int HuntMatches { get; set; }
        public List<string> HuntTargets { get; set; } = new();
    }

    public class ActionSummary
    {
        public string ActionType { get; set; } = string.Empty; // "Cleanup", "Added to Queue", etc.
        public DateTime Timestamp { get; set; }
        public string Target { get; set; } = string.Empty;
        public string Result { get; set; } = string.Empty; // "Success", "Failed", etc.
        public string Details { get; set; } = string.Empty;
    }

    public class HardeningApplied
    {
        public string ActionName { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public DateTime AppliedAt { get; set; }
        public string PreviousState { get; set; } = string.Empty;
        public string NewState { get; set; } = string.Empty;
    }

    public class BaselineInfo
    {
        public DateTime CapturedAt { get; set; }
        public int PersistenceEntriesCaptured { get; set; }
        public int HardeningActionsCaptured { get; set; }
    }

    public class TimelineEvent
    {
        public DateTime Timestamp { get; set; }
        public string EventType { get; set; } = string.Empty; // "Scan", "Action", "Finding"
        public string Description { get; set; } = string.Empty;
        public string Severity { get; set; } = string.Empty; // "INFO", "WARNING", "CRITICAL"
    }
}

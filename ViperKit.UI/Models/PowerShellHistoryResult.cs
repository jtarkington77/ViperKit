// ViperKit.UI - Models\PowerShellHistoryResult.cs
using System.Collections.Generic;

namespace ViperKit.UI.Models
{
    /// <summary>
    /// Container for PowerShell history scan results with summary statistics.
    /// </summary>
    public class PowerShellHistoryResult
    {
        /// <summary>
        /// All history entries found during the scan.
        /// </summary>
        public List<PowerShellHistoryEntry> Entries { get; set; } = new();

        /// <summary>
        /// Total number of commands scanned.
        /// </summary>
        public int TotalCommands { get; set; }

        /// <summary>
        /// Number of HIGH severity commands found.
        /// </summary>
        public int HighRiskCount { get; set; }

        /// <summary>
        /// Number of MEDIUM severity commands found.
        /// </summary>
        public int MediumRiskCount { get; set; }

        /// <summary>
        /// Number of LOW severity commands found.
        /// </summary>
        public int LowRiskCount { get; set; }

        /// <summary>
        /// Number of user profiles that were scanned.
        /// </summary>
        public int UsersScanned { get; set; }

        /// <summary>
        /// Number of commands from PowerShell 5.1.
        /// </summary>
        public int PS51Count { get; set; }

        /// <summary>
        /// Number of commands from PowerShell 7.
        /// </summary>
        public int PS7Count { get; set; }

        /// <summary>
        /// List of history files that were found and scanned.
        /// </summary>
        public List<string> HistoryFilesFound { get; set; } = new();

        /// <summary>
        /// List of history files that could not be accessed (permission denied, etc.)
        /// </summary>
        public List<string> HistoryFilesSkipped { get; set; } = new();

        /// <summary>
        /// Any errors encountered during scanning.
        /// </summary>
        public List<string> Errors { get; set; } = new();

        /// <summary>
        /// Whether the scan completed successfully.
        /// </summary>
        public bool Success { get; set; } = true;

        /// <summary>
        /// Summary message for display.
        /// </summary>
        public string SummaryMessage => Success
            ? $"Found {TotalCommands} commands across {UsersScanned} user(s) in {HistoryFilesFound.Count} history file(s)"
            : $"Scan completed with {Errors.Count} error(s)";
    }
}

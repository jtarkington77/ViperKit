// ViperKit.UI - Models\PowerShellHistoryEntry.cs
using System;
using System.Collections.Generic;

namespace ViperKit.UI.Models
{
    /// <summary>
    /// Represents a single PowerShell command from history with risk assessment.
    /// </summary>
    public class PowerShellHistoryEntry
    {
        /// <summary>
        /// Unique identifier for this entry.
        /// </summary>
        public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];

        /// <summary>
        /// The actual PowerShell command that was executed.
        /// </summary>
        public string Command { get; set; } = string.Empty;

        /// <summary>
        /// Username of the profile this history came from.
        /// </summary>
        public string UserProfile { get; set; } = string.Empty;

        /// <summary>
        /// PowerShell version: "5.1" (Windows PowerShell) or "7" (PowerShell Core).
        /// </summary>
        public string PowerShellVersion { get; set; } = string.Empty;

        /// <summary>
        /// Full path to the history file this command was read from.
        /// </summary>
        public string HistoryFilePath { get; set; } = string.Empty;

        /// <summary>
        /// Line number in the history file (1-based). Higher = more recent.
        /// </summary>
        public int LineNumber { get; set; }

        /// <summary>
        /// Total number of lines in the history file (for context).
        /// </summary>
        public int TotalLinesInFile { get; set; }

        /// <summary>
        /// When the history file was last modified (approximates last PS session).
        /// </summary>
        public DateTime? HistoryFileModified { get; set; }

        /// <summary>
        /// When the history file was created.
        /// </summary>
        public DateTime? HistoryFileCreated { get; set; }

        /// <summary>
        /// Risk severity: HIGH, MEDIUM, LOW.
        /// </summary>
        public string Severity { get; set; } = "LOW";

        /// <summary>
        /// Human-readable reason for the risk classification.
        /// </summary>
        public string RiskReason { get; set; } = string.Empty;

        /// <summary>
        /// List of specific patterns that triggered the risk classification.
        /// </summary>
        public List<string> RiskIndicators { get; set; } = new();

        /// <summary>
        /// Whether this command contains Base64 encoded content.
        /// </summary>
        public bool IsEncoded { get; set; }

        /// <summary>
        /// The decoded command if IsEncoded is true.
        /// </summary>
        public string DecodedCommand { get; set; } = string.Empty;

        /// <summary>
        /// Whether decoding was attempted but failed.
        /// </summary>
        public bool DecodeFailed { get; set; }

        // ---- UI Helper Properties ----

        /// <summary>
        /// Background color for the severity badge.
        /// </summary>
        public string SeverityBackground => Severity?.ToUpperInvariant() switch
        {
            "HIGH" => "#5A1E2C",    // Dark red
            "MEDIUM" => "#4A3D16",  // Amber
            "LOW" => "#1E3D2A",     // Green
            _ => "#333"
        };

        /// <summary>
        /// Foreground color for the severity badge text.
        /// </summary>
        public string SeverityForeground => Severity?.ToUpperInvariant() switch
        {
            "HIGH" => "#FF9999",
            "MEDIUM" => "#FFCC66",
            "LOW" => "#99CC99",
            _ => "#CCCCCC"
        };

        /// <summary>
        /// Icon indicator for severity.
        /// </summary>
        public string SeverityIcon => Severity?.ToUpperInvariant() switch
        {
            "HIGH" => "!!",
            "MEDIUM" => "!",
            "LOW" => "-",
            _ => "?"
        };

        /// <summary>
        /// Display label combining version and user.
        /// </summary>
        public string SourceLabel => $"PS {PowerShellVersion} | {UserProfile}";

        /// <summary>
        /// Display label showing file modification date.
        /// </summary>
        public string FileModifiedLabel => HistoryFileModified.HasValue
            ? $"File modified: {HistoryFileModified.Value:yyyy-MM-dd HH:mm}"
            : "File date: unknown";

        /// <summary>
        /// Display label showing position in file (e.g., "Line 8401 of 8500").
        /// </summary>
        public string PositionLabel => TotalLinesInFile > 0
            ? $"Line {LineNumber} of {TotalLinesInFile}"
            : $"Line {LineNumber}";

        /// <summary>
        /// How recent this command is (percentage through file, 100% = most recent).
        /// </summary>
        public int RecencyPercent => TotalLinesInFile > 0
            ? (int)((double)LineNumber / TotalLinesInFile * 100)
            : 0;

        /// <summary>
        /// Display label for recency (e.g., "Recent" for last 10%, "Old" for first 10%).
        /// </summary>
        public string RecencyLabel => RecencyPercent switch
        {
            >= 90 => "Very Recent",
            >= 70 => "Recent",
            >= 30 => "Middle",
            >= 10 => "Older",
            _ => "Very Old"
        };

        /// <summary>
        /// Combined info line for display.
        /// </summary>
        public string InfoLine => $"{SourceLabel} | {PositionLabel} | {FileModifiedLabel}";

        /// <summary>
        /// Truncated command for list display (max 200 chars).
        /// </summary>
        public string CommandPreview => Command.Length > 200
            ? Command[..197] + "..."
            : Command;

        /// <summary>
        /// Whether this entry has a decoded command to show.
        /// </summary>
        public bool HasDecodedCommand => IsEncoded && !string.IsNullOrEmpty(DecodedCommand);

        /// <summary>
        /// Border color based on severity for list items.
        /// </summary>
        public string BorderBrush => Severity?.ToUpperInvariant() switch
        {
            "HIGH" => "#FF6B6B",
            "MEDIUM" => "#FFB347",
            _ => "#333"
        };

        /// <summary>
        /// Border thickness based on severity.
        /// </summary>
        public string BorderThickness => Severity?.ToUpperInvariant() switch
        {
            "HIGH" => "2",
            "MEDIUM" => "1",
            _ => "1"
        };
    }
}

// ViperKit.UI - Models\CleanupItem.cs
using System;

namespace ViperKit.UI.Models
{
    /// <summary>
    /// Represents an item queued for cleanup (quarantine, disable, delete).
    /// </summary>
    public class CleanupItem
    {
        /// <summary>
        /// Unique ID for this cleanup item (for undo tracking).
        /// </summary>
        public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];

        /// <summary>
        /// Type of item: File, Service, ScheduledTask, RegistryKey, StartupItem
        /// </summary>
        public string ItemType { get; set; } = string.Empty;

        /// <summary>
        /// Display name for the item (filename, service name, task name, etc.)
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Original location (file path, registry path, service key, etc.)
        /// </summary>
        public string OriginalPath { get; set; } = string.Empty;

        /// <summary>
        /// Where the item was quarantined to (for files) or backup stored (for registry).
        /// </summary>
        public string QuarantinePath { get; set; } = string.Empty;

        /// <summary>
        /// Source tab that suggested this item (Persist, Sweep).
        /// </summary>
        public string SourceTab { get; set; } = string.Empty;

        /// <summary>
        /// Original severity from source tab (HIGH, MEDIUM, LOW).
        /// </summary>
        public string Severity { get; set; } = string.Empty;

        /// <summary>
        /// Reason why this item was flagged.
        /// </summary>
        public string Reason { get; set; } = string.Empty;

        /// <summary>
        /// Cleanup action: Quarantine, Disable, Delete, BackupAndDelete
        /// </summary>
        public string Action { get; set; } = string.Empty;

        /// <summary>
        /// Current status: Pending, InProgress, Completed, Failed, Undone
        /// </summary>
        public string Status { get; set; } = "Pending";

        /// <summary>
        /// When this item was added to the cleanup queue.
        /// </summary>
        public DateTime AddedAt { get; set; } = DateTime.Now;

        /// <summary>
        /// When the cleanup action was executed.
        /// </summary>
        public DateTime? ExecutedAt { get; set; }

        /// <summary>
        /// Error message if the action failed.
        /// </summary>
        public string ErrorMessage { get; set; } = string.Empty;

        /// <summary>
        /// Whether this item has been selected in the UI for batch operations.
        /// </summary>
        public bool IsSelected { get; set; }

        // ---- UI helper properties ----

        /// <summary>
        /// Background color for the severity badge.
        /// </summary>
        public string SeverityBackground
        {
            get
            {
                return Severity?.ToUpperInvariant() switch
                {
                    "HIGH" => "#5A1E2C",   // dark red
                    "MEDIUM" => "#4A3D16", // amber
                    "LOW" => "#1E3D2A",    // green
                    _ => "#444"
                };
            }
        }

        /// <summary>
        /// Foreground color for the severity badge text.
        /// </summary>
        public string SeverityForeground
        {
            get
            {
                return Severity?.ToUpperInvariant() switch
                {
                    "HIGH" => "#FF9999",
                    "MEDIUM" => "#FFCC66",
                    "LOW" => "#99CC99",
                    _ => "#CCCCCC"
                };
            }
        }

        /// <summary>
        /// Background color for the status badge.
        /// </summary>
        public string StatusBackground
        {
            get
            {
                return Status?.ToLowerInvariant() switch
                {
                    "pending" => "#333",
                    "inprogress" => "#4A3D16",
                    "completed" => "#1E3D2A",
                    "failed" => "#5A1E2C",
                    "undone" => "#2A2A4A",
                    _ => "#333"
                };
            }
        }

        /// <summary>
        /// Foreground color for the status badge text.
        /// </summary>
        public string StatusForeground
        {
            get
            {
                return Status?.ToLowerInvariant() switch
                {
                    "pending" => "#AAAAAA",
                    "inprogress" => "#FFCC66",
                    "completed" => "#99CC99",
                    "failed" => "#FF9999",
                    "undone" => "#9999FF",
                    _ => "#CCCCCC"
                };
            }
        }

        /// <summary>
        /// Icon/glyph for the item type.
        /// </summary>
        public string ItemTypeIcon
        {
            get
            {
                return ItemType?.ToLowerInvariant() switch
                {
                    "file" => "📄",
                    "service" => "⚙️",
                    "scheduledtask" => "📅",
                    "registrykey" => "🔑",
                    "startupitem" => "🚀",
                    _ => "❓"
                };
            }
        }

        /// <summary>
        /// Whether undo is available for this item.
        /// </summary>
        public bool CanUndo => Status == "Completed" && !string.IsNullOrEmpty(QuarantinePath);
    }
}

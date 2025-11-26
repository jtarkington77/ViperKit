// ViperKit.UI - Models\DemoArtifact.cs
using System;

namespace ViperKit.UI.Models
{
    /// <summary>
    /// Represents a demo artifact created for the guided walkthrough.
    /// </summary>
    public class DemoArtifact
    {
        /// <summary>
        /// Unique identifier for this artifact.
        /// </summary>
        public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];

        /// <summary>
        /// Type of artifact: File, Registry, ScheduledTask, Script
        /// </summary>
        public string ArtifactType { get; set; } = string.Empty;

        /// <summary>
        /// Display name for the artifact.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Full path where the artifact is/will be created.
        /// </summary>
        public string Path { get; set; } = string.Empty;

        /// <summary>
        /// Human-readable description of what this artifact represents.
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// What this artifact simulates in a real attack.
        /// </summary>
        public string SimulatesAttack { get; set; } = string.Empty;

        /// <summary>
        /// Whether this artifact has been created.
        /// </summary>
        public bool IsCreated { get; set; }

        /// <summary>
        /// Whether this artifact has been cleaned up.
        /// </summary>
        public bool IsCleanedUp { get; set; }

        /// <summary>
        /// When the artifact was created.
        /// </summary>
        public DateTime? CreatedAt { get; set; }

        /// <summary>
        /// When the artifact was cleaned up.
        /// </summary>
        public DateTime? CleanedUpAt { get; set; }

        /// <summary>
        /// Any error message if creation/cleanup failed.
        /// </summary>
        public string ErrorMessage { get; set; } = string.Empty;

        // UI Helpers
        public string StatusIcon => IsCleanedUp ? "✓" : (IsCreated ? "●" : "○");

        public string StatusText => IsCleanedUp ? "Cleaned up" : (IsCreated ? "Created" : "Pending");

        public string TypeIcon => ArtifactType switch
        {
            "File" => "📄",
            "Script" => "📜",
            "Registry" => "🔑",
            "ScheduledTask" => "📅",
            _ => "❓"
        };
    }
}

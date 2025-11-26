// ViperKit.UI - Models\SweepEntry.cs
using System;

namespace ViperKit.UI.Models
{
    public class SweepEntry
    {
        // File / Service / Driver / etc.
        public string Category { get; set; } = string.Empty;

        // LOW / MEDIUM / HIGH
        public string Severity { get; set; } = "LOW";

        // Full path to the thing (file path, service exe path, etc.)
        public string Path { get; set; } = string.Empty;

        // Short name: filename, service name, driver name
        public string Name { get; set; } = string.Empty;

        // Source label: "Desktop (Jeremy)", "Service (deep)", etc.
        public string Source { get; set; } = string.Empty;

        // Why we care: "user-writable location, script file, modified <24h"
        public string Reason { get; set; } = string.Empty;

        // For files we know the last write time
        public DateTime? Modified { get; set; }

        // ---- Focus and clustering properties ----

        // True when this entry matches a focus target term
        public bool IsFocusHit { get; set; }

        // True when this entry was created/modified within the clustering window of a focus target
        public bool IsTimeCluster { get; set; }

        // True when this entry is in the same folder tree as a focus target
        public bool IsFolderCluster { get; set; }

        // Which focus target this clusters with (if any)
        public string ClusterTarget { get; set; } = string.Empty;

        // ---- UI helper properties ----

        // Background color for the severity badge
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

        // Foreground color for the severity badge text
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

        // Border color - pink if focus/cluster hit, otherwise gray
        public string ClusterBorderBrush
        {
            get
            {
                if (IsFocusHit) return "#FF6BD5";      // pink for direct focus match
                if (IsTimeCluster) return "#FFB347";   // orange for time cluster
                if (IsFolderCluster) return "#47B3FF"; // blue for folder cluster
                return "#333";
            }
        }

        // Border thickness - thicker if focus/cluster hit
        public string ClusterBorderThickness => (IsFocusHit || IsTimeCluster || IsFolderCluster) ? "2" : "1";

        // Cluster indicator text for the UI
        public string ClusterIndicator
        {
            get
            {
                if (IsFocusHit) return "FOCUS MATCH";
                if (IsTimeCluster) return "TIME CLUSTER";
                if (IsFolderCluster) return "FOLDER CLUSTER";
                return string.Empty;
            }
        }

        // Whether to show the cluster indicator
        public bool HasClusterIndicator => IsFocusHit || IsTimeCluster || IsFolderCluster;
    }
}

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
    }
}

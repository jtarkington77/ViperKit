using System;
using System.Text;

namespace ViperKit.UI.Models
{
    public sealed class PersistItem
    {
        public string Source { get; set; } = string.Empty;
        public string LocationType { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Path { get; set; } = string.Empty;
        public string RegistryPath { get; set; } = string.Empty;
        public string Risk { get; set; } = string.Empty;
        public string Reason { get; set; } = string.Empty;
        public string MitreTechnique { get; set; } = string.Empty;
        public string Publisher { get; set; } = string.Empty;
        public string? FileHash { get; set; }
        public DateTime? FileModified { get; set; }

        // ---- UI helper properties for filtering and highlighting ----

        // True when this entry is new since baseline was captured
        public bool IsNewSinceBaseline { get; set; }

        // True when this entry matches the current case focus terms
        public bool IsFocusHit { get; set; }

        // UI property for showing "NEW" badge when item is new since baseline
        public string BaselineBadge => IsNewSinceBaseline ? "NEW" : string.Empty;
        public bool ShowBaselineBadge => IsNewSinceBaseline;

        // Color for the outer card border (string so Avalonia can parse it)
        public string FocusBorderBrush => IsFocusHit ? "#FF6BD5" : "#333";

        // Thickness for the outer card border
        public string FocusBorderThickness => IsFocusHit ? "2" : "1";

        // Background color for the risk “pill” based on Risk prefix
        public string RiskBackground
        {
            get
            {
                if (string.IsNullOrWhiteSpace(Risk))
                    return "#444";

                if (Risk.StartsWith("CHECK", StringComparison.OrdinalIgnoreCase))
                    return "#5A1E2C"; // dark red-ish

                if (Risk.StartsWith("NOTE", StringComparison.OrdinalIgnoreCase))
                    return "#4A3D16"; // amber/brown

                if (Risk.StartsWith("OK", StringComparison.OrdinalIgnoreCase))
                    return "#1E3D2A"; // green-ish

                return "#444";
            }
        }

        public override string ToString()
        {
            var sb = new StringBuilder();

            sb.Append('[').Append(LocationType).Append("] ");
            sb.Append(Risk).Append(" – ").Append(Name);

            if (!string.IsNullOrWhiteSpace(Source))
                sb.Append("  (").Append(Source).Append(')');

            sb.AppendLine();

            if (!string.IsNullOrWhiteSpace(Path))
                sb.AppendLine("  Path: " + Path);

            if (!string.IsNullOrWhiteSpace(RegistryPath))
                sb.AppendLine("  Registry: " + RegistryPath);

            if (!string.IsNullOrWhiteSpace(Reason))
                sb.AppendLine("  Reason: " + Reason);

            if (!string.IsNullOrWhiteSpace(MitreTechnique))
                sb.AppendLine("  MITRE: " + MitreTechnique);

            if (!string.IsNullOrWhiteSpace(Publisher))
                sb.AppendLine("  Publisher: " + Publisher);

            return sb.ToString().TrimEnd();
        }
    }
}

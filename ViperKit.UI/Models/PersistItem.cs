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

            return sb.ToString().TrimEnd();
        }
    }
}

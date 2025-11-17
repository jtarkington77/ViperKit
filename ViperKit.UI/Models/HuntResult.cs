namespace ViperKit.UI.Models;

public class HuntResult
{
    // e.g. "File", "Folder", "Registry", "IP", "Domain", "HashHit"
    public string Category { get; set; } = string.Empty;

    // Path / registry key / host / IP / etc.
    public string Target { get; set; } = string.Empty;

    // INFO / WARN / HIGH / CRITICAL
    public string Severity { get; set; } = "INFO";

    // Short one-line description
    public string Summary { get; set; } = string.Empty;

    // Longer details for the row
    public string Details { get; set; } = string.Empty;
}

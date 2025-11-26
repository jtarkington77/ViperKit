using System;

namespace ViperKit.UI.Models
{
    public class CaseEvent
    {
        public DateTime Timestamp { get; set; } = DateTime.Now;

        // Which tab / phase: Dashboard, Hunt, Persist, Sweep, Cleanup, Harden, Case
        public string Tab { get; set; } = string.Empty;

        // Short action label: "Scan started", "Autoruns enumerated", "Service removed"
        public string Action { get; set; } = string.Empty;

        // INFO / NOTE / WARN / HIGH
        public string Severity { get; set; } = "INFO";

        // What we touched: path, service name, task name, etc.
        public string Target { get; set; } = string.Empty;

        // Free-form description
        public string Details { get; set; } = string.Empty;
    }
}

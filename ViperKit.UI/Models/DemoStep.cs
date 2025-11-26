// ViperKit.UI - Models\DemoStep.cs
namespace ViperKit.UI.Models
{
    /// <summary>
    /// Represents a step in the demo walkthrough.
    /// </summary>
    public class DemoStep
    {
        /// <summary>
        /// Step number (1-based).
        /// </summary>
        public int StepNumber { get; set; }

        /// <summary>
        /// Title of this step (e.g., "Hunt the suspicious tool").
        /// </summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// Which tab to navigate to for this step.
        /// </summary>
        public string TabTarget { get; set; } = string.Empty;

        /// <summary>
        /// Tab index for navigation (0-based).
        /// </summary>
        public int TabIndex { get; set; }

        /// <summary>
        /// Main instructions for the user.
        /// </summary>
        public string Instructions { get; set; } = string.Empty;

        /// <summary>
        /// What to search for or look at.
        /// </summary>
        public string SearchTerm { get; set; } = string.Empty;

        /// <summary>
        /// What the user should expect to find.
        /// </summary>
        public string ExpectedFindings { get; set; } = string.Empty;

        /// <summary>
        /// Helpful tip for this step.
        /// </summary>
        public string Tip { get; set; } = string.Empty;

        /// <summary>
        /// Action to take (e.g., "Set as case focus", "Add to Cleanup").
        /// </summary>
        public string ActionToTake { get; set; } = string.Empty;

        /// <summary>
        /// What this step teaches.
        /// </summary>
        public string LearningPoint { get; set; } = string.Empty;

        /// <summary>
        /// Whether this step has been completed.
        /// </summary>
        public bool IsCompleted { get; set; }

        // UI Helpers
        public string StepLabel => $"Step {StepNumber}";
        public string StatusIcon => IsCompleted ? "✓" : "○";
    }
}

// ViperKit.UI - Models\HardenAction.cs
using System;
using System.ComponentModel;

namespace ViperKit.UI.Models
{
    /// <summary>
    /// Represents a single hardening action that can be applied to the system.
    /// </summary>
    public class HardenAction : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        private bool _isSelected;
        private bool _isApplied;
        private string _currentState = string.Empty;

        /// <summary>
        /// Unique identifier for this action.
        /// </summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// Category: ScriptExecution, Firewall, Defender, AutoRun, RemoteAccess, Office
        /// </summary>
        public string Category { get; set; } = string.Empty;

        /// <summary>
        /// Display name for the action.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Detailed description of what this action does.
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Current state on the system (detected at scan time).
        /// </summary>
        public string CurrentState
        {
            get => _currentState;
            set
            {
                _currentState = value;
                OnPropertyChanged(nameof(CurrentState));
                OnPropertyChanged(nameof(StateDisplay));
                OnPropertyChanged(nameof(IsAlreadyHardened));
            }
        }

        /// <summary>
        /// The recommended/target state after applying this action.
        /// </summary>
        public string RecommendedState { get; set; } = string.Empty;

        /// <summary>
        /// Whether this action is selected for application.
        /// </summary>
        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                _isSelected = value;
                OnPropertyChanged(nameof(IsSelected));
            }
        }

        /// <summary>
        /// Whether this action has been applied in the current session.
        /// </summary>
        public bool IsApplied
        {
            get => _isApplied;
            set
            {
                _isApplied = value;
                OnPropertyChanged(nameof(IsApplied));
                OnPropertyChanged(nameof(StatusIcon));
                OnPropertyChanged(nameof(StatusColor));
            }
        }

        /// <summary>
        /// Whether this action can be rolled back.
        /// </summary>
        public bool CanRollback { get; set; } = true;

        /// <summary>
        /// Which profile(s) include this action: Standard, Strict, Both
        /// </summary>
        public string Profile { get; set; } = "Standard";

        /// <summary>
        /// Data needed to rollback (original registry value, etc.)
        /// </summary>
        public string RollbackData { get; set; } = string.Empty;

        /// <summary>
        /// When this action was applied.
        /// </summary>
        public DateTime? AppliedAt { get; set; }

        /// <summary>
        /// Warning message for potentially disruptive actions.
        /// </summary>
        public string WarningMessage { get; set; } = string.Empty;

        /// <summary>
        /// Whether this action requires elevation/admin.
        /// </summary>
        public bool RequiresAdmin { get; set; } = true;

        /// <summary>
        /// Error message if the action failed.
        /// </summary>
        public string ErrorMessage { get; set; } = string.Empty;

        // ---- UI Helper Properties ----

        /// <summary>
        /// Whether the system is already in the recommended state.
        /// </summary>
        public bool IsAlreadyHardened =>
            !string.IsNullOrEmpty(CurrentState) &&
            CurrentState.Equals(RecommendedState, StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// Display text showing current → recommended state.
        /// </summary>
        public string StateDisplay
        {
            get
            {
                if (IsAlreadyHardened)
                    return $"Current: {CurrentState} (Already set)";
                return $"Current: {CurrentState} → {RecommendedState}";
            }
        }

        /// <summary>
        /// Status icon for the action.
        /// </summary>
        public string StatusIcon
        {
            get
            {
                if (IsApplied) return "✓";
                if (IsAlreadyHardened) return "●";
                if (!string.IsNullOrEmpty(ErrorMessage)) return "✗";
                return "○";
            }
        }

        /// <summary>
        /// Status color for the action.
        /// </summary>
        public string StatusColor
        {
            get
            {
                if (IsApplied) return "#99CC99";           // Green - applied
                if (IsAlreadyHardened) return "#66CCFF";   // Blue - already set
                if (!string.IsNullOrEmpty(ErrorMessage)) return "#FF9999"; // Red - error
                return "#AAAAAA";                           // Gray - pending
            }
        }

        /// <summary>
        /// Background color for category badge.
        /// </summary>
        public string CategoryBackground
        {
            get
            {
                return Category switch
                {
                    "ScriptExecution" => "#3A2A4A",  // Purple
                    "Firewall" => "#2A3A4A",         // Blue
                    "Defender" => "#2A4A3A",         // Green
                    "AutoRun" => "#4A3A2A",          // Orange
                    "RemoteAccess" => "#4A2A3A",     // Red
                    "Office" => "#3A3A2A",           // Yellow
                    _ => "#333"
                };
            }
        }

        /// <summary>
        /// Foreground color for category badge.
        /// </summary>
        public string CategoryForeground
        {
            get
            {
                return Category switch
                {
                    "ScriptExecution" => "#CC99FF",
                    "Firewall" => "#99CCFF",
                    "Defender" => "#99FFCC",
                    "AutoRun" => "#FFCC99",
                    "RemoteAccess" => "#FF99CC",
                    "Office" => "#CCCC99",
                    _ => "#CCCCCC"
                };
            }
        }

        /// <summary>
        /// Whether to show warning indicator.
        /// </summary>
        public bool HasWarning => !string.IsNullOrEmpty(WarningMessage);

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}

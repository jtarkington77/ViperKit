using System;
using System.Runtime.InteropServices;
using Avalonia.Controls;
using Avalonia.Interactivity;
using ViperKit.UI.ViewModels;
using ViperKit.UI.Models;


namespace ViperKit.UI.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        CaseManager.StartNewCase();
        PopulateDashboardSystemSnapshot();
        UpdateDashboardCaseSummary();
        RefreshCaseTab();

        DataContext = new MainWindowViewModel();
        PopulateSystemSnapshot();
    }

    // =========================
    // DASHBOARD – SYSTEM SNAPSHOT ONLY
    // =========================
    private void PopulateSystemSnapshot()
    {
        try
        {
            string machineName = Environment.MachineName;
            string userName    = Environment.UserName;
            string domain      = Environment.UserDomainName;
            var    arch        = RuntimeInformation.OSArchitecture;

            // UHelper for Windows build.
            string osLabel     = GetFriendlyOsLabel();

            if (SystemHostNameText != null)
                SystemHostNameText.Text = $"Host: {machineName}";

            if (SystemUserText != null)
                SystemUserText.Text     = $"User: {domain}\\{userName}";

            if (SystemOsText != null)
                SystemOsText.Text       = $"OS: {osLabel} ({arch})";
        }
        catch
        {
            // Dashboard should never blow up just because env info failed
        }
    }

    private void PopulateDashboardSystemSnapshot()
    {
        if (SystemHostNameText != null)
            SystemHostNameText.Text = $"Host: {CaseManager.HostName}";

        if (SystemUserText != null)
            SystemUserText.Text = $"User: {CaseManager.UserName}";

        if (SystemOsText != null)
            SystemOsText.Text = $"OS: {CaseManager.OsDescription}";
    }

    private void UpdateDashboardCaseSummary()
    {
        var events = CaseManager.GetSnapshot();

        if (CaseIdText != null)
            CaseIdText.Text = $"Case ID: {CaseManager.CaseId}";

        if (CaseEventsCountText != null)
            CaseEventsCountText.Text = $"Events: {events.Count}";

        if (CaseLastEventText != null)
        {
            if (events.Count == 0)
            {
                CaseLastEventText.Text = "Last: (no events yet)";
            }
            else
            {
                var last = events[^1];
                CaseLastEventText.Text =
                    $"Last: [{last.Tab}] [{last.Severity}] {last.Action}";
            }
        }
    }

    private void RefreshCaseTab()
    {
        try
        {
            var events = CaseManager.GetSnapshot();

            if (CaseSummaryIdText != null)
                CaseSummaryIdText.Text = string.IsNullOrWhiteSpace(CaseManager.CaseId)
                    ? "(no case yet)"
                    : CaseManager.CaseId;

            if (CaseSummaryHostText != null)
                CaseSummaryHostText.Text = string.IsNullOrWhiteSpace(CaseManager.HostName)
                    ? "(unknown host)"
                    : CaseManager.HostName;

            if (CaseSummaryUserText != null)
                CaseSummaryUserText.Text = string.IsNullOrWhiteSpace(CaseManager.UserName)
                    ? "(unknown user)"
                    : CaseManager.UserName;

            if (CaseSummaryStartedText != null)
                CaseSummaryStartedText.Text = CaseManager.StartedAt == default
                    ? "(not started)"
                    : CaseManager.StartedAt.ToString("yyyy-MM-dd HH:mm:ss");

            if (CaseEventsList != null)
                CaseEventsList.ItemsSource = events;
        }
        catch
        {
            // Case tab should never crash the app
        }
    }

    private void CaseRefreshButton_OnClick(object? sender, RoutedEventArgs e)
    {
        RefreshCaseTab();
        UpdateDashboardCaseSummary();
    }


    private void CaseExportButton_OnClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            string path = CaseManager.ExportToFile();

            if (DashboardStatusText != null)
                DashboardStatusText.Text = $"Status: case exported to {path}";
        }
        catch (Exception ex)
        {
            if (DashboardStatusText != null)
                DashboardStatusText.Text = $"Status: error exporting case – {ex.Message}";
        }
    }

    private void CaseFocusSetButton_OnClick(object? sender, RoutedEventArgs e)
    {
        // Look up controls by name instead of using generated fields
        var focusBox    = this.FindControl<TextBox>("CaseFocusTextBox");
        var statusBlock = this.FindControl<TextBlock>("CaseFocusStatusText");

        var value = focusBox?.Text ?? string.Empty;

        CaseManager.SetFocusTarget(value, "Case");

        if (statusBlock != null)
        {
            statusBlock.Text = string.IsNullOrWhiteSpace(value)
                ? "Focus: (none)"
                : $"Focus: {value}";
        }

        // Refresh Persist tab – it already calls MatchesCaseFocus in BindPersistResults
        BindPersistResults();
    }

    private void CaseFocusClearButton_OnClick(object? sender, RoutedEventArgs e)
    {
        // Look up controls by name instead of using generated fields
        var focusBox    = this.FindControl<TextBox>("CaseFocusTextBox");
        var statusBlock = this.FindControl<TextBlock>("CaseFocusStatusText");

        CaseManager.SetFocusTarget(string.Empty, "Case");

        if (focusBox != null)
            focusBox.Text = string.Empty;

        if (statusBlock != null)
            statusBlock.Text = "Focus: (none)";

        BindPersistResults();
    }

    private void HuntSetCaseFocusButton_OnClick(object? sender, RoutedEventArgs e)
    {
        // Take whatever the analyst typed as the thing they “stopped”
        var value = HuntIocInput?.Text ?? string.Empty;

        // Record it into the shared case focus
        CaseManager.SetFocusTarget(value, "Hunt");

        // Mirror it into the Case tab UI if those controls exist
        if (CaseFocusTextBox != null)
            CaseFocusTextBox.Text = value;

        if (CaseFocusStatusText != null)
        {
            CaseFocusStatusText.Text = string.IsNullOrWhiteSpace(value)
                ? "Focus: (none)"
                : $"Focus: {value}";
        }

        // Refresh the rest of the app so Persist/Sweep can start honoring the focus
        try
        {
            PopulateDashboardSystemSnapshot();
            UpdateDashboardCaseSummary();

            // These are in the other partials; if they exist, this will compile fine.
            BindPersistResults();
            BindSweepResults();
        }
        catch
        {
            // We never want a focus change to crash the UI
        }
    }


    private static string GetFriendlyOsLabel()
    {
        try
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return RuntimeInformation.OSDescription;

            Version v = Environment.OSVersion.Version;

            // Windows 11 "marketing" builds start at 22000 but still report Major=10
            if (v.Major == 10 && v.Build >= 22000)
                return $"Windows 11 (build {v.Build})";

            if (v.Major == 10 && v.Build >= 10240)
                return $"Windows 10 (build {v.Build})";

            // Fallback – whatever .NET reports
            return RuntimeInformation.OSDescription;
        }
        catch
        {
            return RuntimeInformation.OSDescription;
        }
    }

    // Returns true if there's no focus set, or if any of the provided fields match it.
    private static bool MatchesCaseFocus(params string?[] fields)
    {
        var focusList = CaseManager.GetFocusTargets();
        var focus = focusList.Count > 0 ? string.Join(", ", focusList) : "(none)";

        if (string.IsNullOrWhiteSpace(focus))
            return true; // no focus → don't filter anything

        focus = focus.Trim();
        var focusLower = focus.ToLowerInvariant();

        foreach (var field in fields)
        {
            if (string.IsNullOrWhiteSpace(field))
                continue;

            var lower = field.ToLowerInvariant();
            if (lower.Contains(focusLower))
                return true;
        }

        return false;
    }
}


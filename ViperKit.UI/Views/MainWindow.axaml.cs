using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Principal;
using Avalonia.Controls;
using Avalonia.Interactivity;
using ViperKit.UI.ViewModels;
using ViperKit.UI.Models;


namespace ViperKit.UI.Views;

public partial class MainWindow : Window
{
    private bool _caseActive = false;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainWindowViewModel();

        // Check admin status and show warning if needed
        CheckAdminStatus();

        // Load hunt history
        LoadHuntHistory();

        // Show case selection panel, don't auto-start case
        ShowCaseSelectionPanel();
        RefreshAvailableCases();
        PopulateSystemSnapshot();

        // Initialize Demo Mode
        InitializeDemoMode();
    }

    private void CheckAdminStatus()
    {
        try
        {
            bool isAdmin = false;

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                using (WindowsIdentity identity = WindowsIdentity.GetCurrent())
                {
                    WindowsPrincipal principal = new WindowsPrincipal(identity);
                    isAdmin = principal.IsInRole(WindowsBuiltInRole.Administrator);
                }
            }

            // Show/hide the admin warning banner
            if (AdminWarningBanner != null)
            {
                AdminWarningBanner.IsVisible = !isAdmin;
            }
        }
        catch
        {
            // If we can't determine admin status, assume not admin and show warning
            if (AdminWarningBanner != null)
            {
                AdminWarningBanner.IsVisible = true;
            }
        }
    }

    private async System.Threading.Tasks.Task<bool> ShowConfirmationDialog(string title, string message)
    {
        try
        {
            var dialog = new Window
            {
                Title = title,
                Width = 500,
                Height = 250,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                CanResize = false
            };

            bool result = false;

            var panel = new StackPanel
            {
                Margin = new Avalonia.Thickness(20),
                Spacing = 20
            };

            panel.Children.Add(new TextBlock
            {
                Text = message,
                TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                FontSize = 13
            });

            var buttonPanel = new StackPanel
            {
                Orientation = Avalonia.Layout.Orientation.Horizontal,
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                Spacing = 10
            };

            var okButton = new Button
            {
                Content = "OK",
                Width = 100,
                Height = 35
            };
            okButton.Click += (s, e) =>
            {
                result = true;
                dialog.Close();
            };

            var cancelButton = new Button
            {
                Content = "Cancel",
                Width = 100,
                Height = 35
            };
            cancelButton.Click += (s, e) =>
            {
                result = false;
                dialog.Close();
            };

            buttonPanel.Children.Add(okButton);
            buttonPanel.Children.Add(cancelButton);
            panel.Children.Add(buttonPanel);

            dialog.Content = panel;

            await dialog.ShowDialog(this);
            return result;
        }
        catch
        {
            // If dialog fails, default to not confirmed (safe choice)
            return false;
        }
    }

    // =========================
    // CASE SELECTION / MANAGEMENT
    // =========================
    private void ShowCaseSelectionPanel()
    {
        _caseActive = false;
        if (CaseSelectionPanel != null) CaseSelectionPanel.IsVisible = true;
        // Hide case-dependent panels until case is started
    }

    private void HideCaseSelectionPanel()
    {
        _caseActive = true;
        if (CaseSelectionPanel != null) CaseSelectionPanel.IsVisible = false;
    }

    private void RefreshAvailableCases()
    {
        try
        {
            var cases = CaseStorage.GetAvailableCases();
            if (AvailableCasesList != null)
            {
                AvailableCasesList.ItemsSource = cases;
            }

            if (NoCasesText != null)
            {
                NoCasesText.IsVisible = cases.Count == 0;
            }
        }
        catch
        {
            // Don't crash if we can't list cases
        }
    }

    private void RefreshCaseListButton_OnClick(object? sender, RoutedEventArgs e)
    {
        RefreshAvailableCases();
    }

    private void AvailableCasesList_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        bool hasSelection = AvailableCasesList?.SelectedItem != null;
        if (LoadSelectedCaseButton != null) LoadSelectedCaseButton.IsEnabled = hasSelection;
        if (DeleteSelectedCaseButton != null) DeleteSelectedCaseButton.IsEnabled = hasSelection;
    }

    private void StartNewCaseButton_OnClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            string? caseName = NewCaseNameInput?.Text?.Trim();
            if (string.IsNullOrWhiteSpace(caseName))
                caseName = null;

            CaseManager.StartNewCase(caseName);
            HideCaseSelectionPanel();
            OnCaseStarted();

            if (DashboardStatusText != null)
                DashboardStatusText.Text = $"Status: New case started - {CaseManager.GetDisplayName()}";
        }
        catch (Exception ex)
        {
            if (DashboardStatusText != null)
                DashboardStatusText.Text = $"Status: Error starting case - {ex.Message}";
        }
    }

    private void LoadSelectedCaseButton_OnClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            if (AvailableCasesList?.SelectedItem is CaseSummary summary)
            {
                if (CaseManager.LoadCase(summary.CaseId))
                {
                    HideCaseSelectionPanel();
                    OnCaseStarted();
                    UpdateBaselineUI();

                    if (DashboardStatusText != null)
                        DashboardStatusText.Text = $"Status: Case loaded - {CaseManager.GetDisplayName()}";
                }
                else
                {
                    if (DashboardStatusText != null)
                        DashboardStatusText.Text = "Status: Failed to load case";
                }
            }
        }
        catch (Exception ex)
        {
            if (DashboardStatusText != null)
                DashboardStatusText.Text = $"Status: Error loading case - {ex.Message}";
        }
    }

    private void DeleteSelectedCaseButton_OnClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            if (AvailableCasesList?.SelectedItem is CaseSummary summary)
            {
                CaseStorage.DeleteCase(summary.CaseId);
                RefreshAvailableCases();

                if (DashboardStatusText != null)
                    DashboardStatusText.Text = $"Status: Case deleted - {summary.DisplayName}";
            }
        }
        catch (Exception ex)
        {
            if (DashboardStatusText != null)
                DashboardStatusText.Text = $"Status: Error deleting case - {ex.Message}";
        }
    }

    private void CloseCaseButton_OnClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            CaseManager.SaveCurrentCase();
            ShowCaseSelectionPanel();
            RefreshAvailableCases();

            if (DashboardStatusText != null)
                DashboardStatusText.Text = "Status: Case closed and saved";
        }
        catch (Exception ex)
        {
            if (DashboardStatusText != null)
                DashboardStatusText.Text = $"Status: Error closing case - {ex.Message}";
        }
    }

    private void OnCaseStarted()
    {
        PopulateDashboardSystemSnapshot();
        UpdateDashboardCaseSummary();
        RefreshCaseTab();
        UpdateBaselineUI();
    }

    // =========================
    // BASELINE MANAGEMENT
    // =========================
    private void UpdateBaselineUI()
    {
        try
        {
            var baseline = CaseManager.GetBaseline();
            bool hasBaseline = baseline != null;

            if (BaselineStatusText != null)
            {
                BaselineStatusText.Text = hasBaseline
                    ? "Baseline captured"
                    : "No baseline captured";
                BaselineStatusText.Foreground = hasBaseline
                    ? Avalonia.Media.Brushes.LightGreen
                    : Avalonia.Media.Brushes.Gray;
            }

            if (BaselineDetailsText != null)
            {
                BaselineDetailsText.Text = hasBaseline
                    ? "Baseline is available for comparison."
                    : "Capture a baseline after cleaning to monitor for reinfection.";
            }

            if (CompareBaselineButton != null)
                CompareBaselineButton.IsEnabled = hasBaseline;

            if (BaselineInfoPanel != null)
            {
                BaselineInfoPanel.IsVisible = hasBaseline;
                if (hasBaseline && baseline != null)
                {
                    if (BaselineCapturedAtText != null)
                        BaselineCapturedAtText.Text = $"Captured: {baseline.CapturedAt:yyyy-MM-dd HH:mm:ss}";

                    if (BaselinePersistCountText != null)
                        BaselinePersistCountText.Text = $"Persist entries: {baseline.PersistEntries?.Count ?? 0}";

                    if (BaselineHardenCountText != null)
                        BaselineHardenCountText.Text = $"Hardening applied: {baseline.HardeningApplied?.Count ?? 0}";
                }
            }
        }
        catch
        {
            // Never crash updating baseline UI
        }
    }

    private void CaptureBaselineButton_OnClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            // Get current persist results
            var persistItems = GetCurrentPersistItems();

            CaseManager.CaptureBaseline(persistItems);
            UpdateBaselineUI();

            if (DashboardStatusText != null)
                DashboardStatusText.Text = $"Status: Baseline captured with {persistItems.Count} persistence entries";
        }
        catch (Exception ex)
        {
            if (DashboardStatusText != null)
                DashboardStatusText.Text = $"Status: Error capturing baseline - {ex.Message}";
        }
    }

    private void CompareBaselineButton_OnClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            // Switch to Persist tab
            if (MainTabs != null)
                MainTabs.SelectedIndex = 2; // Persist tab

            // Trigger persist scan by simulating button click
            PersistRunButton_OnClick(sender, e);

            if (DashboardStatusText != null)
                DashboardStatusText.Text = "Status: Running persist scan to compare against baseline...";
        }
        catch (Exception ex)
        {
            if (DashboardStatusText != null)
                DashboardStatusText.Text = $"Status: Error comparing baseline - {ex.Message}";
        }
    }

    // Helper to get current persist items (from the last scan)
    private System.Collections.Generic.List<PersistItem> GetCurrentPersistItems()
    {
        return _persistItems ?? new System.Collections.Generic.List<PersistItem>();
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
            // Generate structured report data
            var report = CaseManager.GenerateReport();

            // Populate findings from current scan data (if available)
            PopulateReportFindings(report);

            // Generate PDF
            string folder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "ViperKit", "Reports");
            Directory.CreateDirectory(folder);

            string filename = $"ViperKit_Report_{CaseManager.CaseId}_{DateTime.Now:yyyyMMdd_HHmmss}.pdf";
            string path = Path.Combine(folder, filename);

            ViperKit.UI.Services.PdfReportGenerator.GenerateReport(report, path);

            // Also save the old text export for compatibility
            CaseManager.ExportToFile();

            if (DashboardStatusText != null)
                DashboardStatusText.Text = $"Status: PDF report exported to {path}";

            // Open the folder containing the report
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = folder,
                    UseShellExecute = true
                });
            }
            catch
            {
                // Ignore if can't open folder
            }
        }
        catch (Exception ex)
        {
            if (DashboardStatusText != null)
                DashboardStatusText.Text = $"Status: error exporting report – {ex.Message}";
        }
    }

    private void PopulateReportFindings(CaseReport report)
    {
        try
        {
            // Get persist findings from current scan
            var persistItems = GetCurrentPersistItems();
            if (persistItems.Count > 0)
            {
                report.Findings.PersistenceTotal = persistItems.Count;
                report.Findings.PersistenceCheck = persistItems.Count(p =>
                    p.Risk.StartsWith("CHECK", StringComparison.OrdinalIgnoreCase));
                report.Findings.PersistenceNote = persistItems.Count(p =>
                    p.Risk.StartsWith("NOTE", StringComparison.OrdinalIgnoreCase));
                report.Findings.PersistenceOk = persistItems.Count(p =>
                    p.Risk.StartsWith("OK", StringComparison.OrdinalIgnoreCase));

                // Top 10 high-risk findings
                report.Findings.TopPersistenceFindings = persistItems
                    .Where(p => p.Risk.StartsWith("CHECK", StringComparison.OrdinalIgnoreCase))
                    .Take(10)
                    .Select(p => $"{p.Name} - {p.Path ?? p.RegistryPath}")
                    .ToList();
            }

            // Get sweep findings from current results
            var sweepItems = GetCurrentSweepItems();
            if (sweepItems.Count > 0)
            {
                report.Findings.SweepTotal = sweepItems.Count;
                report.Findings.SweepSuspicious = sweepItems.Count(s =>
                    s.Severity == "HIGH" || s.Severity == "MEDIUM" || s.IsFocusHit);

                report.Findings.TopSweepFindings = sweepItems
                    .Where(s => s.Severity == "HIGH" || s.IsFocusHit)
                    .Take(10)
                    .Select(s => s.Modified.HasValue
                        ? $"{s.Name} ({s.Modified.Value:yyyy-MM-dd}) - {s.Path}"
                        : $"{s.Name} - {s.Path}")
                    .ToList();
            }

            // Get hunt results if available
            var huntResults = GetCurrentHuntResults();
            if (huntResults.Count > 0)
            {
                report.Findings.HuntMatches = huntResults.Count;
                report.Findings.HuntTargets = huntResults
                    .Take(10)
                    .Select(h => $"{h.Summary} - {h.Target}")
                    .ToList();
            }

            // Get PowerShell history if available
            var psHistory = GetCurrentPowerShellHistory();
            if (psHistory.Count > 0)
            {
                report.Findings.PowerShellCommandsAnalyzed = psHistory.Count;
                report.Findings.PowerShellHighRisk = psHistory.Count(p =>
                    p.Severity == "HIGH");

                report.Findings.TopPowerShellCommands = psHistory
                    .Where(p => p.Severity == "HIGH")
                    .Take(5)
                    .Select(p => p.Command)
                    .ToList();
            }
        }
        catch
        {
            // Don't fail export if we can't populate findings
        }
    }

    // Helper methods to get current scan results (these will be in the partial classes)
    private List<SweepEntry> GetCurrentSweepItems()
    {
        return _sweepEntries?.ToList() ?? new List<SweepEntry>();
    }

    private List<HuntResult> GetCurrentHuntResults()
    {
        return _huntResults?.ToList() ?? new List<HuntResult>();
    }

    private List<PowerShellHistoryEntry> GetCurrentPowerShellHistory()
    {
        return _psHistoryFiltered?.ToList() ?? new List<PowerShellHistoryEntry>();
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

    // Handle tab selection changes - refresh relevant data when tabs are selected
    private void MainTabs_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        // Only process TabControl selection changes, not nested combo boxes
        if (sender is not TabControl tabControl)
            return;

        // Verify it's actually our main tabs by checking x:Name
        if (tabControl != MainTabs)
            return;

        try
        {
            int selectedIndex = tabControl.SelectedIndex;

            // Tab indices: 0=Dashboard, 1=Hunt, 2=Persist, 3=Sweep, 4=Cleanup, 5=Harden, 6=Case, 7=Help
            switch (selectedIndex)
            {
                case 0: // Dashboard - refresh summary
                    UpdateDashboardCaseSummary();
                    break;
                case 4: // Cleanup tab - refresh the queue
                    RefreshCleanupQueue();
                    break;
                case 6: // Case tab - refresh case events
                    RefreshCaseTab();
                    UpdateDashboardCaseSummary();
                    break;
            }
        }
        catch
        {
            // Tab refresh should never crash the app
        }
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


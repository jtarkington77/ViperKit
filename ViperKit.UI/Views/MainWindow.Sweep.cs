using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Avalonia.Controls;
using Avalonia.Interactivity;
using ViperKit.UI.Models;

namespace ViperKit.UI.Views;

public partial class MainWindow
{
    // Cache of last sweep so we can filter without rescanning
    private readonly List<SweepEntry> _sweepEntries = new();

    // =========================
    // SWEEP TAB – RECENT CHANGE RADAR
    // =========================

    private void SweepRunButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (SweepStatusText != null)
            SweepStatusText.Text = "Status: scanning for recent changes...";

        _sweepEntries.Clear();

        DateTime now    = DateTime.Now;
        TimeSpan window = GetSweepLookbackWindow();
        DateTime cutoff = now - window;

        // File types worth caring about on a first pass 
        var interestingExts = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".exe", ".dll", ".com",
            ".bat", ".cmd", ".ps1", ".vbs", ".js", ".jse",
            ".scr", ".sys",
            ".msi",
            ".zip", ".7z", ".rar", ".iso"
        };

        // Build sweep roots across ALL user profiles + common paths
        var roots = BuildSweepRoots();

        foreach (var (label, path) in roots)
        {
            ScanSweepRoot(label, path, cutoff, now, interestingExts);
        }

        // Deep services + drivers (other partial uses _sweepEntries too)
        RunSweepServicesAndDrivers();

        BindSweepResults();

        int total = _sweepEntries.Count;
        int flagged = _sweepEntries.Count(entry =>
            entry.Severity.Equals("HIGH", StringComparison.OrdinalIgnoreCase) ||
            entry.Severity.Equals("MEDIUM", StringComparison.OrdinalIgnoreCase));

        if (SweepStatusText != null)
            SweepStatusText.Text = $"Status: sweep complete – {total} item(s), {flagged} MED/HIGH.";
        
        // Log into the case timeline
        try
        {
            CaseManager.AddEvent(
                tab: "Sweep",
                action: "Sweep scan completed",
                severity: flagged > 0 ? "WARN" : "INFO",
                target: $"Items: {total}",
                details: flagged > 0
                    ? $"Flagged entries (MED/HIGH): {flagged}"
                    : "No MED/HIGH entries found in sweep.");
        }
        catch
        {
        }

        // Update dashboard + case tab summaries
        try
        {
            UpdateDashboardCaseSummary();
            RefreshCaseTab();
        }
        catch
        {
        }

    }

    private TimeSpan GetSweepLookbackWindow()
    {
        int index = SweepLookbackCombo?.SelectedIndex ?? -1;

        return index switch
        {
            0 => TimeSpan.FromHours(24), // 24 hours
            1 => TimeSpan.FromDays(3),
            2 => TimeSpan.FromDays(7),
            3 => TimeSpan.FromDays(30),
            _ => TimeSpan.FromHours(24)  // default
        };
    }

    private List<(string Label, string Path)> BuildSweepRoots()
    {
        var roots = new List<(string Label, string Path)>();

        try
        {
            // Work out C:\Users from the current profile
            string currentProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            string usersRoot      = Path.GetDirectoryName(currentProfile) ?? currentProfile;

            if (Directory.Exists(usersRoot))
            {
                foreach (var userDir in Directory.EnumerateDirectories(usersRoot))
                {
                    string userName = Path.GetFileName(userDir);
                    if (string.IsNullOrWhiteSpace(userName))
                        continue;

                    string lower = userName.ToLowerInvariant();

                    // Skip obvious system / template profiles
                    if (lower is "default" or "default user" or "public" or "all users")
                        continue;

                    // Desktop
                    string desktop = Path.Combine(userDir, "Desktop");
                    if (Directory.Exists(desktop))
                        roots.Add(($"Desktop ({userName})", desktop));

                    // Downloads
                    string downloads = Path.Combine(userDir, "Downloads");
                    if (Directory.Exists(downloads))
                        roots.Add(($"Downloads ({userName})", downloads));

                    // AppData\Roaming
                    string appRoaming = Path.Combine(userDir, "AppData", "Roaming");
                    if (Directory.Exists(appRoaming))
                        roots.Add(($"AppData\\Roaming ({userName})", appRoaming));

                    // AppData\Local
                    string appLocal = Path.Combine(userDir, "AppData", "Local");
                    if (Directory.Exists(appLocal))
                        roots.Add(($"AppData\\Local ({userName})", appLocal));

                    // AppData\Local\Temp
                    string tempLocal = Path.Combine(userDir, "AppData", "Local", "Temp");
                    if (Directory.Exists(tempLocal))
                        roots.Add(($"Temp ({userName})", tempLocal));
                }
            }
        }
        catch (Exception ex)
        {
            _sweepEntries.Add(new SweepEntry
            {
                Category = "Error",
                Severity = "LOW",
                Source   = "BuildSweepRoots",
                Reason   = ex.Message
            });
        }

        // Global-ish roots
        AddRootIfExists(roots, "ProgramData",
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData));

        AddRootIfExists(roots, "Startup (current user)",
            Environment.GetFolderPath(Environment.SpecialFolder.Startup));

        AddRootIfExists(roots, "Startup (all users)",
            Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup));

        return roots;
    }

    private static void AddRootIfExists(List<(string Label, string Path)> roots, string label, string? path)
    {
        if (!string.IsNullOrWhiteSpace(path) && Directory.Exists(path))
            roots.Add((label, path));
    }

    private void ScanSweepRoot(
        string label,
        string rootPath,
        DateTime cutoff,
        DateTime now,
        HashSet<string> interestingExts)
    {
        try
        {
            foreach (var file in Directory.EnumerateFiles(rootPath, "*", SearchOption.AllDirectories))
            {
                DateTime created  = File.GetCreationTime(file);
                DateTime modified = File.GetLastWriteTime(file);

                // Respect the lookback window
                if (created < cutoff && modified < cutoff)
                    continue;

                string ext = Path.GetExtension(file);
                if (!interestingExts.Contains(ext))
                    continue;

                TimeSpan age     = now - modified;
                string lowerPath = file.ToLowerInvariant();

                bool inDesktop   = lowerPath.Contains(@"\desktop\");
                bool inDownloads = lowerPath.Contains(@"\downloads\");
                bool inStartup   = lowerPath.Contains(@"\startup\");
                bool inAppData   = lowerPath.Contains(@"\appdata\");
                bool inTemp      = lowerPath.Contains(@"\temp\");

                bool hotLocation  = inDesktop || inDownloads || inStartup;
                bool warmLocation = inAppData || inTemp;

                bool isScript = ext is ".ps1" or ".js" or ".jse" or ".vbs" or ".bat" or ".cmd";
                bool isExe    = ext is ".exe" or ".com";
                bool isDriver = ext is ".sys";
                bool isDll    = ext is ".dll";

                var reasons = new List<string>();

                if (hotLocation || warmLocation)
                    reasons.Add("user-writable location");

                if (isExe)
                    reasons.Add("executable file");
                else if (isDriver)
                    reasons.Add("driver file");
                else if (isScript)
                    reasons.Add("script file");
                else if (isDll)
                    reasons.Add("DLL file");

                if (age.TotalHours <= 24)
                    reasons.Add("modified within last 24h");

                // ----------------- SEVERITY RULES -----------------
                string severity = "LOW";

                // HIGH: EXE / script / driver in Desktop / Downloads / Startup
                if (hotLocation && (isExe || isDriver || isScript))
                {
                    severity = "HIGH";
                }
                // HIGH: VERY recent EXE/script/driver in AppData/Temp (4h window)
                else if (warmLocation && (isExe || isDriver || isScript) && age.TotalHours <= 4)
                {
                    severity = "HIGH";
                }
                // MEDIUM: older EXE/script/driver in AppData/Temp, or DLLs there
                else if (warmLocation && (isExe || isDriver || isScript || isDll))
                {
                    severity = "MEDIUM";
                }
                // Everything else stays LOW – still logged.

                var entry = new SweepEntry
                {
                    Category = "File",
                    Severity = severity,
                    Path     = file,
                    Name     = Path.GetFileName(file),
                    Source   = label,
                    Reason   = reasons.Count > 0 ? string.Join(", ", reasons) : string.Empty,
                    Modified = modified
                };

                _sweepEntries.Add(entry);
            }
        }
        catch (Exception ex)
        {
            _sweepEntries.Add(new SweepEntry
            {
                Category = "Error",
                Severity = "LOW",
                Source   = label,
                Reason   = ex.Message
            });
        }
    }

    // ------------ Binding & filter ------------

    private void BindSweepResults()
    {
        if (SweepResultsList == null)
            return;

        IEnumerable<SweepEntry> source = _sweepEntries;

        // 1) "Show only flagged" => MED/HIGH only
        if (SweepShowOnlyFlaggedCheckBox?.IsChecked == true)
        {
            source = source.Where(entry =>
                entry.Severity.Equals("HIGH", StringComparison.OrdinalIgnoreCase) ||
                entry.Severity.Equals("MEDIUM", StringComparison.OrdinalIgnoreCase));
        }

        // 2) Severity dropdown
        if (SweepSeverityFilterCombo != null && SweepSeverityFilterCombo.SelectedIndex > 0)
        {
            string? sev = SweepSeverityFilterCombo.SelectedIndex switch
            {
                1 => "HIGH",
                2 => "MEDIUM",
                3 => "LOW",
                _ => null
            };

            if (!string.IsNullOrEmpty(sev))
            {
                source = source.Where(entry =>
                    entry.Severity.Equals(sev, StringComparison.OrdinalIgnoreCase));
            }
        }

        // 3) Text search (path / name / reason / source)
        string? term = SweepSearchTextBox?.Text;
        if (!string.IsNullOrWhiteSpace(term))
        {
            term = term.Trim();

            source = source.Where(entry =>
                (!string.IsNullOrEmpty(entry.Name)   && entry.Name.Contains(term, StringComparison.OrdinalIgnoreCase)) ||
                (!string.IsNullOrEmpty(entry.Path)   && entry.Path.Contains(term, StringComparison.OrdinalIgnoreCase)) ||
                (!string.IsNullOrEmpty(entry.Source) && entry.Source.Contains(term, StringComparison.OrdinalIgnoreCase)) ||
                (!string.IsNullOrEmpty(entry.Reason) && entry.Reason.Contains(term, StringComparison.OrdinalIgnoreCase)));
        }

        // DataGrid binds directly to SweepEntry properties
        SweepResultsList.ItemsSource = source.ToList();
    }

    private void SweepFilterCheckBox_OnChanged(object? sender, RoutedEventArgs e)
    {
        BindSweepResults();
    }

    private void SweepSeverityFilterCombo_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        BindSweepResults();
    }

    private void SweepSearchTextBox_OnTextChanged(object? sender, TextChangedEventArgs e)
    {
        BindSweepResults();
    }

    // ------------ Copy / Save ------------

    private async void SweepCopyResultsButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (_sweepEntries.Count == 0)
        {
            if (SweepStatusText != null)
                SweepStatusText.Text = "Status: nothing to copy.";
            return;
        }

        // Copy full details, not just the short display line
        var sb = new StringBuilder();

        foreach (var entry in _sweepEntries)
        {
            sb.AppendLine($"Category: {entry.Category}");
            sb.AppendLine($"Severity: {entry.Severity}");
            sb.AppendLine($"Source:   {entry.Source}");
            sb.AppendLine($"Name:     {entry.Name}");
            sb.AppendLine($"Path:     {entry.Path}");
            if (entry.Modified.HasValue)
                sb.AppendLine($"Modified: {entry.Modified.Value}");
            if (!string.IsNullOrWhiteSpace(entry.Reason))
                sb.AppendLine($"Reason:   {entry.Reason}");
            sb.AppendLine();
        }

        string text = sb.ToString();

        try
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel?.Clipboard is not null)
            {
                await topLevel.Clipboard.SetTextAsync(text);
                if (SweepStatusText != null)
                    SweepStatusText.Text = "Status: sweep results copied to clipboard.";
            }
            else
            {
                if (SweepStatusText != null)
                    SweepStatusText.Text = "Status: clipboard not available.";
            }
        }
        catch (Exception ex)
        {
            if (SweepStatusText != null)
                SweepStatusText.Text = $"Status: error copying results – {ex.Message}";
        }
    }

    private void SweepSaveResultsButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (_sweepEntries.Count == 0)
        {
            if (SweepStatusText != null)
                SweepStatusText.Text = "Status: nothing to save.";
            return;
        }

        try
        {
            string baseDir     = AppContext.BaseDirectory;
            string snapshotDir = Path.Combine(baseDir, "logs", "SweepSnapshots");
            Directory.CreateDirectory(snapshotDir);

            string stamp    = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string fileName = $"Sweep_{stamp}.txt";
            string fullPath = Path.Combine(snapshotDir, fileName);

            var sb = new StringBuilder();

            foreach (var entry in _sweepEntries)
            {
                sb.AppendLine($"Category: {entry.Category}");
                sb.AppendLine($"Severity: {entry.Severity}");
                sb.AppendLine($"Source:   {entry.Source}");
                sb.AppendLine($"Name:     {entry.Name}");
                sb.AppendLine($"Path:     {entry.Path}");
                if (entry.Modified.HasValue)
                    sb.AppendLine($"Modified: {entry.Modified.Value}");
                if (!string.IsNullOrWhiteSpace(entry.Reason))
                    sb.AppendLine($"Reason:   {entry.Reason}");
                sb.AppendLine();
            }

            File.WriteAllText(fullPath, sb.ToString(), Encoding.UTF8);

            if (SweepStatusText != null)
                SweepStatusText.Text = $"Status: sweep snapshot saved to {fileName}.";
        }
        catch (Exception ex)
        {
            if (SweepStatusText != null)
                SweepStatusText.Text = $"Status: error saving snapshot – {ex.Message}";
        }
    }
}

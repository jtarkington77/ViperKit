using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using ViperKit.UI.Models;

namespace ViperKit.UI.Views;

public partial class MainWindow
{
    // Cache of last sweep so we can filter without rescanning
    private readonly List<SweepEntry> _sweepEntries = new();

    // =========================
    // SWEEP TAB – RECENT CHANGE RADAR
    // =========================

    private async void SweepRunButton_OnClick(object? sender, RoutedEventArgs e)
    {
        // Disable button during scan to prevent double-clicks and show activity
        if (sender is Button btn)
            btn.IsEnabled = false;

        try
        {
            if (SweepStatusText != null)
                SweepStatusText.Text = "Status: preparing sweep...";

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
            int totalRoots = roots.Count;
            int currentRoot = 0;

            // Scan each root on background thread with progress updates
            foreach (var (label, path) in roots)
            {
                currentRoot++;
                if (SweepStatusText != null)
                    SweepStatusText.Text = $"Status: scanning {label}... ({currentRoot}/{totalRoots})";

                // Run the heavy file enumeration on background thread
                var entries = await Task.Run(() => ScanSweepRootAsync(label, path, cutoff, now, interestingExts));
                _sweepEntries.AddRange(entries);
            }

            // Deep services + drivers scan
            if (SweepStatusText != null)
                SweepStatusText.Text = "Status: scanning services and drivers...";

            var serviceEntries = await Task.Run(() => RunSweepServicesAndDriversAsync());
            _sweepEntries.AddRange(serviceEntries);

            // Apply focus matching and temporal clustering
            if (SweepStatusText != null)
                SweepStatusText.Text = "Status: applying clustering analysis...";

            ApplyFocusAndClustering();

            BindSweepResults();

            // Update summary panel counts
            UpdateSweepSummaryCounts();

            int total = _sweepEntries.Count;
            int flagged = _sweepEntries.Count(entry =>
                entry.Severity.Equals("HIGH", StringComparison.OrdinalIgnoreCase) ||
                entry.Severity.Equals("MEDIUM", StringComparison.OrdinalIgnoreCase));
            int clustered = _sweepEntries.Count(e => e.IsFocusHit || e.IsTimeCluster);

            if (SweepStatusText != null)
                SweepStatusText.Text = $"Status: sweep complete – {total} item(s), {flagged} MED/HIGH, {clustered} cluster hit(s).";

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
        finally
        {
            // Re-enable button
            if (sender is Button btn2)
                btn2.IsEnabled = true;
        }
    }

    /// <summary>
    /// Async-friendly version of ScanSweepRoot that returns entries instead of adding to shared list.
    /// </summary>
    private List<SweepEntry> ScanSweepRootAsync(
        string label,
        string rootPath,
        DateTime cutoff,
        DateTime now,
        HashSet<string> interestingExts)
    {
        var entries = new List<SweepEntry>();

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

                entries.Add(entry);
            }
        }
        catch (Exception ex)
        {
            entries.Add(new SweepEntry
            {
                Category = "Error",
                Severity = "LOW",
                Source   = label,
                Reason   = ex.Message
            });
        }

        return entries;
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

        // 4) Show cluster hits only (focus match, time cluster, or folder cluster)
        if (SweepShowClusterHitsOnlyCheckBox?.IsChecked == true)
        {
            source = source.Where(entry =>
                entry.IsFocusHit || entry.IsTimeCluster || entry.IsFolderCluster);
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

    private void SweepClusterWindowCombo_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        // Re-apply clustering with the new window and rebind
        if (_sweepEntries.Count > 0)
        {
            ApplyFocusAndClustering();
            UpdateSweepSummaryCounts();
            BindSweepResults();
        }
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

    // ---- Focus and temporal clustering ----

    /// <summary>
    /// Apply focus matching and temporal clustering to sweep entries.
    /// - Focus match: entry name/path contains a focus target term
    /// - Time cluster: entry was modified within the configurable window of a focus target's timestamp
    /// </summary>
    private void ApplyFocusAndClustering()
    {
        // Reset all clustering flags first (important when re-applying with new window)
        foreach (var entry in _sweepEntries)
        {
            entry.IsFocusHit = false;
            entry.IsTimeCluster = false;
            entry.IsFolderCluster = false;
            entry.ClusterTarget = string.Empty;
        }

        var focusTargets = CaseManager.GetFocusTargets();
        if (focusTargets.Count == 0)
            return;

        // Build a list of focus target timestamps (for files that exist)
        var focusTimestamps = new List<(string Target, DateTime Timestamp)>();

        foreach (var target in focusTargets)
        {
            // If the focus target is a file path, get its timestamp
            if (File.Exists(target))
            {
                try
                {
                    DateTime modified = File.GetLastWriteTime(target);
                    focusTimestamps.Add((target, modified));
                }
                catch { }
            }
            // Also check if it's a directory
            else if (Directory.Exists(target))
            {
                try
                {
                    DateTime modified = Directory.GetLastWriteTime(target);
                    focusTimestamps.Add((target, modified));
                }
                catch { }
            }
        }

        // Clustering window: configurable via dropdown (default ±2 hours)
        TimeSpan clusterWindow = GetClusterWindow();

        foreach (var entry in _sweepEntries)
        {
            // Check for direct focus term match (name or path contains focus target)
            foreach (var target in focusTargets)
            {
                if (!string.IsNullOrEmpty(entry.Name) &&
                    entry.Name.Contains(target, StringComparison.OrdinalIgnoreCase))
                {
                    entry.IsFocusHit = true;
                    entry.ClusterTarget = target;
                    break;
                }

                if (!string.IsNullOrEmpty(entry.Path) &&
                    entry.Path.Contains(target, StringComparison.OrdinalIgnoreCase))
                {
                    entry.IsFocusHit = true;
                    entry.ClusterTarget = target;
                    break;
                }
            }

            // Check for temporal clustering (if entry has a modified time)
            if (!entry.IsFocusHit && entry.Modified.HasValue)
            {
                foreach (var (target, timestamp) in focusTimestamps)
                {
                    TimeSpan diff = (entry.Modified.Value - timestamp).Duration();
                    if (diff <= clusterWindow)
                    {
                        entry.IsTimeCluster = true;
                        entry.ClusterTarget = Path.GetFileName(target);
                        break;
                    }
                }
            }

            // Check for folder clustering (same directory tree as a focus target)
            if (!entry.IsFocusHit && !entry.IsTimeCluster && !string.IsNullOrEmpty(entry.Path))
            {
                foreach (var target in focusTargets)
                {
                    if (File.Exists(target) || Directory.Exists(target))
                    {
                        string? focusDir = File.Exists(target)
                            ? Path.GetDirectoryName(target)
                            : target;

                        if (!string.IsNullOrEmpty(focusDir) &&
                            entry.Path.StartsWith(focusDir, StringComparison.OrdinalIgnoreCase))
                        {
                            entry.IsFolderCluster = true;
                            entry.ClusterTarget = Path.GetFileName(target);
                            break;
                        }
                    }
                }
            }
        }
    }

    /// <summary>
    /// Update the summary panel counts for quick triage.
    /// </summary>
    private void UpdateSweepSummaryCounts()
    {
        try
        {
            int highCount = _sweepEntries.Count(e =>
                e.Severity.Equals("HIGH", StringComparison.OrdinalIgnoreCase));
            if (SweepHighCount != null)
                SweepHighCount.Text = highCount.ToString();

            int mediumCount = _sweepEntries.Count(e =>
                e.Severity.Equals("MEDIUM", StringComparison.OrdinalIgnoreCase));
            if (SweepMediumCount != null)
                SweepMediumCount.Text = mediumCount.ToString();

            int lowCount = _sweepEntries.Count(e =>
                e.Severity.Equals("LOW", StringComparison.OrdinalIgnoreCase));
            if (SweepLowCount != null)
                SweepLowCount.Text = lowCount.ToString();

            int focusCount = _sweepEntries.Count(e => e.IsFocusHit);
            if (SweepFocusMatchCount != null)
                SweepFocusMatchCount.Text = focusCount.ToString();

            int timeClusterCount = _sweepEntries.Count(e => e.IsTimeCluster);
            if (SweepTimeClusterCount != null)
                SweepTimeClusterCount.Text = timeClusterCount.ToString();

            // Update focus targets display with timestamps
            UpdateFocusTargetsDisplay();
        }
        catch
        {
            // Summary updates should never break UI
        }
    }

    /// <summary>
    /// Get the clustering time window from the dropdown.
    /// </summary>
    private TimeSpan GetClusterWindow()
    {
        int index = SweepClusterWindowCombo?.SelectedIndex ?? 1;
        return index switch
        {
            0 => TimeSpan.FromHours(1),
            1 => TimeSpan.FromHours(2),
            2 => TimeSpan.FromHours(4),
            3 => TimeSpan.FromHours(8),
            _ => TimeSpan.FromHours(2)
        };
    }

    /// <summary>
    /// Update the focus targets text with timestamps.
    /// </summary>
    private void UpdateFocusTargetsDisplay()
    {
        if (SweepFocusTargetsText == null)
            return;

        var focusTargets = CaseManager.GetFocusTargets();
        if (focusTargets.Count == 0)
        {
            SweepFocusTargetsText.Text = "Focus targets: (none set – set focus in Hunt tab first)";
            return;
        }

        var parts = new List<string>();
        foreach (var target in focusTargets)
        {
            string display = target;
            DateTime? timestamp = null;

            // Try to get timestamp for file or directory
            if (File.Exists(target))
            {
                try
                {
                    timestamp = File.GetLastWriteTime(target);
                    display = $"{Path.GetFileName(target)} ({timestamp:yyyy-MM-dd HH:mm})";
                }
                catch { }
            }
            else if (Directory.Exists(target))
            {
                try
                {
                    timestamp = Directory.GetLastWriteTime(target);
                    display = $"{Path.GetFileName(target)} ({timestamp:yyyy-MM-dd HH:mm})";
                }
                catch { }
            }
            else
            {
                // Just a keyword, no timestamp
                display = target;
            }

            parts.Add(display);
        }

        SweepFocusTargetsText.Text = $"Focus targets: {string.Join(" | ", parts)}";
    }

    // ---- Button handlers ----

    private void SweepAddToFocusButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (SweepResultsList?.SelectedItem is not SweepEntry entry)
        {
            if (SweepStatusText != null)
                SweepStatusText.Text = "Status: select an item to add to focus.";
            return;
        }

        // Add the file name (or path if no name) to focus
        string target = !string.IsNullOrEmpty(entry.Name) ? entry.Name : entry.Path;

        if (string.IsNullOrWhiteSpace(target))
        {
            if (SweepStatusText != null)
                SweepStatusText.Text = "Status: selected item has no name or path.";
            return;
        }

        CaseManager.SetFocusTarget(target, "Sweep");

        if (SweepStatusText != null)
            SweepStatusText.Text = $"Status: added '{target}' to case focus.";

        // Log the action
        try
        {
            CaseManager.AddEvent(
                tab: "Sweep",
                action: "Added to focus from sweep",
                severity: "INFO",
                target: target,
                details: $"Path: {entry.Path}");

            UpdateDashboardCaseSummary();
            RefreshCaseTab();
        }
        catch { }
    }

    private void SweepOpenLocationButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (SweepResultsList?.SelectedItem is not SweepEntry entry)
        {
            if (SweepStatusText != null)
                SweepStatusText.Text = "Status: select an item to open its location.";
            return;
        }

        if (string.IsNullOrEmpty(entry.Path))
        {
            if (SweepStatusText != null)
                SweepStatusText.Text = "Status: selected item has no path.";
            return;
        }

        try
        {
            string? folder = File.Exists(entry.Path)
                ? Path.GetDirectoryName(entry.Path)
                : (Directory.Exists(entry.Path) ? entry.Path : null);

            if (!string.IsNullOrEmpty(folder) && Directory.Exists(folder))
            {
                using var process = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = folder,
                    UseShellExecute = true
                });

                if (SweepStatusText != null)
                    SweepStatusText.Text = $"Status: opened {folder}";
            }
            else
            {
                if (SweepStatusText != null)
                    SweepStatusText.Text = "Status: folder not found.";
            }
        }
        catch (Exception ex)
        {
            if (SweepStatusText != null)
                SweepStatusText.Text = $"Status: error opening location – {ex.Message}";
        }
    }

    private void SweepAddToCaseButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (SweepResultsList?.SelectedItem is not SweepEntry entry)
        {
            if (SweepStatusText != null)
                SweepStatusText.Text = "Status: select an item to add to case.";
            return;
        }

        try
        {
            // Build details string with all relevant info
            var details = new List<string>();

            if (!string.IsNullOrEmpty(entry.Path))
                details.Add($"Path: {entry.Path}");
            if (entry.Modified.HasValue)
                details.Add($"Modified: {entry.Modified.Value:yyyy-MM-dd HH:mm:ss}");
            if (!string.IsNullOrEmpty(entry.Reason))
                details.Add($"Reason: {entry.Reason}");
            if (!string.IsNullOrEmpty(entry.Source))
                details.Add($"Source: {entry.Source}");
            if (entry.HasClusterIndicator)
                details.Add($"Cluster: {entry.ClusterIndicator} ({entry.ClusterTarget})");

            CaseManager.AddEvent(
                tab: "Sweep",
                action: "Item flagged for case",
                severity: entry.Severity.ToUpperInvariant() == "HIGH" ? "WARN" : "INFO",
                target: entry.Name,
                details: string.Join("; ", details));

            if (SweepStatusText != null)
                SweepStatusText.Text = $"Status: '{entry.Name}' added to case log.";

            UpdateDashboardCaseSummary();
            RefreshCaseTab();
        }
        catch (Exception ex)
        {
            if (SweepStatusText != null)
                SweepStatusText.Text = $"Status: error adding to case – {ex.Message}";
        }
    }

    private void SweepInvestigateButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (SweepResultsList?.SelectedItem is not SweepEntry entry)
        {
            if (SweepStatusText != null)
                SweepStatusText.Text = "Status: select an item to investigate.";
            return;
        }

        if (string.IsNullOrEmpty(entry.Path) || !File.Exists(entry.Path))
        {
            if (SweepStatusText != null)
                SweepStatusText.Text = "Status: file not found or path is empty.";
            return;
        }

        try
        {
            // Calculate SHA256 hash
            string hash;
            using (var sha256 = System.Security.Cryptography.SHA256.Create())
            using (var stream = File.OpenRead(entry.Path))
            {
                byte[] hashBytes = sha256.ComputeHash(stream);
                hash = BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
            }

            // Get file info
            var fileInfo = new FileInfo(entry.Path);
            long sizeKb = fileInfo.Length / 1024;

            // Update status with hash (easy to copy)
            if (SweepStatusText != null)
                SweepStatusText.Text = $"SHA256: {hash} | Size: {sizeKb} KB | Created: {fileInfo.CreationTime:yyyy-MM-dd HH:mm}";

            // Open VirusTotal search with the hash
            string vtUrl = $"https://www.virustotal.com/gui/search/{hash}";
            using var vtProcess = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = vtUrl,
                UseShellExecute = true
            });

            // Log the investigation to case
            CaseManager.AddEvent(
                tab: "Sweep",
                action: "File investigated",
                severity: "INFO",
                target: entry.Name,
                details: $"SHA256: {hash}; Size: {sizeKb} KB; VirusTotal lookup opened");

            UpdateDashboardCaseSummary();
            RefreshCaseTab();
        }
        catch (Exception ex)
        {
            if (SweepStatusText != null)
                SweepStatusText.Text = $"Status: error investigating – {ex.Message}";
        }
    }

    // ----------------------------
    // SWEEP – Add to Cleanup Queue
    // ----------------------------
    private void SweepAddToCleanupButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (SweepResultsList?.SelectedItem is not SweepEntry entry)
        {
            if (SweepStatusText != null)
                SweepStatusText.Text = "Status: select an item to add to cleanup queue.";
            return;
        }

        // Check if already in queue
        if (CaseManager.IsInCleanupQueue(entry.Path))
        {
            if (SweepStatusText != null)
                SweepStatusText.Text = $"Status: {entry.Name} is already in the cleanup queue.";
            return;
        }

        // Determine item type and action based on Category
        string itemType = entry.Category.ToLowerInvariant() switch
        {
            "service" => "Service",
            "driver" => "Service", // Drivers are handled through service registry
            _ => "File"
        };

        string action = itemType switch
        {
            "Service" => "Disable",
            _ => "Quarantine"
        };

        var cleanupItem = new CleanupItem
        {
            ItemType = itemType,
            Name = entry.Name,
            OriginalPath = entry.Path,
            SourceTab = "Sweep",
            Severity = entry.Severity,
            Reason = entry.Reason,
            Action = action
        };

        CaseManager.AddToCleanupQueue(cleanupItem);

        if (SweepStatusText != null)
            SweepStatusText.Text = $"Status: {entry.Name} added to cleanup queue ({action}).";
    }
}

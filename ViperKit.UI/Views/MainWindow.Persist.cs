using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Microsoft.Win32;
using ViperKit.UI;
using ViperKit.UI.Models;

namespace ViperKit.UI.Views;

public partial class MainWindow
{
    // In-memory list of persistence entries for the current scan
    private readonly List<PersistItem> _persistItems = new();

        // Persist filter toggles – we track state here instead of relying on XAML fields
        private bool _persistShowCheckOnly;
        private bool _persistFilterByFocus;


    // ----------------------------
    // PERSIST – main entry point
    // ----------------------------
    private void PersistRunButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (PersistStatusText != null)
            PersistStatusText.Text = "Status: scanning common persistence locations...";

        if (PersistResultsList != null)
            PersistResultsList.ItemsSource = Array.Empty<PersistItem>();

        _persistItems.Clear();

        try
        {
            // 1) Registry Run / RunOnce – 32/64-bit, HKCU + HKLM
            CollectRunKeyAutoruns();

            // 2) Winlogon Shell / Userinit
            CollectWinlogonPersistence();

            // 3) Image File Execution Options (IFEO) debugger hijacks
            CollectImageFileExecutionOptionsPersistence();

            // 4) AppInit DLLs (process-wide DLL injection)
            CollectAppInitDllsPersistence();

            // 5) Startup folders – per-user + all users
            CollectStartupFolderAutoruns();

            // 6) Services + drivers – HKLM\SYSTEM\CurrentControlSet\Services
            CollectServiceAndDriverPersistence();

            // 7) Scheduled tasks
            CollectScheduledTasks();

            // 8) PowerShell profiles
            CollectPowerShellProfiles();

            // Count how many items are flagged as CHECK
            int flaggedCount = _persistItems.Count(p =>
                p.Risk.StartsWith("CHECK", StringComparison.OrdinalIgnoreCase));

            // NEW: count high-signal hotspots for triage view
            int hotspotCount = _persistItems.Count(IsHighSignalPersistItem);

            // Bind into UI ListBox.
            BindPersistResults();

            // Update summary panel counts
            UpdatePersistSummaryCounts();

            if (PersistStatusText != null)
            {
                PersistStatusText.Text =
                    $"Status: found {_persistItems.Count} persistence entries; " +
                    $"{flaggedCount} marked 'CHECK'; {hotspotCount} high-signal hotspot(s).";
            }

            // JSON log of this snapshot.
            try
            {
               JsonLog.Append("persist", new
                {
                    Timestamp    = DateTime.Now,
                    Host         = Environment.MachineName,
                    User         = Environment.UserName,
                    TotalEntries = _persistItems.Count,
                    FlaggedCount = flaggedCount,
                    HotspotCount = hotspotCount,   // <— add this line
                    Entries      = _persistItems.Select(p => new
                    {
                        p.Source,
                        p.LocationType,
                        p.Name,
                        p.Path,
                        p.RegistryPath,
                        p.Risk,
                        p.Reason,
                        p.MitreTechnique,
                        p.Publisher
                    }).ToArray()
                });
            }
            catch
            {
                // Logging should never break UI
            }

            // Case log summary
            try
            {
                var summaryTarget = _persistItems.Count == 0
                    ? "No persistence entries found."
                    : $"Entries: {_persistItems.Count}, CHECK: {flaggedCount}, Hotspots: {hotspotCount}";

                var summaryDetails = _persistItems.Count == 0
                    ? "No persistence entries found in any monitored location."
                    : $"{flaggedCount} entry/entries marked CHECK; {hotspotCount} high-signal hotspot(s).";

                CaseManager.AddEvent(
                    tab: "Persist",
                    action: "Persistence scan completed",
                    severity: flaggedCount > 0 ? "WARN" : "INFO",
                    target: summaryTarget,
                    details: summaryDetails);
            }
            catch
            {
                // Case logging must not crash UI
            }

        }
        catch (Exception ex)
        {
            if (PersistStatusText != null)
                PersistStatusText.Text = $"Status: error running persistence scan – {ex.Message}";

            // Record a failure entry so something shows up in the list
            _persistItems.Add(new PersistItem
            {
                Source         = "Persist",
                LocationType   = "Error",
                Name           = "Persistence scan failed",
                Path           = string.Empty,
                RegistryPath   = string.Empty,
                Risk           = "CHECK – scan error",
                Reason         = ex.Message,
                MitreTechnique = string.Empty
            });

            BindPersistResults();

            try
            {
                CaseManager.AddEvent(
                    tab: "Persist",
                    action: "Persistence scan error",
                    severity: "WARN",
                    target: "(scan failed)",
                    details: ex.Message);
            }
            catch
            {
            }
        }
    }


    // Central place to apply all Persist filters
    private void BindPersistResults()
    {
        if (PersistResultsList == null)
            return;

        // Start from the full set
        IEnumerable<PersistItem> query = _persistItems;

        // 1) "Show only CHECK" toggle
        if (_persistShowCheckOnly)
        {
            query = query.Where(p =>
                !string.IsNullOrEmpty(p.Risk) &&
                p.Risk.StartsWith("CHECK", StringComparison.OrdinalIgnoreCase));
        }

        // 2) Location filter (All / Registry / Startup / Services / Tasks)
        if (PersistLocationFilterCombo != null && PersistLocationFilterCombo.SelectedIndex > 0)
        {
            switch (PersistLocationFilterCombo.SelectedIndex)
            {
                case 1: // Registry autoruns
                    query = query.Where(p =>
                        p.LocationType.StartsWith("Autorun (Registry",
                            StringComparison.OrdinalIgnoreCase));
                    break;

                case 2: // Startup folders
                    query = query.Where(p =>
                        p.LocationType.StartsWith("Startup folder",
                            StringComparison.OrdinalIgnoreCase));
                    break;

                case 3: // Services / drivers
                    query = query.Where(p =>
                        p.LocationType.StartsWith("Service",
                            StringComparison.OrdinalIgnoreCase) ||
                        p.LocationType.StartsWith("Driver",
                            StringComparison.OrdinalIgnoreCase));
                    break;

                case 4: // Scheduled tasks
                    query = query.Where(p =>
                        p.LocationType.StartsWith("Scheduled task",
                            StringComparison.OrdinalIgnoreCase));
                    break;
            }
        }

        // 3) Risk / triage mode (All severities / High-signal / CHECK / Notes&OK)
        if (PersistRiskFilterCombo != null)
        {
            switch (PersistRiskFilterCombo.SelectedIndex)
            {
                case 1: // High-signal hotspots
                    query = query.Where(IsHighSignalPersistItem);
                    break;

                case 2: // CHECK only
                    query = query.Where(p =>
                        !string.IsNullOrEmpty(p.Risk) &&
                        p.Risk.StartsWith("CHECK", StringComparison.OrdinalIgnoreCase));
                    break;

                case 3: // Notes & OK
                    query = query.Where(p =>
                        (!string.IsNullOrEmpty(p.Risk) &&
                        (p.Risk.StartsWith("NOTE", StringComparison.OrdinalIgnoreCase) ||
                        p.Risk.StartsWith("OK",   StringComparison.OrdinalIgnoreCase))));
                    break;
                // 0 = All severities
            }
        }

        // 4) Text search across name / path / source / reason
        string? term = PersistSearchTextBox?.Text;
        if (!string.IsNullOrWhiteSpace(term))
        {
            term = term.Trim();

            query = query.Where(p =>
                (!string.IsNullOrEmpty(p.Name)   && p.Name.Contains(term, StringComparison.OrdinalIgnoreCase)) ||
                (!string.IsNullOrEmpty(p.Path)   && p.Path.Contains(term, StringComparison.OrdinalIgnoreCase)) ||
                (!string.IsNullOrEmpty(p.Source) && p.Source.Contains(term, StringComparison.OrdinalIgnoreCase)) ||
                (!string.IsNullOrEmpty(p.Reason) && p.Reason.Contains(term, StringComparison.OrdinalIgnoreCase)));
        }

        // 5) Global case focus – union of ALL focus terms, behind a toggle
        var focusTerms = CaseManager.GetFocusTargets();
        bool hasFocusTerms = focusTerms.Count > 0;

        // Mark items for visual highlight (even when the filter toggle is off)
        foreach (var item in _persistItems)
        {
            item.IsFocusHit = hasFocusTerms && MatchesCaseFocus(item, focusTerms);
        }

        // Compare against baseline if one exists - mark new items
        if (CaseManager.HasBaseline)
        {
            CaseManager.CompareToBaseline(_persistItems);
        }
        else
        {
            // No baseline - clear any previous markers
            foreach (var item in _persistItems)
            {
                item.IsNewSinceBaseline = false;
            }
        }

        bool filterByFocus = _persistFilterByFocus && hasFocusTerms;

        if (filterByFocus)
        {
            // When the toggle is on, only show actual focus hits
            query = query.Where(p => p.IsFocusHit);
        }

        var results = query.ToList();
        PersistResultsList.ItemsSource = results;

        // Status when focus is on but nothing matched
        if (PersistStatusText != null &&
            filterByFocus &&
            focusTerms.Count > 0 &&
            results.Count == 0 &&
            _persistItems.Count > 0)
        {
            PersistStatusText.Text =
                "Status: no persistence hits for current case focus – run Sweep and add related items to focus.";
        }
    }

    // Update the summary panel counts for quick triage
    private void UpdatePersistSummaryCounts()
    {
        try
        {
            // High-signal count
            int highSignal = _persistItems.Count(IsHighSignalPersistItem);
            if (PersistHighSignalCount != null)
                PersistHighSignalCount.Text = highSignal.ToString();

            // CHECK count
            int checkCount = _persistItems.Count(p =>
                !string.IsNullOrEmpty(p.Risk) &&
                p.Risk.StartsWith("CHECK", StringComparison.OrdinalIgnoreCase));
            if (PersistCheckCount != null)
                PersistCheckCount.Text = checkCount.ToString();

            // NOTE count
            int noteCount = _persistItems.Count(p =>
                !string.IsNullOrEmpty(p.Risk) &&
                p.Risk.StartsWith("NOTE", StringComparison.OrdinalIgnoreCase));
            if (PersistNoteCount != null)
                PersistNoteCount.Text = noteCount.ToString();

            // OK count
            int okCount = _persistItems.Count(p =>
                !string.IsNullOrEmpty(p.Risk) &&
                p.Risk.StartsWith("OK", StringComparison.OrdinalIgnoreCase));
            if (PersistOkCount != null)
                PersistOkCount.Text = okCount.ToString();

            // Focus match count
            int focusCount = _persistItems.Count(p => p.IsFocusHit);
            if (PersistFocusMatchCount != null)
                PersistFocusMatchCount.Text = focusCount.ToString();
        }
        catch
        {
            // Summary panel updates should never break the UI
        }
    }

    // Both Persist checkboxes (Show CHECK only / Filter by focus) land here
    private void PersistFilterCheckBox_OnChanged(object? sender, RoutedEventArgs e)
    {
        if (sender is CheckBox cb)
        {
            var label     = cb.Content?.ToString() ?? string.Empty;
            var isChecked = cb.IsChecked == true;

            // Be tolerant of minor wording changes – look for key phrases instead of exact match
            if (label.IndexOf("check", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                _persistShowCheckOnly = isChecked;
            }
            else if (label.IndexOf("focus", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                _persistFilterByFocus = isChecked;
            }
        }

        BindPersistResults();
    }


    private void PersistLocationFilterCombo_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        BindPersistResults();
    }

    private void PersistSearchTextBox_OnTextChanged(object? sender, TextChangedEventArgs e)
    {
        BindPersistResults();
    }

    private void PersistRiskFilterCombo_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        BindPersistResults();
    }


    // Investigate button
    private void PersistOpenSelectedButton_OnClick(object? sender, RoutedEventArgs e)
    {
        var item = PersistResultsList?.SelectedItem as PersistItem;
        if (item == null)
            return;

        OpenPersistItem(item);
    }

    // Double-click on list item
    private void PersistResultsList_OnDoubleTapped(object? sender, RoutedEventArgs e)
    {
        var item = PersistResultsList?.SelectedItem as PersistItem;
        if (item == null)
            return;

        OpenPersistItem(item);
    }

    // Copy the currently visible (filtered) Persist entries to clipboard
    private async void PersistCopyResultsButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (PersistResultsList?.ItemsSource is not IEnumerable<PersistItem> items)
            return;

        var list = items.ToList();
        if (list.Count == 0)
        {
            if (PersistStatusText != null)
                PersistStatusText.Text = "Status: nothing to copy (no entries match current filters).";
            return;
        }

        var sb = new StringBuilder();
        foreach (var item in list)
        {
            sb.AppendLine(item.ToString());
            sb.AppendLine(new string('-', 80));
        }

        var text = sb.ToString().TrimEnd();

        try
        {
            var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
            if (clipboard != null)
            {
                await clipboard.SetTextAsync(text);
                if (PersistStatusText != null)
                    PersistStatusText.Text = $"Status: copied {list.Count} persistence entries to clipboard.";
            }
            else
            {
                if (PersistStatusText != null)
                    PersistStatusText.Text = "Status: clipboard not available.";
            }
        }
        catch (Exception ex)
        {
            if (PersistStatusText != null)
                PersistStatusText.Text = $"Status: failed to copy results – {ex.Message}";
        }
    }

    // Save the currently visible (filtered) Persist entries to a text snapshot on disk
    private void PersistSaveResultsButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (PersistResultsList?.ItemsSource is not IEnumerable<PersistItem> items)
            return;

        var list = items.ToList();
        if (list.Count == 0)
        {
            if (PersistStatusText != null)
                PersistStatusText.Text = "Status: nothing to save (no entries match current filters).";
            return;
        }

        var sb = new StringBuilder();
        foreach (var item in list)
        {
            sb.AppendLine(item.ToString());
            sb.AppendLine(new string('-', 80));
        }

        var text = sb.ToString().TrimEnd();

        try
        {
            // Folder under CommonDocuments – same idea as blueprint: predictable path, per-tab snapshots
            var baseDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonDocuments),
                "ViperKit",
                "Persist");

            Directory.CreateDirectory(baseDir);

            var fileName = $"persist-{DateTime.Now:yyyyMMdd-HHmmss}.txt";
            var fullPath = Path.Combine(baseDir, fileName);

            File.WriteAllText(fullPath, text, Encoding.UTF8);

            if (PersistStatusText != null)
                PersistStatusText.Text = $"Status: saved persistence snapshot to {fullPath}.";

            // Case log entry for the snapshot action
            try
            {
                CaseManager.AddEvent(
                    tab: "Persist",
                    action: "Persistence snapshot saved",
                    severity: "INFO",
                    target: fullPath,
                    details: $"Entries: {list.Count}");
            }
            catch
            {
                // Case logging must not break UI
            }
        }
        catch (Exception ex)
        {
            if (PersistStatusText != null)
                PersistStatusText.Text = $"Status: failed to save snapshot – {ex.Message}";
        }
    }


    // -----------------------------------------
    // COLLECTORS – Run keys + Startup folders
    // -----------------------------------------
#pragma warning disable CA1416 // Windows-only APIs

    private void CollectRunKeyAutoruns()
    {
        // HKCU
        CollectRunKeyHive("HKCU Run", Registry.CurrentUser,
            @"Software\Microsoft\Windows\CurrentVersion\Run", "T1547.001");
        CollectRunKeyHive("HKCU RunOnce", Registry.CurrentUser,
            @"Software\Microsoft\Windows\CurrentVersion\RunOnce", "T1547.001");

        // HKCU RunServices / RunServicesOnce (legacy)
        CollectRunKeyHive("HKCU RunServices", Registry.CurrentUser,
            @"Software\Microsoft\Windows\CurrentVersion\RunServices", "T1547.001");
        CollectRunKeyHive("HKCU RunServicesOnce", Registry.CurrentUser,
            @"Software\Microsoft\Windows\CurrentVersion\RunServicesOnce", "T1547.001");

        // HKLM 64-bit
        CollectRunKeyHive("HKLM Run", Registry.LocalMachine,
            @"Software\Microsoft\Windows\CurrentVersion\Run", "T1547.001");
        CollectRunKeyHive("HKLM RunOnce", Registry.LocalMachine,
            @"Software\Microsoft\Windows\CurrentVersion\RunOnce", "T1547.001");

        // HKLM RunServices / RunServicesOnce
        CollectRunKeyHive("HKLM RunServices", Registry.LocalMachine,
            @"Software\Microsoft\Windows\CurrentVersion\RunServices", "T1547.001");
        CollectRunKeyHive("HKLM RunServicesOnce", Registry.LocalMachine,
            @"Software\Microsoft\Windows\CurrentVersion\RunServicesOnce", "T1547.001");

        // HKLM Wow6432Node (32-bit autoruns on 64-bit OS)
        CollectRunKeyHive("HKLM Wow6432Node Run", Registry.LocalMachine,
            @"Software\Wow6432Node\Microsoft\Windows\CurrentVersion\Run", "T1547.001");
        CollectRunKeyHive("HKLM Wow6432Node RunOnce", Registry.LocalMachine,
            @"Software\Wow6432Node\Microsoft\Windows\CurrentVersion\RunOnce", "T1547.001");
    }


    private void CollectRunKeyHive(string label, RegistryKey root, string subKeyPath, string mitreId)
    {
        try
        {
            using var key = root.OpenSubKey(subKeyPath);
            if (key == null)
                return;

            foreach (var valueName in key.GetValueNames())
            {
                object? value = null;
                try
                {
                    value = key.GetValue(valueName);
                }
                catch
                {
                    // Skip unreadable values
                }

                string? raw = value?.ToString();
                string expandedPath = raw ?? string.Empty;

                // Expand environment variables if present
                if (!string.IsNullOrWhiteSpace(expandedPath) &&
                    expandedPath.Contains('%'))
                {
                    expandedPath = Environment.ExpandEnvironmentVariables(expandedPath);
                }

                // Extract executable-ish path 
                string exePath = ExtractExecutablePath(expandedPath);
                // Who signed / shipped this
                string publisher = GetFilePublisherSafe(exePath);

                // Basic risk heuristic for help-desk
                string risk;
                string reason;

                if (string.IsNullOrWhiteSpace(exePath))
                {
                    risk   = "CHECK – empty or non-path value";
                    reason = "Registry Run entry without a clear executable path.";
                }
                else if (IsSuspiciousLocation(exePath))
                {
                    risk   = "CHECK – unusual location";
                    reason = $"Executable under user/temporary path: {exePath}";
                }
                else
                {
                    risk   = "OK";
                    reason = "Common location for startup program.";
                }

                _persistItems.Add(new PersistItem
                {
                    Source         = label,
                    LocationType   = "Autorun (Registry)",
                    Name           = valueName,
                    Path           = exePath,
                    RegistryPath   = $"{root.Name}\\{subKeyPath}",
                    Risk           = risk,
                    Reason         = reason,
                    MitreTechnique = mitreId,
                    Publisher      = publisher
                });
            }
        }
        catch
        {
            // Ignore hive-level errors; nothing we can safely do
        }
    }

    private void CollectStartupFolderAutoruns()
    {
        try
        {
            string? appData   = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
            string? commonApp = Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup);

            if (!string.IsNullOrWhiteSpace(appData) && Directory.Exists(appData))
            {
                CollectStartupFolder("Startup (current user)", appData, "T1547.009");
            }

            if (!string.IsNullOrWhiteSpace(commonApp) && Directory.Exists(commonApp))
            {
                CollectStartupFolder("Startup (all users)", commonApp, "T1547.009");
            }
        }
        catch
        {
            // Fail silently; worst case the tab just shows fewer entries
        }
    }

    private void CollectStartupFolder(string label, string folderPath, string mitreId)
    {
        try
        {
            foreach (var file in Directory.EnumerateFiles(folderPath, "*", SearchOption.TopDirectoryOnly))
            {
                string name    = Path.GetFileName(file);
                string publisher = GetFilePublisherSafe(file);
                string risk;
                string reason;

                if (IsSuspiciousLocation(file))
                {
                    risk   = "CHECK – unusual location";
                    reason = "Startup item under user/temporary path.";
                }
                else
                {
                    risk   = "OK";
                    reason = "Startup shortcut or executable in standard folder.";
                }

                _persistItems.Add(new PersistItem
                {
                    Source         = label,
                    LocationType   = "Startup folder",
                    Name           = name,
                    Path           = file,
                    RegistryPath   = folderPath,
                    Risk           = risk,
                    Reason         = reason,
                    MitreTechnique = mitreId,
                    Publisher      = publisher
                });
            }
        }
        catch
        {
            // Ignore folder-level errors
        }
    }

    /// <summary>
    /// Enumerate Windows services and drivers from
    /// HKLM\SYSTEM\CurrentControlSet\Services and add them as persistence entries.
    /// </summary>
    private void CollectServiceAndDriverPersistence()
    {
        try
        {
            using var servicesRoot = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services");
            if (servicesRoot == null)
                return;

            foreach (var serviceName in servicesRoot.GetSubKeyNames())
            {
                using var svcKey = servicesRoot.OpenSubKey(serviceName);
                if (svcKey == null)
                    continue;

                // --- Start type: only keep real persistence (Boot/System/Automatic) ---
                int startRaw;
                try
                {
                    startRaw = Convert.ToInt32(svcKey.GetValue("Start", 3));
                }
                catch
                {
                    startRaw = 3; // treat unknown as manual
                }

                if (startRaw > 2)
                {
                    // Skip Manual (3) and Disabled (4) – not true persistence for v1
                    continue;
                }

                string startType = startRaw switch
                {
                    0 => "Boot",
                    1 => "System",
                    2 => "Automatic",
                    _ => $"Start={startRaw}"
                };

                // --- Type: distinguish services vs drivers ---
                int typeRaw;
                try
                {
                    typeRaw = Convert.ToInt32(svcKey.GetValue("Type", 0));
                }
                catch
                {
                    typeRaw = 0;
                }

                // Very simple driver detection: type flags 1/2/4 are "driver-ish"
                bool isDriver = (typeRaw & 0x00000001) != 0 ||
                                (typeRaw & 0x00000002) != 0 ||
                                (typeRaw & 0x00000004) != 0;

                // --- ImagePath / binary location ---
                string rawImagePath = svcKey.GetValue("ImagePath") as string ?? string.Empty;
                string expandedPath = rawImagePath;

                if (!string.IsNullOrWhiteSpace(expandedPath) && expandedPath.Contains('%'))
                    expandedPath = Environment.ExpandEnvironmentVariables(expandedPath);

                // Re-use the same helper used for Run keys to peel out the EXE path
                string exePath       = ExtractExecutablePath(expandedPath);
                bool   existsOnDisk  = !string.IsNullOrWhiteSpace(exePath) && File.Exists(exePath);
                // Publisher for the binary backing the service/driver
                string publisher = GetFilePublisherSafe(exePath);

                // Risk heuristic: tone down noise from stale services
                string risk;

                if (string.IsNullOrWhiteSpace(exePath))
                {
                    // Unknown / grouped services (e.g. svchost groups) – don't scream
                    risk = "OK";
                }
                else if (!existsOnDisk)
                {
                    if (IsSuspiciousLocation(exePath))
                    {
                        // Missing AND in a weird place = worth a CHECK
                        risk = "CHECK – binary missing in suspicious path";
                    }
                    else
                    {
                        // Common case: leftover service from an uninstalled app
                        risk = "NOTE – binary missing (likely stale service entry)";
                    }
                }
                else if (IsSuspiciousLocation(exePath))
                {
                    risk = "CHECK – unusual location";
                }
                else
                {
                    risk = "OK";
                }


                // MITRE mapping: Windows service vs driver persistence
                string mitreId = isDriver ? "T1547.006" : "T1543.003";

                // Friendly display name (what techs see in services.msc)
                string displayName = svcKey.GetValue("DisplayName") as string ?? serviceName;

                var reasonBuilder = new StringBuilder();
                reasonBuilder.Append($"Start type: {startType}. ");

                if (string.IsNullOrWhiteSpace(rawImagePath))
                {
                    reasonBuilder.Append("No ImagePath set (may be driverless or misconfigured).");
                }
                else
                {
                    reasonBuilder.Append($"ImagePath: {rawImagePath}");
                    if (!string.Equals(rawImagePath, exePath, StringComparison.OrdinalIgnoreCase)
                        && !string.IsNullOrWhiteSpace(exePath))
                    {
                        reasonBuilder.Append($" (resolved: {exePath})");
                    }
                    reasonBuilder.Append('.');
                }

                _persistItems.Add(new PersistItem
                {
                    Source         = "Services/Drivers",
                    LocationType   = isDriver ? "Driver" : "Service",
                    Name           = displayName,
                    Path           = exePath,
                    RegistryPath   = svcKey.Name,
                    Risk           = risk,
                    Reason         = reasonBuilder.ToString(),
                    MitreTechnique = mitreId,
                    Publisher      = publisher
                });
            }
        }
        catch
        {
            // In v1 we silently skip a full failure here; Run/Startup still show up.
        }
    }

    // Inspect PowerShell profile scripts (Windows PowerShell + PowerShell 7) as persistence.
    private void CollectPowerShellProfiles()
    {
        try
        {
            var candidates = new List<(string Label, string Path)>();

            // Windows PowerShell – AllUsersAllHosts: %WINDIR%\System32\WindowsPowerShell\v1.0\profile.ps1
            string windowsDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            if (!string.IsNullOrWhiteSpace(windowsDir))
            {
                string allUsersWp = Path.Combine(
                    windowsDir,
                    "System32",
                    "WindowsPowerShell",
                    "v1.0",
                    "profile.ps1");

                candidates.Add(("PowerShell profile – AllUsersAllHosts (WindowsPowerShell)", allUsersWp));
            }

            // Current user – Documents\WindowsPowerShell\profile.ps1
            string myDocs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            if (!string.IsNullOrWhiteSpace(myDocs))
            {
                string cuWp = Path.Combine(myDocs, "WindowsPowerShell", "profile.ps1");
                candidates.Add(("PowerShell profile – CurrentUserAllHosts (WindowsPowerShell)", cuWp));

                // PowerShell 7 – CurrentUserAllHosts: Documents\PowerShell\profile.ps1
                string cuPwsh = Path.Combine(myDocs, "PowerShell", "profile.ps1");
                candidates.Add(("PowerShell 7 profile – CurrentUserAllHosts", cuPwsh));
            }

            // PowerShell 7 – AllUsersAllHosts: %ProgramFiles%\PowerShell\7\profile.ps1
            string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            if (!string.IsNullOrWhiteSpace(programFiles))
            {
                string allUsersPwsh = Path.Combine(programFiles, "PowerShell", "7", "profile.ps1");
                candidates.Add(("PowerShell 7 profile – AllUsersAllHosts", allUsersPwsh));
            }

            foreach (var (label, path) in candidates)
            {
                if (!File.Exists(path))
                    continue;

                string publisher = GetFilePublisherSafe(path);

                const string risk   = "CHECK – PowerShell profile script present";
                const string reason =
                    "PowerShell profile executes on PowerShell startup. Review script contents for malicious commands or persistence.";

                _persistItems.Add(new PersistItem
                {
                    Source         = label,
                    LocationType   = "PowerShell profile",
                    Name           = Path.GetFileName(path),
                    Path           = path,
                    RegistryPath   = string.Empty,
                    Risk           = risk,
                    Reason         = reason,
                    MitreTechnique = "T1546.013", // PowerShell profile
                    Publisher      = publisher
                });
            }
        }
        catch
        {
            // Silent fail – other persistence sources will still appear.
        }
    }

    //Enumerate WIndows Scheduled Tasks and add them as persistence entries
    private void CollectScheduledTasks()
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName               = "schtasks.exe",
                Arguments              = "/Query /FO CSV /V",
                UseShellExecute        = false,
                RedirectStandardOutput = true,
                RedirectStandardError  = false,
                CreateNoWindow         = true
            };

            using var proc = Process.Start(psi);
            if (proc == null)
                return;

            using var reader = proc.StandardOutput;

            string? headerLine = reader.ReadLine();
            if (string.IsNullOrWhiteSpace(headerLine))
                return;

            var headers = SplitCsvLine(headerLine).ToArray();
            if (headers.Length == 0)
                return;

            int idxTaskName   = Array.FindIndex(headers, h => string.Equals(h, "TaskName",   StringComparison.OrdinalIgnoreCase));
            int idxTaskToRun  = Array.FindIndex(headers, h => string.Equals(h, "Task To Run", StringComparison.OrdinalIgnoreCase));
            int idxSchedule   = Array.FindIndex(headers, h => string.Equals(h, "Schedule",    StringComparison.OrdinalIgnoreCase));
            int idxNextRun    = Array.FindIndex(headers, h => string.Equals(h, "Next Run Time", StringComparison.OrdinalIgnoreCase));

            if (idxTaskName < 0 || idxTaskToRun < 0)
                return; // critical fields missing

            string? line;
            while ((line = reader.ReadLine()) != null)
            {
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                var fields = SplitCsvLine(line).ToArray();
                if (fields.Length <= Math.Max(idxTaskName, idxTaskToRun))
                    continue;

                string taskName    = GetCsvField(fields, idxTaskName);
                string taskToRun   = GetCsvField(fields, idxTaskToRun);
                string schedule    = idxSchedule >= 0 ? GetCsvField(fields, idxSchedule) : string.Empty;
                string nextRunTime = idxNextRun >= 0 ? GetCsvField(fields, idxNextRun) : string.Empty;

                if (string.IsNullOrWhiteSpace(taskName))
                    continue;

                // Expand env vars; some tasks use %SystemRoot%, etc.
                if (!string.IsNullOrWhiteSpace(taskToRun) && taskToRun.Contains('%'))
                    taskToRun = Environment.ExpandEnvironmentVariables(taskToRun);

                string exePath      = ExtractExecutablePath(taskToRun);
                bool   existsOnDisk = !string.IsNullOrWhiteSpace(exePath) && File.Exists(exePath);
                string publisher = GetFilePublisherSafe(exePath);

                // Built-in Microsoft tasks usually live under \Microsoft\Windows\...
                bool isBuiltIn = taskName.StartsWith(@"\Microsoft\Windows\", StringComparison.OrdinalIgnoreCase);

                string risk;
                string reason;

                if (string.IsNullOrWhiteSpace(exePath))
                {
                    if (isBuiltIn)
                    {
                        risk   = "OK";
                        reason = "Built-in Windows scheduled task with no explicit action path.";
                    }
                    else
                    {
                        risk   = "CHECK – no action path";
                        reason = "Scheduled task without a clear executable/script path.";
                    }
                }
                else if (!existsOnDisk)
                {
                    if (!isBuiltIn && IsSuspiciousLocation(exePath))
                    {
                        risk   = "CHECK – binary missing in suspicious path";
                        reason = $"Task action points to missing file in suspicious location: {exePath}";
                    }
                    else
                    {
                        // Most of the noisy stuff ends up here
                        risk   = "NOTE – binary missing (likely stale task)";
                        reason = $"Task action points to a file that does not exist: {exePath}";
                    }
                }
                else if (!isBuiltIn && IsSuspiciousLocation(exePath))
                {
                    risk   = "CHECK – unusual location";
                    reason = $"Task action under user/temporary path: {exePath}";
                }
                else
                {
                    risk   = "OK";
                    reason = isBuiltIn
                        ? "Built-in Windows scheduled task."
                        : "Scheduled task in common location for system/third-party software.";
                }


                var reasonBuilder = new StringBuilder(reason);

                if (!string.IsNullOrWhiteSpace(schedule))
                    reasonBuilder.Append($" Schedule: {schedule}.");

                if (!string.IsNullOrWhiteSpace(nextRunTime))
                    reasonBuilder.Append($" Next run: {nextRunTime}.");

                _persistItems.Add(new PersistItem
                {
                    Source         = "Scheduled tasks",
                    LocationType   = "Scheduled task",
                    Name           = taskName,
                    Path           = exePath,
                    RegistryPath   = taskName, // task path, stored here to avoid a new field
                    Risk           = risk,
                    Reason         = reasonBuilder.ToString(),
                    MitreTechnique = "T1053.005",
                    Publisher      = publisher
                });
            }
        }
        catch
        {
            // In v1, fail silently – other persistence sources still populate the tab.
        }
    }


// Inspect Winlogon Shell / Userinit for logon-time persistence.

private void CollectWinlogonPersistence()
{
    try
    {
        using var key = Registry.LocalMachine.OpenSubKey(
            @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Winlogon");
        if (key == null)
            return;

        // Shell -------------------------------------------------
        string shellRaw = key.GetValue("Shell") as string ?? string.Empty;
        string shellExpanded = shellRaw;

        if (!string.IsNullOrWhiteSpace(shellExpanded) && shellExpanded.Contains('%'))
            shellExpanded = Environment.ExpandEnvironmentVariables(shellExpanded);

        string shellExe = ExtractExecutablePath(shellExpanded);
        string shellPublisher = GetFilePublisherSafe(shellExe);

        // Default-ish: "explorer.exe" or something ending in "\explorer.exe"
        bool shellLooksDefault =
            !string.IsNullOrWhiteSpace(shellExe) &&
            (shellExe.Equals("explorer.exe", StringComparison.OrdinalIgnoreCase) ||
             shellExe.EndsWith(@"\explorer.exe", StringComparison.OrdinalIgnoreCase));

        string shellRisk;
        string shellReason;

        if (string.IsNullOrWhiteSpace(shellRaw))
        {
            shellRisk   = "CHECK – Shell value missing";
            shellReason = "Winlogon Shell is empty or not set (may be tampered).";
        }
        else if (!shellLooksDefault)
        {
            shellRisk   = "CHECK – non-standard shell";
            shellReason = $"Winlogon Shell is set to: {shellRaw}";
        }
        else
        {
            shellRisk   = "OK";
            shellReason = "Shell points to the standard Windows Explorer shell.";
        }

        _persistItems.Add(new PersistItem
        {
            Source         = "Winlogon",
            LocationType   = "Autorun (Registry – Winlogon)",
            Name           = "Winlogon Shell",
            Path           = shellExe,
            RegistryPath   = @"HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Winlogon\Shell",
            Risk           = shellRisk,
            Reason         = shellReason,
            MitreTechnique = "T1547.004",
            Publisher      = shellPublisher
        });

        // Userinit ----------------------------------------------
        string userinitRaw = key.GetValue("Userinit") as string ?? string.Empty;
        string userinitExpanded = userinitRaw;

        if (!string.IsNullOrWhiteSpace(userinitExpanded) && userinitExpanded.Contains('%'))
            userinitExpanded = Environment.ExpandEnvironmentVariables(userinitExpanded);

        string userinitExe = ExtractExecutablePath(userinitExpanded);
        string userinitPublisher = GetFilePublisherSafe(userinitExe);

        // Default pattern: C:\Windows\system32\userinit.exe,
        bool userinitLooksDefault =
            !string.IsNullOrWhiteSpace(userinitRaw) &&
            userinitRaw.IndexOf("userinit.exe", StringComparison.OrdinalIgnoreCase) >= 0;

        string userinitRisk;
        string userinitReason;

        if (string.IsNullOrWhiteSpace(userinitRaw))
        {
            userinitRisk   = "CHECK – Userinit missing";
            userinitReason = "Winlogon Userinit value is empty or not set (may be tampered).";
        }
        else if (!userinitLooksDefault)
        {
            userinitRisk   = "CHECK – non-standard Userinit";
            userinitReason = $"Winlogon Userinit is set to: {userinitRaw}";
        }
        else
        {
            userinitRisk   = "OK";
            userinitReason = "Userinit matches standard Windows configuration.";
        }

        _persistItems.Add(new PersistItem
        {
            Source         = "Winlogon",
            LocationType   = "Autorun (Registry – Winlogon)",
            Name           = "Winlogon Userinit",
            Path           = userinitExe,
            RegistryPath   = @"HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Winlogon\Userinit",
            Risk           = userinitRisk,
            Reason         = userinitReason,
            MitreTechnique = "T1547.004",
            Publisher      = userinitPublisher
        });
    }
    catch
    {
        // Silent fail – other persistence sources will still appear.
    }
}

// Inspect Image File Execution Options (IFEO) for debugger hijacks.
private void CollectImageFileExecutionOptionsPersistence()
{
    try
    {
        using var ifeoRoot = Registry.LocalMachine.OpenSubKey(
            @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Image File Execution Options");
        if (ifeoRoot == null)
            return;

        foreach (var exeName in ifeoRoot.GetSubKeyNames())
        {
            using var exeKey = ifeoRoot.OpenSubKey(exeName);
            if (exeKey == null)
                continue;

            string debuggerRaw = exeKey.GetValue("Debugger") as string ?? string.Empty;
            if (string.IsNullOrWhiteSpace(debuggerRaw))
                continue; // no debugger set, ignore for v1

            string debuggerExpanded = debuggerRaw;
            if (debuggerExpanded.Contains('%'))
                debuggerExpanded = Environment.ExpandEnvironmentVariables(debuggerExpanded);

            string debuggerExe  = ExtractExecutablePath(debuggerExpanded);
            string publisher = GetFilePublisherSafe(debuggerExe);
            bool   existsOnDisk = !string.IsNullOrWhiteSpace(debuggerExe) && File.Exists(debuggerExe);

            // IFEO debugger is always worth a look for help-desk, so treat as CHECK by default
            string risk;
            string reason;

            if (!existsOnDisk)
            {
                risk   = "CHECK – IFEO debugger missing on disk";
                reason = $"IFEO Debugger for {exeName} points to {debuggerExe}, which does not exist.";
            }
            else if (IsSuspiciousLocation(debuggerExe))
            {
                risk   = "CHECK – IFEO debugger in unusual location";
                reason = $"IFEO Debugger for {exeName} points to {debuggerExe} under a user/temporary path.";
            }
            else
            {
                risk   = "CHECK – IFEO debugger configured";
                reason = $"IFEO Debugger is configured for {exeName}: {debuggerRaw}";
            }

            _persistItems.Add(new PersistItem
            {
                Source         = "IFEO",
                LocationType   = "Autorun (Registry – IFEO Debugger)",
                Name           = exeName,
                Path           = debuggerExe,
                RegistryPath   = exeKey.Name, // full IFEO subkey path
                Risk           = risk,
                Reason         = reason,
                MitreTechnique = "T1546.012",
                Publisher      = publisher
            });
        }
    }
    catch
    {
        // Silent fail; other categories still populate.
    }
}

/// <summary>
/// Inspect AppInit_DLLs / LoadAppInit_DLLs for process-wide DLL injection.
/// HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Windows
/// and the Wow6432Node equivalent.
/// </summary>
private void CollectAppInitDllsPersistence()
{
    try
    {
        // 64-bit view
        CollectAppInitDllsHive(
            "AppInit DLLs (64-bit)",
            Registry.LocalMachine,
            @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Windows");

        // 32-bit view on 64-bit systems
        CollectAppInitDllsHive(
            "AppInit DLLs (32-bit Wow6432Node)",
            Registry.LocalMachine,
            @"SOFTWARE\Wow6432Node\Microsoft\Windows NT\CurrentVersion\Windows");
    }
    catch
    {
        // If this whole section fails, other persistence sources still populate.
    }
}

private void CollectAppInitDllsHive(string label, RegistryKey root, string subKeyPath)
{
    try
    {
        using var key = root.OpenSubKey(subKeyPath);
        if (key == null)
            return;

        string dllListRaw  = key.GetValue("AppInit_DLLs") as string ?? string.Empty;
        string loadFlagRaw = key.GetValue("LoadAppInit_DLLs")?.ToString() ?? string.Empty;

        // If both are totally blank, there is nothing interesting here.
        if (string.IsNullOrWhiteSpace(dllListRaw) && string.IsNullOrWhiteSpace(loadFlagRaw))
            return;

        bool loadEnabled = false;
        if (int.TryParse(loadFlagRaw, out var loadVal))
            loadEnabled = loadVal != 0;

        // AppInit_DLLs can be a space- or semicolon-separated list of DLLs.
        var dlls = dllListRaw
            .Split(new[] { ' ', '\t', ';' }, StringSplitOptions.RemoveEmptyEntries)
            .ToArray();

        // If LoadAppInit_DLLs is enabled but DLL list is empty, still flag it as weird.
        if (dlls.Length == 0)
        {
            if (loadEnabled)
            {
                _persistItems.Add(new PersistItem
                {
                    Source         = label,
                    LocationType   = "Autorun (Registry – AppInit_DLLs)",
                    Name           = "AppInit_DLLs",
                    Path           = string.Empty,
                    RegistryPath   = $"{root.Name}\\{subKeyPath}",
                    Risk           = "CHECK – AppInit enabled with no DLLs",
                    Reason         = "LoadAppInit_DLLs is enabled but AppInit_DLLs is empty.",
                    MitreTechnique = "T1546.010"
                });
            }

            return;
        }

        foreach (var rawDll in dlls)
        {
            string expanded = rawDll;
            if (expanded.Contains('%'))
                expanded = Environment.ExpandEnvironmentVariables(expanded);

            // If the path is not rooted, assume System32 
            string fullPath = expanded;
            if (!Path.IsPathRooted(fullPath))
            {
                var systemDir = Environment.GetFolderPath(Environment.SpecialFolder.System);
                fullPath = Path.Combine(systemDir, fullPath);
            }

            bool exists = File.Exists(fullPath);
            string publisher = GetFilePublisherSafe(fullPath);

            string risk;
            var reasonBuilder = new StringBuilder();
            reasonBuilder.Append($"AppInit DLL: {rawDll}. ");

            if (!exists)
            {
                risk = "CHECK – AppInit DLL missing on disk";
                reasonBuilder.Append($"Resolved path {fullPath} does not exist. ");
            }
            else if (IsSuspiciousLocation(fullPath))
            {
                risk = "CHECK – AppInit DLL in unusual location";
                reasonBuilder.Append($"DLL located under user/temporary path: {fullPath}. ");
            }
            else
            {
                risk = "CHECK – AppInit DLL configured";
                reasonBuilder.Append($"DLL in common system location: {fullPath}. ");
            }

            if (loadEnabled)
                reasonBuilder.Append("LoadAppInit_DLLs is enabled.");
            else
                reasonBuilder.Append("LoadAppInit_DLLs is disabled (may not be active on this system).");

            _persistItems.Add(new PersistItem
            {
                Source         = label,
                LocationType   = "Autorun (Registry – AppInit_DLLs)",
                Name           = Path.GetFileName(rawDll),
                Path           = fullPath,
                RegistryPath   = $"{root.Name}\\{subKeyPath}",
                Risk           = risk,
                Reason         = reasonBuilder.ToString(),
                MitreTechnique = "T1546.010",
                Publisher      = publisher
            });
        }
    }
    catch
    {
        // Ignore hive-level errors; worst case, fewer entries show up.
    }
}

#pragma warning restore CA1416

    // -----------------------------------------
    // OPEN / INVESTIGATE helpers
    // -----------------------------------------
    private void OpenPersistItem(PersistItem item)
    {
        try
        {
            bool openedSomething = false;

            // Registry-backed entry → open regedit
            if (!string.IsNullOrWhiteSpace(item.RegistryPath) &&
                item.LocationType.StartsWith("Autorun (Registry", StringComparison.OrdinalIgnoreCase))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName        = "regedit.exe",
                    UseShellExecute = true
                });

                openedSomething = true;

                if (PersistStatusText != null)
                    PersistStatusText.Text = $"Status: opened Regedit – navigate to {item.RegistryPath}.";

                LogPersistInvestigation(item, "Regedit");
                return;
            }

            // File-backed entry → open Explorer at path/parent
            if (!string.IsNullOrWhiteSpace(item.Path))
            {
                string path = item.Path;

                if (File.Exists(path))
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName        = "explorer.exe",
                        Arguments       = $"/select,\"{path}\"",
                        UseShellExecute = true
                    });

                    openedSomething = true;

                    if (PersistStatusText != null)
                        PersistStatusText.Text = $"Status: opened Explorer at {path}.";
                }
                else if (Directory.Exists(path))
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName        = "explorer.exe",
                        Arguments       = $"\"{path}\"",
                        UseShellExecute = true
                    });

                    openedSomething = true;

                    if (PersistStatusText != null)
                        PersistStatusText.Text = $"Status: opened Explorer at {path}.";
                }

                if (openedSomething)
                {
                    LogPersistInvestigation(item, "Explorer");
                    return;
                }
            }

            // Scheduled task → open Task Scheduler
            if (item.LocationType.StartsWith("Scheduled task", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName        = "taskschd.msc",
                        UseShellExecute = true
                    });

                    if (PersistStatusText != null)
                        PersistStatusText.Text =
                            $"Status: opened Task Scheduler – locate task: {item.Name}.";

                    LogPersistInvestigation(item, "Task Scheduler");
                    return;
                }
                catch
                {
                    if (PersistStatusText != null)
                        PersistStatusText.Text =
                            "Status: failed to open Task Scheduler for this entry.";
                    // fall through to other options
                }
            }

            // Service / Driver → open Services.msc
            if (item.LocationType.StartsWith("Service", StringComparison.OrdinalIgnoreCase) ||
                item.LocationType.StartsWith("Driver",  StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName        = "services.msc",
                        UseShellExecute = true
                    });

                    if (PersistStatusText != null)
                    {
                        var nameText = string.IsNullOrWhiteSpace(item.Name)
                            ? "this entry"
                            : $"service/driver: {item.Name}";

                        PersistStatusText.Text =
                            $"Status: opened Services – locate {nameText}.";
                    }

                    LogPersistInvestigation(item, "Services.msc");
                    return;
                }
                catch
                {
                    if (PersistStatusText != null)
                        PersistStatusText.Text =
                            "Status: failed to open Services for this entry.";
                    // fall through to generic handling
                }
            }

            if (!openedSomething && PersistStatusText != null)
                PersistStatusText.Text = "Status: nothing to open for this entry.";
        }
        catch
        {
            if (PersistStatusText != null)
                PersistStatusText.Text = "Status: failed to open location for this entry.";
        }
    }

    private void PersistSetFocusButton_OnClick(object? sender, RoutedEventArgs e)
    {
        var selected = GetSelectedPersistItem();
        if (selected == null)
            return;

        // Prefer the file path if we have one
        var focus = selected.Path;

        // Fallbacks if Path is empty for some reason
        if (string.IsNullOrWhiteSpace(focus))
            focus = !string.IsNullOrWhiteSpace(selected.Name)
                ? selected.Name
                : selected.Source;

        if (string.IsNullOrWhiteSpace(focus))
            return;

        // Update global case focus
        CaseManager.SetFocusTarget(focus, "Persist");

        // Re-bind so MatchesFocus highlighting / filters update
        BindPersistResults();

        if (PersistStatusText != null)
            PersistStatusText.Text = "Status: case focus updated from Persist.";
    }

    private void PersistAddToCaseButton_OnClick(object? sender, RoutedEventArgs e)
    {
        var item = PersistResultsList?.SelectedItem as PersistItem;
        if (item == null)
        {
            if (PersistStatusText != null)
                PersistStatusText.Text = "Status: no entry selected to add to case.";
            return;
        }

        try
        {
            var severity = item.Risk.StartsWith("CHECK", StringComparison.OrdinalIgnoreCase)
                ? "WARN"
                : "INFO";

            var target = string.IsNullOrWhiteSpace(item.Name)
                ? "(unnamed entry)"
                : item.Name;

            var detailsBuilder = new StringBuilder();

            if (!string.IsNullOrWhiteSpace(item.LocationType))
                detailsBuilder.Append($"Location: {item.LocationType}. ");

            if (!string.IsNullOrWhiteSpace(item.Source))
                detailsBuilder.Append($"Source: {item.Source}. ");

            if (!string.IsNullOrWhiteSpace(item.Path))
                detailsBuilder.Append($"Path: {item.Path}. ");

            if (!string.IsNullOrWhiteSpace(item.RegistryPath))
                detailsBuilder.Append($"Registry: {item.RegistryPath}. ");

            if (!string.IsNullOrWhiteSpace(item.Risk))
                detailsBuilder.Append($"Risk: {item.Risk}. ");

            if (!string.IsNullOrWhiteSpace(item.Reason))
                detailsBuilder.Append($"Reason: {item.Reason}. ");

            if (!string.IsNullOrWhiteSpace(item.MitreTechnique))
                detailsBuilder.Append($"MITRE: {item.MitreTechnique}. ");

            if (!string.IsNullOrWhiteSpace(item.Publisher))
                detailsBuilder.Append($"Publisher: {item.Publisher}. ");

            CaseManager.AddEvent(
                tab: "Persist",
                action: "Persistence entry added to case",
                severity: severity,
                target: target,
                details: detailsBuilder.ToString());

            if (PersistStatusText != null)
                PersistStatusText.Text = "Status: added selected persistence entry to case log.";
        }
        catch (Exception ex)
        {
            if (PersistStatusText != null)
                PersistStatusText.Text = $"Status: failed to add entry to case – {ex.Message}";
        }
    }

    private void LogPersistInvestigation(PersistItem item, string via)
    {
        try
        {
            var severity = item.Risk.StartsWith("CHECK", StringComparison.OrdinalIgnoreCase)
                ? "WARN"
                : "INFO";

            var target = string.IsNullOrWhiteSpace(item.Name)
                ? "(unnamed entry)"
                : item.Name;

            var detailsBuilder = new StringBuilder();
            detailsBuilder.Append($"Via: {via}. ");

            if (!string.IsNullOrWhiteSpace(item.LocationType))
                detailsBuilder.Append($"Location: {item.LocationType}. ");

            if (!string.IsNullOrWhiteSpace(item.Source))
                detailsBuilder.Append($"Source: {item.Source}. ");

            if (!string.IsNullOrWhiteSpace(item.Path))
                detailsBuilder.Append($"Path: {item.Path}. ");

            if (!string.IsNullOrWhiteSpace(item.RegistryPath))
                detailsBuilder.Append($"Registry: {item.RegistryPath}. ");

            if (!string.IsNullOrWhiteSpace(item.Risk))
                detailsBuilder.Append($"Risk: {item.Risk}. ");

            if (!string.IsNullOrWhiteSpace(item.MitreTechnique))
                detailsBuilder.Append($"MITRE: {item.MitreTechnique}. ");

            if (!string.IsNullOrWhiteSpace(item.Publisher))
                detailsBuilder.Append($"Publisher: {item.Publisher}. ");

            CaseManager.AddEvent(
                tab: "Persist",
                action: "Investigated persistence entry",
                severity: severity,
                target: target,
                details: detailsBuilder.ToString());
        }
        catch
        {
            // Case logging must never break the UI.
        }
    }

    private bool MatchesCaseFocus(PersistItem p, IReadOnlyList<string> focusTerms)
    {
        if (p == null || focusTerms == null || focusTerms.Count == 0)
            return false;

        string name     = p.Name         ?? string.Empty;
        string path     = p.Path         ?? string.Empty;
        string regPath  = p.RegistryPath ?? string.Empty;
        string source   = p.Source       ?? string.Empty;
        string locType  = p.LocationType ?? string.Empty;

        foreach (var termRaw in focusTerms)
        {
            if (string.IsNullOrWhiteSpace(termRaw))
                continue;

            string term = termRaw.Trim();

            if (name.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                path.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                regPath.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                source.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                locType.Contains(term, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }


    // High-signal triage helper for the risk filter
    private bool IsHighSignalPersistItem(PersistItem p)
    {
        if (p == null)
            return false;

        string loc  = p.LocationType ?? string.Empty;
        string src  = p.Source       ?? string.Empty;
        string path = p.Path         ?? string.Empty;
        string risk = p.Risk         ?? string.Empty;

            // IFEO / Winlogon / AppInit_DLLs are always worth a look
        if (src.Equals("IFEO", StringComparison.OrdinalIgnoreCase))
            return true;

        if (src.Equals("Winlogon", StringComparison.OrdinalIgnoreCase))
            return true;

        if (loc.Contains("AppInit_DLLs", StringComparison.OrdinalIgnoreCase))
            return true;

        if (loc.Contains("PowerShell profile", StringComparison.OrdinalIgnoreCase))
            return true;

        // Autorun / Startup entries under user/AppData/Temp
        if (loc.StartsWith("Autorun", StringComparison.OrdinalIgnoreCase) ||
            loc.StartsWith("Startup folder", StringComparison.OrdinalIgnoreCase))
        {
            if (IsSuspiciousLocation(path))
                return true;
        }

        // Services / drivers / tasks that are actively flagged CHECK
        if ((loc.StartsWith("Service", StringComparison.OrdinalIgnoreCase) ||
            loc.StartsWith("Driver",  StringComparison.OrdinalIgnoreCase) ||
            loc.StartsWith("Scheduled task", StringComparison.OrdinalIgnoreCase)) &&
            risk.StartsWith("CHECK", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }

    // -----------------------------------------
    // Utility helpers
    // -----------------------------------------
    private static IEnumerable<string> SplitCsvLine(string line)
    {
        if (line == null)
            yield break;

        var sb      = new StringBuilder();
        bool inQuotes = false;

        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];

            if (c == '"')
            {
                // Handle escaped quotes ("")
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    sb.Append('"');
                    i++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }
            }
            else if (c == ',' && !inQuotes)
            {
                yield return sb.ToString();
                sb.Clear();
            }
            else
            {
                sb.Append(c);
            }
        }

        // Last field
        yield return sb.ToString();
    }

    private static string GetFilePublisherSafe(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return string.Empty;

        try
        {
            if (!File.Exists(path))
                return string.Empty;

            var vi = FileVersionInfo.GetVersionInfo(path);
            return vi.CompanyName ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }


    private static string GetCsvField(string[] fields, int index)
    {
        if (index < 0 || index >= fields.Length)
            return string.Empty;

        return fields[index]?.Trim() ?? string.Empty;
    }

    private static string ExtractExecutablePath(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return string.Empty;

        string trimmed = input.Trim();

        if (trimmed.StartsWith("\""))
        {
            int secondQuote = trimmed.IndexOf('\"', 1);
            if (secondQuote > 1)
                return trimmed.Substring(1, secondQuote - 1);
        }

        // First token until first space
        int spaceIndex = trimmed.IndexOf(' ');
        if (spaceIndex > 0)
            return trimmed.Substring(0, spaceIndex);

        return trimmed;
    }

   private static bool IsSuspiciousLocation(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return true;

        string lower = path.Replace('/', '\\').ToLowerInvariant();

        // 1) Very high-risk: stuff directly under user profile temp / downloads / desktop / public
        if (lower.Contains(@"\users\"))
        {
            if (lower.Contains(@"\appdata\local\temp\") ||
                lower.Contains(@"\appdata\local\microsoft\windows\temporary internet files\") ||
                lower.Contains(@"\downloads\") ||
                lower.Contains(@"\desktop\") ||
                lower.Contains(@"\public\") ||
                lower.Contains(@"\onedrive\"))
            {
                return true;
            }
        }

        // 2) Windows temp
        if (lower.Contains(@"\windows\temp\"))
            return true;

        // 3) ProgramData is *common* for legit services → do NOT treat as suspicious
        if (lower.Contains(@"\programdata\"))
            return false;

        // 4) Anything under Windows or Program Files is considered normal for v1
        if (lower.Contains(@":\windows\") ||
            lower.Contains(@":\program files"))
            return false;

        // 5) Everything else: treat as NOT suspicious for now (we were way too aggressive before)
        return false;
    }

    // Utility: return the currently selected PersistItem in the list
    private PersistItem? GetSelectedPersistItem()
    {
        return PersistResultsList?.SelectedItem as PersistItem;
    }

    // ----------------------------
    // PERSIST – Add to Cleanup Queue
    // ----------------------------
    private void PersistAddToCleanupButton_OnClick(object? sender, RoutedEventArgs e)
    {
        var item = GetSelectedPersistItem();
        if (item == null)
        {
            if (PersistStatusText != null)
                PersistStatusText.Text = "Status: select an item to add to cleanup queue.";
            return;
        }

        // Check if already in queue
        if (CaseManager.IsInCleanupQueue(item.Path))
        {
            if (PersistStatusText != null)
                PersistStatusText.Text = $"Status: {item.Name} is already in the cleanup queue.";
            return;
        }

        // Determine item type and action based on LocationType
        string itemType = item.LocationType.ToLowerInvariant() switch
        {
            "service" or "driver" => "Service",
            "scheduled task" => "ScheduledTask",
            "startup folder" => "StartupItem",
            _ when item.RegistryPath.Length > 0 => "RegistryKey",
            _ => "File"
        };

        string action = itemType switch
        {
            "Service" => "Disable",
            "ScheduledTask" => "Disable",
            "RegistryKey" => "BackupAndDelete",
            _ => "Quarantine"
        };

        // Map Risk to Severity
        string severity = item.Risk.StartsWith("CHECK", StringComparison.OrdinalIgnoreCase) ? "HIGH"
            : item.Risk.StartsWith("NOTE", StringComparison.OrdinalIgnoreCase) ? "MEDIUM"
            : "LOW";

        // For registry items, use RegistryPath as OriginalPath (that's what we're deleting)
        // For files/services/tasks, use the executable Path
        string originalPath = itemType switch
        {
            "RegistryKey" => item.RegistryPath,  // Delete the registry key, not the exe it points to
            _ => !string.IsNullOrEmpty(item.Path) ? item.Path : item.RegistryPath
        };

        var cleanupItem = new CleanupItem
        {
            ItemType = itemType,
            Name = item.Name,
            OriginalPath = originalPath,
            SourceTab = "Persist",
            Severity = severity,
            Reason = !string.IsNullOrEmpty(item.Path) && itemType == "RegistryKey"
                ? $"{item.Reason} (Executable: {item.Path})"  // Include exe path in reason for context
                : item.Reason,
            Action = action
        };

        CaseManager.AddToCleanupQueue(cleanupItem);

        if (PersistStatusText != null)
            PersistStatusText.Text = $"Status: {item.Name} added to cleanup queue ({action}).";
    }
}

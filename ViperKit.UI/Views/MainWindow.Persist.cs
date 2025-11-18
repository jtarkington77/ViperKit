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

            // 4) Startup folders – per-user + all users
            CollectStartupFolderAutoruns();

            // 5) Services + drivers – HKLM\SYSTEM\CurrentControlSet\Services
            CollectServiceAndDriverPersistence();

            // 6) Scheduled tasks
            CollectScheduledTasks();

            // Count how many items are flagged as CHECK
            int flaggedCount = _persistItems.Count(p =>
                p.Risk.StartsWith("CHECK", StringComparison.OrdinalIgnoreCase));

            // Bind into UI ListBox (respecting "show only CHECK" checkbox)
            BindPersistResults();

            if (PersistStatusText != null)
            {
                PersistStatusText.Text =
                    $"Status: found {_persistItems.Count} persistence entry/entries; {flaggedCount} marked 'CHECK'.";
            }

            // JSON log of this snapshot (lightweight, for later case review)
            try
            {
                JsonLog.Append("persist", new
                {
                    Timestamp    = DateTime.Now,
                    Host         = Environment.MachineName,
                    User         = Environment.UserName,
                    TotalEntries = _persistItems.Count,
                    FlaggedCount = flaggedCount,
                    Entries      = _persistItems.Select(p => new
                    {
                        p.Source,
                        p.LocationType,
                        p.Name,
                        p.Path,
                        p.RegistryPath,
                        p.Risk,
                        p.Reason,
                        p.MitreTechnique
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
                CaseManager.AddEvent(
                    tab: "Persist",
                    action: "Persistence scan completed",
                    severity: flaggedCount > 0 ? "WARN" : "INFO",
                    target: $"Entries: {_persistItems.Count}",
                    details: flaggedCount > 0
                        ? $"{flaggedCount} entry/entries marked CHECK."
                        : "No entries marked CHECK.");
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

        IEnumerable<PersistItem> source = _persistItems;

        // 1) "Show only CHECK" checkbox
        if (PersistShowCheckOnlyCheckBox?.IsChecked == true)
        {
            source = source.Where(p =>
                p.Risk.StartsWith("CHECK", StringComparison.OrdinalIgnoreCase));
        }

        // 2) Location filter (All / Registry / Startup / Services / Tasks)
        if (PersistLocationFilterCombo != null && PersistLocationFilterCombo.SelectedIndex > 0)
        {
            switch (PersistLocationFilterCombo.SelectedIndex)
            {
                case 1: // Registry autoruns
                    source = source.Where(p =>
                        p.LocationType.StartsWith("Autorun (Registry",
                            StringComparison.OrdinalIgnoreCase));
                    break;

                case 2: // Startup folders
                    source = source.Where(p =>
                        p.LocationType.StartsWith("Startup folder",
                            StringComparison.OrdinalIgnoreCase));
                    break;

                case 3: // Services / drivers
                    source = source.Where(p =>
                        p.LocationType.StartsWith("Service",
                            StringComparison.OrdinalIgnoreCase) ||
                        p.LocationType.StartsWith("Driver",
                            StringComparison.OrdinalIgnoreCase));
                    break;

                case 4: // Scheduled tasks
                    source = source.Where(p =>
                        p.LocationType.StartsWith("Scheduled task",
                            StringComparison.OrdinalIgnoreCase));
                    break;
            }
        }

        // 3) Text search across name / path / source / reason
        string? term = PersistSearchTextBox?.Text;
        if (!string.IsNullOrWhiteSpace(term))
        {
            term = term.Trim();

            source = source.Where(p =>
                (!string.IsNullOrEmpty(p.Name)   && p.Name.Contains(term, StringComparison.OrdinalIgnoreCase)) ||
                (!string.IsNullOrEmpty(p.Path)   && p.Path.Contains(term, StringComparison.OrdinalIgnoreCase)) ||
                (!string.IsNullOrEmpty(p.Source) && p.Source.Contains(term, StringComparison.OrdinalIgnoreCase)) ||
                (!string.IsNullOrEmpty(p.Reason) && p.Reason.Contains(term, StringComparison.OrdinalIgnoreCase)));
        }

        PersistResultsList.ItemsSource = source.ToList();
    }


    // Checkbox toggled
    private void PersistFilterCheckBox_OnChanged(object? sender, RoutedEventArgs e)
    {
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

        // HKLM 64-bit
        CollectRunKeyHive("HKLM Run", Registry.LocalMachine,
            @"Software\Microsoft\Windows\CurrentVersion\Run", "T1547.001");
        CollectRunKeyHive("HKLM RunOnce", Registry.LocalMachine,
            @"Software\Microsoft\Windows\CurrentVersion\RunOnce", "T1547.001");

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

                // Extract executable-ish path (first token before any arguments)
                string exePath = ExtractExecutablePath(expandedPath);

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
                    MitreTechnique = mitreId
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
                    MitreTechnique = mitreId
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

                // Risk heuristic: same flavor as Run/Startup
                string risk;
                if (!existsOnDisk || string.IsNullOrWhiteSpace(exePath))
                {
                    risk = "CHECK – binary missing on disk";
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
                    MitreTechnique = mitreId
                });
            }
        }
        catch
        {
            // In v1 we silently skip a full failure here; Run/Startup still show up.
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

                // Built-in Microsoft tasks usually live under \Microsoft\Windows\...
                bool isBuiltIn = taskName.StartsWith(@"\Microsoft\Windows\", StringComparison.OrdinalIgnoreCase);

                string risk;
                string reason;

                if (string.IsNullOrWhiteSpace(exePath))
                {
                    risk   = "CHECK – no action path";
                    reason = "Scheduled task without a clear executable/script path.";
                }
                else if (!existsOnDisk)
                {
                    risk   = "CHECK – binary missing on disk";
                    reason = $"Task action points to a file that does not exist: {exePath}";
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
                    MitreTechnique = "T1053.005"
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
            MitreTechnique = "T1547.004" // Boot or Logon Autostart – Winlogon
        });

        // Userinit ----------------------------------------------
        string userinitRaw = key.GetValue("Userinit") as string ?? string.Empty;
        string userinitExpanded = userinitRaw;

        if (!string.IsNullOrWhiteSpace(userinitExpanded) && userinitExpanded.Contains('%'))
            userinitExpanded = Environment.ExpandEnvironmentVariables(userinitExpanded);

        string userinitExe = ExtractExecutablePath(userinitExpanded);

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
            MitreTechnique = "T1547.004"
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
                MitreTechnique = "T1546.012" // Image File Execution Options Injection
            });
        }
    }
    catch
    {
        // Silent fail; other categories still populate.
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
            if (item.LocationType.StartsWith("Autorun (Registry)", StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrWhiteSpace(item.RegistryPath))
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

        // Quotes around full path
        if (trimmed.StartsWith("\""))
        {
            int secondQuote = trimmed.IndexOf('"', 1);
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

        string lower = path.ToLowerInvariant();

        // User profile / AppData / Temp = more suspicious for autoruns
        if (lower.Contains(@"\users\") ||
            lower.Contains(@"\appdata\") ||
            lower.Contains(@"\temp\"))
        {
            return true;
        }

        // If not under Windows or Program Files at all, treat as slightly odd
        if (!lower.Contains(@":\windows") &&
            !lower.Contains(@":\program files"))
        {
            return true;
        }

        return false;
    }
    
}

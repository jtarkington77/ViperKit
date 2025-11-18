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

            // 2) Startup folders – per-user + all users
            CollectStartupFolderAutoruns();

            // 3) Services + drivers – HKLM\SYSTEM\CurrentControlSet\Services
            CollectServiceAndDriverPersistence();


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

        // 2) Location filter (All / Registry / Startup / Services)
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

            if (!openedSomething && PersistStatusText != null)
                PersistStatusText.Text = "Status: nothing to open for this entry.";
        }
        catch
        {
            if (PersistStatusText != null)
                PersistStatusText.Text = "Status: failed to open location for this entry.";
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

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Microsoft.Win32;
using ViperKit.UI.Models;
using ViperKit.UI;


namespace ViperKit.UI.Views;

public partial class MainWindow
{
    // Cache of last run so we can filter without rescanning
    private readonly List<string> _persistEntries = new();
    private void LogCaseAndRefresh(string tab, string action, string severity, string target, string details)
    {
        try
        {
            CaseManager.AddEvent(tab, action, severity, target, details);
        }
        catch
        {
            // Case logging must never break the UI
        }

        try
        {
            UpdateDashboardCaseSummary();
            RefreshCaseTab();
        }
        catch
        {
            // Dashboard/case refresh failures are non-fatal
        }
    }


    // =========================
    // PERSIST TAB – PERSISTENCE MAP
    // =========================
    private void PersistRunButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (PersistStatusText != null)
            PersistStatusText.Text = "Status: scanning common persistence locations...";

        if (PersistResultsList != null)
            PersistResultsList.ItemsSource = Array.Empty<string>();

        _persistEntries.Clear();

        try
        {
            // -------------------------
            // 1) Autoruns – HKCU/HKLM
            // -------------------------

            // HKCU Run / RunOnce
            EnumerateRunKey(
                Registry.CurrentUser,
                @"Software\Microsoft\Windows\CurrentVersion\Run",
                "HKCU",
                _persistEntries);

            EnumerateRunKey(
                Registry.CurrentUser,
                @"Software\Microsoft\Windows\CurrentVersion\RunOnce",
                "HKCU",
                _persistEntries);

            // HKLM Run / RunOnce (combined 32/64 view)
            EnumerateRunKey(
                Registry.LocalMachine,
                @"Software\Microsoft\Windows\CurrentVersion\Run",
                "HKLM",
                _persistEntries);

            EnumerateRunKey(
                Registry.LocalMachine,
                @"Software\Microsoft\Windows\CurrentVersion\RunOnce",
                "HKLM",
                _persistEntries);

            // Wow6432Node Run / RunOnce – 32-bit apps on 64-bit Windows
            EnumerateRunKey(
                Registry.LocalMachine,
                @"Software\WOW6432Node\Microsoft\Windows\CurrentVersion\Run",
                "HKLM (Wow6432Node)",
                _persistEntries);

            EnumerateRunKey(
                Registry.LocalMachine,
                @"Software\WOW6432Node\Microsoft\Windows\CurrentVersion\RunOnce",
                "HKLM (Wow6432Node)",
                _persistEntries);

            // -------------------------
            // 2) Startup folders
            // -------------------------
            string currentUserStartup = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
            string allUsersStartup    = Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup);

            EnumerateStartupFolder(currentUserStartup, "Startup (Current User)", _persistEntries);
            EnumerateStartupFolder(allUsersStartup,    "Startup (All Users)",     _persistEntries);

            // -------------------------
            // 3) Auto-start services (registry-based)
            // -------------------------
            EnumerateAutoServices(_persistEntries);

            // Count "CHECK" entries for quick triage
            int flaggedCount = _persistEntries.Count(
                s => s.Contains(">>> RISK: CHECK", StringComparison.OrdinalIgnoreCase));

            // Bind results respecting filter checkbox
            BindPersistResults();

            if (PersistStatusText != null)
            {
                PersistStatusText.Text =
                    $"Status: found {_persistEntries.Count} persistence entry/entries; {flaggedCount} marked 'CHECK'.";
            }

            // -------- JSON log for this persistence snapshot --------
            try
            {
                JsonLog.Append("persist", new
                {
                    Timestamp    = DateTime.Now,
                    Host         = Environment.MachineName,
                    User         = Environment.UserName,
                    TotalEntries = _persistEntries.Count,
                    FlaggedCount = flaggedCount,
                    Entries      = _persistEntries
                });
            }
            catch
            {
                // JSON logging failure should never break the scan
            }

            // 3) Case event log
            try
            {
                CaseManager.AddEvent(
                    tab: "Persist",
                    action: "Persistence scan completed",
                    severity: flaggedCount > 0 ? "WARN" : "INFO",
                    target: $"Entries: {_persistEntries.Count}",
                    details: flaggedCount > 0
                        ? $"Flagged entries: {flaggedCount}"
                        : "No CHECK-rated entries found.");
            }
            catch
            {
                // Case logging must never break the scan
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
        catch (Exception ex)
        {
            if (PersistStatusText != null)
                PersistStatusText.Text = "Status: error while scanning persistence.";

            if (PersistResultsList != null)
                PersistResultsList.ItemsSource = new[] { $"Error: {ex.Message}" };
        }
}

    // Central place to apply "show only CHECK" filter
    private void BindPersistResults()
    {
        if (PersistResultsList == null)
            return;

        IEnumerable<string> source = _persistEntries;

        if (PersistShowCheckOnlyCheckBox?.IsChecked == true)
        {
            source = source.Where(s =>
                s.Contains(">>> RISK: CHECK", StringComparison.OrdinalIgnoreCase));
        }

        PersistResultsList.ItemsSource = source.ToList();
    }

    private void PersistFilterCheckBox_OnChanged(object? sender, RoutedEventArgs e)
    {
        BindPersistResults();
    }

#pragma warning disable CA1416 // Windows-only APIs (Registry)

    // -------- Autorun keys --------
    private void EnumerateRunKey(
        RegistryKey root,
        string subKeyPath,
        string hiveLabel,
        List<string> output)
    {
        try
        {
            using var key = root.OpenSubKey(subKeyPath);
            if (key == null)
                return;

            foreach (var valueName in key.GetValueNames())
            {
                object? rawObj   = key.GetValue(valueName);
                string  rawValue = rawObj?.ToString() ?? "(non-string value)";

                // Expand %PATH% style vars
                string expanded    = Environment.ExpandEnvironmentVariables(rawValue);
                string probableExe = ExtractExecutablePath(expanded);

                bool hasExe = !string.IsNullOrWhiteSpace(probableExe);
                bool exists = hasExe && File.Exists(probableExe);

                var sb = new StringBuilder();
                sb.AppendLine($"[Run] {hiveLabel}\\{subKeyPath}");
                sb.AppendLine($"  Name:      {valueName}");
                sb.AppendLine($"  Command:   {rawValue}");

                if (!string.Equals(rawValue, expanded, StringComparison.OrdinalIgnoreCase))
                    sb.AppendLine($"  Expanded:  {expanded}");

                if (hasExe)
                {
                    string existsLabel = exists ? "(exists)" : "(MISSING)";
                    sb.AppendLine($"  Executable: {probableExe} {existsLabel}");

                    string risk = BuildRiskLabel(probableExe, exists);

                    if (risk.StartsWith("CHECK", StringComparison.OrdinalIgnoreCase))
                        sb.AppendLine($"  >>> RISK: {risk} <<<");
                    else
                        sb.AppendLine($"  Risk:      {risk}");
                }
                else
                {
                    sb.AppendLine("  Executable: (could not parse from command line)");
                    sb.AppendLine("  Risk:      UNKNOWN – no executable parsed");
                }

                output.Add(sb.ToString());
            }
        }
        catch
        {
            // Ignore individual key failures
        }
    }

    // -------- Startup folders --------
    private void EnumerateStartupFolder(
        string? folderPath,
        string label,
        List<string> output)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(folderPath) || !Directory.Exists(folderPath))
                return;

            foreach (var file in Directory.EnumerateFiles(folderPath))
            {
                var info = new FileInfo(file);

                var sb = new StringBuilder();
                sb.AppendLine($"[Startup] {label}");
                sb.AppendLine($"  Name:     {info.Name}");
                sb.AppendLine($"  Path:     {info.FullName}");
                sb.AppendLine($"  Type:     {info.Extension}");
                sb.AppendLine($"  Modified: {info.LastWriteTime}");

                if (string.Equals(info.Extension, ".exe", StringComparison.OrdinalIgnoreCase))
                {
                    bool exists = File.Exists(info.FullName);
                    string existsLabel = exists ? "(exists)" : "(MISSING)";
                    sb.AppendLine($"  Executable: {info.FullName} {existsLabel}");

                    string risk = BuildRiskLabel(info.FullName, exists);

                    if (risk.StartsWith("CHECK", StringComparison.OrdinalIgnoreCase))
                        sb.AppendLine($"  >>> RISK: {risk} <<<");
                    else
                        sb.AppendLine($"  Risk:      {risk}");
                }
                else if (string.Equals(info.Extension, ".lnk", StringComparison.OrdinalIgnoreCase))
                {
                    sb.AppendLine("  Note: .lnk shortcut – target not resolved (check manually).");
                    sb.AppendLine("  Risk:      UNKNOWN – shortcut, inspect target");
                }

                output.Add(sb.ToString());
            }
        }
        catch
        {
            // Startup folders can have permission issues; ignore individual failures.
        }
    }

    // -------- Auto services (registry-based) --------
    private void EnumerateAutoServices(List<string> output)
    {
        try
        {
            using var servicesRoot = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services");
            if (servicesRoot == null)
                return;

            string systemRoot = Environment.GetEnvironmentVariable("SystemRoot") ?? @"C:\Windows";

            foreach (var serviceName in servicesRoot.GetSubKeyNames())
            {
                using var svcKey = servicesRoot.OpenSubKey(serviceName);
                if (svcKey == null)
                    continue;

                // "Start" DWORD: 2 = Automatic
                object? startObj   = svcKey.GetValue("Start");
                int     startValue = startObj is int i ? i : -1;
                if (startValue != 2)
                    continue; // not Automatic

                // Delayed start?
                bool delayed = false;
                object? delayedObj = svcKey.GetValue("DelayedAutoStart");
                if (delayedObj is int d && d == 1)
                    delayed = true;

                string displayName  = svcKey.GetValue("DisplayName")?.ToString() ?? "(no DisplayName)";
                string rawImagePath = svcKey.GetValue("ImagePath")?.ToString() ?? "(no ImagePath)";

                string expanded = Environment.ExpandEnvironmentVariables(rawImagePath);
                string probableExe = ExtractExecutablePath(expanded);

                // Normalise common driver formats like:
                //   system32\drivers\foo.sys
                //   \SystemRoot\System32\drivers\foo.sys
                if (!string.IsNullOrWhiteSpace(probableExe) && !Path.IsPathRooted(probableExe))
                {
                    string lower = probableExe.ToLowerInvariant();

                    if (lower.StartsWith(@"system32\", StringComparison.OrdinalIgnoreCase))
                    {
                        probableExe = Path.Combine(systemRoot, probableExe);
                    }
                    else if (lower.StartsWith(@"\systemroot\", StringComparison.OrdinalIgnoreCase))
                    {
                        string rest = probableExe.Substring(@"\SystemRoot\".Length);
                        probableExe = Path.Combine(systemRoot, rest);
                    }
                }

                bool hasExe = !string.IsNullOrWhiteSpace(probableExe);
                bool exists = hasExe && File.Exists(probableExe);

                var sb = new StringBuilder();
                sb.AppendLine($"[Service] {serviceName}");
                sb.AppendLine($"  Display:   {displayName}");
                sb.AppendLine($"  StartType: {(delayed ? "Automatic (Delayed Start)" : "Automatic")}");
                sb.AppendLine($"  Command:   {rawImagePath}");

                if (!string.Equals(rawImagePath, expanded, StringComparison.OrdinalIgnoreCase))
                    sb.AppendLine($"  Expanded:  {expanded}");

                if (hasExe)
                {
                    string existsLabel = exists ? "(exists)" : "(MISSING)";
                    sb.AppendLine($"  Executable: {probableExe} {existsLabel}");

                    string risk = BuildRiskLabel(probableExe, exists);

                    if (risk.StartsWith("CHECK", StringComparison.OrdinalIgnoreCase))
                        sb.AppendLine($"  >>> RISK: {risk} <<<");
                    else
                        sb.AppendLine($"  Risk:      {risk}");
                }
                else
                {
                    sb.AppendLine("  Executable: (could not parse from ImagePath)");
                    sb.AppendLine("  Risk:      UNKNOWN – no executable parsed");
                }

                output.Add(sb.ToString());
            }
        }
        catch
        {
            // If services enumeration fails entirely, just skip it; the rest is still useful.
        }
    }


#pragma warning restore CA1416

    // ---- Shared helper for parsing exe path ----
    private static string ExtractExecutablePath(string command)
    {
        if (string.IsNullOrWhiteSpace(command))
            return string.Empty;

        string trimmed = command.Trim();

        // Quoted path first:  "C:\Program Files\X\y.exe" /stuff
        if (trimmed.StartsWith("\"", StringComparison.Ordinal))
        {
            int closing = trimmed.IndexOf('"', 1);
            if (closing > 1)
            {
                return trimmed.Substring(1, closing - 1);
            }
        }

        // Otherwise, take first token up to the first space
        int    spaceIdx  = trimmed.IndexOf(' ');
        string candidate = spaceIdx > 0 ? trimmed[..spaceIdx] : trimmed;

        // Sanity check: must contain a backslash and a dot
        if (!candidate.Contains('\\') || !candidate.Contains('.'))
            return string.Empty;

        return candidate;
    }

    // ---- classifier ----
    private static string BuildRiskLabel(string? exePath, bool exists)
    {
        if (string.IsNullOrWhiteSpace(exePath))
            return "UNKNOWN – no executable parsed";

        string lower = exePath.ToLowerInvariant();
        var flags = new List<string>();

        // Does the file actually exist?
        if (!exists)
            flags.Add("MISSING exe");

        // Suspicious locations
        if (lower.Contains(@"\appdata\"))
            flags.Add("runs from AppData");
        if (lower.Contains(@"\temp\"))
            flags.Add("runs from Temp");
        if (lower.Contains(@"\users\"))
            flags.Add("runs from user profile");

        // Try to see if it's *not* under Windows / Program Files
        try
        {
            string winDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows) ?? "";
            string pf     = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles) ?? "";
            string pf86   = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86) ?? "";

            bool inWindows =
                !string.IsNullOrWhiteSpace(winDir) &&
                lower.StartsWith(winDir.ToLowerInvariant());

            bool inProgramFiles =
                (!string.IsNullOrWhiteSpace(pf)   && lower.StartsWith(pf.ToLowerInvariant()))  ||
                (!string.IsNullOrWhiteSpace(pf86) && lower.StartsWith(pf86.ToLowerInvariant()));

            if (!inWindows && !inProgramFiles)
                flags.Add("non-standard path");
        }
        catch
        {
            // If anything explodes here, still keep whatever flags we already have.
        }

        if (flags.Count == 0)
            return "OK – likely benign (standard location)";

        return "CHECK – " + string.Join(", ", flags);
    }


    // ---- Investigation integration (services / regedit / explorer) ----

    private void PersistOpenSelectedButton_OnClick(object? sender, RoutedEventArgs e)
    {
        OpenPersistEntry(PersistResultsList?.SelectedItem as string);
    }

    private void PersistResultsList_OnDoubleTapped(object? sender, RoutedEventArgs e)
    {
        OpenPersistEntry(PersistResultsList?.SelectedItem as string);
    }

    private void OpenPersistEntry(string? entry)
    {
        if (string.IsNullOrWhiteSpace(entry))
        {
            if (PersistStatusText != null)
                PersistStatusText.Text = "Status: select an entry first.";
            return;
        }

        var lines = entry.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length == 0)
            return;

        string headerLine = lines[0].Trim();

        // -------------------------
        // 1) Service entries → Services.msc
        // -------------------------
        if (headerLine.StartsWith("[Service]", StringComparison.OrdinalIgnoreCase))
        {
            // Internal service name, e.g. "MDCoreSvc"
            string serviceName = headerLine.Substring("[Service]".Length).Trim();

            // Try to pull the DisplayName so you can search by the friendly name
            string displayName = serviceName;
            var displayLine = lines.FirstOrDefault(l =>
                l.TrimStart().StartsWith("Display:", StringComparison.OrdinalIgnoreCase));

            if (displayLine != null)
            {
                int colon = displayLine.IndexOf(':');
                if (colon >= 0 && colon + 1 < displayLine.Length)
                {
                    displayName = displayLine.Substring(colon + 1).Trim();
                }
            }

            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName      = "services.msc",
                    UseShellExecute = true
                });

                if (PersistStatusText != null)
                {
                    PersistStatusText.Text =
                        $"Status: opened Services.msc – look for display name '{displayName}' (service name: {serviceName}).";
                }

                LogCaseAndRefresh(
                    tab: "Persist",
                    action: "Opened Services.msc for service",
                    severity: "NOTE",
                    target: serviceName,
                    details: $"Display name: {displayName}");

            }
            catch
            {
                if (PersistStatusText != null)
                    PersistStatusText.Text = "Status: could not launch Services.msc.";
            }

            return;
        }

        // -------------------------
        // 2) Run / RunOnce autoruns → Regedit
        //    Header line looks like:
        //    [Run] HKCU\\Software\\Microsoft\\Windows\\CurrentVersion\\Run
        // -------------------------
        if (headerLine.StartsWith("[Run]", StringComparison.OrdinalIgnoreCase))
        {
            string regPath = headerLine.Substring("[Run]".Length).Trim();

            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName      = "regedit.exe",
                    UseShellExecute = true
                });

                if (PersistStatusText != null)
                    PersistStatusText.Text =
                        $"Status: opened Regedit – navigate to {regPath} to inspect this autorun.";

                LogCaseAndRefresh(
                    tab: "Persist",
                    action: "Opened Regedit for autorun key",
                    severity: "NOTE",
                    target: regPath,
                    details: "Technician reviewing autorun entry in registry.");

            }
            catch
            {
                if (PersistStatusText != null)
                    PersistStatusText.Text =
                        $"Status: autorun at {regPath} (failed to open Regedit automatically).";
            }

            return;
        }

        // -------------------------
        // 3) Startup entries → open the .lnk/.exe in Explorer using the Path: line
        // -------------------------
        if (headerLine.StartsWith("[Startup]", StringComparison.OrdinalIgnoreCase))
        {
            var pathLine = lines.FirstOrDefault(l =>
                l.TrimStart().StartsWith("Path:", StringComparison.OrdinalIgnoreCase));

            if (pathLine == null)
            {
                if (PersistStatusText != null)
                    PersistStatusText.Text = "Status: startup entry has no Path: line.";
                return;
            }

            int colon = pathLine.IndexOf(':');
            if (colon < 0 || colon + 1 >= pathLine.Length)
                return;

            // Take everything after "Path:" and trim it
            string startupPath = pathLine.Substring(colon + 1).Trim();

            if (!File.Exists(startupPath))
            {
                if (PersistStatusText != null)
                    PersistStatusText.Text = $"Status: startup item not found on disk: {startupPath}";

                LogCaseAndRefresh(
                    tab: "Persist",
                    action: "Startup entry missing on disk",
                    severity: "WARN",
                    target: startupPath,
                    details: "Listed startup entry could not be found on disk.");
            }
                else
                {
                    try
                    {
                        Process.Start(new ProcessStartInfo
                        {
                            FileName        = "explorer.exe",
                            Arguments       = $"/select,\"{startupPath}\"",
                            UseShellExecute = true
                        });

                        if (PersistStatusText != null)
                            PersistStatusText.Text = $"Status: opened Startup item in Explorer: {startupPath}.";

                        LogCaseAndRefresh(
                            tab: "Persist",
                            action: "Opened startup item in Explorer",
                            severity: "NOTE",
                            target: startupPath,
                            details: "Technician reviewing startup executable/shortcut.");
                    }
                    catch
                    {
                        if (PersistStatusText != null)
                            PersistStatusText.Text = $"Status: could not open Startup item: {startupPath}.";
                    }
                }

            return;
        }

        // -------------------------
        // 4) Anything else with an Executable: line → Explorer
        // -------------------------
        var exeLine = lines.FirstOrDefault(l =>
            l.TrimStart().StartsWith("Executable:", StringComparison.OrdinalIgnoreCase));

        if (exeLine == null)
        {
            if (PersistStatusText != null)
                PersistStatusText.Text = "Status: no executable path found for this entry.";
            return;
        }

        int exeColon = exeLine.IndexOf(':');
        if (exeColon < 0 || exeColon + 1 >= exeLine.Length)
            return;

        string rest = exeLine.Substring(exeColon + 1).Trim();

        // Strip trailing " (exists)" / " (MISSING)" if present
        int parenIndex = rest.LastIndexOf(" (", StringComparison.Ordinal);
        if (parenIndex > 0)
            rest = rest.Substring(0, parenIndex).Trim();

        string exePath = rest;

        if (!File.Exists(exePath))
        {
            if (PersistStatusText != null)
                PersistStatusText.Text = $"Status: executable not found on disk: {exePath}";
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName      = "explorer.exe",
                Arguments     = $"/select,\"{exePath}\"",
                UseShellExecute = true
            });

            if (PersistStatusText != null)
                PersistStatusText.Text = $"Status: opened Explorer at {exePath}.";
        }
        catch
        {
            if (PersistStatusText != null)
                PersistStatusText.Text = "Status: failed to open Explorer for this entry.";
        }
    }
}


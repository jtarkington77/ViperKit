// ViperKit.UI - Views\MainWindow.PSHistory.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using ViperKit.UI.Models;

namespace ViperKit.UI.Views;

public partial class MainWindow
{
#pragma warning disable CA1416 // Windows-only APIs

    // Storage for PS history scan results
    private PowerShellHistoryResult? _psHistoryResult;
    private List<PowerShellHistoryEntry> _psHistoryFiltered = new();

    // ---- HIGH RISK PATTERNS ----
    // These indicate download+execute, encoded commands, or known attack tools
    private static readonly (string Pattern, string Description)[] HighRiskPatterns =
    {
        // Download and execute patterns
        (@"Invoke-WebRequest.*\|\s*[Ii][Ee][Xx]", "Download + Execute (IWR | IEX)"),
        (@"Invoke-WebRequest.*Invoke-Expression", "Download + Execute (IWR + Invoke-Expression)"),
        (@"Invoke-RestMethod.*\|\s*[Ii][Ee][Xx]", "Download + Execute (IRM | IEX)"),
        (@"Invoke-RestMethod.*Invoke-Expression", "Download + Execute (IRM + Invoke-Expression)"),
        (@"\(New-Object\s+Net\.WebClient\)\.DownloadString", "WebClient DownloadString"),
        (@"\(New-Object\s+System\.Net\.WebClient\)\.DownloadString", "WebClient DownloadString"),
        (@"DownloadString\s*\(.*\)\s*\|\s*[Ii][Ee][Xx]", "DownloadString + IEX"),
        (@"DownloadFile\s*\(.*\).*Start-Process", "Download + Execute file"),

        // Encoded command execution
        (@"-[Ee][Nn][Cc][Oo]?[Dd]?[Ee]?[Dd]?[Cc]?[Oo]?[Mm]?[Mm]?[Aa]?[Nn]?[Dd]?\s+[A-Za-z0-9+/=]{20,}", "Base64 encoded command"),
        (@"-[Ee][Nn][Cc]\s+[A-Za-z0-9+/=]{20,}", "Base64 encoded command (-enc)"),
        (@"-[Ee]\s+[A-Za-z0-9+/=]{50,}", "Base64 encoded command (-e)"),
        (@"\[Convert\]::FromBase64String", "Base64 decoding"),
        (@"\[System\.Convert\]::FromBase64String", "Base64 decoding"),
        (@"\[Text\.Encoding\]::.*\.GetString.*FromBase64", "Base64 decode to string"),

        // Known attack tools
        (@"Invoke-Mimikatz", "Mimikatz execution"),
        (@"Invoke-Kerberoast", "Kerberoasting attack"),
        (@"Invoke-BloodHound", "BloodHound collection"),
        (@"Invoke-PowerShellTcp", "PowerShell reverse shell"),
        (@"Invoke-Shellcode", "Shellcode injection"),
        (@"Invoke-ReflectivePEInjection", "Reflective PE injection"),
        (@"Invoke-DllInjection", "DLL injection"),
        (@"Invoke-TokenManipulation", "Token manipulation"),
        (@"Invoke-CredentialInjection", "Credential injection"),
        (@"Get-GPPPassword", "GPP password extraction"),
        (@"Get-GPPAutologon", "GPP autologon extraction"),
        (@"Empire", "Empire C2 framework"),
        (@"Covenant", "Covenant C2 framework"),

        // Dangerous utilities
        (@"certutil\s+.*-decode", "Certutil decode (LOLBin)"),
        (@"certutil\s+.*-urlcache", "Certutil URL cache (LOLBin)"),
        (@"bitsadmin\s+/transfer", "BITS transfer (LOLBin)"),
        (@"mshta\s+", "MSHTA execution (LOLBin)"),
        (@"rundll32\s+.*javascript", "Rundll32 JavaScript (LOLBin)"),
        (@"regsvr32\s+/s\s+/n\s+/u\s+/i:", "Regsvr32 scrobj (LOLBin)"),

        // Credential access
        (@"Get-Credential", "Credential prompt (potential phishing)"),
        (@"ConvertTo-SecureString.*-AsPlainText", "Plain text password conversion"),
        (@"SecureString.*ConvertFrom", "SecureString extraction"),
    };

    // ---- MEDIUM RISK PATTERNS ----
    // These indicate persistence, defense evasion, or privilege changes
    private static readonly (string Pattern, string Description)[] MediumRiskPatterns =
    {
        // Execution policy bypass
        (@"Set-ExecutionPolicy\s+(Bypass|Unrestricted|RemoteSigned)", "Execution policy change"),
        (@"-ExecutionPolicy\s+(Bypass|Unrestricted)", "Execution policy bypass"),
        (@"-[Ee][Pp]\s+(Bypass|Unrestricted)", "Execution policy bypass (-ep)"),

        // Persistence mechanisms
        (@"New-ScheduledTask", "Scheduled task creation"),
        (@"Register-ScheduledTask", "Scheduled task registration"),
        (@"schtasks\s+/create", "Scheduled task creation (schtasks)"),
        (@"New-Service", "Service creation"),
        (@"Set-Service", "Service modification"),
        (@"sc\.exe\s+(create|config)", "Service creation/config (sc.exe)"),
        (@"New-ItemProperty.*\\Run", "Registry Run key creation"),
        (@"Set-ItemProperty.*\\Run", "Registry Run key modification"),
        (@"reg\s+add.*\\Run", "Registry Run key (reg.exe)"),

        // Defense evasion
        (@"Add-MpPreference\s+-ExclusionPath", "Defender exclusion path"),
        (@"Add-MpPreference\s+-ExclusionProcess", "Defender exclusion process"),
        (@"Add-MpPreference\s+-ExclusionExtension", "Defender exclusion extension"),
        (@"Set-MpPreference\s+-DisableRealtimeMonitoring", "Defender real-time disabled"),
        (@"Disable-WindowsOptionalFeature", "Windows feature disabled"),
        (@"Stop-Service\s+.*Defender", "Defender service stopped"),
        (@"Stop-Service\s+.*WinDefend", "Defender service stopped"),

        // Network/firewall changes
        (@"netsh\s+advfirewall", "Firewall modification"),
        (@"New-NetFirewallRule", "Firewall rule creation"),
        (@"Set-NetFirewallProfile", "Firewall profile change"),

        // User/group management
        (@"net\s+user\s+\w+\s+", "User account modification"),
        (@"net\s+localgroup\s+administrators", "Admin group modification"),
        (@"Add-LocalGroupMember", "Local group member added"),
        (@"New-LocalUser", "Local user creation"),

        // Registry modifications
        (@"reg\s+add", "Registry modification (reg.exe)"),
        (@"New-ItemProperty\s+-Path\s+.*(HKLM|HKCU)", "Registry key creation"),
        (@"Set-ItemProperty\s+-Path\s+.*(HKLM|HKCU)", "Registry value modification"),

        // Process manipulation
        (@"Start-Process\s+.*-WindowStyle\s+Hidden", "Hidden process start"),
        (@"Start-Process\s+.*-NoNewWindow", "Background process start"),
        (@"Invoke-WmiMethod.*Win32_Process", "WMI process creation"),
        (@"Invoke-CimMethod.*Win32_Process", "CIM process creation"),

        // Remote execution
        (@"Enter-PSSession", "Remote PS session"),
        (@"Invoke-Command\s+-ComputerName", "Remote command execution"),
        (@"New-PSSession", "New PS session"),
        (@"winrm\s+", "WinRM command"),
    };

    // ----------------------------
    // PS HISTORY – Main Scan
    // ----------------------------
    private async void PSHistoryScanButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button btn)
            btn.IsEnabled = false;

        try
        {
            if (PSHistoryStatusText != null)
                PSHistoryStatusText.Text = "Status: scanning PowerShell history...";

            _psHistoryResult = await Task.Run(() => ScanPowerShellHistory());

            // Update summary counts
            UpdatePSHistorySummary();

            // Populate user filter dropdown
            PopulatePSHistoryUserFilter();

            // Apply filters and display
            ApplyPSHistoryFilters();

            if (PSHistoryStatusText != null)
                PSHistoryStatusText.Text = $"Status: {_psHistoryResult.SummaryMessage}";

            // Log to case
            CaseManager.AddEvent("Persist", "PowerShell history scanned", "INFO", null,
                $"Found {_psHistoryResult.TotalCommands} commands: {_psHistoryResult.HighRiskCount} HIGH, {_psHistoryResult.MediumRiskCount} MEDIUM");
        }
        catch (Exception ex)
        {
            if (PSHistoryStatusText != null)
                PSHistoryStatusText.Text = $"Status: error - {ex.Message}";
        }
        finally
        {
            if (sender is Button btn2)
                btn2.IsEnabled = true;
        }
    }

    // ----------------------------
    // PS HISTORY – Scan Logic
    // ----------------------------
    private PowerShellHistoryResult ScanPowerShellHistory()
    {
        var result = new PowerShellHistoryResult();
        var usersScanned = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Get all user profile directories
        string usersRoot = Path.Combine(Environment.GetEnvironmentVariable("SystemDrive") ?? "C:", "Users");

        if (!Directory.Exists(usersRoot))
        {
            result.Errors.Add($"Users directory not found: {usersRoot}");
            result.Success = false;
            return result;
        }

        // Scan each user profile
        foreach (string userDir in Directory.GetDirectories(usersRoot))
        {
            string userName = Path.GetFileName(userDir);

            // Skip system profiles
            if (userName.Equals("Public", StringComparison.OrdinalIgnoreCase) ||
                userName.Equals("Default", StringComparison.OrdinalIgnoreCase) ||
                userName.Equals("Default User", StringComparison.OrdinalIgnoreCase) ||
                userName.Equals("All Users", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            bool userHadHistory = false;

            // Windows PowerShell 5.1 history
            string ps51HistoryPath = Path.Combine(userDir,
                @"AppData\Roaming\Microsoft\Windows\PowerShell\PSReadLine\ConsoleHost_history.txt");

            if (File.Exists(ps51HistoryPath))
            {
                try
                {
                    var entries = ParseHistoryFile(ps51HistoryPath, userName, "5.1");
                    result.Entries.AddRange(entries);
                    result.HistoryFilesFound.Add(ps51HistoryPath);
                    userHadHistory = true;
                }
                catch (UnauthorizedAccessException)
                {
                    result.HistoryFilesSkipped.Add(ps51HistoryPath + " (access denied)");
                }
                catch (Exception ex)
                {
                    result.Errors.Add($"Error reading {ps51HistoryPath}: {ex.Message}");
                }
            }

            // PowerShell 7.x history
            string ps7HistoryPath = Path.Combine(userDir,
                @"AppData\Roaming\Microsoft\PowerShell\PSReadLine\ConsoleHost_history.txt");

            if (File.Exists(ps7HistoryPath))
            {
                try
                {
                    var entries = ParseHistoryFile(ps7HistoryPath, userName, "7");
                    result.Entries.AddRange(entries);
                    result.HistoryFilesFound.Add(ps7HistoryPath);
                    userHadHistory = true;
                }
                catch (UnauthorizedAccessException)
                {
                    result.HistoryFilesSkipped.Add(ps7HistoryPath + " (access denied)");
                }
                catch (Exception ex)
                {
                    result.Errors.Add($"Error reading {ps7HistoryPath}: {ex.Message}");
                }
            }

            if (userHadHistory)
                usersScanned.Add(userName);
        }

        // Calculate statistics
        result.UsersScanned = usersScanned.Count;
        result.TotalCommands = result.Entries.Count;
        result.HighRiskCount = result.Entries.Count(e => e.Severity == "HIGH");
        result.MediumRiskCount = result.Entries.Count(e => e.Severity == "MEDIUM");
        result.LowRiskCount = result.Entries.Count(e => e.Severity == "LOW");
        result.PS51Count = result.Entries.Count(e => e.PowerShellVersion == "5.1");
        result.PS7Count = result.Entries.Count(e => e.PowerShellVersion == "7");

        return result;
    }

    // ----------------------------
    // PS HISTORY – Parse History File
    // ----------------------------
    private List<PowerShellHistoryEntry> ParseHistoryFile(string filePath, string userName, string psVersion)
    {
        var entries = new List<PowerShellHistoryEntry>();
        var lines = File.ReadAllLines(filePath, Encoding.UTF8);

        // Get file info for dates
        var fileInfo = new FileInfo(filePath);
        DateTime? fileModified = fileInfo.LastWriteTime;
        DateTime? fileCreated = fileInfo.CreationTime;
        int totalLines = lines.Length;

        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i].Trim();
            if (string.IsNullOrWhiteSpace(line))
                continue;

            var entry = new PowerShellHistoryEntry
            {
                Command = line,
                UserProfile = userName,
                PowerShellVersion = psVersion,
                HistoryFilePath = filePath,
                LineNumber = i + 1,
                TotalLinesInFile = totalLines,
                HistoryFileModified = fileModified,
                HistoryFileCreated = fileCreated
            };

            // Assess risk
            AssessCommandRisk(entry);

            // Try to decode if it's an encoded command
            if (entry.IsEncoded)
            {
                TryDecodeBase64Command(entry);
            }

            entries.Add(entry);
        }

        return entries;
    }

    // ----------------------------
    // PS HISTORY – Risk Assessment
    // ----------------------------
    private void AssessCommandRisk(PowerShellHistoryEntry entry)
    {
        string cmd = entry.Command;

        // Check HIGH risk patterns first
        foreach (var (pattern, description) in HighRiskPatterns)
        {
            if (Regex.IsMatch(cmd, pattern, RegexOptions.IgnoreCase))
            {
                entry.Severity = "HIGH";
                entry.RiskIndicators.Add(description);
            }
        }

        // Check for encoded commands specifically
        if (Regex.IsMatch(cmd, @"-[Ee][Nn][Cc]?\s+[A-Za-z0-9+/=]{20,}", RegexOptions.IgnoreCase) ||
            Regex.IsMatch(cmd, @"-[Ee]\s+[A-Za-z0-9+/=]{50,}", RegexOptions.IgnoreCase) ||
            Regex.IsMatch(cmd, @"-EncodedCommand\s+[A-Za-z0-9+/=]{20,}", RegexOptions.IgnoreCase))
        {
            entry.IsEncoded = true;
            if (entry.Severity != "HIGH")
            {
                entry.Severity = "HIGH";
                entry.RiskIndicators.Add("Base64 encoded command");
            }
        }

        // If already HIGH, set reason and return
        if (entry.Severity == "HIGH")
        {
            entry.RiskReason = string.Join("; ", entry.RiskIndicators);
            return;
        }

        // Check MEDIUM risk patterns
        foreach (var (pattern, description) in MediumRiskPatterns)
        {
            if (Regex.IsMatch(cmd, pattern, RegexOptions.IgnoreCase))
            {
                entry.Severity = "MEDIUM";
                entry.RiskIndicators.Add(description);
            }
        }

        if (entry.Severity == "MEDIUM")
        {
            entry.RiskReason = string.Join("; ", entry.RiskIndicators);
            return;
        }

        // Default to LOW
        entry.Severity = "LOW";
        entry.RiskReason = "Normal command";
    }

    // ----------------------------
    // PS HISTORY – Base64 Decoding
    // ----------------------------
    private void TryDecodeBase64Command(PowerShellHistoryEntry entry)
    {
        try
        {
            // Extract Base64 string from command
            var match = Regex.Match(entry.Command,
                @"-[Ee][Nn][Cc][Oo]?[Dd]?[Ee]?[Dd]?[Cc]?[Oo]?[Mm]?[Mm]?[Aa]?[Nn]?[Dd]?\s+([A-Za-z0-9+/=]+)",
                RegexOptions.IgnoreCase);

            if (!match.Success)
            {
                match = Regex.Match(entry.Command, @"-[Ee]\s+([A-Za-z0-9+/=]{50,})", RegexOptions.IgnoreCase);
            }

            if (match.Success && match.Groups.Count > 1)
            {
                string base64 = match.Groups[1].Value;

                // PowerShell uses UTF-16LE for encoded commands
                byte[] bytes = Convert.FromBase64String(base64);
                string decoded = Encoding.Unicode.GetString(bytes);

                entry.DecodedCommand = decoded.Trim();

                // Re-assess the decoded command for additional risk indicators
                if (!string.IsNullOrEmpty(entry.DecodedCommand))
                {
                    foreach (var (pattern, description) in HighRiskPatterns)
                    {
                        if (Regex.IsMatch(entry.DecodedCommand, pattern, RegexOptions.IgnoreCase))
                        {
                            if (!entry.RiskIndicators.Contains(description))
                                entry.RiskIndicators.Add($"[Decoded] {description}");
                        }
                    }
                    entry.RiskReason = string.Join("; ", entry.RiskIndicators);
                }
            }
        }
        catch
        {
            entry.DecodeFailed = true;
        }
    }

    // ----------------------------
    // PS HISTORY – UI Updates
    // ----------------------------
    private void UpdatePSHistorySummary()
    {
        if (_psHistoryResult == null)
            return;

        if (PSHistoryHighCount != null)
            PSHistoryHighCount.Text = _psHistoryResult.HighRiskCount.ToString();

        if (PSHistoryMediumCount != null)
            PSHistoryMediumCount.Text = _psHistoryResult.MediumRiskCount.ToString();

        if (PSHistoryLowCount != null)
            PSHistoryLowCount.Text = _psHistoryResult.LowRiskCount.ToString();

        if (PSHistoryPS51Count != null)
            PSHistoryPS51Count.Text = _psHistoryResult.PS51Count.ToString();

        if (PSHistoryPS7Count != null)
            PSHistoryPS7Count.Text = _psHistoryResult.PS7Count.ToString();

        if (PSHistoryUsersCount != null)
            PSHistoryUsersCount.Text = _psHistoryResult.UsersScanned.ToString();
    }

    private void ApplyPSHistoryFilters()
    {
        if (_psHistoryResult == null || PSHistoryResultsList == null)
            return;

        var entries = _psHistoryResult.Entries.AsEnumerable();

        // Filter by PowerShell version
        string? psVersionFilter = (PSHistoryVersionFilterCombo?.SelectedItem as ComboBoxItem)?.Content?.ToString();
        if (!string.IsNullOrEmpty(psVersionFilter) && psVersionFilter != "All Versions")
        {
            if (psVersionFilter == "PowerShell 5.1")
                entries = entries.Where(e => e.PowerShellVersion == "5.1");
            else if (psVersionFilter == "PowerShell 7")
                entries = entries.Where(e => e.PowerShellVersion == "7");
        }

        // Filter by severity (checkbox)
        bool showSuspiciousOnly = PSHistoryShowSuspiciousCheckBox?.IsChecked == true;
        if (showSuspiciousOnly)
        {
            entries = entries.Where(e => e.Severity == "HIGH" || e.Severity == "MEDIUM");
        }

        // Filter by selected severity (dropdown)
        string? severityFilter = (PSHistorySeverityFilterCombo?.SelectedItem as ComboBoxItem)?.Content?.ToString();
        if (!string.IsNullOrEmpty(severityFilter) && severityFilter != "All Severities")
        {
            string sev = severityFilter.Replace(" only", "").ToUpperInvariant();
            entries = entries.Where(e => e.Severity == sev);
        }

        // Filter by user
        string? userFilter = (PSHistoryUserFilterCombo?.SelectedItem as ComboBoxItem)?.Content?.ToString();
        if (!string.IsNullOrEmpty(userFilter) && userFilter != "All Users")
        {
            entries = entries.Where(e => e.UserProfile.Equals(userFilter, StringComparison.OrdinalIgnoreCase));
        }

        // Filter by recency (last N commands per file)
        string? recencyFilter = (PSHistoryRecencyFilterCombo?.SelectedItem as ComboBoxItem)?.Content?.ToString();
        if (!string.IsNullOrEmpty(recencyFilter) && recencyFilter != "All Commands")
        {
            // Parse "Last 100", "Last 500", etc.
            if (recencyFilter.StartsWith("Last "))
            {
                string numStr = recencyFilter.Replace("Last ", "").Replace(",", "");
                if (int.TryParse(numStr, out int lastN))
                {
                    // For each file, only keep the last N commands
                    entries = entries
                        .GroupBy(e => e.HistoryFilePath)
                        .SelectMany(g => g.Where(e => e.LineNumber > e.TotalLinesInFile - lastN));
                }
            }
            else if (recencyFilter == "Recent (last 10%)")
            {
                entries = entries.Where(e => e.RecencyPercent >= 90);
            }
        }

        // Text search
        string? searchText = PSHistorySearchBox?.Text;
        if (!string.IsNullOrWhiteSpace(searchText))
        {
            entries = entries.Where(e =>
                e.Command.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
                e.RiskReason.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
                e.DecodedCommand.Contains(searchText, StringComparison.OrdinalIgnoreCase));
        }

        // Sort by severity (HIGH first, then MEDIUM, then LOW), then by most recent
        _psHistoryFiltered = entries
            .OrderByDescending(e => e.Severity == "HIGH")
            .ThenByDescending(e => e.Severity == "MEDIUM")
            .ThenByDescending(e => e.LineNumber)  // Most recent first within severity
            .ToList();

        PSHistoryResultsList.ItemsSource = null;
        PSHistoryResultsList.ItemsSource = _psHistoryFiltered;

        // Update filter status
        if (PSHistoryFilterStatus != null)
        {
            int shown = _psHistoryFiltered.Count;
            int total = _psHistoryResult.TotalCommands;
            PSHistoryFilterStatus.Text = shown == total
                ? $"Showing all {total} commands"
                : $"Showing {shown} of {total} commands";
        }
    }

    // ----------------------------
    // PS HISTORY – Filter Handlers
    // ----------------------------
    private void PSHistoryFilterChanged(object? sender, RoutedEventArgs e)
    {
        ApplyPSHistoryFilters();
    }

    private void PSHistorySearchBox_OnTextChanged(object? sender, TextChangedEventArgs e)
    {
        ApplyPSHistoryFilters();
    }

    private void PSHistorySeverityFilterCombo_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        ApplyPSHistoryFilters();
    }

    private void PSHistoryUserFilterCombo_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        ApplyPSHistoryFilters();
    }

    private void PSHistoryVersionFilterCombo_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        ApplyPSHistoryFilters();
    }

    private void PSHistoryRecencyFilterCombo_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        ApplyPSHistoryFilters();
    }

    // ----------------------------
    // PS HISTORY – Actions
    // ----------------------------
    private void PSHistoryDecodeButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (PSHistoryResultsList?.SelectedItem is not PowerShellHistoryEntry entry)
            return;

        if (!entry.IsEncoded)
        {
            if (PSHistoryStatusText != null)
                PSHistoryStatusText.Text = "Status: selected command is not encoded.";
            return;
        }

        if (entry.DecodeFailed)
        {
            if (PSHistoryStatusText != null)
                PSHistoryStatusText.Text = "Status: failed to decode this command.";
            return;
        }

        if (!string.IsNullOrEmpty(entry.DecodedCommand))
        {
            // Copy decoded command to clipboard
            var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
            clipboard?.SetTextAsync(entry.DecodedCommand);

            if (PSHistoryStatusText != null)
                PSHistoryStatusText.Text = "Status: decoded command copied to clipboard.";
        }
    }

    private async void PSHistoryCopySelectedButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (PSHistoryResultsList?.SelectedItem is not PowerShellHistoryEntry entry)
            return;

        var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        if (clipboard != null)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"User: {entry.UserProfile}");
            sb.AppendLine($"PowerShell: {entry.PowerShellVersion}");
            sb.AppendLine($"Severity: {entry.Severity}");
            sb.AppendLine($"Risk: {entry.RiskReason}");
            sb.AppendLine($"Command: {entry.Command}");
            if (entry.HasDecodedCommand)
                sb.AppendLine($"Decoded: {entry.DecodedCommand}");

            await clipboard.SetTextAsync(sb.ToString());

            if (PSHistoryStatusText != null)
                PSHistoryStatusText.Text = "Status: command details copied to clipboard.";
        }
    }

    private void PSHistoryAddToCaseButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (PSHistoryResultsList?.SelectedItem is not PowerShellHistoryEntry entry)
        {
            if (PSHistoryStatusText != null)
                PSHistoryStatusText.Text = "Status: select a command to add to case.";
            return;
        }

        string details = entry.HasDecodedCommand
            ? $"Command: {entry.Command}\nDecoded: {entry.DecodedCommand}"
            : $"Command: {entry.Command}";

        CaseManager.AddEvent("Persist", "Suspicious PowerShell command", entry.Severity,
            $"{entry.UserProfile} (PS {entry.PowerShellVersion})",
            $"{entry.RiskReason}\n{details}");

        if (PSHistoryStatusText != null)
            PSHistoryStatusText.Text = $"Status: added {entry.Severity} command to case.";
    }

    private async void PSHistoryExportButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (_psHistoryResult == null || _psHistoryResult.Entries.Count == 0)
        {
            if (PSHistoryStatusText != null)
                PSHistoryStatusText.Text = "Status: no history to export.";
            return;
        }

        try
        {
            string baseDir = AppContext.BaseDirectory;
            string logsDir = Path.Combine(baseDir, "logs", "PSHistory");
            Directory.CreateDirectory(logsDir);

            string fileName = $"PSHistory-{DateTime.Now:yyyyMMdd-HHmmss}.txt";
            string fullPath = Path.Combine(logsDir, fileName);

            var sb = new StringBuilder();
            sb.AppendLine("PowerShell History Analysis Report");
            sb.AppendLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"Case ID: {CaseManager.CaseId}");
            sb.AppendLine(new string('=', 80));
            sb.AppendLine();
            sb.AppendLine($"Summary: {_psHistoryResult.SummaryMessage}");
            sb.AppendLine($"HIGH Risk: {_psHistoryResult.HighRiskCount}");
            sb.AppendLine($"MEDIUM Risk: {_psHistoryResult.MediumRiskCount}");
            sb.AppendLine($"LOW Risk: {_psHistoryResult.LowRiskCount}");
            sb.AppendLine();
            sb.AppendLine(new string('=', 80));
            sb.AppendLine();

            // Export HIGH and MEDIUM first
            var suspicious = _psHistoryResult.Entries
                .Where(e => e.Severity == "HIGH" || e.Severity == "MEDIUM")
                .OrderByDescending(e => e.Severity == "HIGH")
                .ToList();

            if (suspicious.Any())
            {
                sb.AppendLine("SUSPICIOUS COMMANDS");
                sb.AppendLine(new string('-', 40));
                foreach (var entry in suspicious)
                {
                    sb.AppendLine($"[{entry.Severity}] {entry.UserProfile} (PS {entry.PowerShellVersion})");
                    sb.AppendLine($"Risk: {entry.RiskReason}");
                    sb.AppendLine($"Command: {entry.Command}");
                    if (entry.HasDecodedCommand)
                        sb.AppendLine($"Decoded: {entry.DecodedCommand}");
                    sb.AppendLine();
                }
            }

            await File.WriteAllTextAsync(fullPath, sb.ToString());

            if (PSHistoryStatusText != null)
                PSHistoryStatusText.Text = $"Status: exported to {fileName}";

            CaseManager.AddEvent("Persist", "PowerShell history exported", "INFO", fullPath, null);
        }
        catch (Exception ex)
        {
            if (PSHistoryStatusText != null)
                PSHistoryStatusText.Text = $"Status: export error - {ex.Message}";
        }
    }

    // ----------------------------
    // PS HISTORY – Populate User Filter
    // ----------------------------
    private void PopulatePSHistoryUserFilter()
    {
        if (_psHistoryResult == null || PSHistoryUserFilterCombo == null)
            return;

        var users = _psHistoryResult.Entries
            .Select(e => e.UserProfile)
            .Distinct()
            .OrderBy(u => u)
            .ToList();

        PSHistoryUserFilterCombo.Items.Clear();
        PSHistoryUserFilterCombo.Items.Add(new ComboBoxItem { Content = "All Users" });

        foreach (var user in users)
        {
            PSHistoryUserFilterCombo.Items.Add(new ComboBoxItem { Content = user });
        }

        PSHistoryUserFilterCombo.SelectedIndex = 0;
    }

#pragma warning restore CA1416
}

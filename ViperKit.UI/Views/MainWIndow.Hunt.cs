using System;
using System.IO;
using System.Collections.Generic;
using System.Text;
using System.Net;
using System.Net.NetworkInformation;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Microsoft.Win32;

namespace ViperKit.UI.Views;

public partial class MainWindow
{
    // =========================
    // HUNT TAB
    // =========================

    private void HuntRunButton_OnClick(object? sender, RoutedEventArgs e)
    {
        var iocText = HuntIocInput?.Text?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(iocText))
        {
            HuntStatusText.Text  = "Status: no IOC provided.";
            HuntResultsText.Text = "Enter an IOC above and click Run Hunt to simulate a search.";
            return;
        }

        // Figure out selected type
        var selectedIndex = HuntIocType?.SelectedIndex ?? 0;
        var effectiveType = DetermineIocType(iocText, selectedIndex);

        // Log the action
        LogHuntAction(iocText, effectiveType);

        var timestamp = DateTime.Now.ToString("HH:mm:ss");
        HuntStatusText.Text = $"Status: (demo) {effectiveType} hunt executed at {timestamp}.";

        switch (effectiveType)
        {
            case "FilePath":
                HandleFilePathHunt(iocText);
                break;

            case "Registry":
                HandleRegistryHunt(iocText);
                break;

            case "IpAddress":
                HandleIpHunt(iocText);
                break;

            case "DomainOrUrl":
                HandleDomainHunt(iocText);
                break;

            default:
                HuntResultsText.Text =
                    $"(demo) Received IOC of type {effectiveType}: \"{iocText}\".\n\n" +
                    "This IOC type is not wired yet.";
                break;
        }
    }

    private static string DetermineIocType(string ioc, int selectedIndex)
    {
        // Respect explicit selection from dropdown
        switch (selectedIndex)
        {
            case 1: return "FilePath";
            case 2: return "Hash";
            case 3: return "DomainOrUrl";
            case 4: return "IpAddress";
            case 5: return "Registry";
        }

        // Auto-detect (index 0)
        string lowered  = ioc.ToLowerInvariant();
        string trimmed  = ioc.Trim();
        string noSpaces = trimmed.Replace(" ", string.Empty);

        // Windows path (C:\ or \\server\share)
        if (trimmed.Contains(@":\") || lowered.StartsWith(@"\\"))
            return "FilePath";

        // Registry-style
        if (lowered.StartsWith("hklm\\") ||
            lowered.StartsWith("hkcu\\") ||
            lowered.StartsWith("hkcr\\") ||
            lowered.StartsWith("hku\\")  ||
            lowered.StartsWith("hkcc\\") ||
            lowered.StartsWith("hkey_local_machine\\")   ||
            lowered.StartsWith("hkey_current_user\\")    ||
            lowered.StartsWith("hkey_classes_root\\")    ||
            lowered.StartsWith("hkey_users\\")           ||
            lowered.StartsWith("hkey_current_config\\"))
        {
            return "Registry";
        }

        // IP-ish: 3 dots and only digits/dots
        int dotCount = 0;
        foreach (char c in trimmed)
        {
            if (c == '.') dotCount++;
        }
        if (dotCount == 3 && IsIpLike(trimmed))
            return "IpAddress";

        // Hash-ish: 32–64 hex chars, no spaces
        if (noSpaces.Length >= 32 && noSpaces.Length <= 64 && IsHexString(noSpaces))
            return "Hash";

        // Everything else → treat as Domain/URL
        return "DomainOrUrl";
    }

    private static bool IsHexString(string value)
    {
        foreach (char c in value)
        {
            bool isDigit     = c >= '0' && c <= '9';
            bool isHexLetter = (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F');

            if (!isDigit && !isHexLetter)
                return false;
        }
        return true;
    }

    private static bool IsIpLike(string value)
    {
        foreach (char c in value)
        {
            if (c != '.' && (c < '0' || c > '9'))
                return false;
        }
        return true;
    }

    // ---- File / Folder hunt ----
    private void HandleFilePathHunt(string path)
    {
        try
        {
            // If it's a directory
            if (Directory.Exists(path))
            {
                var dirInfo = new DirectoryInfo(path);
                long totalSize = 0;
                int fileCount = 0;

                foreach (var file in dirInfo.EnumerateFiles("*", SearchOption.AllDirectories))
                {
                    fileCount++;
                    totalSize += file.Length;
                }

                HuntResultsText.Text =
                    $"Folder found:\n" +
                    $"  Path: {dirInfo.FullName}\n" +
                    $"  Files: {fileCount}\n" +
                    $"  Approx. size: {totalSize} bytes\n\n" +
                    "(demo) Future builds: walk this tree for artifacts, suspicious names, etc.";
                return;
            }

            // If it's a file
            if (File.Exists(path))
            {
                var info = new FileInfo(path);
                HuntResultsText.Text =
                    $"File found:\n" +
                    $"  Path: {info.FullName}\n" +
                    $"  Size: {info.Length} bytes\n" +
                    $"  Created: {info.CreationTime}\n" +
                    $"  Modified: {info.LastWriteTime}\n\n" +
                    "(demo) Future builds: hash + correlate against other sources.";
                return;
            }

            // Neither file nor directory exists
            HuntResultsText.Text =
                $"File or folder not found at:\n  {path}\n\n" +
                "Verify the path is correct and accessible from this machine.";
        }
        catch (Exception ex)
        {
            HuntResultsText.Text =
                $"Error while checking path:\n  {ex.Message}\n\n" +
                "Make sure you have permission to access this path.";
        }
    }

    // ---- IP hunt ----
    private void HandleIpHunt(string input)
    {
        if (!IPAddress.TryParse(input.Trim(), out var ip))
        {
            HuntResultsText.Text =
                $"\"{input}\" does not look like a valid IP address.\n" +
                "Example: 1.2.3.4";
            return;
        }

        var sb = new StringBuilder();
        sb.AppendLine($"IP address: {ip}");
        sb.AppendLine($"Family: {ip.AddressFamily}");

        // Reverse DNS
        try
        {
            var hostEntry = Dns.GetHostEntry(ip);
            if (hostEntry.Aliases.Length == 0 && string.IsNullOrWhiteSpace(hostEntry.HostName))
            {
                sb.AppendLine("Reverse DNS: (no hostnames returned)");
            }
            else
            {
                sb.AppendLine("Reverse DNS:");
                if (!string.IsNullOrWhiteSpace(hostEntry.HostName))
                    sb.AppendLine($"  Primary: {hostEntry.HostName}");

                foreach (var alias in hostEntry.Aliases)
                    sb.AppendLine($"  Alias:   {alias}");
            }
        }
        catch (Exception ex)
        {
            sb.AppendLine($"Reverse DNS: error – {ex.Message}");
        }

        // Reachability ping
        try
        {
            using var ping = new Ping();
            var reply = ping.Send(ip, 1000); // 1 second timeout

            sb.AppendLine();
            sb.AppendLine("Ping test:");
            sb.AppendLine($"  Status: {reply.Status}");

            if (reply.Status == IPStatus.Success)
                sb.AppendLine($"  Roundtrip: {reply.RoundtripTime} ms");
        }
        catch (Exception ex)
        {
            sb.AppendLine();
            sb.AppendLine($"Ping test: error – {ex.Message}");
        }

        sb.AppendLine();
        sb.AppendLine("(demo) Future builds: correlate IP against threat intel feeds, geo, ASN, etc.");

        HuntResultsText.Text = sb.ToString();
    }

    // ---- Domain / URL hunt ----
    private void HandleDomainHunt(string ioc)
    {
        string host = ioc.Trim();

        // Try to extract host from URL if needed
        if (Uri.TryCreate(host, UriKind.Absolute, out var uri))
        {
            host = uri.Host;
        }
        else if (Uri.TryCreate("http://" + host, UriKind.Absolute, out var uri2))
        {
            host = uri2.Host;
        }

        host = host.Trim().TrimEnd('/');

        if (string.IsNullOrWhiteSpace(host))
        {
            HuntResultsText.Text =
                $"Could not parse host from: \"{ioc}\"";
            return;
        }

        var sb = new StringBuilder();
        sb.AppendLine($"Domain / URL IOC: {ioc}");
        sb.AppendLine($"Host parsed:      {host}");
        sb.AppendLine();

        try
        {
            var addresses = Dns.GetHostAddresses(host);

            if (addresses.Length == 0)
            {
                sb.AppendLine("DNS: no addresses returned.");
            }
            else
            {
                sb.AppendLine("DNS addresses:");
                foreach (var addr in addresses)
                    sb.AppendLine($"  {addr} ({addr.AddressFamily})");
            }
        }
        catch (Exception ex)
        {
            sb.AppendLine($"DNS lookup error: {ex.Message}");
        }

        sb.AppendLine();
        sb.AppendLine("(demo) Future builds: pivot from this host into IP hunt, WHOIS, HTTP banner grabs, etc.");

        HuntResultsText.Text = sb.ToString();
    }

    // ---- Registry hunt ----
    private void HandleRegistryHunt(string regPath)
    {
#pragma warning disable CA1416 // Registry is Windows-only
        try
        {
            if (string.IsNullOrWhiteSpace(regPath))
            {
                HuntResultsText.Text = "Registry path was empty.";
                return;
            }

            // Normalise slashes
            string cleaned = regPath.Trim().Replace('/', '\\');
            int firstSlash = cleaned.IndexOf('\\');
            if (firstSlash <= 0)
            {
                HuntResultsText.Text =
                    $"Could not parse registry path:\n  {cleaned}\n\n" +
                    "Expected format: HKLM\\Path\\To\\Key";
                return;
            }

            string hivePart   = cleaned[..firstSlash];
            string subKeyPath = cleaned[(firstSlash + 1)..];

            RegistryKey? root = hivePart.ToUpperInvariant() switch
            {
                "HKLM" or "HKEY_LOCAL_MACHINE"      => Registry.LocalMachine,
                "HKCU" or "HKEY_CURRENT_USER"       => Registry.CurrentUser,
                "HKCR" or "HKEY_CLASSES_ROOT"       => Registry.ClassesRoot,
                "HKU"  or "HKEY_USERS"              => Registry.Users,
                "HKCC" or "HKEY_CURRENT_CONFIG"     => Registry.CurrentConfig,
                _ => null
            };

            if (root == null)
            {
                HuntResultsText.Text =
                    $"Unknown registry hive in path:\n  {cleaned}\n\n" +
                    "Supported hives: HKLM, HKCU, HKCR, HKU, HKCC.";
                return;
            }

            using var key = root.OpenSubKey(subKeyPath);
            if (key == null)
            {
                HuntResultsText.Text =
                    $"Registry key not found:\n  {hivePart}\\{subKeyPath}";
                return;
            }

            var valueLines = new List<string>();
            foreach (var valueName in key.GetValueNames())
            {
                object? value = key.GetValue(valueName);
                var kind      = key.GetValueKind(valueName);

                string valueDisplay = value switch
                {
                    string s        => s,
                    string[] arr    => string.Join(", ", arr),
                    _               => value?.ToString() ?? "(null)"
                };

                valueLines.Add($"    {valueName} ({kind}): {valueDisplay}");
            }

            var subKeys = key.GetSubKeyNames();

            var sb = new StringBuilder();
            sb.AppendLine("Registry key found:");
            sb.AppendLine($"  Path: {hivePart}\\{subKeyPath}");
            sb.AppendLine($"  Subkeys: {subKeys.Length}");
            sb.AppendLine($"  Values: {valueLines.Count}");
            sb.AppendLine();
            sb.AppendLine("Values:");
            if (valueLines.Count == 0)
            {
                sb.AppendLine("    (no values)");
            }
            else
            {
                foreach (var line in valueLines)
                    sb.AppendLine(line);
            }

            HuntResultsText.Text = sb.ToString();
        }
        catch (Exception ex)
        {
            HuntResultsText.Text =
                $"Error while reading registry key:\n  {ex.Message}";
        }
#pragma warning restore CA1416
    }

    // ---- Logging ----
    private void LogHuntAction(string ioc, string type)
    {
        try
        {
            string baseDir = AppContext.BaseDirectory;
            string logDir  = Path.Combine(baseDir, "logs");
            Directory.CreateDirectory(logDir);

            string logPath = Path.Combine(logDir, "Hunt.log");
            string line    = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}\t{type}\t{ioc}";

            File.AppendAllLines(logPath, new[] { line });
        }
        catch
        {
            // Logging should never break the UI
        }
    }
}

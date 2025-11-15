using System;
using System.IO;
using System.Collections.Generic;
using System.Text;
using System.Net;
using System.Net.NetworkInformation;
using System.Security.Cryptography;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Microsoft.Win32;
using System.Net.Http;


namespace ViperKit.UI.Views;

public partial class MainWindow
{
    private static readonly HttpClient HttpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(3)
    };
    
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
        HuntStatusText.Text = $"Status:  {effectiveType} hunt executed at {timestamp}.";

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

            case "Hash":
                HandleHashHunt(iocText);
                break;

            default:
                HuntResultsText.Text =
                    $"Received IOC of type {effectiveType}: \"{iocText}\".\n\n" +
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
        if (trimmed.Contains(@":\") || lowered.StartsWith(@"\\")) return "FilePath";

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
            if (c == '.') dotCount++;

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
                    "Future builds: walk this tree for artifacts, suspicious names, etc.";
                return;
            }

            // If it's a file
            if (File.Exists(path))
            {
                var info = new FileInfo(path);

                string md5Hex    = "(error)";
                string sha256Hex = "(error)";

                try
                {
                    using var stream = File.OpenRead(path);

                    // MD5
                    using (var md5 = MD5.Create())
                    {
                        var md5Bytes = md5.ComputeHash(stream);
                        md5Hex = Convert.ToHexString(md5Bytes);
                    }

                    // Reset stream for second hash
                    stream.Position = 0;

                    // SHA-256
                    using (var sha = SHA256.Create())
                    {
                        var shaBytes = sha.ComputeHash(stream);
                        sha256Hex = Convert.ToHexString(shaBytes);
                    }

                    // Log both hashes so they show up in Hashes.log as well
                    LogHashObserved(md5Hex.ToLowerInvariant(),  "MD5 (file path hunt)");
                    LogHashObserved(sha256Hex.ToLowerInvariant(), "SHA-256 (file path hunt)");
                }
                catch (Exception ex)
                {
                    // If hashing fails, we still show file metadata
                    md5Hex    = $"(error computing hash: {ex.Message})";
                    sha256Hex = "(not computed)";
                }

                var sb = new StringBuilder();
                sb.AppendLine("File found:");
                sb.AppendLine($"  Path:     {info.FullName}");
                sb.AppendLine($"  Size:     {info.Length} bytes");
                sb.AppendLine($"  Created:  {info.CreationTime}");
                sb.AppendLine($"  Modified: {info.LastWriteTime}");
                sb.AppendLine();
                sb.AppendLine("Hashes:");
                sb.AppendLine($"  MD5:      {md5Hex}");
                sb.AppendLine($"  SHA-256:  {sha256Hex}");

                HuntResultsText.Text = sb.ToString();
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
        sb.AppendLine("Future builds: correlate IP against threat intel feeds, geo, ASN, etc.");

        HuntResultsText.Text = sb.ToString();
    }

    // ---- Domain / URL hunt ----
    private async void HandleDomainHunt(string ioc)
    {
        string original = ioc ?? string.Empty;
        string host     = original.Trim();

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
            HuntResultsText.Text = $"Could not parse host from: \"{original}\"";
            return;
        }

        var sb = new StringBuilder();
        sb.AppendLine($"Domain / URL IOC: {original}");
        sb.AppendLine($"Host parsed:      {host}");
        sb.AppendLine();

        // --- DNS resolution ---
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
        sb.AppendLine("HTTP probe (best effort):");

        // Build something we can actually request
        string urlToCheck = original.Trim();

        if (!urlToCheck.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
            !urlToCheck.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            urlToCheck = "http://" + host;
        }

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Head, urlToCheck);
            request.Headers.UserAgent.ParseAdd("ViperKit/0.1");

            using var response = await HttpClient.SendAsync(request);

            sb.AppendLine($"  URL:    {urlToCheck}");
            sb.AppendLine($"  Status: {(int)response.StatusCode} {response.ReasonPhrase}");

            if (response.Headers.TryGetValues("Server", out var serverValues))
            {
                sb.AppendLine($"  Server: {string.Join(", ", serverValues)}");
            }

            if (response.Content.Headers.ContentType is not null)
            {
                sb.AppendLine($"  Content-Type: {response.Content.Headers.ContentType}");
            }
        }
        catch (Exception ex)
        {
            sb.AppendLine($"  Error making HTTP request: {ex.Message}");
        }

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
                "HKLM" or "HKEY_LOCAL_MACHINE"  => Registry.LocalMachine,
                "HKCU" or "HKEY_CURRENT_USER"   => Registry.CurrentUser,
                "HKCR" or "HKEY_CLASSES_ROOT"   => Registry.ClassesRoot,
                "HKU"  or "HKEY_USERS"          => Registry.Users,
                "HKCC" or "HKEY_CURRENT_CONFIG" => Registry.CurrentConfig,
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
                    string s     => s,
                    string[] arr => string.Join(", ", arr),
                    _            => value?.ToString() ?? "(null)"
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

    // ---- Hash hunt (with optional disk sweep) ----
    private void HandleHashHunt(string hash)
    {
        if (string.IsNullOrWhiteSpace(hash))
        {
            HuntResultsText.Text = "Hash IOC was empty.";
            return;
        }

        string original   = hash.Trim();
        string normalized = original.Replace(" ", "").ToLowerInvariant();

        string kind;
        bool   canScanDisk = false;

        switch (normalized.Length)
        {
            case 32:
                kind        = "MD5 (32 hex chars)";
                canScanDisk = true;
                break;
            case 40:
                kind        = "SHA-1 (40 hex chars)";
                canScanDisk = false; // we won't scan disk on SHA-1 for now
                break;
            case 64:
                kind        = "SHA-256 (64 hex chars)";
                canScanDisk = true;
                break;
            default:
                kind        = $"Unknown / non-standard length ({normalized.Length} chars)";
                canScanDisk = false;
                break;
        }

        var sb = new StringBuilder();
        sb.AppendLine($"Hash IOC: {original}");
        sb.AppendLine($"Normalized: {normalized}");
        sb.AppendLine($"Detected type: {kind}");
        sb.AppendLine();

        // Optional disk sweep if a scope folder is set
        string? scopeFolder = HuntScopeFolderInput?.Text?.Trim();
        if (canScanDisk && !string.IsNullOrWhiteSpace(scopeFolder) && Directory.Exists(scopeFolder))
        {
            sb.AppendLine($"Disk scan scope: {scopeFolder}");
            sb.AppendLine();

            int matchCount = 0;
            var matches = new List<string>();

            try
            {
                foreach (var file in Directory.EnumerateFiles(scopeFolder, "*", SearchOption.AllDirectories))
                {
                    try
                    {
                        using var stream = File.OpenRead(file);
                        byte[] hashBytes;

                        if (normalized.Length == 32)
                        {
                            hashBytes = MD5.HashData(stream);
                        }
                        else // 64
                        {
                            hashBytes = SHA256.HashData(stream);
                        }

                        string fileHash = BitConverter.ToString(hashBytes)
                            .Replace("-", "")
                            .ToLowerInvariant();

                        if (fileHash == normalized)
                        {
                            matchCount++;
                            matches.Add(file);
                        }
                    }
                    catch
                    {
                        // Skip unreadable files, don't kill the scan
                    }
                }

                if (matchCount == 0)
                {
                    sb.AppendLine("Disk scan: no matching files found under scope.");
                }
                else
                {
                    sb.AppendLine($"Disk scan: {matchCount} matching file(s) found:");
                    foreach (var m in matches)
                        sb.AppendLine($"  {m}");
                }
            }
            catch (Exception ex)
            {
                sb.AppendLine($"Disk scan error: {ex.Message}");
            }
        }
        else
        {
            sb.AppendLine("Disk scan: (not run).");
            if (!canScanDisk)
                sb.AppendLine("Reason: only MD5 (32 chars) and SHA-256 (64 chars) are supported for disk scanning right now.");
            else
                sb.AppendLine("Reason: no valid scope folder set. Use the Scope folder row above to pick a directory.");
        }

        sb.AppendLine();
        sb.AppendLine("Future builds: pivot hash into VT/OSINT, correlate across hosts/files, etc.");

        HuntResultsText.Text = sb.ToString();

        LogHashObserved(normalized, kind);
    }

    private void LogHashObserved(string normalizedHash, string kind)
    {
        try
        {
            string baseDir = AppContext.BaseDirectory;
            string logDir  = Path.Combine(baseDir, "logs");
            Directory.CreateDirectory(logDir);

            string logPath = Path.Combine(logDir, "Hashes.log");
            string line    = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}\t{kind}\t{normalizedHash}";

            File.AppendAllLines(logPath, new[] { line });
        }
        catch
        {
            // Logging must never break UI
        }
    }

    // ---- Hunt utility actions: Copy / Save / Clear ----

    private async void HuntCopyResultsButton_OnClick(object? sender, RoutedEventArgs e)
    {
        var text = HuntResultsText?.Text ?? string.Empty;

        if (string.IsNullOrWhiteSpace(text))
        {
            HuntStatusText.Text = "Status: nothing to copy.";
            return;
        }

        try
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel?.Clipboard is not null)
            {
                await topLevel.Clipboard.SetTextAsync(text);
                HuntStatusText.Text = "Status: hunt results copied to clipboard.";
            }
            else
            {
                HuntStatusText.Text = "Status: clipboard not available.";
            }
        }
        catch (Exception ex)
        {
            HuntStatusText.Text = $"Status: error copying to clipboard – {ex.Message}";
        }
    }

    private void HuntSaveResultsButton_OnClick(object? sender, RoutedEventArgs e)
    {
        var text = HuntResultsText?.Text ?? string.Empty;

        if (string.IsNullOrWhiteSpace(text))
        {
            HuntStatusText.Text = "Status: nothing to save.";
            return;
        }

        try
        {
            string baseDir     = AppContext.BaseDirectory;
            string snapshotDir = Path.Combine(baseDir, "logs", "HuntSnapshots");
            Directory.CreateDirectory(snapshotDir);

            string stamp    = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string fileName = $"Hunt_{stamp}.txt";
            string fullPath = Path.Combine(snapshotDir, fileName);

            File.WriteAllText(fullPath, text, Encoding.UTF8);
            HuntStatusText.Text = $"Status: results saved to {fileName}.";
        }
        catch (Exception ex)
        {
            HuntStatusText.Text = $"Status: error saving snapshot – {ex.Message}";
        }
    }

    private void HuntClearButton_OnClick(object? sender, RoutedEventArgs e)
    {
        HuntResultsText.Text = string.Empty;
        HuntStatusText.Text  = "Status: cleared.";
    }

    // ---- Browse for scope folder ----
    private async void HuntBrowseScopeButton_OnClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            var dialog = new OpenFolderDialog
            {
                Title = "Select scope folder for hash / file hunts"
            };

            // MainWindow *is* a Window, so we can use it directly
            string? path = await dialog.ShowAsync(this);

            if (!string.IsNullOrWhiteSpace(path))
            {
                HuntScopeFolderInput.Text = path;
                HuntStatusText.Text       = "Status: scope folder updated.";
            }
        }
        catch (Exception ex)
        {
            HuntStatusText.Text = $"Status: error opening folder dialog – {ex.Message}";
        }
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

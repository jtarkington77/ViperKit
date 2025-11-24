using System;
using System.IO;
using System.Collections.Generic;
using System.Text;
using System.Net;
using System.Net.NetworkInformation;
using System.Security.Cryptography;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Microsoft.Win32;
using System.Net.Http;
using ViperKit.UI.Models;
using ViperKit.UI;



namespace ViperKit.UI.Views;

public partial class MainWindow
{
    private static readonly HttpClient HttpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(3)
    };
    private readonly List<HuntResult> _huntResults = new();


    // =========================
    // HUNT TAB
    // =========================

   private async void HuntRunButton_OnClick(object? sender, RoutedEventArgs e)
    {
        var iocText = HuntIocInput?.Text?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(iocText))
        {
            if (HuntStatusText != null)
                HuntStatusText.Text = "Status: no IOC provided.";

            if (HuntResultsText != null)
                HuntResultsText.Text = "Enter an IOC above and click Run Hunt to search.";

            return;
        }

        // Disable button during hunt to prevent double-clicks
        if (sender is Button btn)
            btn.IsEnabled = false;

        try
        {
            // Clear previous structured results
            _huntResults.Clear();
            if (HuntResultsList != null)
                HuntResultsList.ItemsSource = null;

            // Figure out selected type
            var selectedIndex = HuntIocType?.SelectedIndex ?? 0;
            var effectiveType = DetermineIocType(iocText, selectedIndex);

            // Log the action (file, json, case)
            LogHuntAction(iocText, effectiveType);

            var timestamp = DateTime.Now.ToString("HH:mm:ss");
            if (HuntStatusText != null)
                HuntStatusText.Text = $"Status: {effectiveType} hunt running...";

            switch (effectiveType)
            {
                case "FilePath":
                    await HandleFilePathHuntAsync(iocText);
                    break;

                case "Registry":
                    HandleRegistryHunt(iocText);
                    break;

                case "IpAddress":
                    await HandleIpHuntAsync(iocText);
                    break;

                case "DomainOrUrl":
                    await HandleDomainHuntAsync(iocText);
                    break;

                case "Hash":
                    await HandleHashHuntAsync(iocText);
                    break;

                case "NameKeyword":
                    await HandleNameKeywordHuntAsync(iocText);
                    break;

                default:
                    if (HuntResultsText != null)
                    {
                        HuntResultsText.Text =
                            $"Received IOC of type {effectiveType}: \"{iocText}\".\n\n" +
                            "This IOC type is not wired yet.";
                    }
                    break;
            }

            if (HuntStatusText != null)
                HuntStatusText.Text = $"Status: {effectiveType} hunt completed at {timestamp}.";

            // Keep dashboard + case tab in sync
            try
            {
                UpdateDashboardCaseSummary();
                RefreshCaseTab();
            }
            catch
            {
                // Never let UI updates crash the hunt
            }
        }
        finally
        {
            // Re-enable button
            if (sender is Button btn2)
                btn2.IsEnabled = true;
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
            if (c == '.') dotCount++;

        if (dotCount == 3 && IsIpLike(trimmed))
            return "IpAddress";

        // Hash-ish: 32–64 hex chars, no spaces
        if (noSpaces.Length >= 32 && noSpaces.Length <= 64 && IsHexString(noSpaces))
            return "Hash";

        // Domain-ish: has a dot and letters on both sides (example.com, foo.bar)
        var parts = noSpaces.Split('.');
        if (parts.Length >= 2 &&
            parts[0].Length > 0 &&
            parts[^1].Length > 0 &&
            ContainsLetter(parts[0]) &&
            ContainsLetter(parts[^1]))
        {
            return "DomainOrUrl";
        }

        // Everything else → treat as name.
        return "NameKeyword";
    }

    private static bool ContainsLetter(string value)
    {
        foreach (char c in value)
        {
            if (char.IsLetter(c))
                return true;
        }
        return false;
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
    private async Task HandleFilePathHuntAsync(string path)
    {
        try
        {
            // If it's a directory - run enumeration on background thread
            if (Directory.Exists(path))
            {
                var dirInfo = new DirectoryInfo(path);

                // Run file enumeration on background thread to avoid UI freeze
                var (fileCount, totalSize) = await Task.Run(() =>
                {
                    long size = 0;
                    int count = 0;
                    try
                    {
                        foreach (var file in EnumerateFilesSafe(path))
                        {
                            count++;
                            try { size += new FileInfo(file).Length; }
                            catch { /* skip files we can't read */ }
                        }
                    }
                    catch { /* enumeration error */ }
                    return (count, size);
                });

                var text =
                    $"Folder found:\n" +
                    $"  Path: {dirInfo.FullName}\n" +
                    $"  Files: {fileCount}\n" +
                    $"  Approx. size: {totalSize} bytes\n\n" +
                    "Future builds: walk this tree for artifacts, suspicious names, etc.";

                if (HuntResultsText != null)
                    HuntResultsText.Text = text;

                // Add structured result
                _huntResults.Add(new HuntResult
                {
                    Category = "Folder",
                    Target   = dirInfo.FullName,
                    Severity = "INFO",
                    Summary  = "Folder IOC checked",
                    Details  = $"Files: {fileCount}, Approx. size: {totalSize} bytes"
                });

                RefreshHuntResultsList();
                return;
            }

            // If it's a file - run hashing on background thread
            if (File.Exists(path))
            {
                var info = new FileInfo(path);

                // Run hash computation on background thread
                var (md5Hex, sha256Hex) = await Task.Run(() =>
                {
                    string md5 = "(error)";
                    string sha256 = "(error)";

                    try
                    {
                        using var stream = File.OpenRead(path);

                        // MD5
                        var md5Bytes = MD5.HashData(stream);
                        md5 = Convert.ToHexString(md5Bytes);

                        // Reset stream for second hash
                        stream.Position = 0;

                        // SHA-256
                        var shaBytes = SHA256.HashData(stream);
                        sha256 = Convert.ToHexString(shaBytes);
                    }
                    catch (Exception ex)
                    {
                        md5 = $"(error: {ex.Message})";
                        sha256 = "(not computed)";
                    }

                    return (md5, sha256);
                });

                // Log both hashes so they show up in Hashes.log as well
                if (!md5Hex.StartsWith("(error"))
                    LogHashObserved(md5Hex.ToLowerInvariant(), "MD5 (file path hunt)");
                if (!sha256Hex.StartsWith("("))
                    LogHashObserved(sha256Hex.ToLowerInvariant(), "SHA-256 (file path hunt)");

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

                if (HuntResultsText != null)
                    HuntResultsText.Text = sb.ToString();

                // Add structured result
                _huntResults.Add(new HuntResult
                {
                    Category = "File",
                    Target   = info.FullName,
                    Severity = "INFO",
                    Summary  = "File IOC checked",
                    Details  = $"Size: {info.Length} bytes; MD5: {md5Hex}; SHA-256: {sha256Hex}"
                });

                RefreshHuntResultsList();
                return;
            }

            // Neither file nor directory exists
            if (HuntResultsText != null)
            {
                HuntResultsText.Text =
                    $"File or folder not found at:\n  {path}\n\n" +
                    "Verify the path is correct and accessible from this machine.";
            }

            _huntResults.Add(new HuntResult
            {
                Category = "File",
                Target   = path,
                Severity = "WARN",
                Summary  = "Path not found",
                Details  = "Verify the path is correct and accessible from this machine."
            });

            RefreshHuntResultsList();
        }
        catch (Exception ex)
        {
            if (HuntResultsText != null)
            {
                HuntResultsText.Text =
                    $"Error while checking path:\n  {ex.Message}\n\n" +
                    "Make sure you have permission to access this path.";
            }

            _huntResults.Add(new HuntResult
            {
                Category = "File",
                Target   = path,
                Severity = "WARN",
                Summary  = "Error while checking path",
                Details  = ex.Message
            });

            RefreshHuntResultsList();
        }
    }

    /// <summary>
    /// Helper to refresh hunt results list - reduces code duplication.
    /// </summary>
    private void RefreshHuntResultsList()
    {
        if (HuntResultsList != null)
        {
            HuntResultsList.ItemsSource = null;
            HuntResultsList.ItemsSource = _huntResults.ToArray();
        }
    }


    // ---- IP hunt ----
    private async Task HandleIpHuntAsync(string input)
    {
        if (!IPAddress.TryParse(input.Trim(), out var ip))
        {
            if (HuntResultsText != null)
            {
                HuntResultsText.Text =
                    $"\"{input}\" does not look like a valid IP address.\n" +
                    "Example: 1.2.3.4";
            }

            _huntResults.Add(new HuntResult
            {
                Category = "IP",
                Target   = input,
                Severity = "WARN",
                Summary  = "Invalid IP address format",
                Details  = "Example: 1.2.3.4"
            });

            RefreshHuntResultsList();
            return;
        }

        var sb = new StringBuilder();
        sb.AppendLine($"IP address: {ip}");
        sb.AppendLine($"Family: {ip.AddressFamily}");

        // Reverse DNS - run on background thread with timeout
        string reverseDnsSummary;
        try
        {
            var hostEntry = await Task.Run(() =>
            {
                try { return Dns.GetHostEntry(ip); }
                catch { return null; }
            });

            if (hostEntry == null || (hostEntry.Aliases.Length == 0 && string.IsNullOrWhiteSpace(hostEntry.HostName)))
            {
                sb.AppendLine("Reverse DNS: (no hostnames returned)");
                reverseDnsSummary = "Reverse DNS: (no hostnames)";
            }
            else
            {
                sb.AppendLine("Reverse DNS:");
                if (!string.IsNullOrWhiteSpace(hostEntry.HostName))
                    sb.AppendLine($"  Primary: {hostEntry.HostName}");

                foreach (var alias in hostEntry.Aliases)
                    sb.AppendLine($"  Alias:   {alias}");

                reverseDnsSummary = string.IsNullOrWhiteSpace(hostEntry.HostName)
                    ? "Reverse DNS: aliases returned"
                    : $"Reverse DNS: {hostEntry.HostName}";
            }
        }
        catch (Exception ex)
        {
            sb.AppendLine($"Reverse DNS: error – {ex.Message}");
            reverseDnsSummary = "Reverse DNS lookup error";
        }

        // Reachability ping - run on background thread
        string pingSummary;
        try
        {
            var reply = await Task.Run(() =>
            {
                using var ping = new Ping();
                return ping.Send(ip, 1000); // 1 second timeout
            });

            sb.AppendLine();
            sb.AppendLine("Ping test:");
            sb.AppendLine($"  Status: {reply.Status}");

            if (reply.Status == IPStatus.Success)
            {
                sb.AppendLine($"  Roundtrip: {reply.RoundtripTime} ms");
                pingSummary = $"Ping: {reply.Status}, {reply.RoundtripTime} ms";
            }
            else
            {
                pingSummary = $"Ping: {reply.Status}";
            }
        }
        catch (Exception ex)
        {
            sb.AppendLine();
            sb.AppendLine($"Ping test: error – {ex.Message}");
            pingSummary = "Ping error";
        }

        sb.AppendLine();
        sb.AppendLine("Next steps: if this IP looks suspicious, set it as case focus and pivot into Persist/Sweep to look for related services, tasks, and binaries.");

        if (HuntResultsText != null)
            HuntResultsText.Text = sb.ToString();

        // Structured result row
        _huntResults.Add(new HuntResult
        {
            Category = "IP",
            Target   = ip.ToString(),
            Severity = "INFO",
            Summary  = "IP IOC checked",
            Details  = $"{reverseDnsSummary}; {pingSummary}"
        });

        RefreshHuntResultsList();
    }

    // ---- Name / keyword hunt (ScreenConnect, AnyDesk, etc.) ----
    private async Task HandleNameKeywordHuntAsync(string term)
    {
        string keyword = term?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(keyword))
        {
            if (HuntResultsText != null)
                HuntResultsText.Text = "Name / keyword IOC was empty.";
            return;
        }

        var sb = new StringBuilder();
        sb.AppendLine($"Name / keyword hunt: \"{keyword}\"");
        sb.AppendLine();

        if (HuntStatusText != null)
            HuntStatusText.Text = "Status: searching processes...";

        // 1) Processes - run on background thread
        sb.AppendLine("=== RUNNING PROCESSES ===");
        bool anyProcess = false;

        try
        {
            var processMatches = await Task.Run(() =>
            {
                var matches = new List<(string Name, int Id, string? Path)>();
                foreach (var proc in Process.GetProcesses())
                {
                    try
                    {
                        string name = proc.ProcessName ?? string.Empty;
                        if (name.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            string? exePath = null;
                            try { exePath = proc.MainModule?.FileName; } catch { }
                            matches.Add((name, proc.Id, exePath));
                        }
                    }
                    finally
                    {
                        proc.Dispose();
                    }
                }
                return matches;
            });

            foreach (var (name, pid, path) in processMatches)
            {
                anyProcess = true;
                sb.AppendLine($"  {name} (PID {pid})");
                if (!string.IsNullOrEmpty(path))
                    sb.AppendLine($"    Path: {path}");

                _huntResults.Add(new HuntResult
                {
                    Category = "Process",
                    Target   = path ?? $"{name} (PID {pid})",
                    Severity = "WARN",
                    Summary  = $"Running process: {name}",
                    Details  = $"PID: {pid}"
                });
            }
        }
        catch (Exception ex)
        {
            sb.AppendLine($"  Error: {ex.Message}");
        }

        if (!anyProcess)
            sb.AppendLine("  (none found)");

        // 2) Build comprehensive search locations
        if (HuntStatusText != null)
            HuntStatusText.Text = "Status: building search locations...";

        var searchRoots = BuildKeywordSearchRoots();

        sb.AppendLine();
        sb.AppendLine($"=== FILE SYSTEM SEARCH ({searchRoots.Count} locations) ===");

        int totalFilesFound = 0;
        int totalFoldersFound = 0;
        int locationsSearched = 0;

        foreach (var (label, rootPath) in searchRoots)
        {
            locationsSearched++;
            if (HuntStatusText != null)
                HuntStatusText.Text = $"Status: searching {label}... ({locationsSearched}/{searchRoots.Count})";

            // Search this root for matching folders and files
            var (folders, files) = await Task.Run(() => SearchLocationForKeyword(rootPath, keyword));

            if (folders.Count > 0 || files.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine($"[{label}]");

                foreach (var folder in folders)
                {
                    totalFoldersFound++;
                    sb.AppendLine($"  FOLDER: {folder}");

                    _huntResults.Add(new HuntResult
                    {
                        Category = "Folder",
                        Target   = folder,
                        Severity = IsSuspiciousHuntLocation(folder) ? "WARN" : "INFO",
                        Summary  = $"Folder matches \"{keyword}\"",
                        Details  = $"Found in {label}"
                    });
                }

                foreach (var file in files)
                {
                    totalFilesFound++;
                    sb.AppendLine($"  FILE: {file}");

                    _huntResults.Add(new HuntResult
                    {
                        Category = "File",
                        Target   = file,
                        Severity = IsSuspiciousHuntLocation(file) ? "WARN" : "INFO",
                        Summary  = $"File matches \"{keyword}\"",
                        Details  = $"Found in {label}"
                    });
                }
            }
        }

        if (totalFilesFound == 0 && totalFoldersFound == 0)
        {
            sb.AppendLine("  (no matching files or folders found)");
        }
        else
        {
            sb.AppendLine();
            sb.AppendLine($"Total: {totalFoldersFound} folder(s), {totalFilesFound} file(s) matching \"{keyword}\"");
        }

        // 3) Optional scope folder: file-name scan (e.g. point at C:\ or ProgramData)
        string? scopeFolder = HuntScopeFolderInput?.Text?.Trim();
        if (!string.IsNullOrWhiteSpace(scopeFolder) && Directory.Exists(scopeFolder))
        {
            const int MaxFilesToScan = 4000;

            sb.AppendLine();
            sb.AppendLine($"Scope folder file-name scan: {scopeFolder}");
            sb.AppendLine($"Max files to scan this run: {MaxFilesToScan}");

            if (HuntStatusText != null)
                HuntStatusText.Text = "Status: keyword disk scan running...";

            try
            {
                // Run file scan on background thread
                var (fileMatches, filesScanned, limitHit) = await Task.Run(() =>
                {
                    var matches = new List<string>();
                    int scanned = 0;
                    bool hitLimit = false;

                    foreach (var file in EnumerateFilesSafe(scopeFolder))
                    {
                        if (scanned >= MaxFilesToScan)
                        {
                            hitLimit = true;
                            break;
                        }

                        scanned++;

                        string fileName = Path.GetFileName(file);
                        if (fileName.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            matches.Add(file);
                        }
                    }

                    return (matches, scanned, hitLimit);
                });

                foreach (var file in fileMatches)
                {
                    sb.AppendLine($"  {file}");

                    _huntResults.Add(new HuntResult
                    {
                        Category = "File",
                        Target   = file,
                        Severity = "INFO",
                        Summary  = "File name match",
                        Details  = $"Found under scope folder {scopeFolder}"
                    });
                }

                sb.AppendLine();
                sb.AppendLine($"Files scanned: {filesScanned}");
                sb.AppendLine($"Name matches: {fileMatches.Count}");
                if (limitHit)
                    sb.AppendLine("File scan limit hit; narrow your scope for deeper sweeps.");
            }
            catch (Exception ex)
            {
                sb.AppendLine();
                sb.AppendLine($"Error while scanning scope folder: {ex.Message}");
            }
        }

        sb.AppendLine();
        sb.AppendLine("Next steps: set this as the case focus, then pivot into Persist to hunt autoruns/services and into Sweep to collect related files and tasks.");

        if (HuntResultsText != null)
            HuntResultsText.Text = sb.ToString();

        RefreshHuntResultsList();
    }



    // ---- Domain / URL hunt ----
    private async Task HandleDomainHuntAsync(string ioc)
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
            if (HuntResultsText != null)
                HuntResultsText.Text = $"Could not parse host from: \"{original}\"";

            _huntResults.Add(new HuntResult
            {
                Category = "Domain",
                Target   = original,
                Severity = "WARN",
                Summary  = "Could not parse host from IOC",
                Details  = "Try a plain domain like example.com or a full URL."
            });

            if (HuntResultsList != null)
            {
                HuntResultsList.ItemsSource = null;
                HuntResultsList.ItemsSource = _huntResults.ToArray();
            }

            return;
        }

        var sb = new StringBuilder();
        sb.AppendLine($"Domain / URL IOC: {original}");
        sb.AppendLine($"Host parsed:      {host}");
        sb.AppendLine();

        // --- DNS resolution ---
        string dnsSummary;
        try
        {
            var addresses = Dns.GetHostAddresses(host);

            if (addresses.Length == 0)
            {
                sb.AppendLine("DNS: no addresses returned.");
                dnsSummary = "DNS: no addresses returned";
            }
            else
            {
                sb.AppendLine("DNS addresses:");
                foreach (var addr in addresses)
                    sb.AppendLine($"  {addr} ({addr.AddressFamily})");

                dnsSummary = $"DNS: {addresses.Length} address(es)";
            }
        }
        catch (Exception ex)
        {
            sb.AppendLine($"DNS lookup error: {ex.Message}");
            dnsSummary = "DNS lookup error";
        }

        sb.AppendLine();
        sb.AppendLine("HTTP probe (best effort):");

        string urlToCheck = original.Trim();

        if (!urlToCheck.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
            !urlToCheck.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            urlToCheck = "http://" + host;
        }

        string httpSummary;
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

            httpSummary = $"HTTP {(int)response.StatusCode} {response.ReasonPhrase}";
        }
        catch (Exception ex)
        {
            sb.AppendLine($"  Error making HTTP request: {ex.Message}");
            httpSummary = "HTTP probe error";
        }

        if (HuntResultsText != null)
            HuntResultsText.Text = sb.ToString();

        // Structured result
        _huntResults.Add(new HuntResult
        {
            Category = "Domain",
            Target   = host,
            Severity = "INFO",
            Summary  = "Domain/URL IOC checked",
            Details  = $"{dnsSummary}; {httpSummary}"
        });

        RefreshHuntResultsList();
    }


    // ---- Registry hunt ----
    private void HandleRegistryHunt(string regPath)
    {
#pragma warning disable CA1416 // Registry is Windows-only
        try
        {
            if (string.IsNullOrWhiteSpace(regPath))
            {
                if (HuntResultsText != null)
                    HuntResultsText.Text = "Registry path was empty.";

                _huntResults.Add(new HuntResult
                {
                    Category = "Registry",
                    Target   = "(empty)",
                    Severity = "WARN",
                    Summary  = "Registry path was empty",
                    Details  = "Provide a full path like HKLM\\Path\\To\\Key."
                });

                if (HuntResultsList != null)
                {
                    HuntResultsList.ItemsSource = null;
                    HuntResultsList.ItemsSource = _huntResults.ToArray();
                }

                return;
            }

            // Normalise slashes
            string cleaned = regPath.Trim().Replace('/', '\\');
            int firstSlash = cleaned.IndexOf('\\');
            if (firstSlash <= 0)
            {
                if (HuntResultsText != null)
                {
                    HuntResultsText.Text =
                        $"Could not parse registry path:\n  {cleaned}\n\n" +
                        "Expected format: HKLM\\Path\\To\\Key";
                }

                _huntResults.Add(new HuntResult
                {
                    Category = "Registry",
                    Target   = cleaned,
                    Severity = "WARN",
                    Summary  = "Could not parse registry path",
                    Details  = "Expected format: HKLM\\Path\\To\\Key"
                });

                if (HuntResultsList != null)
                {
                    HuntResultsList.ItemsSource = null;
                    HuntResultsList.ItemsSource = _huntResults.ToArray();
                }

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
                if (HuntResultsText != null)
                {
                    HuntResultsText.Text =
                        $"Unknown registry hive in path:\n  {cleaned}\n\n" +
                        "Supported hives: HKLM, HKCU, HKCR, HKU, HKCC.";
                }

                _huntResults.Add(new HuntResult
                {
                    Category = "Registry",
                    Target   = cleaned,
                    Severity = "WARN",
                    Summary  = "Unknown registry hive",
                    Details  = "Supported hives: HKLM, HKCU, HKCR, HKU, HKCC."
                });

                if (HuntResultsList != null)
                {
                    HuntResultsList.ItemsSource = null;
                    HuntResultsList.ItemsSource = _huntResults.ToArray();
                }

                return;
            }

            using var key = root.OpenSubKey(subKeyPath);
            string fullPath = $"{hivePart}\\{subKeyPath}";

            if (key == null)
            {
                if (HuntResultsText != null)
                {
                    HuntResultsText.Text =
                        $"Registry key not found:\n  {fullPath}";
                }

                _huntResults.Add(new HuntResult
                {
                    Category = "Registry",
                    Target   = fullPath,
                    Severity = "WARN",
                    Summary  = "Registry key not found",
                    Details  = "Verify the path is correct and accessible."
                });

                if (HuntResultsList != null)
                {
                    HuntResultsList.ItemsSource = null;
                    HuntResultsList.ItemsSource = _huntResults.ToArray();
                }

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
            sb.AppendLine($"  Path: {fullPath}");
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

            if (HuntResultsText != null)
                HuntResultsText.Text = sb.ToString();

            // Bump severity if this looks like a common autorun / persistence area
            string severity = "INFO";
            string subKeyUpper = subKeyPath.ToUpperInvariant();

            if (subKeyUpper.Contains(@"\CURRENTVERSION\RUN") ||
                subKeyUpper.Contains(@"\CURRENTVERSION\RUNONCE") ||
                subKeyUpper.Contains(@"\SERVICES\"))
            {
                severity = "WARN";
            }

            _huntResults.Add(new HuntResult
            {
                Category = "Registry",
                Target   = fullPath,
                Severity = severity,
                Summary  = "Registry key found",
                Details  = $"Subkeys: {subKeys.Length}, Values: {valueLines.Count}"
            });

            if (HuntResultsList != null)
            {
                HuntResultsList.ItemsSource = null;
                HuntResultsList.ItemsSource = _huntResults.ToArray();
            }
        }
        catch (Exception ex)
        {
            if (HuntResultsText != null)
            {
                HuntResultsText.Text =
                    $"Error while reading registry key:\n  {ex.Message}";
            }

            _huntResults.Add(new HuntResult
            {
                Category = "Registry",
                Target   = regPath,
                Severity = "WARN",
                Summary  = "Error while reading registry key",
                Details  = ex.Message
            });

            if (HuntResultsList != null)
            {
                HuntResultsList.ItemsSource = null;
                HuntResultsList.ItemsSource = _huntResults.ToArray();
            }
        }
#pragma warning restore CA1416
    }


    // ---- Hash hunt (with optional disk sweep) ----
    private async Task HandleHashHuntAsync(string hash)
    {
        if (string.IsNullOrWhiteSpace(hash))
        {
            if (HuntResultsText != null)
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

        // Always log the hash itself as a top-level result row
        _huntResults.Add(new HuntResult
        {
            Category = "Hash",
            Target   = normalized,
            Severity = "INFO",
            Summary  = $"Hash IOC ({kind})",
            Details  = "Use disk scan scope to search for matching files."
        });

        // Optional disk sweep if a scope folder is set
        string? scopeFolder = HuntScopeFolderInput?.Text?.Trim();
        if (canScanDisk && !string.IsNullOrWhiteSpace(scopeFolder) && Directory.Exists(scopeFolder))
        {
            const int MaxFilesToScan = 3000; // safety rail so we don't freeze on huge trees

            sb.AppendLine($"Disk scan scope: {scopeFolder}");
            sb.AppendLine($"Max files to scan this run: {MaxFilesToScan}");
            sb.AppendLine();

            if (HuntStatusText != null)
                HuntStatusText.Text = "Status: disk scan running...";

            try
            {
                // Run hash scan on background thread
                var (matches, filesScanned, limitHit) = await Task.Run(() =>
                {
                    var matchList = new List<string>();
                    int scanned = 0;
                    bool hitLimit = false;

                    foreach (var file in EnumerateFilesSafe(scopeFolder))
                    {
                        if (scanned >= MaxFilesToScan)
                        {
                            hitLimit = true;
                            break;
                        }

                        scanned++;

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
                                matchList.Add(file);
                            }
                        }
                        catch
                        {
                            // Skip unreadable files, don't kill the scan
                        }
                    }

                    return (matchList, scanned, hitLimit);
                });

                // Add results on UI thread
                foreach (var file in matches)
                {
                    _huntResults.Add(new HuntResult
                    {
                        Category = "HashHit",
                        Target   = file,
                        Severity = "WARN",
                        Summary  = "File matches IOC hash",
                        Details  = $"Scope: {scopeFolder}"
                    });
                }

                if (matches.Count == 0)
                {
                    sb.AppendLine($"Disk scan: no matching files found under scope. Files scanned: {filesScanned}.");

                    if (limitHit)
                        sb.AppendLine("Note: scan stopped early after hitting the safety file limit.");

                    sb.AppendLine("Note: some system-protected folders may have been skipped if access was denied.");

                    _huntResults.Add(new HuntResult
                    {
                        Category = "HashScan",
                        Target   = scopeFolder,
                        Severity = "INFO",
                        Summary  = "Disk scan completed – no matches",
                        Details  = limitHit
                            ? $"Files scanned: {filesScanned} (safety limit hit). Protected folders may have been skipped."
                            : $"Files scanned: {filesScanned}. Protected folders may have been skipped."
                    });
                }
                else
                {
                    sb.AppendLine($"Disk scan: {matches.Count} matching file(s) found. Files scanned: {filesScanned}.");
                    foreach (var m in matches)
                        sb.AppendLine($"  {m}");

                    if (limitHit)
                        sb.AppendLine("Note: scan stopped early after hitting the safety file limit.");

                    sb.AppendLine("Note: some system-protected folders may have been skipped if access was denied.");

                    _huntResults.Add(new HuntResult
                    {
                        Category = "HashScan",
                        Target   = scopeFolder,
                        Severity = "WARN",
                        Summary  = $"Disk scan completed – {matches.Count} match(es)",
                        Details  = limitHit
                            ? $"Files scanned: {filesScanned} (safety limit hit). Protected folders may have been skipped."
                            : $"Files scanned: {filesScanned}. Protected folders may have been skipped."
                    });
                }

                if (HuntStatusText != null)
                    HuntStatusText.Text = "Status: disk scan completed.";
            }
            catch (Exception ex)
            {
                sb.AppendLine($"Disk scan error: {ex.Message}");

                _huntResults.Add(new HuntResult
                {
                    Category = "HashScan",
                    Target   = scopeFolder,
                    Severity = "WARN",
                    Summary  = "Disk scan error",
                    Details  = ex.Message
                });

                if (HuntStatusText != null)
                    HuntStatusText.Text = "Status: disk scan error.";
            }
        }
        else
        {
            sb.AppendLine("Disk scan: (not run).");
            if (!canScanDisk)
                sb.AppendLine("Reason: only MD5 (32 chars) and SHA-256 (64 chars) are supported for disk scanning right now.");
            else
                sb.AppendLine("Reason: no valid scope folder set. Use the Scope folder row above to pick a directory.");

            _huntResults.Add(new HuntResult
            {
                Category = "HashScan",
                Target   = string.IsNullOrWhiteSpace(scopeFolder) ? "(no scope)" : scopeFolder,
                Severity = "INFO",
                Summary  = "Disk scan not run",
                Details  = canScanDisk
                    ? "No valid scope folder set."
                    : "Hash type not supported for disk scan."
            });
        }

        sb.AppendLine();
        sb.AppendLine("Next steps: use any matching files as the case focus and follow them through Persist (autoruns/services) and Sweep (other copies, installers, droppers).");

        if (HuntResultsText != null)
            HuntResultsText.Text = sb.ToString();

        RefreshHuntResultsList();

        // Log to Hashes.log as before
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

     private HuntResult? GetSelectedHuntResult()
    {
        return HuntResultsList?.SelectedItem as HuntResult;
    }

    private void HuntOpenLocationButton_OnClick(object? sender, RoutedEventArgs e)
    {
        var selected = GetSelectedHuntResult();
        if (selected == null || string.IsNullOrWhiteSpace(selected.Target))
            return;

        try
        {
            var category = selected.Category ?? string.Empty;
            var target   = selected.Target.Trim();

            // 1) Registry result → open Regedit
            if (category.Equals("Registry", StringComparison.OrdinalIgnoreCase) ||
                target.StartsWith("HK", StringComparison.OrdinalIgnoreCase))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName        = "regedit.exe",
                    UseShellExecute = true
                });

                if (HuntStatusText != null)
                    HuntStatusText.Text = $"Status: opened Regedit – navigate to {target}.";

                return;
            }

            // 2) Domain / URL result → open default browser
            if (category.Equals("Domain", StringComparison.OrdinalIgnoreCase))
            {
                if (!string.IsNullOrWhiteSpace(target))
                {
                    var url = (target.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                            target.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                            ? target
                            : "https://" + target;

                    Process.Start(new ProcessStartInfo
                    {
                        FileName        = url,
                        UseShellExecute = true
                    });

                    if (HuntStatusText != null)
                        HuntStatusText.Text = $"Status: opened {url} in default browser.";
                }

                return;
            }

            // 3) Everything else: treat as file/folder path
            if (File.Exists(target))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName        = "explorer.exe",
                    Arguments       = $"/select,\"{target}\"",
                    UseShellExecute = true
                });
            }
            else if (Directory.Exists(target))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName        = "explorer.exe",
                    Arguments       = $"\"{target}\"",
                    UseShellExecute = true
                });
            }
        }
        catch
        {
            // Don't crash if Explorer / Regedit / browser can't open
        }
    }

    /// <summary>
    /// Safely enumerates all files under a root folder.
    /// Skips directories that throw access/IO errors instead of aborting the whole scan.
    /// </summary>
    private static IEnumerable<string> EnumerateFilesSafe(string root)
    {
        var pending = new Stack<string>();
        pending.Push(root);

        while (pending.Count > 0)
        {
            var currentDir = pending.Pop();

            // Get files in this directory
            string[] files;
            try
            {
                files = Directory.GetFiles(currentDir);
            }
            catch
            {
                continue; // can't read this directory
            }

            foreach (var file in files)
                yield return file;

            // Queue subdirectories
            string[] subDirs;
            try
            {
                subDirs = Directory.GetDirectories(currentDir);
            }
            catch
            {
                continue; // can't list subdirs here
            }

            foreach (var sub in subDirs)
                pending.Push(sub);
        }
    }

    private async void HuntCopyTargetButton_OnClick(object? sender, RoutedEventArgs e)
    {
        var selected = GetSelectedHuntResult();
        if (selected == null || string.IsNullOrWhiteSpace(selected.Target))
            return;

        try
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel?.Clipboard is not null)
            {
                await topLevel.Clipboard.SetTextAsync(selected.Target);
                if (HuntStatusText != null)
                    HuntStatusText.Text = "Status: target path copied to clipboard.";
            }
        }
        catch
        {
            // Ignore clipboard errors
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

            // MainWindow *is* a Window
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

    /// ---- Logging ----
    private void LogHuntAction(string ioc, string type)
    {
        // 1) Text log (Hunt.log)
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
            // Text log failure shouldn't break anything
        }

        // 2) JSON log (hunt.jsonl)
        try
        {
            JsonLog.Append("hunt", new
            {
                Timestamp   = DateTime.Now,
                Host        = Environment.MachineName,
                User        = Environment.UserName,
                Type        = type,
                Ioc         = ioc,
                ScopeFolder = HuntScopeFolderInput?.Text
            });
        }
        catch
        {
            // Extra safety; JsonLog already swallows errors
        }

        // 3) Case log (for Case tab + export)
        try
        {
            string? scope = HuntScopeFolderInput?.Text;

            CaseManager.AddEvent(
                tab: "Hunt",
                action: $"Hunt run ({type})",
                severity: "INFO",
                target: ioc,
                details: string.IsNullOrWhiteSpace(scope)
                    ? "Scope: (none)"
                    : $"Scope: {scope}");
        }
        catch
        {
            // Case logging should never crash the UI either
        }
    }

    // ============================================
    // COMPREHENSIVE KEYWORD SEARCH HELPERS
    // ============================================

    /// <summary>
    /// Build a comprehensive list of search locations for keyword hunting.
    /// Includes user-writable locations where malware commonly hides.
    /// </summary>
    private static List<(string Label, string Path)> BuildKeywordSearchRoots()
    {
        var roots = new List<(string Label, string Path)>();

        // Standard install locations
        AddHuntRootIfExists(roots, "Program Files",
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles));
        AddHuntRootIfExists(roots, "Program Files (x86)",
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86));
        AddHuntRootIfExists(roots, "ProgramData",
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData));

        // Current user locations
        AddHuntRootIfExists(roots, "AppData Roaming",
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData));
        AddHuntRootIfExists(roots, "AppData Local",
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData));
        AddHuntRootIfExists(roots, "Desktop",
            Environment.GetFolderPath(Environment.SpecialFolder.Desktop));
        AddHuntRootIfExists(roots, "Downloads",
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads"));
        AddHuntRootIfExists(roots, "Documents",
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments));

        // Temp locations
        AddHuntRootIfExists(roots, "User Temp", Path.GetTempPath());
        AddHuntRootIfExists(roots, "Windows Temp",
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Temp"));

        // Startup folders (common persistence)
        AddHuntRootIfExists(roots, "Startup (User)",
            Environment.GetFolderPath(Environment.SpecialFolder.Startup));
        AddHuntRootIfExists(roots, "Startup (All Users)",
            Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup));

        // Scan all user profiles if running elevated
        try
        {
            string usersRoot = Path.GetDirectoryName(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)) ?? string.Empty;

            if (Directory.Exists(usersRoot))
            {
                foreach (var userDir in Directory.GetDirectories(usersRoot))
                {
                    string userName = Path.GetFileName(userDir);
                    if (string.IsNullOrWhiteSpace(userName))
                        continue;

                    string lower = userName.ToLowerInvariant();
                    if (lower is "default" or "default user" or "public" or "all users")
                        continue;

                    // Skip current user (already added above)
                    if (userDir.Equals(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                        StringComparison.OrdinalIgnoreCase))
                        continue;

                    // Add other users' key locations
                    AddHuntRootIfExists(roots, $"AppData Roaming ({userName})",
                        Path.Combine(userDir, "AppData", "Roaming"));
                    AddHuntRootIfExists(roots, $"AppData Local ({userName})",
                        Path.Combine(userDir, "AppData", "Local"));
                    AddHuntRootIfExists(roots, $"Desktop ({userName})",
                        Path.Combine(userDir, "Desktop"));
                    AddHuntRootIfExists(roots, $"Downloads ({userName})",
                        Path.Combine(userDir, "Downloads"));
                    AddHuntRootIfExists(roots, $"Temp ({userName})",
                        Path.Combine(userDir, "AppData", "Local", "Temp"));
                }
            }
        }
        catch
        {
            // User enumeration may fail without elevation - that's OK
        }

        return roots;
    }

    private static void AddHuntRootIfExists(List<(string Label, string Path)> roots, string label, string? path)
    {
        if (!string.IsNullOrWhiteSpace(path) && Directory.Exists(path))
        {
            // Avoid duplicates
            if (!roots.Any(r => r.Path.Equals(path, StringComparison.OrdinalIgnoreCase)))
                roots.Add((label, path));
        }
    }

    /// <summary>
    /// Search a location for folders and files matching the keyword.
    /// Does partial/fuzzy matching on names.
    /// </summary>
    private static (List<string> Folders, List<string> Files) SearchLocationForKeyword(string rootPath, string keyword)
    {
        var folders = new List<string>();
        var files = new List<string>();

        const int MaxFilesToScan = 10000; // Safety limit per location
        int filesScanned = 0;

        try
        {
            var pending = new Stack<string>();
            pending.Push(rootPath);

            while (pending.Count > 0 && filesScanned < MaxFilesToScan)
            {
                var currentDir = pending.Pop();

                // Check if this folder name matches
                string folderName = Path.GetFileName(currentDir);
                if (!string.IsNullOrEmpty(folderName) &&
                    folderName.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0 &&
                    !currentDir.Equals(rootPath, StringComparison.OrdinalIgnoreCase))
                {
                    folders.Add(currentDir);
                }

                // Get files in this directory
                try
                {
                    foreach (var file in Directory.GetFiles(currentDir))
                    {
                        filesScanned++;
                        if (filesScanned >= MaxFilesToScan)
                            break;

                        string fileName = Path.GetFileName(file);
                        if (fileName.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            files.Add(file);
                        }
                    }
                }
                catch
                {
                    // Skip unreadable directories
                }

                // Queue subdirectories
                try
                {
                    foreach (var subDir in Directory.GetDirectories(currentDir))
                    {
                        pending.Push(subDir);
                    }
                }
                catch
                {
                    // Skip unreadable directories
                }
            }
        }
        catch
        {
            // Root-level errors
        }

        return (folders, files);
    }

    /// <summary>
    /// Check if a path is in a suspicious location (user-writable, temp, etc.)
    /// </summary>
    private static bool IsSuspiciousHuntLocation(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return false;

        string lower = path.ToLowerInvariant().Replace('/', '\\');

        // User-writable locations are suspicious for installed software
        if (lower.Contains(@"\appdata\") ||
            lower.Contains(@"\temp\") ||
            lower.Contains(@"\downloads\") ||
            lower.Contains(@"\desktop\") ||
            lower.Contains(@"\documents\") ||
            lower.Contains(@"\public\"))
        {
            return true;
        }

        return false;
    }
}

using System;
using System.IO;
using System.Runtime.InteropServices;
using Avalonia.Controls;
using Avalonia.Interactivity;
using ViperKit.UI.ViewModels;

namespace ViperKit.UI.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainWindowViewModel();
        PopulateSystemSnapshot();
    }

    private void PopulateSystemSnapshot()
    {
        try
        {
            string machineName   = Environment.MachineName;
            string userName      = Environment.UserName;
            string domain        = Environment.UserDomainName;
            string osDescription = RuntimeInformation.OSDescription;
            var    arch          = RuntimeInformation.OSArchitecture;

            if (SystemHostNameText != null)
                SystemHostNameText.Text = $"Host: {machineName}";
            if (SystemUserText != null)
                SystemUserText.Text     = $"User: {domain}\\{userName}";
            if (SystemOsText != null)
                SystemOsText.Text       = $"OS: {osDescription} ({arch})";
        }
        catch
        {
            // Dashboard should never blow up just because env info failed
        }
    }

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

            default:
                HuntResultsText.Text =
                    $"(demo only) Received IOC of type {effectiveType}: \"{iocText}\".\n\n" +
                    "File/Path checks are live; other IOC types will be wired to real collectors later.";
                break;
        }
    }

    private static string DetermineIocType(string ioc, int selectedIndex)
    {
        // Respect explicit selection
        switch (selectedIndex)
        {
            case 1: return "FilePath";
            case 2: return "Hash";
            case 3: return "DomainOrUrl";
            case 4: return "IpAddress";
            case 5: return "Registry";
        }

        // Auto-detect (index 0)
        string lowered = ioc.ToLowerInvariant();

        // Windows path (C:\ or \\server\share)
        if (ioc.Contains(@":\") || lowered.StartsWith(@"\\"))
            return "FilePath";

        // Hash-ish: 32–64 hex chars, no spaces
        string noSpaces = ioc.Replace(" ", string.Empty);
        if (noSpaces.Length >= 32 && noSpaces.Length <= 64 && IsHexString(noSpaces))
            return "Hash";

        // Very rough IP: 3 dots, only digits + dots
        int dotCount = 0;
        foreach (char c in ioc)
        {
            if (c == '.')
                dotCount++;
        }
        if (dotCount == 3 && IsIpLike(ioc))
            return "IpAddress";

        // Registry-style
        if (lowered.StartsWith("hklm\\") || lowered.StartsWith("hkcu\\"))
            return "Registry";

        // Fallback
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

    private void HandleFilePathHunt(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                var info = new FileInfo(path);
                HuntResultsText.Text =
                    $"File found:\n" +
                    $"  Path: {info.FullName}\n" +
                    $"  Size: {info.Length} bytes\n" +
                    $"  Created: {info.CreationTime}\n" +
                    $"  Modified: {info.LastWriteTime}\n\n" +
                    "(demo) Full fan-out (prefetch, registry, tasks, services, WMI) will be added in later milestones.";
            }
            else
            {
                HuntResultsText.Text =
                    $"File not found at:\n  {path}\n\n" +
                    "Verify the path is correct and accessible from this machine.";
            }
        }
        catch (Exception ex)
        {
            HuntResultsText.Text =
                $"Error while checking path:\n  {ex.Message}\n\n" +
                "Make sure you have permission to access this path.";
        }
    }

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

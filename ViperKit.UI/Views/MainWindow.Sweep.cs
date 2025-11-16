using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace ViperKit.UI.Views;

public partial class MainWindow
{
    // Cache of last sweep so we can filter without rescanning
    private readonly List<string> _sweepEntries = new();

    // =========================
    // SWEEP TAB – RECENT CHANGE RADAR
    // =========================

    private void SweepRunButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (SweepStatusText != null)
            SweepStatusText.Text = "Status: scanning for recent changes...";

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

        // High-signal roots
        var roots = new List<(string Label, string? Path)>
        {
            ("Desktop (current user)",   Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory)),
            ("Downloads (current user)", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) ?? "", "Downloads")),
            ("Temp (user)",              Path.GetTempPath()),
            ("AppData\\Roaming",         Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)),
            ("AppData\\Local",           Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)),
            ("Startup (current user)",   Environment.GetFolderPath(Environment.SpecialFolder.Startup)),
            ("Startup (all users)",      Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup)),
        };

        foreach (var (label, path) in roots)
        {
            if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
                continue;

            ScanSweepRoot(label, path, cutoff, now, interestingExts);
        }

        BindSweepResults();

        int total   = _sweepEntries.Count;
        int flagged = _sweepEntries.Count(s =>
            s.Contains(">>> FLAG: CHECK", StringComparison.OrdinalIgnoreCase));

        if (SweepStatusText != null)
            SweepStatusText.Text = $"Status: sweep complete – {total} item(s), {flagged} flagged.";
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
                FileInfo info;
                try
                {
                    info = new FileInfo(file);
                }
                catch
                {
                    continue;
                }

                if (!interestingExts.Contains(info.Extension))
                    continue;

                DateTime created  = info.CreationTime;
                DateTime modified = info.LastWriteTime;

                // Only keep things that are "recent" by either creation or modification
                if (created < cutoff && modified < cutoff)
                    continue;

                var sb = new StringBuilder();
                sb.AppendLine($"[Sweep] {label}");
                sb.AppendLine($"  Path:     {info.FullName}");
                sb.AppendLine($"  Type:     {info.Extension}");
                sb.AppendLine($"  Created:  {created}");
                sb.AppendLine($"  Modified: {modified}");

                TimeSpan age = now - modified;
                sb.AppendLine($"  Age:      {age:g} ago");

                string flag = BuildSweepFlagLabel(info.FullName);
                if (flag.StartsWith("CHECK", StringComparison.OrdinalIgnoreCase))
                    sb.AppendLine($"  >>> FLAG: {flag} <<<");
                else
                    sb.AppendLine($"  Flag:     {flag}");

                _sweepEntries.Add(sb.ToString());
            }
        }
        catch
        {
            // If one root blows up (permissions, etc), don't kill the whole sweep.
        }
    }

    private static string BuildSweepFlagLabel(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return "UNKNOWN";

        string lower = path.ToLowerInvariant();
        string ext   = Path.GetExtension(path).ToLowerInvariant();

        // Stuff we really care about if it lands in Downloads / Temp / AppData / Startup
        var riskyExts = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".exe", ".dll", ".com",
            ".bat", ".cmd", ".ps1", ".vbs", ".js", ".jse",
            ".scr", ".sys",
            ".msi",
            ".zip", ".7z", ".rar", ".iso"
        };

        bool fromDownloads = lower.Contains(@"\downloads\");
        bool fromTemp      = lower.Contains(@"\temp\");
        bool fromAppData   = lower.Contains(@"\appdata\");
        bool inStartup     = lower.Contains(@"\startup\");
        bool fromDesktop   = lower.Contains(@"\desktop\");

        bool isRiskyExt = riskyExts.Contains(ext);

        var tags = new List<string>();
        if (fromDownloads) tags.Add("from Downloads");
        if (fromTemp)      tags.Add("from Temp");
        if (fromAppData)   tags.Add("from AppData");
        if (inStartup)     tags.Add("in Startup");
        if (fromDesktop)   tags.Add("from Desktop");

        // High-risk locations: Downloads / Temp / AppData / Startup
        if (fromDownloads || fromTemp || fromAppData || inStartup)
        {
            if (isRiskyExt)
                return "CHECK – " + string.Join(", ", tags) + ", executable/script/archive";

            return "NOTE – " + string.Join(", ", tags);
        }

        // Desktop only: usually user workspace, not auto-red
        if (fromDesktop)
            return "OK – Desktop file (likely user workspace)";

        if (tags.Count == 0)
            return "OK – recent but standard-looking location";

        return "OK – " + string.Join(", ", tags);
    }


    // ------------ Binding & filter ------------

    private void BindSweepResults()
    {
        if (SweepResultsList == null)
            return;

        IEnumerable<string> source = _sweepEntries;

        if (SweepShowOnlyFlaggedCheckBox?.IsChecked == true)
        {
            source = source.Where(s =>
                s.Contains(">>> FLAG: CHECK", StringComparison.OrdinalIgnoreCase));
        }

        SweepResultsList.ItemsSource = source.ToList();
    }

    private void SweepFilterCheckBox_OnChanged(object? sender, RoutedEventArgs e)
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

        string text = string.Join(
            Environment.NewLine + Environment.NewLine,
            _sweepEntries);

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

            string text = string.Join(
                Environment.NewLine + Environment.NewLine,
                _sweepEntries);

            File.WriteAllText(fullPath, text, Encoding.UTF8);

            if (SweepStatusText != null)
                SweepStatusText.Text = $"Status: sweep snapshot saved to {fileName}.";
        }
        catch (Exception ex)
        {
            if (SweepStatusText != null)
                SweepStatusText.Text = $"Status: error saving snapshot – {ex.Message}";
        }
    }
}

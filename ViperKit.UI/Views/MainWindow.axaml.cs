using System;
using System.Runtime.InteropServices;
using Avalonia.Controls;
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

    // =========================
    // DASHBOARD – SYSTEM SNAPSHOT ONLY
    // =========================
    private void PopulateSystemSnapshot()
    {
        try
        {
            string machineName = Environment.MachineName;
            string userName    = Environment.UserName;
            string domain      = Environment.UserDomainName;
            var    arch        = RuntimeInformation.OSArchitecture;

            // Use a helper that turns "10.0.26100" into "Windows 11 (build 26100)".
            string osLabel     = GetFriendlyOsLabel();

            if (SystemHostNameText != null)
                SystemHostNameText.Text = $"Host: {machineName}";

            if (SystemUserText != null)
                SystemUserText.Text     = $"User: {domain}\\{userName}";

            if (SystemOsText != null)
                SystemOsText.Text       = $"OS: {osLabel} ({arch})";
        }
        catch
        {
            // Dashboard should never blow up just because env info failed
        }
    }

    private static string GetFriendlyOsLabel()
    {
        try
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return RuntimeInformation.OSDescription;

            Version v = Environment.OSVersion.Version;

            // Windows 11 "marketing" builds start at 22000 but still report Major=10
            if (v.Major == 10 && v.Build >= 22000)
                return $"Windows 11 (build {v.Build})";

            if (v.Major == 10 && v.Build >= 10240)
                return $"Windows 10 (build {v.Build})";

            // Fallback – whatever .NET reports
            return RuntimeInformation.OSDescription;
        }
        catch
        {
            return RuntimeInformation.OSDescription;
        }
    }
}

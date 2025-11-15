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
}

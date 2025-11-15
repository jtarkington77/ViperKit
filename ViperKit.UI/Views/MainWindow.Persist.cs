using System;
using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Microsoft.Win32;

namespace ViperKit.UI.Views;

public partial class MainWindow
{
    // =========================
    // PERSIST TAB – AUTORUN ENUM
    // =========================

    private void PersistRunButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (PersistStatusText != null)
            PersistStatusText.Text = "Status: scanning Run/RunOnce keys...";

        if (PersistResultsList != null)
            PersistResultsList.ItemsSource = Array.Empty<string>();

        try
        {
            var items = new List<string>();

            EnumerateRunKey(Registry.CurrentUser,
                            @"Software\Microsoft\Windows\CurrentVersion\Run",
                            "HKCU",
                            items);

            EnumerateRunKey(Registry.CurrentUser,
                            @"Software\Microsoft\Windows\CurrentVersion\RunOnce",
                            "HKCU",
                            items);

            EnumerateRunKey(Registry.LocalMachine,
                            @"Software\Microsoft\Windows\CurrentVersion\Run",
                            "HKLM",
                            items);

            EnumerateRunKey(Registry.LocalMachine,
                            @"Software\Microsoft\Windows\CurrentVersion\RunOnce",
                            "HKLM",
                            items);

            if (PersistResultsList != null)
            {
                if (items.Count == 0)
                {
                    PersistResultsList.ItemsSource = new[]
                    {
                        "No autorun values found in HKCU/HKLM Run* keys."
                    };
                }
                else
                {
                    PersistResultsList.ItemsSource = items;
                }
            }

            if (PersistStatusText != null)
                PersistStatusText.Text = $"Status: found {items.Count} autorun value(s).";
        }
        catch (Exception ex)
        {
            if (PersistStatusText != null)
                PersistStatusText.Text = "Status: error while scanning autoruns.";

            if (PersistResultsList != null)
                PersistResultsList.ItemsSource = new[] { $"Error: {ex.Message}" };

        }
    }

#pragma warning disable CA1416
    private void EnumerateRunKey(RegistryKey root, string subKeyPath, string hiveLabel, List<string> output)
    {
        try
        {
            using var key = root.OpenSubKey(subKeyPath);
            if (key == null)
                return;

            foreach (var valueName in key.GetValueNames())
            {
                var value = key.GetValue(valueName) as string ?? "(non-string value)";
                string line = $"[{hiveLabel}\\{subKeyPath}] {valueName} = {value}";
                output.Add(line);
            }
        }
        catch
        {
            // Ignore individual key failures; we can log later if needed
        }
    }
#pragma warning restore CA1416
}

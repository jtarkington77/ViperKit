// ViperKit.UI - Views\MainWindow.Cleanup.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Microsoft.Win32;
using ViperKit.UI.Models;

namespace ViperKit.UI.Views;

public partial class MainWindow
{
#pragma warning disable CA1416 // Windows-only APIs

    // ----------------------------
    // CLEANUP – Refresh Queue Display
    // ----------------------------
    private void RefreshCleanupQueue()
    {
        if (CleanupQueueList == null)
            return;

        var queue = CaseManager.GetCleanupQueue();
        CleanupQueueList.ItemsSource = null;
        CleanupQueueList.ItemsSource = queue.ToArray();

        UpdateCleanupStats();
    }

    private void UpdateCleanupStats()
    {
        var (total, pending, completed, failed) = CaseManager.GetCleanupStats();

        if (CleanupStatsText != null)
            CleanupStatsText.Text = $"Queue: {total} items | Pending: {pending} | Completed: {completed} | Failed: {failed}";
    }

    // ----------------------------
    // CLEANUP – Execute All Pending
    // ----------------------------
    private async void CleanupExecuteAllButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button btn)
            btn.IsEnabled = false;

        try
        {
            var pendingItems = CaseManager.GetCleanupQueueByStatus("Pending").ToList();

            if (pendingItems.Count == 0)
            {
                if (CleanupStatusText != null)
                    CleanupStatusText.Text = "Status: no pending items to process.";
                return;
            }

            int processed = 0;
            int failed = 0;

            foreach (var item in pendingItems)
            {
                if (CleanupStatusText != null)
                    CleanupStatusText.Text = $"Status: processing {item.Name}... ({processed + 1}/{pendingItems.Count})";

                CaseManager.UpdateCleanupItemStatus(item.Id, "InProgress");
                RefreshCleanupQueue();

                bool success = await Task.Run(() => ExecuteCleanupAction(item));

                if (success)
                {
                    CaseManager.UpdateCleanupItemStatus(item.Id, "Completed");
                    processed++;
                }
                else
                {
                    failed++;
                }

                RefreshCleanupQueue();
            }

            if (CleanupStatusText != null)
                CleanupStatusText.Text = $"Status: completed {processed} items, {failed} failed.";
        }
        finally
        {
            if (sender is Button btn2)
                btn2.IsEnabled = true;
        }
    }

    // ----------------------------
    // CLEANUP – Execute Single Item
    // ----------------------------
    private async void CleanupExecuteSelectedButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (CleanupQueueList?.SelectedItem is not CleanupItem item)
        {
            if (CleanupStatusText != null)
                CleanupStatusText.Text = "Status: select an item to execute.";
            return;
        }

        if (item.Status != "Pending")
        {
            if (CleanupStatusText != null)
                CleanupStatusText.Text = $"Status: item is already {item.Status.ToLower()}.";
            return;
        }

        if (sender is Button btn)
            btn.IsEnabled = false;

        try
        {
            if (CleanupStatusText != null)
                CleanupStatusText.Text = $"Status: processing {item.Name}...";

            CaseManager.UpdateCleanupItemStatus(item.Id, "InProgress");
            RefreshCleanupQueue();

            bool success = await Task.Run(() => ExecuteCleanupAction(item));

            if (success)
            {
                CaseManager.UpdateCleanupItemStatus(item.Id, "Completed");
                if (CleanupStatusText != null)
                    CleanupStatusText.Text = $"Status: {item.Name} cleaned up successfully.";
            }
            else
            {
                if (CleanupStatusText != null)
                    CleanupStatusText.Text = $"Status: failed to clean up {item.Name}. Check error details.";
            }

            RefreshCleanupQueue();
        }
        finally
        {
            if (sender is Button btn2)
                btn2.IsEnabled = true;
        }
    }

    // ----------------------------
    // CLEANUP – Execute Action
    // ----------------------------
    private bool ExecuteCleanupAction(CleanupItem item)
    {
        try
        {
            return item.ItemType.ToLowerInvariant() switch
            {
                "file" => QuarantineFile(item),
                "service" => DisableService(item),
                "scheduledtask" => DisableScheduledTask(item),
                "registrykey" => BackupAndDeleteRegistryKey(item),
                "startupitem" => QuarantineStartupItem(item),
                _ => false
            };
        }
        catch (Exception ex)
        {
            CaseManager.UpdateCleanupItemStatus(item.Id, "Failed", ex.Message);
            return false;
        }
    }

    // ----------------------------
    // CLEANUP – Quarantine File
    // ----------------------------
    private bool QuarantineFile(CleanupItem item)
    {
        if (!File.Exists(item.OriginalPath))
        {
            CaseManager.UpdateCleanupItemStatus(item.Id, "Failed", "File not found");
            return false;
        }

        string caseFolder = CleanupJournal.GetCaseQuarantineFolder(CaseManager.CaseId);
        Directory.CreateDirectory(caseFolder);

        // Create quarantine subfolder structure
        string fileName = Path.GetFileName(item.OriginalPath);
        string quarantinePath = Path.Combine(caseFolder, "files", $"{item.Id}_{fileName}");
        Directory.CreateDirectory(Path.GetDirectoryName(quarantinePath)!);

        try
        {
            // Move file to quarantine
            File.Move(item.OriginalPath, quarantinePath);
            item.QuarantinePath = quarantinePath;

            // Record in journal for undo
            CleanupJournal.RecordAction(new CleanupJournalEntry
            {
                ItemId = item.Id,
                ActionType = "Quarantine",
                OriginalState = item.OriginalPath,
                NewState = quarantinePath,
                CaseId = CaseManager.CaseId
            });

            CaseManager.AddEvent("Cleanup", "File quarantined", "INFO", item.Name,
                $"Moved from {item.OriginalPath} to {quarantinePath}");

            return true;
        }
        catch (Exception ex)
        {
            CaseManager.UpdateCleanupItemStatus(item.Id, "Failed", ex.Message);
            return false;
        }
    }

    // ----------------------------
    // CLEANUP – Disable Service
    // ----------------------------
    private bool DisableService(CleanupItem item)
    {
        try
        {
            // Use registry to get current state and disable
            using var key = Registry.LocalMachine.OpenSubKey(
                $@"SYSTEM\CurrentControlSet\Services\{item.Name}", writable: true);

            if (key == null)
            {
                CaseManager.UpdateCleanupItemStatus(item.Id, "Failed", "Service registry key not found");
                return false;
            }

            // Backup original Start value
            int originalStart = key.GetValue("Start") is int s ? s : 3;

            // Set to Disabled (4)
            key.SetValue("Start", 4, RegistryValueKind.DWord);

            item.QuarantinePath = $"Registry:{item.Name}:Start={originalStart}";

            // Record in journal
            CleanupJournal.RecordAction(new CleanupJournalEntry
            {
                ItemId = item.Id,
                ActionType = "Disable",
                OriginalState = $"Start={originalStart}",
                NewState = "Start=4 (Disabled)",
                BackupData = originalStart.ToString(),
                CaseId = CaseManager.CaseId
            });

            // Stop the service if running using sc.exe
            var stopPsi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "sc.exe",
                Arguments = $"stop \"{item.Name}\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var stopProcess = System.Diagnostics.Process.Start(stopPsi);
            stopProcess?.WaitForExit(30000);
            // Ignore stop errors - service may not be running

            CaseManager.AddEvent("Cleanup", "Service disabled", "INFO", item.Name,
                $"Changed Start from {originalStart} to 4 (Disabled)");

            return true;
        }
        catch (Exception ex)
        {
            CaseManager.UpdateCleanupItemStatus(item.Id, "Failed", ex.Message);
            return false;
        }
    }

    // ----------------------------
    // CLEANUP – Disable Scheduled Task
    // ----------------------------
    private bool DisableScheduledTask(CleanupItem item)
    {
        try
        {
            // Use schtasks.exe to disable the task
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "schtasks.exe",
                Arguments = $"/Change /TN \"{item.Name}\" /Disable",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var process = System.Diagnostics.Process.Start(psi);
            process?.WaitForExit(30000);

            if (process?.ExitCode != 0)
            {
                string error = process?.StandardError.ReadToEnd() ?? "Unknown error";
                CaseManager.UpdateCleanupItemStatus(item.Id, "Failed", error);
                return false;
            }

            // Record in journal
            CleanupJournal.RecordAction(new CleanupJournalEntry
            {
                ItemId = item.Id,
                ActionType = "Disable",
                OriginalState = "Enabled",
                NewState = "Disabled",
                CaseId = CaseManager.CaseId
            });

            CaseManager.AddEvent("Cleanup", "Scheduled task disabled", "INFO", item.Name, null);

            return true;
        }
        catch (Exception ex)
        {
            CaseManager.UpdateCleanupItemStatus(item.Id, "Failed", ex.Message);
            return false;
        }
    }

    // ----------------------------
    // CLEANUP – Backup and Delete Registry Key
    // ----------------------------
    private bool BackupAndDeleteRegistryKey(CleanupItem item)
    {
        try
        {
            string caseFolder = CleanupJournal.GetCaseQuarantineFolder(CaseManager.CaseId);
            string regBackupFolder = Path.Combine(caseFolder, "registry");
            Directory.CreateDirectory(regBackupFolder);

            string regBackupPath = Path.Combine(regBackupFolder, $"{item.Id}.reg");

            // Export the key using reg.exe
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "reg.exe",
                Arguments = $"export \"{item.OriginalPath}\" \"{regBackupPath}\" /y",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var exportProcess = System.Diagnostics.Process.Start(psi);
            exportProcess?.WaitForExit(30000);

            if (exportProcess?.ExitCode != 0 || !File.Exists(regBackupPath))
            {
                CaseManager.UpdateCleanupItemStatus(item.Id, "Failed", "Failed to backup registry key");
                return false;
            }

            // Delete the key
            psi.Arguments = $"delete \"{item.OriginalPath}\" /f";
            using var deleteProcess = System.Diagnostics.Process.Start(psi);
            deleteProcess?.WaitForExit(30000);

            if (deleteProcess?.ExitCode != 0)
            {
                string error = deleteProcess?.StandardError.ReadToEnd() ?? "Unknown error";
                CaseManager.UpdateCleanupItemStatus(item.Id, "Failed", error);
                return false;
            }

            item.QuarantinePath = regBackupPath;

            // Record in journal
            CleanupJournal.RecordAction(new CleanupJournalEntry
            {
                ItemId = item.Id,
                ActionType = "BackupAndDelete",
                OriginalState = item.OriginalPath,
                NewState = "Deleted",
                BackupData = regBackupPath,
                CaseId = CaseManager.CaseId
            });

            CaseManager.AddEvent("Cleanup", "Registry key deleted", "INFO", item.Name,
                $"Backed up to {regBackupPath}");

            return true;
        }
        catch (Exception ex)
        {
            CaseManager.UpdateCleanupItemStatus(item.Id, "Failed", ex.Message);
            return false;
        }
    }

    // ----------------------------
    // CLEANUP – Quarantine Startup Item
    // ----------------------------
    private bool QuarantineStartupItem(CleanupItem item)
    {
        // Startup items are typically files in startup folders
        // Treat them like files for quarantine purposes
        return QuarantineFile(item);
    }

    // ----------------------------
    // CLEANUP – Undo Last Action
    // ----------------------------
    private async void CleanupUndoLastButton_OnClick(object? sender, RoutedEventArgs e)
    {
        var lastEntry = CleanupJournal.GetLastUndoableEntry();

        if (lastEntry == null)
        {
            if (CleanupStatusText != null)
                CleanupStatusText.Text = "Status: nothing to undo.";
            return;
        }

        if (sender is Button btn)
            btn.IsEnabled = false;

        try
        {
            if (CleanupStatusText != null)
                CleanupStatusText.Text = $"Status: undoing last action...";

            bool success = await Task.Run(() => UndoCleanupAction(lastEntry));

            if (success)
            {
                CleanupJournal.MarkUndone(lastEntry.ItemId, CaseManager.CaseId);

                // Update the cleanup item status
                var queue = CaseManager.GetCleanupQueue();
                var item = queue.FirstOrDefault(c => c.Id == lastEntry.ItemId);
                if (item != null)
                    CaseManager.UpdateCleanupItemStatus(item.Id, "Undone");

                if (CleanupStatusText != null)
                    CleanupStatusText.Text = "Status: last action undone successfully.";
            }
            else
            {
                if (CleanupStatusText != null)
                    CleanupStatusText.Text = "Status: failed to undo last action.";
            }

            RefreshCleanupQueue();
        }
        finally
        {
            if (sender is Button btn2)
                btn2.IsEnabled = true;
        }
    }

    // ----------------------------
    // CLEANUP – Undo Selected Item
    // ----------------------------
    private async void CleanupUndoSelectedButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (CleanupQueueList?.SelectedItem is not CleanupItem item)
        {
            if (CleanupStatusText != null)
                CleanupStatusText.Text = "Status: select an item to undo.";
            return;
        }

        if (item.Status != "Completed")
        {
            if (CleanupStatusText != null)
                CleanupStatusText.Text = $"Status: can only undo completed items.";
            return;
        }

        var journalEntries = CleanupJournal.GetUndoableEntries();
        var entry = journalEntries.FirstOrDefault(e => e.ItemId == item.Id);

        if (entry == null)
        {
            if (CleanupStatusText != null)
                CleanupStatusText.Text = "Status: no undo record found for this item.";
            return;
        }

        if (sender is Button btn)
            btn.IsEnabled = false;

        try
        {
            if (CleanupStatusText != null)
                CleanupStatusText.Text = $"Status: undoing {item.Name}...";

            bool success = await Task.Run(() => UndoCleanupAction(entry));

            if (success)
            {
                CleanupJournal.MarkUndone(entry.ItemId, CaseManager.CaseId);
                CaseManager.UpdateCleanupItemStatus(item.Id, "Undone");

                if (CleanupStatusText != null)
                    CleanupStatusText.Text = $"Status: {item.Name} restored successfully.";
            }
            else
            {
                if (CleanupStatusText != null)
                    CleanupStatusText.Text = $"Status: failed to undo {item.Name}.";
            }

            RefreshCleanupQueue();
        }
        finally
        {
            if (sender is Button btn2)
                btn2.IsEnabled = true;
        }
    }

    // ----------------------------
    // CLEANUP – Undo Action Implementation
    // ----------------------------
    private bool UndoCleanupAction(CleanupJournalEntry entry)
    {
        try
        {
            return entry.ActionType.ToLowerInvariant() switch
            {
                "quarantine" => RestoreQuarantinedFile(entry),
                "disable" => ReEnableService(entry),
                "backupanddelete" => RestoreRegistryKey(entry),
                _ => false
            };
        }
        catch (Exception ex)
        {
            CaseManager.AddEvent("Cleanup", "Undo failed", "HIGH", entry.ItemId, ex.Message);
            return false;
        }
    }

    private bool RestoreQuarantinedFile(CleanupJournalEntry entry)
    {
        if (!File.Exists(entry.NewState))
            return false;

        // Restore to original location
        string? dir = Path.GetDirectoryName(entry.OriginalState);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        File.Move(entry.NewState, entry.OriginalState);

        CaseManager.AddEvent("Cleanup", "File restored", "INFO", entry.ItemId,
            $"Restored from {entry.NewState} to {entry.OriginalState}");

        return true;
    }

    private bool ReEnableService(CleanupJournalEntry entry)
    {
        // BackupData contains the original Start value
        if (!int.TryParse(entry.BackupData, out int originalStart))
            return false;

        // Parse service name from OriginalState (format: "Start=X")
        // The item name should be in the cleanup queue
        var queue = CaseManager.GetCleanupQueue();
        var item = queue.FirstOrDefault(c => c.Id == entry.ItemId);
        if (item == null)
            return false;

        using var key = Registry.LocalMachine.OpenSubKey(
            $@"SYSTEM\CurrentControlSet\Services\{item.Name}", writable: true);

        if (key == null)
            return false;

        key.SetValue("Start", originalStart, RegistryValueKind.DWord);

        CaseManager.AddEvent("Cleanup", "Service restored", "INFO", item.Name,
            $"Restored Start value to {originalStart}");

        return true;
    }

    private bool RestoreRegistryKey(CleanupJournalEntry entry)
    {
        if (!File.Exists(entry.BackupData))
            return false;

        // Import the .reg file
        var psi = new System.Diagnostics.ProcessStartInfo
        {
            FileName = "reg.exe",
            Arguments = $"import \"{entry.BackupData}\"",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        using var process = System.Diagnostics.Process.Start(psi);
        process?.WaitForExit(30000);

        if (process?.ExitCode != 0)
            return false;

        CaseManager.AddEvent("Cleanup", "Registry key restored", "INFO", entry.ItemId,
            $"Imported from {entry.BackupData}");

        return true;
    }

    // ----------------------------
    // CLEANUP – Remove from Queue
    // ----------------------------
    private void CleanupRemoveSelectedButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (CleanupQueueList?.SelectedItem is not CleanupItem item)
        {
            if (CleanupStatusText != null)
                CleanupStatusText.Text = "Status: select an item to remove.";
            return;
        }

        if (item.Status == "Completed")
        {
            if (CleanupStatusText != null)
                CleanupStatusText.Text = "Status: cannot remove completed items. Use Undo instead.";
            return;
        }

        CaseManager.RemoveFromCleanupQueue(item.Id);
        RefreshCleanupQueue();

        if (CleanupStatusText != null)
            CleanupStatusText.Text = $"Status: {item.Name} removed from queue.";
    }

    // ----------------------------
    // CLEANUP – Open Quarantine Folder
    // ----------------------------
    private void CleanupOpenQuarantineButton_OnClick(object? sender, RoutedEventArgs e)
    {
        string quarantineFolder = CleanupJournal.GetCaseQuarantineFolder(CaseManager.CaseId);

        if (!Directory.Exists(quarantineFolder))
        {
            Directory.CreateDirectory(quarantineFolder);
        }

        try
        {
            using var process = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = quarantineFolder,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            if (CleanupStatusText != null)
                CleanupStatusText.Text = $"Status: failed to open folder: {ex.Message}";
        }
    }

    // ----------------------------
    // CLEANUP – Clear Queue
    // ----------------------------
    private void CleanupClearQueueButton_OnClick(object? sender, RoutedEventArgs e)
    {
        var queue = CaseManager.GetCleanupQueue().ToList();
        int removed = 0;

        foreach (var item in queue)
        {
            // Only remove pending items, keep completed ones for audit trail
            if (item.Status == "Pending")
            {
                CaseManager.RemoveFromCleanupQueue(item.Id);
                removed++;
            }
        }

        RefreshCleanupQueue();

        if (CleanupStatusText != null)
            CleanupStatusText.Text = $"Status: removed {removed} pending items from queue.";
    }

#pragma warning restore CA1416
}

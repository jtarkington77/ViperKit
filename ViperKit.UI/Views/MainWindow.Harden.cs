// ViperKit.UI - Views\MainWindow.Harden.cs
using System;
using System.Collections.Generic;
using System.Diagnostics;
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

    private List<HardenAction> _hardenActions = new();

    // ----------------------------
    // HARDEN – Initialize Actions
    // ----------------------------
    private void InitializeHardenActions()
    {
        _hardenActions = new List<HardenAction>
        {
            // ---- SCRIPT EXECUTION ----
            new HardenAction
            {
                Id = "disable_wsh",
                Category = "ScriptExecution",
                Name = "Disable Windows Script Host",
                Description = "Prevents .vbs, .js, .jse, .wsf scripts from running",
                RecommendedState = "Disabled",
                Profile = "Standard"
            },
            new HardenAction
            {
                Id = "disable_ps_v2",
                Category = "ScriptExecution",
                Name = "Disable PowerShell v2",
                Description = "Removes legacy PowerShell that lacks logging",
                RecommendedState = "Disabled",
                Profile = "Standard"
            },
            new HardenAction
            {
                Id = "ps_script_block_logging",
                Category = "ScriptExecution",
                Name = "Enable Script Block Logging",
                Description = "Logs PowerShell script content for forensics",
                RecommendedState = "Enabled",
                Profile = "Standard"
            },
            new HardenAction
            {
                Id = "ps_module_logging",
                Category = "ScriptExecution",
                Name = "Enable Module Logging",
                Description = "Logs PowerShell module activity",
                RecommendedState = "Enabled",
                Profile = "Strict"
            },
            new HardenAction
            {
                Id = "ps_execution_policy",
                Category = "ScriptExecution",
                Name = "Set ExecutionPolicy RemoteSigned",
                Description = "Requires scripts from internet to be signed",
                RecommendedState = "RemoteSigned",
                Profile = "Standard"
            },

            // ---- FIREWALL ----
            new HardenAction
            {
                Id = "firewall_enable_all",
                Category = "Firewall",
                Name = "Enable Firewall (All Profiles)",
                Description = "Ensures Windows Firewall is on for Domain, Private, and Public",
                RecommendedState = "All On",
                Profile = "Standard"
            },
            new HardenAction
            {
                Id = "firewall_block_rmm_ports",
                Category = "Firewall",
                Name = "Block Common RMM Ports",
                Description = "Blocks outbound ports 5938, 8040, 8041, 5939 (common RMM)",
                RecommendedState = "Blocked",
                Profile = "Strict",
                WarningMessage = "May block legitimate RMM tools your organization uses"
            },

            // ---- DEFENDER ----
            new HardenAction
            {
                Id = "defender_realtime",
                Category = "Defender",
                Name = "Enable Real-Time Protection",
                Description = "Ensures Defender real-time scanning is active",
                RecommendedState = "Enabled",
                Profile = "Standard"
            },
            new HardenAction
            {
                Id = "defender_cloud",
                Category = "Defender",
                Name = "Enable Cloud Protection",
                Description = "Enables cloud-delivered protection (MAPS)",
                RecommendedState = "Advanced",
                Profile = "Standard"
            },
            new HardenAction
            {
                Id = "defender_pua",
                Category = "Defender",
                Name = "Enable PUA Protection",
                Description = "Blocks Potentially Unwanted Applications",
                RecommendedState = "Enabled",
                Profile = "Standard"
            },
            new HardenAction
            {
                Id = "defender_controlled_folders",
                Category = "Defender",
                Name = "Enable Controlled Folder Access",
                Description = "Protects folders from ransomware",
                RecommendedState = "Enabled",
                Profile = "Strict",
                WarningMessage = "May require whitelisting legitimate applications"
            },

            // ---- AUTORUN ----
            new HardenAction
            {
                Id = "disable_autorun",
                Category = "AutoRun",
                Name = "Disable AutoRun (All Drives)",
                Description = "Prevents automatic execution from removable media",
                RecommendedState = "Disabled",
                Profile = "Standard"
            },
            new HardenAction
            {
                Id = "disable_autoplay",
                Category = "AutoRun",
                Name = "Disable AutoPlay",
                Description = "Disables AutoPlay for all media types",
                RecommendedState = "Disabled",
                Profile = "Standard"
            },

            // ---- REMOTE ACCESS ----
            new HardenAction
            {
                Id = "rdp_nla",
                Category = "RemoteAccess",
                Name = "Require NLA for RDP",
                Description = "Requires Network Level Authentication for Remote Desktop",
                RecommendedState = "Required",
                Profile = "Standard"
            },
            new HardenAction
            {
                Id = "disable_rdp",
                Category = "RemoteAccess",
                Name = "Disable Remote Desktop",
                Description = "Completely disables RDP access",
                RecommendedState = "Disabled",
                Profile = "Strict",
                WarningMessage = "Will prevent all remote desktop connections"
            }
        };

        // Initialize journal
        HardenJournal.Initialize(CaseManager.CaseId);
    }

    // ----------------------------
    // HARDEN – Scan Current State
    // ----------------------------
    private async void HardenScanButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button btn)
            btn.IsEnabled = false;

        try
        {
            if (HardenStatusText != null)
                HardenStatusText.Text = "Status: Scanning current system configuration...";

            InitializeHardenActions();

            await Task.Run(() =>
            {
                foreach (var action in _hardenActions)
                {
                    action.CurrentState = DetectCurrentState(action.Id);
                }
            });

            // Update UI
            if (HardenActionsList != null)
                HardenActionsList.ItemsSource = _hardenActions;

            UpdateHardenStats();

            if (HardenStatusText != null)
                HardenStatusText.Text = $"Status: Scanned {_hardenActions.Count} settings. Select items to harden.";
        }
        catch (Exception ex)
        {
            if (HardenStatusText != null)
                HardenStatusText.Text = $"Status: Error scanning - {ex.Message}";
        }
        finally
        {
            if (sender is Button btn2)
                btn2.IsEnabled = true;
        }
    }

    // ----------------------------
    // HARDEN – Detect Current State
    // ----------------------------
    private string DetectCurrentState(string actionId)
    {
        try
        {
            return actionId switch
            {
                "disable_wsh" => DetectWshState(),
                "disable_ps_v2" => DetectPsV2State(),
                "ps_script_block_logging" => DetectScriptBlockLogging(),
                "ps_module_logging" => DetectModuleLogging(),
                "ps_execution_policy" => DetectExecutionPolicy(),
                "firewall_enable_all" => DetectFirewallState(),
                "firewall_block_rmm_ports" => "Open", // Would need netsh parsing
                "defender_realtime" => DetectDefenderRealtime(),
                "defender_cloud" => DetectDefenderCloud(),
                "defender_pua" => DetectDefenderPua(),
                "defender_controlled_folders" => DetectControlledFolders(),
                "disable_autorun" => DetectAutoRunState(),
                "disable_autoplay" => DetectAutoPlayState(),
                "rdp_nla" => DetectRdpNla(),
                "disable_rdp" => DetectRdpState(),
                _ => "Unknown"
            };
        }
        catch
        {
            return "Error";
        }
    }

    private string DetectWshState()
    {
        using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows Script Host\Settings");
        var value = key?.GetValue("Enabled");
        if (value is int i && i == 0) return "Disabled";
        return "Enabled";
    }

    private string DetectPsV2State()
    {
        // Check if PS v2 feature is enabled
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "dism",
                Arguments = "/online /get-featureinfo /featurename:MicrosoftWindowsPowerShellV2",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var proc = Process.Start(psi);
            var output = proc?.StandardOutput.ReadToEnd() ?? "";
            proc?.WaitForExit();
            if (output.Contains("State : Disabled")) return "Disabled";
            if (output.Contains("State : Enabled")) return "Enabled";
        }
        catch { }
        return "Unknown";
    }

    private string DetectScriptBlockLogging()
    {
        using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Policies\Microsoft\Windows\PowerShell\ScriptBlockLogging");
        var value = key?.GetValue("EnableScriptBlockLogging");
        if (value is int i && i == 1) return "Enabled";
        return "Disabled";
    }

    private string DetectModuleLogging()
    {
        using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Policies\Microsoft\Windows\PowerShell\ModuleLogging");
        var value = key?.GetValue("EnableModuleLogging");
        if (value is int i && i == 1) return "Enabled";
        return "Disabled";
    }

    private string DetectExecutionPolicy()
    {
        using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\PowerShell\1\ShellIds\Microsoft.PowerShell");
        var value = key?.GetValue("ExecutionPolicy") as string;
        return value ?? "Undefined";
    }

    private string DetectFirewallState()
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "netsh",
                Arguments = "advfirewall show allprofiles state",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var proc = Process.Start(psi);
            var output = proc?.StandardOutput.ReadToEnd() ?? "";
            proc?.WaitForExit();
            int onCount = output.Split('\n').Count(l => l.Contains("ON", StringComparison.OrdinalIgnoreCase));
            if (onCount >= 3) return "All On";
            if (onCount > 0) return "Partial";
            return "All Off";
        }
        catch { return "Unknown"; }
    }

    private string DetectDefenderRealtime()
    {
        using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows Defender\Real-Time Protection");
        var value = key?.GetValue("DisableRealtimeMonitoring");
        if (value is int i && i == 1) return "Disabled";
        return "Enabled";
    }

    private string DetectDefenderCloud()
    {
        using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows Defender\Spynet");
        var value = key?.GetValue("SpynetReporting");
        return value switch
        {
            0 => "Off",
            1 => "Basic",
            2 => "Advanced",
            _ => "Default"
        };
    }

    private string DetectDefenderPua()
    {
        using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows Defender");
        var value = key?.GetValue("PUAProtection");
        if (value is int i && i == 1) return "Enabled";
        return "Disabled";
    }

    private string DetectControlledFolders()
    {
        using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows Defender\Windows Defender Exploit Guard\Controlled Folder Access");
        var value = key?.GetValue("EnableControlledFolderAccess");
        if (value is int i && i == 1) return "Enabled";
        return "Disabled";
    }

    private string DetectAutoRunState()
    {
        using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\Explorer");
        var value = key?.GetValue("NoDriveTypeAutoRun");
        if (value is int i && i == 255) return "Disabled";
        return "Enabled";
    }

    private string DetectAutoPlayState()
    {
        using var key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\AutoplayHandlers");
        var value = key?.GetValue("DisableAutoplay");
        if (value is int i && i == 1) return "Disabled";
        return "Enabled";
    }

    private string DetectRdpNla()
    {
        using var key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Terminal Server\WinStations\RDP-Tcp");
        var value = key?.GetValue("UserAuthentication");
        if (value is int i && i == 1) return "Required";
        return "Not Required";
    }

    private string DetectRdpState()
    {
        using var key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Terminal Server");
        var value = key?.GetValue("fDenyTSConnections");
        if (value is int i && i == 1) return "Disabled";
        return "Enabled";
    }

    // ----------------------------
    // HARDEN – Apply Selected
    // ----------------------------
    private async void HardenApplySelectedButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button btn)
            btn.IsEnabled = false;

        try
        {
            var selected = _hardenActions.Where(a => a.IsSelected && !a.IsAlreadyHardened).ToList();

            if (selected.Count == 0)
            {
                if (HardenStatusText != null)
                    HardenStatusText.Text = "Status: No actions selected.";
                return;
            }

            int success = 0;
            int failed = 0;

            foreach (var action in selected)
            {
                if (HardenStatusText != null)
                    HardenStatusText.Text = $"Status: Applying {action.Name}...";

                bool result = await Task.Run(() => ApplyHardenAction(action));

                if (result)
                {
                    action.IsApplied = true;
                    action.AppliedAt = DateTime.Now;
                    action.CurrentState = action.RecommendedState;
                    HardenJournal.RecordAction(action);

                    CaseManager.AddEvent("Harden", $"Applied: {action.Name}",
                        "INFO", action.Name, $"Changed from {action.RollbackData} to {action.RecommendedState}");

                    success++;
                }
                else
                {
                    failed++;
                }
            }

            // Refresh display
            if (HardenActionsList != null)
                HardenActionsList.ItemsSource = null;
            if (HardenActionsList != null)
                HardenActionsList.ItemsSource = _hardenActions;

            UpdateHardenStats();

            if (HardenStatusText != null)
                HardenStatusText.Text = $"Status: Applied {success} actions, {failed} failed.";
        }
        catch (Exception ex)
        {
            if (HardenStatusText != null)
                HardenStatusText.Text = $"Status: Error - {ex.Message}";
        }
        finally
        {
            if (sender is Button btn2)
                btn2.IsEnabled = true;
        }
    }

    private bool ApplyHardenAction(HardenAction action)
    {
        try
        {
            // Store original state for rollback
            action.RollbackData = action.CurrentState;

            return action.Id switch
            {
                "disable_wsh" => ApplyDisableWsh(),
                "disable_ps_v2" => ApplyDisablePsV2(),
                "ps_script_block_logging" => ApplyScriptBlockLogging(),
                "ps_module_logging" => ApplyModuleLogging(),
                "ps_execution_policy" => ApplyExecutionPolicy(),
                "firewall_enable_all" => ApplyFirewallEnable(),
                "firewall_block_rmm_ports" => ApplyBlockRmmPorts(),
                "defender_realtime" => ApplyDefenderRealtime(),
                "defender_cloud" => ApplyDefenderCloud(),
                "defender_pua" => ApplyDefenderPua(),
                "defender_controlled_folders" => ApplyControlledFolders(),
                "disable_autorun" => ApplyDisableAutoRun(),
                "disable_autoplay" => ApplyDisableAutoPlay(),
                "rdp_nla" => ApplyRdpNla(),
                "disable_rdp" => ApplyDisableRdp(),
                _ => false
            };
        }
        catch (Exception ex)
        {
            action.ErrorMessage = ex.Message;
            return false;
        }
    }

    private bool ApplyDisableWsh()
    {
        using var key = Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Microsoft\Windows Script Host\Settings");
        key?.SetValue("Enabled", 0, RegistryValueKind.DWord);
        return true;
    }

    private bool ApplyDisablePsV2()
    {
        var psi = new ProcessStartInfo
        {
            FileName = "dism",
            Arguments = "/online /disable-feature /featurename:MicrosoftWindowsPowerShellV2 /norestart",
            UseShellExecute = false,
            CreateNoWindow = true
        };
        using var proc = Process.Start(psi);
        proc?.WaitForExit();
        return proc?.ExitCode == 0;
    }

    private bool ApplyScriptBlockLogging()
    {
        using var key = Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Policies\Microsoft\Windows\PowerShell\ScriptBlockLogging");
        key?.SetValue("EnableScriptBlockLogging", 1, RegistryValueKind.DWord);
        return true;
    }

    private bool ApplyModuleLogging()
    {
        using var key = Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Policies\Microsoft\Windows\PowerShell\ModuleLogging");
        key?.SetValue("EnableModuleLogging", 1, RegistryValueKind.DWord);
        return true;
    }

    private bool ApplyExecutionPolicy()
    {
        using var key = Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Microsoft\PowerShell\1\ShellIds\Microsoft.PowerShell");
        key?.SetValue("ExecutionPolicy", "RemoteSigned", RegistryValueKind.String);
        return true;
    }

    private bool ApplyFirewallEnable()
    {
        var psi = new ProcessStartInfo
        {
            FileName = "netsh",
            Arguments = "advfirewall set allprofiles state on",
            UseShellExecute = false,
            CreateNoWindow = true
        };
        using var proc = Process.Start(psi);
        proc?.WaitForExit();
        return proc?.ExitCode == 0;
    }

    private bool ApplyBlockRmmPorts()
    {
        // Block common RMM ports outbound
        var ports = new[] { "5938", "8040", "8041", "5939" };
        foreach (var port in ports)
        {
            var psi = new ProcessStartInfo
            {
                FileName = "netsh",
                Arguments = $"advfirewall firewall add rule name=\"ViperKit_Block_{port}\" dir=out action=block protocol=tcp remoteport={port}",
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var proc = Process.Start(psi);
            proc?.WaitForExit();
        }
        return true;
    }

    private bool ApplyDefenderRealtime()
    {
        var psi = new ProcessStartInfo
        {
            FileName = "powershell",
            Arguments = "-Command \"Set-MpPreference -DisableRealtimeMonitoring $false\"",
            UseShellExecute = false,
            CreateNoWindow = true
        };
        using var proc = Process.Start(psi);
        proc?.WaitForExit();
        return true;
    }

    private bool ApplyDefenderCloud()
    {
        var psi = new ProcessStartInfo
        {
            FileName = "powershell",
            Arguments = "-Command \"Set-MpPreference -MAPSReporting Advanced\"",
            UseShellExecute = false,
            CreateNoWindow = true
        };
        using var proc = Process.Start(psi);
        proc?.WaitForExit();
        return true;
    }

    private bool ApplyDefenderPua()
    {
        var psi = new ProcessStartInfo
        {
            FileName = "powershell",
            Arguments = "-Command \"Set-MpPreference -PUAProtection Enabled\"",
            UseShellExecute = false,
            CreateNoWindow = true
        };
        using var proc = Process.Start(psi);
        proc?.WaitForExit();
        return true;
    }

    private bool ApplyControlledFolders()
    {
        var psi = new ProcessStartInfo
        {
            FileName = "powershell",
            Arguments = "-Command \"Set-MpPreference -EnableControlledFolderAccess Enabled\"",
            UseShellExecute = false,
            CreateNoWindow = true
        };
        using var proc = Process.Start(psi);
        proc?.WaitForExit();
        return true;
    }

    private bool ApplyDisableAutoRun()
    {
        using var key = Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\Explorer");
        key?.SetValue("NoDriveTypeAutoRun", 255, RegistryValueKind.DWord);
        return true;
    }

    private bool ApplyDisableAutoPlay()
    {
        using var key = Registry.CurrentUser.CreateSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\AutoplayHandlers");
        key?.SetValue("DisableAutoplay", 1, RegistryValueKind.DWord);
        return true;
    }

    private bool ApplyRdpNla()
    {
        using var key = Registry.LocalMachine.CreateSubKey(@"SYSTEM\CurrentControlSet\Control\Terminal Server\WinStations\RDP-Tcp");
        key?.SetValue("UserAuthentication", 1, RegistryValueKind.DWord);
        return true;
    }

    private bool ApplyDisableRdp()
    {
        using var key = Registry.LocalMachine.CreateSubKey(@"SYSTEM\CurrentControlSet\Control\Terminal Server");
        key?.SetValue("fDenyTSConnections", 1, RegistryValueKind.DWord);
        return true;
    }

    // ----------------------------
    // HARDEN – Rollback
    // ----------------------------
    private async void HardenRollbackLastButton_OnClick(object? sender, RoutedEventArgs e)
    {
        var lastEntry = HardenJournal.GetLastRollbackable();
        if (lastEntry == null)
        {
            if (HardenStatusText != null)
                HardenStatusText.Text = "Status: Nothing to rollback.";
            return;
        }

        if (sender is Button btn)
            btn.IsEnabled = false;

        try
        {
            if (HardenStatusText != null)
                HardenStatusText.Text = $"Status: Rolling back {lastEntry.ActionName}...";

            bool success = await Task.Run(() => RollbackAction(lastEntry));

            if (success)
            {
                HardenJournal.MarkRolledBack(lastEntry.ActionId);

                // Update the action in the list
                var action = _hardenActions.FirstOrDefault(a => a.Id == lastEntry.ActionId);
                if (action != null)
                {
                    action.IsApplied = false;
                    action.CurrentState = lastEntry.PreviousState;
                }

                CaseManager.AddEvent("Harden", $"Rolled back: {lastEntry.ActionName}",
                    "INFO", lastEntry.ActionName, $"Restored to {lastEntry.PreviousState}");

                // Refresh display
                if (HardenActionsList != null)
                {
                    HardenActionsList.ItemsSource = null;
                    HardenActionsList.ItemsSource = _hardenActions;
                }

                UpdateHardenStats();

                if (HardenStatusText != null)
                    HardenStatusText.Text = $"Status: Rolled back {lastEntry.ActionName}.";
            }
            else
            {
                if (HardenStatusText != null)
                    HardenStatusText.Text = $"Status: Failed to rollback {lastEntry.ActionName}.";
            }
        }
        finally
        {
            if (sender is Button btn2)
                btn2.IsEnabled = true;
        }
    }

    private async void HardenRollbackAllButton_OnClick(object? sender, RoutedEventArgs e)
    {
        var entries = HardenJournal.GetRollbackableEntries().Reverse().ToList();

        if (entries.Count == 0)
        {
            if (HardenStatusText != null)
                HardenStatusText.Text = "Status: Nothing to rollback.";
            return;
        }

        if (sender is Button btn)
            btn.IsEnabled = false;

        try
        {
            int success = 0;
            foreach (var entry in entries)
            {
                if (HardenStatusText != null)
                    HardenStatusText.Text = $"Status: Rolling back {entry.ActionName}...";

                bool result = await Task.Run(() => RollbackAction(entry));

                if (result)
                {
                    HardenJournal.MarkRolledBack(entry.ActionId);
                    var action = _hardenActions.FirstOrDefault(a => a.Id == entry.ActionId);
                    if (action != null)
                    {
                        action.IsApplied = false;
                        action.CurrentState = entry.PreviousState;
                    }
                    success++;
                }
            }

            // Refresh display
            if (HardenActionsList != null)
            {
                HardenActionsList.ItemsSource = null;
                HardenActionsList.ItemsSource = _hardenActions;
            }

            UpdateHardenStats();

            if (HardenStatusText != null)
                HardenStatusText.Text = $"Status: Rolled back {success} of {entries.Count} actions.";
        }
        finally
        {
            if (sender is Button btn2)
                btn2.IsEnabled = true;
        }
    }

    private bool RollbackAction(HardenJournalEntry entry)
    {
        try
        {
            return entry.ActionId switch
            {
                "disable_wsh" => RollbackEnableWsh(),
                "disable_ps_v2" => true, // Re-enabling PS v2 requires special handling
                "ps_script_block_logging" => RollbackDisableScriptBlockLogging(),
                "ps_module_logging" => RollbackDisableModuleLogging(),
                "ps_execution_policy" => RollbackExecutionPolicy(entry.PreviousState),
                "firewall_enable_all" => true, // Don't rollback firewall off
                "firewall_block_rmm_ports" => RollbackUnblockRmmPorts(),
                "defender_realtime" => true, // Don't rollback defender off
                "defender_cloud" => true,
                "defender_pua" => true,
                "defender_controlled_folders" => RollbackDisableControlledFolders(),
                "disable_autorun" => RollbackEnableAutoRun(),
                "disable_autoplay" => RollbackEnableAutoPlay(),
                "rdp_nla" => RollbackDisableRdpNla(),
                "disable_rdp" => RollbackEnableRdp(),
                _ => false
            };
        }
        catch
        {
            return false;
        }
    }

    private bool RollbackEnableWsh()
    {
        using var key = Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Microsoft\Windows Script Host\Settings");
        key?.DeleteValue("Enabled", false);
        return true;
    }

    private bool RollbackDisableScriptBlockLogging()
    {
        using var key = Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Policies\Microsoft\Windows\PowerShell\ScriptBlockLogging");
        key?.SetValue("EnableScriptBlockLogging", 0, RegistryValueKind.DWord);
        return true;
    }

    private bool RollbackDisableModuleLogging()
    {
        using var key = Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Policies\Microsoft\Windows\PowerShell\ModuleLogging");
        key?.SetValue("EnableModuleLogging", 0, RegistryValueKind.DWord);
        return true;
    }

    private bool RollbackExecutionPolicy(string originalPolicy)
    {
        using var key = Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Microsoft\PowerShell\1\ShellIds\Microsoft.PowerShell");
        key?.SetValue("ExecutionPolicy", originalPolicy, RegistryValueKind.String);
        return true;
    }

    private bool RollbackUnblockRmmPorts()
    {
        var ports = new[] { "5938", "8040", "8041", "5939" };
        foreach (var port in ports)
        {
            var psi = new ProcessStartInfo
            {
                FileName = "netsh",
                Arguments = $"advfirewall firewall delete rule name=\"ViperKit_Block_{port}\"",
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var proc = Process.Start(psi);
            proc?.WaitForExit();
        }
        return true;
    }

    private bool RollbackDisableControlledFolders()
    {
        var psi = new ProcessStartInfo
        {
            FileName = "powershell",
            Arguments = "-Command \"Set-MpPreference -EnableControlledFolderAccess Disabled\"",
            UseShellExecute = false,
            CreateNoWindow = true
        };
        using var proc = Process.Start(psi);
        proc?.WaitForExit();
        return true;
    }

    private bool RollbackEnableAutoRun()
    {
        using var key = Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\Explorer");
        key?.DeleteValue("NoDriveTypeAutoRun", false);
        return true;
    }

    private bool RollbackEnableAutoPlay()
    {
        using var key = Registry.CurrentUser.CreateSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\AutoplayHandlers");
        key?.DeleteValue("DisableAutoplay", false);
        return true;
    }

    private bool RollbackDisableRdpNla()
    {
        using var key = Registry.LocalMachine.CreateSubKey(@"SYSTEM\CurrentControlSet\Control\Terminal Server\WinStations\RDP-Tcp");
        key?.SetValue("UserAuthentication", 0, RegistryValueKind.DWord);
        return true;
    }

    private bool RollbackEnableRdp()
    {
        using var key = Registry.LocalMachine.CreateSubKey(@"SYSTEM\CurrentControlSet\Control\Terminal Server");
        key?.SetValue("fDenyTSConnections", 0, RegistryValueKind.DWord);
        return true;
    }

    // ----------------------------
    // HARDEN – Profile & Selection
    // ----------------------------
    private void HardenLoadProfileButton_OnClick(object? sender, RoutedEventArgs e)
    {
        string profile = "Standard";
        if (HardenProfileStrict?.IsChecked == true) profile = "Strict";
        else if (HardenProfileCustom?.IsChecked == true) profile = "Custom";

        foreach (var action in _hardenActions)
        {
            if (action.IsAlreadyHardened) continue;

            action.IsSelected = profile switch
            {
                "Standard" => action.Profile == "Standard" || action.Profile == "Both",
                "Strict" => true, // Strict includes everything
                "Custom" => false, // Custom starts with nothing selected
                _ => false
            };
        }

        // Refresh display
        if (HardenActionsList != null)
        {
            HardenActionsList.ItemsSource = null;
            HardenActionsList.ItemsSource = _hardenActions;
        }

        UpdateHardenStats();

        if (HardenStatusText != null)
            HardenStatusText.Text = $"Status: Loaded {profile} profile.";
    }

    private void HardenSelectAllButton_OnClick(object? sender, RoutedEventArgs e)
    {
        foreach (var action in _hardenActions)
        {
            if (!action.IsAlreadyHardened)
                action.IsSelected = true;
        }

        if (HardenActionsList != null)
        {
            HardenActionsList.ItemsSource = null;
            HardenActionsList.ItemsSource = _hardenActions;
        }

        UpdateHardenStats();
    }

    private void HardenDeselectAllButton_OnClick(object? sender, RoutedEventArgs e)
    {
        foreach (var action in _hardenActions)
        {
            action.IsSelected = false;
        }

        if (HardenActionsList != null)
        {
            HardenActionsList.ItemsSource = null;
            HardenActionsList.ItemsSource = _hardenActions;
        }

        UpdateHardenStats();
    }

    // ----------------------------
    // HARDEN – Update Stats
    // ----------------------------
    private void UpdateHardenStats()
    {
        int applied = _hardenActions.Count(a => a.IsApplied);
        int selected = _hardenActions.Count(a => a.IsSelected && !a.IsAlreadyHardened);
        int alreadySet = _hardenActions.Count(a => a.IsAlreadyHardened);

        if (HardenStatsApplied != null)
            HardenStatsApplied.Text = $"Applied: {applied}";
        if (HardenStatsPending != null)
            HardenStatsPending.Text = $"Selected: {selected}";
        if (HardenStatsSkipped != null)
            HardenStatsSkipped.Text = $"Already Set: {alreadySet}";
    }

#pragma warning restore CA1416
}

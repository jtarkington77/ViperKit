// ViperKit.UI - Models\DemoManager.cs
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using Microsoft.Win32;

namespace ViperKit.UI.Models
{
    /// <summary>
    /// Manages the demo mode - artifact creation, walkthrough steps, and cleanup.
    /// </summary>
    public static class DemoManager
    {
#pragma warning disable CA1416 // Windows-only APIs

        // Demo state
        public static bool IsDemoActive { get; private set; }
        public static int CurrentStep { get; private set; }
        public static int TotalSteps => Steps.Count;
        public static DateTime? DemoStartedAt { get; private set; }

        // Artifacts and steps
        public static List<DemoArtifact> Artifacts { get; } = new();
        public static List<DemoStep> Steps { get; } = new();

        // Demo folder names - using DemoRMM so Hunt keyword search finds it
        private const string DemoFolderName = "DemoRMM";
        private const string DemoToolName = "DemoRMM";

        // Paths - Use ProgramData instead of Program Files (no admin required, and Hunt scans it)
        public static string DemoProgramFilesPath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), DemoFolderName);
        public static string DemoTempPath => Path.Combine(
            Path.GetTempPath(), DemoFolderName);
        public static string DemoAppDataPath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), DemoFolderName);

        /// <summary>
        /// Initialize the demo - define artifacts and steps.
        /// </summary>
        public static void Initialize()
        {
            Artifacts.Clear();
            Steps.Clear();
            CurrentStep = 0;
            IsDemoActive = false;
            DemoStartedAt = null;

            // Define demo artifacts
            DefineArtifacts();

            // Define walkthrough steps
            DefineSteps();
        }

        private static void DefineArtifacts()
        {
            // 1. Fake RMM executable in Program Files
            Artifacts.Add(new DemoArtifact
            {
                ArtifactType = "File",
                Name = $"{DemoToolName}.exe",
                Path = Path.Combine(DemoProgramFilesPath, $"{DemoToolName}.exe"),
                Description = "Fake RMM executable (benign placeholder file)",
                SimulatesAttack = "Attacker-deployed remote access tool"
            });

            // 2. Suspicious script in Temp
            Artifacts.Add(new DemoArtifact
            {
                ArtifactType = "Script",
                Name = "helper.ps1",
                Path = Path.Combine(DemoTempPath, "helper.ps1"),
                Description = "Suspicious PowerShell script in temp folder",
                SimulatesAttack = "Malicious script dropped by attacker"
            });

            // 3. Config file in AppData
            Artifacts.Add(new DemoArtifact
            {
                ArtifactType = "File",
                Name = "config.dat",
                Path = Path.Combine(DemoAppDataPath, "config.dat"),
                Description = "Suspicious data file in AppData",
                SimulatesAttack = "C2 configuration or exfiltrated data"
            });

            // 4. Registry Run key for persistence
            Artifacts.Add(new DemoArtifact
            {
                ArtifactType = "Registry",
                Name = $"{DemoToolName} (Run key)",
                Path = $@"HKCU\Software\Microsoft\Windows\CurrentVersion\Run\{DemoToolName}",
                Description = "Registry autorun entry pointing to demo executable",
                SimulatesAttack = "Persistence via Run key"
            });

            // 5. Scheduled task (disabled)
            Artifacts.Add(new DemoArtifact
            {
                ArtifactType = "ScheduledTask",
                Name = $"{DemoToolName}Task",
                Path = $@"\{DemoFolderName}\{DemoToolName}Task",
                Description = "Scheduled task (created disabled)",
                SimulatesAttack = "Persistence via scheduled task"
            });
        }

        private static void DefineSteps()
        {
            // Step 1: Hunt
            Steps.Add(new DemoStep
            {
                StepNumber = 1,
                Title = "Hunt the suspicious tool",
                TabTarget = "Hunt",
                TabIndex = 1,
                Instructions = $"Go to the HUNT tab and search for \"{DemoToolName}\"",
                SearchTerm = DemoToolName,
                ExpectedFindings = $"You should find:\n• {DemoToolName} folder in ProgramData\n• File metadata and hash information",
                Tip = "Click 'Set as case focus' to track this target across all tabs",
                ActionToTake = "Set as case focus",
                LearningPoint = "Hunt helps you find suspicious files, processes, and artifacts by name, hash, or path"
            });

            // Step 2: Persist
            Steps.Add(new DemoStep
            {
                StepNumber = 2,
                Title = "Check for persistence",
                TabTarget = "Persist",
                TabIndex = 2,
                Instructions = "Go to the PERSIST tab and run a persistence scan",
                SearchTerm = "",
                ExpectedFindings = $"You should see highlighted entries:\n• Registry Run key for {DemoToolName} (pink border)\n• Scheduled task for {DemoToolName}",
                Tip = "Items matching your case focus are highlighted with a pink border",
                ActionToTake = "Review highlighted persistence entries",
                LearningPoint = "Persist shows all autostart locations - Run keys, services, tasks, startup folders, etc."
            });

            // Step 3: Sweep
            Steps.Add(new DemoStep
            {
                StepNumber = 3,
                Title = "Find related artifacts",
                TabTarget = "Sweep",
                TabIndex = 3,
                Instructions = "Go to the SWEEP tab and run a sweep (7-day lookback)",
                SearchTerm = "",
                ExpectedFindings = $"You should see:\n• helper.ps1 (TIME CLUSTER - same install time)\n• config.dat (FOLDER CLUSTER)\n• All demo files clustered together",
                Tip = "Time clustering finds files created around the same time as your focus target",
                ActionToTake = "Add related files to focus",
                LearningPoint = "Sweep finds other artifacts that may be related to the threat based on timing and location"
            });

            // Step 4: Add to Cleanup
            Steps.Add(new DemoStep
            {
                StepNumber = 4,
                Title = "Queue items for removal",
                TabTarget = "Persist",
                TabIndex = 2,
                Instructions = "Go back to PERSIST, select the highlighted entries, and click 'Add to Cleanup'",
                SearchTerm = "",
                ExpectedFindings = "Persistence entries added to cleanup queue",
                Tip = "You can also add items from the Sweep tab",
                ActionToTake = "Add to Cleanup queue",
                LearningPoint = "Items from any tab can be queued for safe removal"
            });

            // Step 5: Execute Cleanup
            Steps.Add(new DemoStep
            {
                StepNumber = 5,
                Title = "Execute the cleanup",
                TabTarget = "Cleanup",
                TabIndex = 4,
                Instructions = "Go to the CLEANUP tab, review the queue, and click 'Execute All Pending'",
                SearchTerm = "",
                ExpectedFindings = "All demo artifacts removed:\n• Files quarantined\n• Registry key deleted\n• Scheduled task disabled",
                Tip = "All actions can be undone if needed",
                ActionToTake = "Execute All Pending",
                LearningPoint = "Cleanup safely removes threats with full undo capability"
            });

            // Step 6: Complete
            Steps.Add(new DemoStep
            {
                StepNumber = 6,
                Title = "Demo complete!",
                TabTarget = "Dashboard",
                TabIndex = 0,
                Instructions = "Return to Dashboard to see the summary",
                SearchTerm = "",
                ExpectedFindings = "All demo artifacts have been cleaned up",
                Tip = "In a real incident, you would export the case report for documentation",
                ActionToTake = "End Demo",
                LearningPoint = "You've completed the full incident response workflow: Hunt → Persist → Sweep → Cleanup"
            });
        }

        /// <summary>
        /// Start the demo by creating all artifacts.
        /// </summary>
        public static (bool Success, string Message) StartDemo()
        {
            try
            {
                Initialize();
                IsDemoActive = true;
                DemoStartedAt = DateTime.Now;
                CurrentStep = 1;

                var errors = new List<string>();

                foreach (var artifact in Artifacts)
                {
                    var result = CreateArtifact(artifact);
                    if (!result.Success)
                        errors.Add($"{artifact.Name}: {result.Message}");
                }

                if (errors.Count > 0)
                {
                    return (false, $"Demo started with {errors.Count} errors:\n" + string.Join("\n", errors));
                }

                CaseManager.AddEvent("Dashboard", "Demo mode started", "INFO", null,
                    $"Created {Artifacts.Count} demo artifacts for walkthrough");

                return (true, $"Demo started! Created {Artifacts.Count} artifacts. Follow the walkthrough steps.");
            }
            catch (Exception ex)
            {
                IsDemoActive = false;
                return (false, $"Failed to start demo: {ex.Message}");
            }
        }

        /// <summary>
        /// Create a single demo artifact.
        /// </summary>
        private static (bool Success, string Message) CreateArtifact(DemoArtifact artifact)
        {
            try
            {
                switch (artifact.ArtifactType)
                {
                    case "File":
                        return CreateFileArtifact(artifact);
                    case "Script":
                        return CreateScriptArtifact(artifact);
                    case "Registry":
                        return CreateRegistryArtifact(artifact);
                    case "ScheduledTask":
                        return CreateScheduledTaskArtifact(artifact);
                    default:
                        return (false, $"Unknown artifact type: {artifact.ArtifactType}");
                }
            }
            catch (Exception ex)
            {
                artifact.ErrorMessage = ex.Message;
                return (false, ex.Message);
            }
        }

        private static (bool Success, string Message) CreateFileArtifact(DemoArtifact artifact)
        {
            string? dir = Path.GetDirectoryName(artifact.Path);
            if (dir != null && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            // Create a benign placeholder file with a marker
            string content = $"[ViperKit Demo File]\n" +
                            $"This is a harmless demo file created by ViperKit.\n" +
                            $"Created: {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n" +
                            $"Purpose: Training and demonstration\n" +
                            $"Safe to delete.\n";

            File.WriteAllText(artifact.Path, content);
            artifact.IsCreated = true;
            artifact.CreatedAt = DateTime.Now;
            return (true, "File created");
        }

        private static (bool Success, string Message) CreateScriptArtifact(DemoArtifact artifact)
        {
            string? dir = Path.GetDirectoryName(artifact.Path);
            if (dir != null && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            // Create a benign PowerShell script (does nothing harmful)
            string content = @"# ViperKit Demo Script
# This is a harmless demo script created by ViperKit for training purposes.
# It does NOT execute any malicious actions.

Write-Host ""[ViperKit Demo] This is a demonstration script.""
Write-Host ""[ViperKit Demo] In a real attack, this could be a malicious payload.""
Write-Host ""[ViperKit Demo] Created: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + @"""

# Safe to delete
";

            File.WriteAllText(artifact.Path, content, Encoding.UTF8);
            artifact.IsCreated = true;
            artifact.CreatedAt = DateTime.Now;
            return (true, "Script created");
        }

        private static (bool Success, string Message) CreateRegistryArtifact(DemoArtifact artifact)
        {
            try
            {
                // Create Run key pointing to the demo executable
                // Use CreateSubKey which creates if missing and opens with write access
                using var key = Registry.CurrentUser.CreateSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Run", true);

                if (key == null)
                    return (false, "Could not open or create Run key - access denied");

                string exePath = Path.Combine(DemoProgramFilesPath, $"{DemoToolName}.exe");
                key.SetValue(DemoToolName, $"\"{exePath}\"", RegistryValueKind.String);

                artifact.IsCreated = true;
                artifact.CreatedAt = DateTime.Now;
                return (true, "Registry key created");
            }
            catch (UnauthorizedAccessException ex)
            {
                return (false, $"Access denied to Run key: {ex.Message}");
            }
            catch (System.Security.SecurityException ex)
            {
                return (false, $"Security error accessing Run key: {ex.Message}");
            }
            catch (Exception ex)
            {
                return (false, $"Error creating registry entry: {ex.Message}");
            }
        }

        private static (bool Success, string Message) CreateScheduledTaskArtifact(DemoArtifact artifact)
        {
            // Create a disabled scheduled task using schtasks.exe
            string exePath = Path.Combine(DemoProgramFilesPath, $"{DemoToolName}.exe");
            string taskName = $"\\{DemoFolderName}\\{DemoToolName}Task";

            // Create task XML
            string taskXml = $@"<?xml version=""1.0"" encoding=""UTF-16""?>
<Task version=""1.2"" xmlns=""http://schemas.microsoft.com/windows/2004/02/mit/task"">
  <RegistrationInfo>
    <Description>ViperKit Demo Task - Safe to delete</Description>
  </RegistrationInfo>
  <Triggers>
    <LogonTrigger>
      <Enabled>false</Enabled>
    </LogonTrigger>
  </Triggers>
  <Settings>
    <Enabled>false</Enabled>
    <Hidden>false</Hidden>
  </Settings>
  <Actions>
    <Exec>
      <Command>""{exePath}""</Command>
    </Exec>
  </Actions>
</Task>";

            // Save XML to temp file
            string xmlPath = Path.Combine(Path.GetTempPath(), "viperkit_demo_task.xml");
            File.WriteAllText(xmlPath, taskXml, Encoding.Unicode);

            try
            {
                // Create the task folder first
                var folderProcess = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "schtasks.exe",
                        Arguments = $"/Create /TN \"{DemoFolderName}\\Placeholder\" /SC ONCE /ST 00:00 /TR \"cmd.exe\" /F",
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true
                    }
                };
                folderProcess.Start();
                folderProcess.WaitForExit(5000);

                // Delete the placeholder
                var deletePlaceholder = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "schtasks.exe",
                        Arguments = $"/Delete /TN \"{DemoFolderName}\\Placeholder\" /F",
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };
                deletePlaceholder.Start();
                deletePlaceholder.WaitForExit(5000);

                // Create the actual task from XML
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "schtasks.exe",
                        Arguments = $"/Create /TN \"{taskName}\" /XML \"{xmlPath}\" /F",
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true
                    }
                };

                process.Start();
                string output = process.StandardOutput.ReadToEnd();
                string error = process.StandardError.ReadToEnd();
                process.WaitForExit(10000);

                if (process.ExitCode != 0)
                {
                    return (false, $"schtasks failed: {error}");
                }

                artifact.IsCreated = true;
                artifact.CreatedAt = DateTime.Now;
                return (true, "Scheduled task created (disabled)");
            }
            finally
            {
                // Clean up temp XML
                if (File.Exists(xmlPath))
                    File.Delete(xmlPath);
            }
        }

        /// <summary>
        /// Clean up all demo artifacts and end the demo.
        /// </summary>
        public static (bool Success, string Message) EndDemo()
        {
            var errors = new List<string>();

            foreach (var artifact in Artifacts)
            {
                if (artifact.IsCreated && !artifact.IsCleanedUp)
                {
                    var result = CleanupArtifact(artifact);
                    if (!result.Success)
                        errors.Add($"{artifact.Name}: {result.Message}");
                }
            }

            // Clean up demo folders
            CleanupDemoFolders();

            IsDemoActive = false;
            CurrentStep = 0;

            CaseManager.AddEvent("Dashboard", "Demo mode ended", "INFO", null,
                $"Cleaned up {Artifacts.Count - errors.Count} of {Artifacts.Count} artifacts");

            if (errors.Count > 0)
            {
                return (false, $"Demo ended with {errors.Count} cleanup errors:\n" + string.Join("\n", errors));
            }

            return (true, "Demo ended. All artifacts cleaned up successfully.");
        }

        private static (bool Success, string Message) CleanupArtifact(DemoArtifact artifact)
        {
            try
            {
                switch (artifact.ArtifactType)
                {
                    case "File":
                    case "Script":
                        if (File.Exists(artifact.Path))
                            File.Delete(artifact.Path);
                        break;

                    case "Registry":
                        try
                        {
                            using var key = Registry.CurrentUser.CreateSubKey(
                                @"Software\Microsoft\Windows\CurrentVersion\Run", true);
                            key?.DeleteValue(DemoToolName, false);
                        }
                        catch
                        {
                            // Ignore errors during cleanup - value might not exist
                        }
                        break;

                    case "ScheduledTask":
                        var process = new Process
                        {
                            StartInfo = new ProcessStartInfo
                            {
                                FileName = "schtasks.exe",
                                Arguments = $"/Delete /TN \"\\{DemoFolderName}\\{DemoToolName}Task\" /F",
                                UseShellExecute = false,
                                CreateNoWindow = true
                            }
                        };
                        process.Start();
                        process.WaitForExit(5000);
                        break;
                }

                artifact.IsCleanedUp = true;
                artifact.CleanedUpAt = DateTime.Now;
                return (true, "Cleaned up");
            }
            catch (Exception ex)
            {
                artifact.ErrorMessage = ex.Message;
                return (false, ex.Message);
            }
        }

        private static void CleanupDemoFolders()
        {
            try
            {
                if (Directory.Exists(DemoProgramFilesPath))
                    Directory.Delete(DemoProgramFilesPath, true);
            }
            catch { }

            try
            {
                if (Directory.Exists(DemoTempPath))
                    Directory.Delete(DemoTempPath, true);
            }
            catch { }

            try
            {
                if (Directory.Exists(DemoAppDataPath))
                    Directory.Delete(DemoAppDataPath, true);
            }
            catch { }

            // Try to delete the task folder
            try
            {
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "schtasks.exe",
                        Arguments = $"/Delete /TN \"\\{DemoFolderName}\" /F",
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };
                process.Start();
                process.WaitForExit(3000);
            }
            catch { }
        }

        /// <summary>
        /// Move to the next step.
        /// </summary>
        public static void NextStep()
        {
            if (CurrentStep < TotalSteps)
                CurrentStep++;
        }

        /// <summary>
        /// Move to the previous step.
        /// </summary>
        public static void PreviousStep()
        {
            if (CurrentStep > 1)
                CurrentStep--;
        }

        /// <summary>
        /// Get the current step.
        /// </summary>
        public static DemoStep? GetCurrentStep()
        {
            if (CurrentStep >= 1 && CurrentStep <= Steps.Count)
                return Steps[CurrentStep - 1];
            return null;
        }

        /// <summary>
        /// Mark current step as completed and move to next.
        /// </summary>
        public static void CompleteCurrentStep()
        {
            var step = GetCurrentStep();
            if (step != null)
            {
                step.IsCompleted = true;
                NextStep();
            }
        }

#pragma warning restore CA1416
    }
}

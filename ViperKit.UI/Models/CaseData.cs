// ViperKit.UI - Models\CaseData.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace ViperKit.UI.Models
{
    /// <summary>
    /// Represents a complete case that can be saved and loaded.
    /// </summary>
    public class CaseData
    {
        public string CaseId { get; set; } = string.Empty;
        public string CaseName { get; set; } = string.Empty;
        public string HostName { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public string OsDescription { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime? ClosedAt { get; set; }
        public string Status { get; set; } = "Active"; // Active, Closed, Archived

        public List<string> FocusTargets { get; set; } = new();
        public List<CaseEvent> Events { get; set; } = new();
        public List<CleanupItem> CleanupQueue { get; set; } = new();

        // Baseline data
        public BaselineData? Baseline { get; set; }
        public DateTime? BaselineCapturedAt { get; set; }

        // Notes
        public string Notes { get; set; } = string.Empty;
    }

    /// <summary>
    /// Stores baseline system state for post-incident comparison.
    /// </summary>
    public class BaselineData
    {
        public DateTime CapturedAt { get; set; }
        public string HostName { get; set; } = string.Empty;
        public string CapturedBy { get; set; } = string.Empty;

        // Persist scan results at baseline time
        public List<BaselinePersistEntry> PersistEntries { get; set; } = new();

        // Hardening actions applied
        public List<BaselineHardenEntry> HardeningApplied { get; set; } = new();

        // System configuration snapshots
        public List<BaselineConfigEntry> ConfigEntries { get; set; } = new();
    }

    /// <summary>
    /// A persistence entry captured at baseline time.
    /// </summary>
    public class BaselinePersistEntry
    {
        public string Name { get; set; } = string.Empty;
        public string Path { get; set; } = string.Empty;
        public string RegistryPath { get; set; } = string.Empty;
        public string LocationType { get; set; } = string.Empty;
        public string Source { get; set; } = string.Empty;
        public string Risk { get; set; } = string.Empty;
        public string Hash { get; set; } = string.Empty;
        public DateTime? FileModified { get; set; }
    }

    /// <summary>
    /// A hardening action recorded in the baseline.
    /// </summary>
    public class BaselineHardenEntry
    {
        public string ActionId { get; set; } = string.Empty;
        public string ActionName { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string PreviousState { get; set; } = string.Empty;
        public string NewState { get; set; } = string.Empty;
        public DateTime AppliedAt { get; set; }
    }

    /// <summary>
    /// A system configuration entry for baseline comparison.
    /// </summary>
    public class BaselineConfigEntry
    {
        public string Category { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
        public string Source { get; set; } = string.Empty;
    }

    /// <summary>
    /// Summary info for displaying available cases.
    /// </summary>
    public class CaseSummary
    {
        public string CaseId { get; set; } = string.Empty;
        public string CaseName { get; set; } = string.Empty;
        public string HostName { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public string Status { get; set; } = string.Empty;
        public int EventCount { get; set; }
        public bool HasBaseline { get; set; }

        public string DisplayName => string.IsNullOrEmpty(CaseName) ? CaseId : CaseName;
        public string DisplayInfo => $"{HostName} | {CreatedAt:yyyy-MM-dd HH:mm} | {EventCount} events";
    }

    /// <summary>
    /// Static manager for case persistence to disk.
    /// </summary>
    public static class CaseStorage
    {
        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            WriteIndented = true
        };

        /// <summary>
        /// Get the base folder for all ViperKit data.
        /// </summary>
        public static string GetViperKitDataFolder()
        {
            string folder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "ViperKit");
            Directory.CreateDirectory(folder);
            return folder;
        }

        /// <summary>
        /// Get the folder for a specific case.
        /// </summary>
        public static string GetCaseFolder(string caseId)
        {
            string folder = Path.Combine(GetViperKitDataFolder(), "Cases", caseId);
            Directory.CreateDirectory(folder);
            return folder;
        }

        /// <summary>
        /// Save a case to disk.
        /// </summary>
        public static void SaveCase(CaseData caseData)
        {
            try
            {
                string folder = GetCaseFolder(caseData.CaseId);
                string path = Path.Combine(folder, "case.json");
                string json = JsonSerializer.Serialize(caseData, _jsonOptions);
                File.WriteAllText(path, json);
            }
            catch
            {
                // Silently fail - don't crash if we can't save
            }
        }

        /// <summary>
        /// Load a case from disk.
        /// </summary>
        public static CaseData? LoadCase(string caseId)
        {
            try
            {
                string folder = GetCaseFolder(caseId);
                string path = Path.Combine(folder, "case.json");
                if (File.Exists(path))
                {
                    string json = File.ReadAllText(path);
                    return JsonSerializer.Deserialize<CaseData>(json);
                }
            }
            catch
            {
                // Silently fail
            }
            return null;
        }

        /// <summary>
        /// Get list of all available cases.
        /// </summary>
        public static List<CaseSummary> GetAvailableCases()
        {
            var summaries = new List<CaseSummary>();
            try
            {
                string casesFolder = Path.Combine(GetViperKitDataFolder(), "Cases");
                if (!Directory.Exists(casesFolder))
                    return summaries;

                foreach (string caseDir in Directory.GetDirectories(casesFolder))
                {
                    string casePath = Path.Combine(caseDir, "case.json");
                    if (File.Exists(casePath))
                    {
                        try
                        {
                            string json = File.ReadAllText(casePath);
                            var caseData = JsonSerializer.Deserialize<CaseData>(json);
                            if (caseData != null)
                            {
                                summaries.Add(new CaseSummary
                                {
                                    CaseId = caseData.CaseId,
                                    CaseName = caseData.CaseName,
                                    HostName = caseData.HostName,
                                    CreatedAt = caseData.CreatedAt,
                                    Status = caseData.Status,
                                    EventCount = caseData.Events?.Count ?? 0,
                                    HasBaseline = caseData.Baseline != null
                                });
                            }
                        }
                        catch
                        {
                            // Skip corrupted case files
                        }
                    }
                }

                // Sort by created date descending
                summaries.Sort((a, b) => b.CreatedAt.CompareTo(a.CreatedAt));
            }
            catch
            {
                // Silently fail
            }
            return summaries;
        }

        /// <summary>
        /// Delete a case from disk.
        /// </summary>
        public static bool DeleteCase(string caseId)
        {
            try
            {
                string folder = GetCaseFolder(caseId);
                if (Directory.Exists(folder))
                {
                    Directory.Delete(folder, true);
                    return true;
                }
            }
            catch
            {
                // Silently fail
            }
            return false;
        }

        /// <summary>
        /// Check if a case exists on disk.
        /// </summary>
        public static bool CaseExists(string caseId)
        {
            string path = Path.Combine(GetCaseFolder(caseId), "case.json");
            return File.Exists(path);
        }
    }
}

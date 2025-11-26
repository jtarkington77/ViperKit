// ViperKit.UI - Models\HardenJournal.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace ViperKit.UI.Models
{
    /// <summary>
    /// Tracks all hardening actions applied during a case for rollback capability.
    /// </summary>
    public static class HardenJournal
    {
        private static readonly List<HardenJournalEntry> _entries = new();
        private static string _caseId = string.Empty;
        private static readonly object _lock = new();

        /// <summary>
        /// Initialize journal for a case.
        /// </summary>
        public static void Initialize(string caseId)
        {
            lock (_lock)
            {
                _caseId = caseId;
                _entries.Clear();
                LoadFromDisk();
            }
        }

        /// <summary>
        /// Record an applied hardening action.
        /// </summary>
        public static void RecordAction(HardenAction action)
        {
            lock (_lock)
            {
                var entry = new HardenJournalEntry
                {
                    Id = Guid.NewGuid().ToString("N")[..8],
                    ActionId = action.Id,
                    ActionName = action.Name,
                    Category = action.Category,
                    Timestamp = DateTime.Now,
                    PreviousState = action.CurrentState,
                    NewState = action.RecommendedState,
                    RollbackData = action.RollbackData,
                    IsRolledBack = false,
                    CaseId = _caseId
                };

                _entries.Add(entry);
                SaveToDisk();
            }
        }

        /// <summary>
        /// Mark an action as rolled back.
        /// </summary>
        public static void MarkRolledBack(string actionId)
        {
            lock (_lock)
            {
                var entry = _entries.FindLast(e => e.ActionId == actionId && !e.IsRolledBack);
                if (entry != null)
                {
                    entry.IsRolledBack = true;
                    entry.RolledBackAt = DateTime.Now;
                    SaveToDisk();
                }
            }
        }

        /// <summary>
        /// Get all entries for the current case.
        /// </summary>
        public static IReadOnlyList<HardenJournalEntry> GetEntries()
        {
            lock (_lock)
            {
                return _entries.ToArray();
            }
        }

        /// <summary>
        /// Get entries that can be rolled back (applied but not yet rolled back).
        /// </summary>
        public static IReadOnlyList<HardenJournalEntry> GetRollbackableEntries()
        {
            lock (_lock)
            {
                return _entries.FindAll(e => !e.IsRolledBack);
            }
        }

        /// <summary>
        /// Get the last applied action that hasn't been rolled back.
        /// </summary>
        public static HardenJournalEntry? GetLastRollbackable()
        {
            lock (_lock)
            {
                for (int i = _entries.Count - 1; i >= 0; i--)
                {
                    if (!_entries[i].IsRolledBack)
                        return _entries[i];
                }
                return null;
            }
        }

        /// <summary>
        /// Get the journal file path for the current case.
        /// </summary>
        public static string GetJournalPath()
        {
            string folder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "ViperKit", "HardenJournals");
            Directory.CreateDirectory(folder);
            return Path.Combine(folder, $"{_caseId}_harden.json");
        }

        private static void SaveToDisk()
        {
            try
            {
                string path = GetJournalPath();
                var options = new JsonSerializerOptions { WriteIndented = true };
                string json = JsonSerializer.Serialize(_entries, options);
                File.WriteAllText(path, json);
            }
            catch
            {
                // Silently fail - don't crash if we can't save
            }
        }

        private static void LoadFromDisk()
        {
            try
            {
                string path = GetJournalPath();
                if (File.Exists(path))
                {
                    string json = File.ReadAllText(path);
                    var loaded = JsonSerializer.Deserialize<List<HardenJournalEntry>>(json);
                    if (loaded != null)
                    {
                        _entries.Clear();
                        _entries.AddRange(loaded);
                    }
                }
            }
            catch
            {
                // Silently fail - start fresh if we can't load
            }
        }

        /// <summary>
        /// Get statistics for the current case.
        /// </summary>
        public static (int applied, int rolledBack) GetStats()
        {
            lock (_lock)
            {
                int applied = _entries.Count;
                int rolledBack = _entries.FindAll(e => e.IsRolledBack).Count;
                return (applied, rolledBack);
            }
        }
    }

    /// <summary>
    /// A single journal entry recording a hardening action.
    /// </summary>
    public class HardenJournalEntry
    {
        public string Id { get; set; } = string.Empty;
        public string ActionId { get; set; } = string.Empty;
        public string ActionName { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public string PreviousState { get; set; } = string.Empty;
        public string NewState { get; set; } = string.Empty;
        public string RollbackData { get; set; } = string.Empty;
        public bool IsRolledBack { get; set; }
        public DateTime? RolledBackAt { get; set; }
        public string CaseId { get; set; } = string.Empty;
    }
}

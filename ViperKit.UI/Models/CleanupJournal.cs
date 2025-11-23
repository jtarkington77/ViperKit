// ViperKit.UI - Models\CleanupJournal.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace ViperKit.UI.Models
{
    /// <summary>
    /// Journal entry for a cleanup action, enabling undo operations.
    /// Stored on the target system alongside quarantined files.
    /// </summary>
    public class CleanupJournalEntry
    {
        /// <summary>
        /// Reference to the CleanupItem.Id
        /// </summary>
        public string ItemId { get; set; } = string.Empty;

        /// <summary>
        /// Type of action performed: Quarantine, Disable, Delete, BackupAndDelete
        /// </summary>
        public string ActionType { get; set; } = string.Empty;

        /// <summary>
        /// When the action was performed.
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.Now;

        /// <summary>
        /// Original state before the action (path, registry data, service state, etc.)
        /// </summary>
        public string OriginalState { get; set; } = string.Empty;

        /// <summary>
        /// New state after the action (quarantine path, disabled, etc.)
        /// </summary>
        public string NewState { get; set; } = string.Empty;

        /// <summary>
        /// For registry items: the exported .reg file path or base64 encoded data.
        /// </summary>
        public string BackupData { get; set; } = string.Empty;

        /// <summary>
        /// Whether this action has been undone.
        /// </summary>
        public bool IsUndone { get; set; }

        /// <summary>
        /// When the undo was performed (if applicable).
        /// </summary>
        public DateTime? UndoneAt { get; set; }

        /// <summary>
        /// Case ID this action belongs to.
        /// </summary>
        public string CaseId { get; set; } = string.Empty;
    }

    /// <summary>
    /// Manages the cleanup journal stored on the target system.
    /// Allows undo of individual items or batch undo of recent actions.
    /// </summary>
    public static class CleanupJournal
    {
        private static readonly object _lock = new();
        private static readonly List<CleanupJournalEntry> _entries = new();

        /// <summary>
        /// Base quarantine directory on the target system.
        /// Using SystemDrive to ensure it stays on the target, not portable app directory.
        /// </summary>
        public static string QuarantineRoot
        {
            get
            {
                string systemDrive = Environment.GetEnvironmentVariable("SystemDrive") ?? "C:";
                return Path.Combine(systemDrive, "ViperKit_Quarantine");
            }
        }

        /// <summary>
        /// Get the quarantine folder for the current case.
        /// </summary>
        public static string GetCaseQuarantineFolder(string caseId)
        {
            return Path.Combine(QuarantineRoot, caseId);
        }

        /// <summary>
        /// Get the journal file path for a case.
        /// </summary>
        public static string GetJournalPath(string caseId)
        {
            return Path.Combine(GetCaseQuarantineFolder(caseId), "cleanup_journal.json");
        }

        /// <summary>
        /// Initialize the journal for a case (creates directory and loads existing entries).
        /// </summary>
        public static void Initialize(string caseId)
        {
            lock (_lock)
            {
                _entries.Clear();

                string caseFolder = GetCaseQuarantineFolder(caseId);
                Directory.CreateDirectory(caseFolder);

                string journalPath = GetJournalPath(caseId);
                if (File.Exists(journalPath))
                {
                    try
                    {
                        string json = File.ReadAllText(journalPath);
                        var loaded = JsonSerializer.Deserialize<List<CleanupJournalEntry>>(json);
                        if (loaded != null)
                            _entries.AddRange(loaded);
                    }
                    catch
                    {
                        // If journal is corrupt, start fresh but don't lose quarantined files
                    }
                }
            }
        }

        /// <summary>
        /// Record a cleanup action in the journal.
        /// </summary>
        public static void RecordAction(CleanupJournalEntry entry)
        {
            lock (_lock)
            {
                _entries.Add(entry);
                SaveJournal(entry.CaseId);
            }
        }

        /// <summary>
        /// Get all journal entries for the current session.
        /// </summary>
        public static IReadOnlyList<CleanupJournalEntry> GetEntries()
        {
            lock (_lock)
            {
                return _entries.ToArray();
            }
        }

        /// <summary>
        /// Get entries that can be undone (completed but not yet undone).
        /// </summary>
        public static IReadOnlyList<CleanupJournalEntry> GetUndoableEntries()
        {
            lock (_lock)
            {
                return _entries.FindAll(e => !e.IsUndone);
            }
        }

        /// <summary>
        /// Get the most recent undoable action (for "Undo Last" functionality).
        /// </summary>
        public static CleanupJournalEntry? GetLastUndoableEntry()
        {
            lock (_lock)
            {
                for (int i = _entries.Count - 1; i >= 0; i--)
                {
                    if (!_entries[i].IsUndone)
                        return _entries[i];
                }
                return null;
            }
        }

        /// <summary>
        /// Mark an entry as undone.
        /// </summary>
        public static void MarkUndone(string itemId, string caseId)
        {
            lock (_lock)
            {
                var entry = _entries.Find(e => e.ItemId == itemId && !e.IsUndone);
                if (entry != null)
                {
                    entry.IsUndone = true;
                    entry.UndoneAt = DateTime.Now;
                    SaveJournal(caseId);
                }
            }
        }

        /// <summary>
        /// Save the journal to disk.
        /// </summary>
        private static void SaveJournal(string caseId)
        {
            try
            {
                string journalPath = GetJournalPath(caseId);
                string json = JsonSerializer.Serialize(_entries, new JsonSerializerOptions
                {
                    WriteIndented = true
                });
                File.WriteAllText(journalPath, json);
            }
            catch
            {
                // Log error but don't crash - journal is best-effort
            }
        }

        /// <summary>
        /// Get statistics about cleanup actions for this case.
        /// </summary>
        public static (int total, int completed, int undone) GetStats()
        {
            lock (_lock)
            {
                int total = _entries.Count;
                int undone = _entries.FindAll(e => e.IsUndone).Count;
                return (total, total - undone, undone);
            }
        }
    }
}

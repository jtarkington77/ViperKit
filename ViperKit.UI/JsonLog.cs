using System;
using System.IO;
using System.Text.Json;

namespace ViperKit.UI
{
    public static class JsonLog
    {
        private static readonly JsonSerializerOptions Options = new()
        {
            WriteIndented = false
        };

        /// <summary>
        /// Append one JSON line to logs/json/{channel}.jsonl
        /// </summary>
        public static void Append(string channel, object payload)
        {
            try
            {
                string baseDir = AppContext.BaseDirectory;
                string logDir  = Path.Combine(baseDir, "logs", "json");
                Directory.CreateDirectory(logDir);

                string filePath = Path.Combine(logDir, $"{channel}.jsonl");
                string line     = JsonSerializer.Serialize(payload, Options);

                File.AppendAllText(filePath, line + Environment.NewLine);
            }
            catch
            {
                // Logging must never break IR flow
            }
        }
    }
}

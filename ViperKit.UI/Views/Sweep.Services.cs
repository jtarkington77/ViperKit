using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Microsoft.Win32;
using ViperKit.UI.Models;

namespace ViperKit.UI.Views;

public partial class MainWindow
{
#pragma warning disable CA1416 // Windows-only APIs

    private void RunSweepServicesAndDrivers()
    {
        try
        {
            using var servicesRoot = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services");
            if (servicesRoot == null)
                return;

            foreach (var serviceName in servicesRoot.GetSubKeyNames())
            {
                using var svcKey = servicesRoot.OpenSubKey(serviceName);
                if (svcKey == null)
                    continue;

                string displayName = svcKey.GetValue("DisplayName")?.ToString() ?? "(no DisplayName)";
                string rawImage    = svcKey.GetValue("ImagePath")?.ToString() ?? string.Empty;
                string expanded    = Environment.ExpandEnvironmentVariables(rawImage ?? string.Empty);

                // Reuse helper from Persist partial
                string exePath  = ExtractExecutablePath(expanded);
                bool   hasExe   = !string.IsNullOrWhiteSpace(exePath);
                bool   exists   = hasExe && File.Exists(exePath);
                bool   isDriver = exePath.EndsWith(".sys", StringComparison.OrdinalIgnoreCase);

                int startRaw = svcKey.GetValue("Start") is int s ? s : -1;
                string startLabel = startRaw switch
                {
                    0 => "Boot",
                    1 => "System",
                    2 => "Automatic",
                    3 => "Manual",
                    4 => "Disabled",
                    _ => $"Unknown ({startRaw})"
                };

                bool flagged = false;
                var reasons  = new List<string>();

                // 1) Missing binary
                if (!exists)
                {
                    flagged = true;
                    reasons.Add("service/driver binary missing on disk");
                }

                // 2) Company info
                string company = string.Empty;
                if (exists)
                {
                    try
                    {
                        var vi = FileVersionInfo.GetVersionInfo(exePath);
                        company = vi.CompanyName ?? string.Empty;
                    }
                    catch
                    {
                        // ignore version info failures
                    }
                }

                bool isMicrosoft = company.Contains("Microsoft", StringComparison.OrdinalIgnoreCase);

                // 3) Path-based risk
                if (exists)
                {
                    string riskLabel = BuildRiskLabel(exePath, exists);
                    if (riskLabel.StartsWith("CHECK", StringComparison.OrdinalIgnoreCase))
                    {
                        flagged = true;
                        reasons.Add(riskLabel.Replace("CHECK –", "").Trim());
                    }
                }

                // 4) Non-MS drivers
                if (isDriver && !isMicrosoft)
                {
                    flagged = true;
                    reasons.Add("non-Microsoft driver");
                }

                // 5) Random-looking service names
                if (serviceName.Length >= 20 && !serviceName.Contains(' ', StringComparison.Ordinal))
                {
                    flagged = true;
                    reasons.Add("service name looks randomized");
                }

                // 6) Boot/System non-MS drivers
                if ((startRaw == 0 || startRaw == 1) && !isMicrosoft && isDriver)
                {
                    flagged = true;
                    reasons.Add("boot/system driver from non-Microsoft binary");
                }

                // --------- SEVERITY FOR SERVICES / DRIVERS ----------
                string severity = "LOW";
                if (flagged)
                {
                    bool high = reasons.Exists(r =>
                        r.Contains("missing on disk", StringComparison.OrdinalIgnoreCase) ||
                        r.Contains("boot/system driver", StringComparison.OrdinalIgnoreCase));

                    severity = high ? "HIGH" : "MEDIUM";
                }

                var entry = new SweepEntry
                {
                    Category = isDriver ? "Driver" : "Service",
                    Severity = severity,
                    Path     = exePath,
                    Name     = serviceName,
                    Source   = "Services/Drivers",
                    Reason   = reasons.Count > 0
                        ? string.Join(", ", reasons)
                        : string.Empty
                };

                _sweepEntries.Add(entry);
            }
        }
        catch (Exception ex)
        {
            // If the services sweep itself blows up, record that as a LOW severity error entry
            _sweepEntries.Add(new SweepEntry
            {
                Category = "Error",
                Severity = "LOW",
                Source   = "Services/Drivers",
                Reason   = ex.Message
            });
        }
    }

#pragma warning restore CA1416
}

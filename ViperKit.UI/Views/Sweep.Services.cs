using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using Microsoft.Win32;

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

                bool include = false;
                bool flagged = false;
                var reasons  = new List<string>();

                // 1) Missing binary
                if (!exists)
                {
                    include = true;
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
                        include = true;
                        flagged = true;
                        reasons.Add(riskLabel.Replace("CHECK –", "").Trim());
                    }
                }

                // 4) Non-MS drivers
                if (isDriver && !isMicrosoft)
                {
                    include = true;
                    flagged = true;
                    reasons.Add("non-Microsoft driver");
                }

                // 5) Random-looking service names
                if (serviceName.Length >= 20 && !serviceName.Contains(' ', StringComparison.Ordinal))
                {
                    include = true;
                    flagged = true;
                    reasons.Add("service name looks randomized");
                }

                // 6) Boot/System non-MS drivers
                if ((startRaw == 0 || startRaw == 1) && !isMicrosoft && isDriver)
                {
                    include = true;
                    flagged = true;
                    reasons.Add("boot/system driver from non-Microsoft binary");
                }

                if (!include)
                    continue;

                string flagReason = string.Join(", ", reasons);

                // --------- SEVERITY FOR SERVICES / DRIVERS ----------
                string severity = "LOW";
                if (flagged)
                {
                    // HIGH if missing binary OR boot/system non-MS driver
                    bool high = false;
                    foreach (var r in reasons)
                    {
                        if (r.Contains("missing on disk", StringComparison.OrdinalIgnoreCase) ||
                            r.Contains("boot/system driver", StringComparison.OrdinalIgnoreCase))
                        {
                            high = true;
                            break;
                        }
                    }

                    severity = high ? "HIGH" : "MEDIUM";
                }

                // --------- BUILD OUTPUT BLOCK ----------
                var sb = new StringBuilder();
                sb.AppendLine(isDriver
                    ? "[Sweep] Driver (deep)"
                    : "[Sweep] Service (deep)");
                sb.AppendLine($"  Name:      {serviceName}");
                sb.AppendLine($"  Display:   {displayName}");
                sb.AppendLine($"  StartType: {startLabel}");
                sb.AppendLine($"  Command:   {rawImage}");

                if (!string.IsNullOrWhiteSpace(rawImage) &&
                    !string.Equals(rawImage, expanded, StringComparison.OrdinalIgnoreCase))
                {
                    sb.AppendLine($"  Expanded:  {expanded}");
                }

                if (hasExe)
                {
                    string existsLabel = exists ? "(exists)" : "(MISSING)";
                    sb.AppendLine($"  Executable: {exePath} {existsLabel}");
                }
                else
                {
                    sb.AppendLine("  Executable: (could not parse from ImagePath)");
                }

                sb.AppendLine($"  Severity:  {severity}");

                if (severity != "LOW" && !string.IsNullOrEmpty(flagReason))
                    sb.AppendLine($">>> Reasons: {flagReason} <<<");

                _sweepEntries.Add(sb.ToString());
            }
        }
        catch
        {
            // Sweep is best-effort; if this fails, we just skip it.
        }
    }

#pragma warning restore CA1416
}

// ViperKit.UI - Services\PdfReportGenerator.cs
using System;
using System.IO;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using ViperKit.UI.Models;

namespace ViperKit.UI.Services
{
    /// <summary>
    /// Generates professional PDF reports for ViperKit cases.
    /// </summary>
    public static class PdfReportGenerator
    {
        static PdfReportGenerator()
        {
            // Set QuestPDF license (Community license for non-commercial or evaluation)
            QuestPDF.Settings.License = LicenseType.Community;
        }

        public static string GenerateReport(CaseReport report, string outputPath)
        {
            var document = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.Letter);
                    page.Margin(40);
                    page.DefaultTextStyle(x => x.FontSize(10).FontFamily("Arial"));

                    page.Header().Element(c => ComposeHeader(c, report));
                    page.Content().Element(c => ComposeContent(c, report));
                    page.Footer().Element(c => ComposeFooter(c, report));
                });
            });

            document.GeneratePdf(outputPath);
            return outputPath;
        }

        private static void ComposeHeader(IContainer container, CaseReport report)
        {
            container.Column(column =>
            {
                // Title bar
                column.Item().Background("#2A3F4F").Padding(12).Row(row =>
                {
                    row.RelativeItem().Column(col =>
                    {
                        col.Item().Text("VIPERKIT INCIDENT RESPONSE REPORT")
                            .FontSize(18)
                            .Bold()
                            .FontColor("#00FFF8");

                        col.Item().Text($"Case: {report.CaseName ?? report.CaseId}")
                            .FontSize(12)
                            .FontColor("#FFFFFF");
                    });

                    row.ConstantItem(120).Column(col =>
                    {
                        col.Item().AlignRight().Text($"Generated: {report.ReportGenerated:yyyy-MM-dd}")
                            .FontSize(9)
                            .FontColor("#CCCCCC");
                        col.Item().AlignRight().Text($"Page")
                            .FontSize(9)
                            .FontColor("#CCCCCC");
                    });
                });

                // Case summary box
                column.Item().PaddingVertical(10).Border(1).BorderColor("#CCCCCC").Padding(10).Row(row =>
                {
                    row.RelativeItem().Column(col =>
                    {
                        col.Item().Text("SYSTEM INFORMATION").FontSize(11).Bold();
                        col.Item().PaddingTop(5).Text($"Hostname: {report.HostName}").FontSize(9);
                        col.Item().Text($"OS: {report.OsDescription}").FontSize(9);
                        col.Item().Text($"User: {report.UserName}").FontSize(9);
                    });

                    row.RelativeItem().Column(col =>
                    {
                        col.Item().Text("INVESTIGATION DETAILS").FontSize(11).Bold();
                        col.Item().PaddingTop(5).Text($"Started: {report.CaseStarted:yyyy-MM-dd HH:mm}").FontSize(9);
                        col.Item().Text($"Duration: {(report.ReportGenerated - report.CaseStarted).TotalHours:F1} hours").FontSize(9);
                        if (!string.IsNullOrEmpty(report.InvestigatorName))
                            col.Item().Text($"Investigator: {report.InvestigatorName}").FontSize(9);
                    });
                });
            });
        }

        private static void ComposeContent(IContainer container, CaseReport report)
        {
            container.PaddingVertical(10).Column(column =>
            {
                // Executive Summary
                column.Item().Element(c => ComposeExecutiveSummary(c, report));

                // Focus Targets
                if (report.FocusTargets.Count > 0)
                    column.Item().PaddingTop(15).Element(c => ComposeFocusTargets(c, report));

                // Scans Performed
                if (report.ScansPerformed.Count > 0)
                    column.Item().PaddingTop(15).Element(c => ComposeScansPerformed(c, report));

                // Findings
                column.Item().PaddingTop(15).Element(c => ComposeFindings(c, report));

                // Actions Taken
                if (report.ActionsTaken.Count > 0)
                    column.Item().PaddingTop(15).Element(c => ComposeActionsTaken(c, report));

                // Hardening Applied
                if (report.HardeningActions.Count > 0)
                    column.Item().PaddingTop(15).Element(c => ComposeHardening(c, report));

                // Baseline Info
                if (report.Baseline != null)
                    column.Item().PaddingTop(15).Element(c => ComposeBaseline(c, report));

                // Timeline
                if (report.KeyEvents.Count > 0)
                    column.Item().PaddingTop(15).Element(c => ComposeTimeline(c, report));
            });
        }

        private static void ComposeExecutiveSummary(IContainer container, CaseReport report)
        {
            container.Column(column =>
            {
                column.Item().Text("EXECUTIVE SUMMARY").FontSize(14).Bold().FontColor("#2A3F4F");
                column.Item().PaddingTop(5).LineHorizontal(1).LineColor("#2A3F4F");

                column.Item().PaddingTop(10).Background("#F5F5F5").Padding(10).Column(col =>
                {
                    var findings = report.Findings;

                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Text("Total Persistence Entries").FontSize(9).FontColor("#666666");
                            c.Item().Text($"{findings.PersistenceTotal}").FontSize(16).Bold();
                        });

                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Text("High Risk (CHECK)").FontSize(9).FontColor("#AA0000");
                            c.Item().Text($"{findings.PersistenceCheck}").FontSize(16).Bold().FontColor("#AA0000");
                        });

                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Text("Medium Risk (NOTE)").FontSize(9).FontColor("#CC8800");
                            c.Item().Text($"{findings.PersistenceNote}").FontSize(16).Bold().FontColor("#CC8800");
                        });

                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Text("Sweep Findings").FontSize(9).FontColor("#666666");
                            c.Item().Text($"{findings.SweepTotal}").FontSize(16).Bold();
                        });
                    });

                    if (findings.PowerShellHighRisk > 0)
                    {
                        col.Item().PaddingTop(10).Border(1).BorderColor("#FF6666").Padding(8).Column(c =>
                        {
                            c.Item().Text("CRITICAL: High-risk PowerShell commands detected").FontSize(10).Bold().FontColor("#CC0000");
                            c.Item().Text($"{findings.PowerShellHighRisk} suspicious commands found in history").FontSize(9);
                        });
                    }
                });
            });
        }

        private static void ComposeFocusTargets(IContainer container, CaseReport report)
        {
            container.Column(column =>
            {
                column.Item().Text("INVESTIGATION FOCUS").FontSize(12).Bold().FontColor("#2A3F4F");
                column.Item().PaddingTop(5).LineHorizontal(1).LineColor("#CCCCCC");

                column.Item().PaddingTop(5).Text("The investigation focused on the following targets:")
                    .FontSize(9).Italic();

                foreach (var target in report.FocusTargets)
                {
                    column.Item().PaddingLeft(10).PaddingTop(3).Row(row =>
                    {
                        row.ConstantItem(15).Text("•");
                        row.RelativeItem().Text(target).FontSize(9).FontFamily("Consolas");
                    });
                }
            });
        }

        private static void ComposeScansPerformed(IContainer container, CaseReport report)
        {
            container.Column(column =>
            {
                column.Item().Text("SCANS PERFORMED").FontSize(12).Bold().FontColor("#2A3F4F");
                column.Item().PaddingTop(5).LineHorizontal(1).LineColor("#CCCCCC");

                column.Item().PaddingTop(10).Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.RelativeColumn(2);
                        columns.RelativeColumn(2);
                        columns.RelativeColumn(1);
                        columns.RelativeColumn(1);
                        columns.RelativeColumn(1);
                        columns.RelativeColumn(1);
                    });

                    // Header
                    table.Header(header =>
                    {
                        header.Cell().Background("#2A3F4F").Padding(5).Text("Scan Type").FontSize(9).Bold().FontColor("#FFFFFF");
                        header.Cell().Background("#2A3F4F").Padding(5).Text("Timestamp").FontSize(9).Bold().FontColor("#FFFFFF");
                        header.Cell().Background("#2A3F4F").Padding(5).Text("Total").FontSize(9).Bold().FontColor("#FFFFFF");
                        header.Cell().Background("#2A3F4F").Padding(5).Text("High").FontSize(9).Bold().FontColor("#FFFFFF");
                        header.Cell().Background("#2A3F4F").Padding(5).Text("Medium").FontSize(9).Bold().FontColor("#FFFFFF");
                        header.Cell().Background("#2A3F4F").Padding(5).Text("Low").FontSize(9).Bold().FontColor("#FFFFFF");
                    });

                    // Rows
                    foreach (var scan in report.ScansPerformed)
                    {
                        table.Cell().Border(1).BorderColor("#DDDDDD").Padding(5).Text(scan.ScanType).FontSize(8);
                        table.Cell().Border(1).BorderColor("#DDDDDD").Padding(5).Text(scan.Timestamp.ToString("yyyy-MM-dd HH:mm")).FontSize(8);
                        table.Cell().Border(1).BorderColor("#DDDDDD").Padding(5).Text(scan.TotalFindings.ToString()).FontSize(8);
                        table.Cell().Border(1).BorderColor("#DDDDDD").Padding(5).Text(scan.HighRiskFindings.ToString()).FontSize(8).FontColor("#AA0000");
                        table.Cell().Border(1).BorderColor("#DDDDDD").Padding(5).Text(scan.MediumRiskFindings.ToString()).FontSize(8).FontColor("#CC8800");
                        table.Cell().Border(1).BorderColor("#DDDDDD").Padding(5).Text(scan.LowRiskFindings.ToString()).FontSize(8);
                    }
                });
            });
        }

        private static void ComposeFindings(IContainer container, CaseReport report)
        {
            var findings = report.Findings;

            container.Column(column =>
            {
                column.Item().Text("KEY FINDINGS").FontSize(12).Bold().FontColor("#2A3F4F");
                column.Item().PaddingTop(5).LineHorizontal(1).LineColor("#CCCCCC");

                // Persistence findings
                if (findings.TopPersistenceFindings.Count > 0)
                {
                    column.Item().PaddingTop(10).Column(col =>
                    {
                        col.Item().Text("Top Persistence Entries (High Priority)").FontSize(10).Bold();

                        foreach (var finding in findings.TopPersistenceFindings)
                        {
                            col.Item().PaddingLeft(10).PaddingTop(3).Row(row =>
                            {
                                row.ConstantItem(15).Text("•").FontSize(8);
                                row.RelativeItem().Text(finding).FontSize(8).FontFamily("Consolas");
                            });
                        }
                    });
                }

                // PowerShell commands
                if (findings.TopPowerShellCommands.Count > 0)
                {
                    column.Item().PaddingTop(10).Column(col =>
                    {
                        col.Item().Text("Suspicious PowerShell Commands").FontSize(10).Bold().FontColor("#CC0000");

                        foreach (var cmd in findings.TopPowerShellCommands)
                        {
                            col.Item().PaddingLeft(10).PaddingTop(3).Background("#FFF5F5").Padding(5)
                                .Text(cmd).FontSize(8).FontFamily("Consolas");
                        }
                    });
                }

                // Sweep findings
                if (findings.TopSweepFindings.Count > 0)
                {
                    column.Item().PaddingTop(10).Column(col =>
                    {
                        col.Item().Text("Related Artifacts (Time-Clustered)").FontSize(10).Bold();

                        foreach (var finding in findings.TopSweepFindings)
                        {
                            col.Item().PaddingLeft(10).PaddingTop(3).Row(row =>
                            {
                                row.ConstantItem(15).Text("•").FontSize(8);
                                row.RelativeItem().Text(finding).FontSize(8).FontFamily("Consolas");
                            });
                        }
                    });
                }
            });
        }

        private static void ComposeActionsTaken(IContainer container, CaseReport report)
        {
            container.Column(column =>
            {
                column.Item().Text("REMEDIATION ACTIONS").FontSize(12).Bold().FontColor("#2A3F4F");
                column.Item().PaddingTop(5).LineHorizontal(1).LineColor("#CCCCCC");

                column.Item().PaddingTop(10).Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.RelativeColumn(2);
                        columns.RelativeColumn(1);
                        columns.RelativeColumn(3);
                        columns.RelativeColumn(1);
                    });

                    // Header
                    table.Header(header =>
                    {
                        header.Cell().Background("#2A3F4F").Padding(5).Text("Timestamp").FontSize(9).Bold().FontColor("#FFFFFF");
                        header.Cell().Background("#2A3F4F").Padding(5).Text("Action").FontSize(9).Bold().FontColor("#FFFFFF");
                        header.Cell().Background("#2A3F4F").Padding(5).Text("Target").FontSize(9).Bold().FontColor("#FFFFFF");
                        header.Cell().Background("#2A3F4F").Padding(5).Text("Result").FontSize(9).Bold().FontColor("#FFFFFF");
                    });

                    // Rows
                    foreach (var action in report.ActionsTaken)
                    {
                        table.Cell().Border(1).BorderColor("#DDDDDD").Padding(5).Text(action.Timestamp.ToString("HH:mm:ss")).FontSize(8);
                        table.Cell().Border(1).BorderColor("#DDDDDD").Padding(5).Text(action.ActionType).FontSize(8);
                        table.Cell().Border(1).BorderColor("#DDDDDD").Padding(5).Text(action.Target).FontSize(7).FontFamily("Consolas");
                        table.Cell().Border(1).BorderColor("#DDDDDD").Padding(5).Text(action.Result).FontSize(8)
                            .FontColor(action.Result.Contains("Success") ? "#00AA00" : "#AA0000");
                    }
                });
            });
        }

        private static void ComposeHardening(IContainer container, CaseReport report)
        {
            container.Column(column =>
            {
                column.Item().Text("HARDENING APPLIED").FontSize(12).Bold().FontColor("#2A3F4F");
                column.Item().PaddingTop(5).LineHorizontal(1).LineColor("#CCCCCC");

                column.Item().PaddingTop(5).Text($"{report.HardeningActions.Count} security hardening actions were applied to prevent reinfection.")
                    .FontSize(9).Italic();

                foreach (var action in report.HardeningActions)
                {
                    column.Item().PaddingTop(8).Background("#F0F8FF").Padding(8).Column(col =>
                    {
                        col.Item().Text(action.ActionName).FontSize(9).Bold();
                        col.Item().Text($"Category: {action.Category}").FontSize(8).FontColor("#666666");
                        col.Item().PaddingTop(3).Row(row =>
                        {
                            row.RelativeItem().Text($"Previous: {action.PreviousState}").FontSize(8).FontColor("#AA0000");
                            row.ConstantItem(20).Text("→").FontSize(8);
                            row.RelativeItem().Text($"New: {action.NewState}").FontSize(8).FontColor("#00AA00");
                        });
                        col.Item().PaddingTop(2).Text($"Applied: {action.AppliedAt:yyyy-MM-dd HH:mm}").FontSize(7).FontColor("#999999");
                    });
                }
            });
        }

        private static void ComposeBaseline(IContainer container, CaseReport report)
        {
            if (report.Baseline == null) return;

            container.Column(column =>
            {
                column.Item().Text("BASELINE CAPTURED").FontSize(12).Bold().FontColor("#2A3F4F");
                column.Item().PaddingTop(5).LineHorizontal(1).LineColor("#CCCCCC");

                column.Item().PaddingTop(10).Background("#F0FFF0").Padding(10).Column(col =>
                {
                    col.Item().Text("A system baseline was captured for post-incident monitoring.").FontSize(9);
                    col.Item().PaddingTop(5).Text($"Captured: {report.Baseline.CapturedAt:yyyy-MM-dd HH:mm:ss}").FontSize(9);
                    col.Item().Text($"Persistence entries: {report.Baseline.PersistenceEntriesCaptured}").FontSize(9);
                    col.Item().Text($"Hardening actions: {report.Baseline.HardeningActionsCaptured}").FontSize(9);
                    col.Item().PaddingTop(5).Text("Recommendation: Re-scan periodically and compare against baseline to detect reinfection.").FontSize(8).Italic();
                });
            });
        }

        private static void ComposeTimeline(IContainer container, CaseReport report)
        {
            container.Column(column =>
            {
                column.Item().Text("KEY TIMELINE EVENTS").FontSize(12).Bold().FontColor("#2A3F4F");
                column.Item().PaddingTop(5).LineHorizontal(1).LineColor("#CCCCCC");

                foreach (var evt in report.KeyEvents)
                {
                    column.Item().PaddingTop(5).Row(row =>
                    {
                        row.ConstantItem(80).Text(evt.Timestamp.ToString("HH:mm:ss")).FontSize(8).FontColor("#666666");

                        row.ConstantItem(20).Text(evt.Severity switch
                        {
                            "CRITICAL" => "!",
                            "WARNING" => "⚠",
                            _ => "•"
                        }).FontSize(10).FontColor(evt.Severity switch
                        {
                            "CRITICAL" => "#CC0000",
                            "WARNING" => "#CC8800",
                            _ => "#000000"
                        });

                        row.RelativeItem().Text(evt.Description).FontSize(8);
                    });
                }
            });
        }

        private static void ComposeFooter(IContainer container, CaseReport report)
        {
            container.AlignCenter().Row(row =>
            {
                row.RelativeItem().Column(column =>
                {
                    column.Item().AlignLeft().Text(text =>
                    {
                        text.Span("Generated by ViperKit ").FontSize(8).FontColor("#999999");
                        text.Span("v1.0").FontSize(8).FontColor("#999999");
                    });
                });

                row.RelativeItem().Column(column =>
                {
                    column.Item().AlignCenter().Text(text =>
                    {
                        text.CurrentPageNumber().FontSize(8);
                        text.Span(" / ").FontSize(8);
                        text.TotalPages().FontSize(8);
                    });
                });

                row.RelativeItem().Column(column =>
                {
                    column.Item().AlignRight().Text($"{report.ReportGenerated:yyyy-MM-dd HH:mm}")
                        .FontSize(8).FontColor("#999999");
                });
            });
        }
    }
}

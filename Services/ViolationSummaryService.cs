using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using AccessibilityChecker.Models;
using CsvHelper;

namespace AccessibilityChecker.Services
{
    public class ViolationSummaryService
    {
        public void GenerateHtmlReport(string inputCsvPath, string outputHtmlPath)
        {
            List<AccessibilityViolation> violations;
            using (var reader = new StreamReader(inputCsvPath))
            using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
            {
                violations = csv.GetRecords<AccessibilityViolation>().ToList();
            }

            var summaryByRule = violations
                .GroupBy(v => v.Rule)
                .Select(g => new ViolationSummary
                {
                    Rule = g.Key,
                    Count = g.Count(),
                    Impact = g.First().Impact,
                    ExampleUrl = g.First().Url,
                    ExampleDescription = g.First().Description
                })
                .OrderByDescending(x => x.Count)
                .ToList();

            var summaryByImpact = violations
                .GroupBy(v => v.Impact)
                .Select(g => new ImpactSummary
                {
                    Impact = g.Key,
                    Count = g.Count()
                })
                .OrderByDescending(x => x.Count)
                .ToList();

            var totalViolations = violations.Count;
            var totalPages = violations.Select(v => v.Url).Distinct().Count();

            var html = GenerateHtml(summaryByRule, summaryByImpact, totalViolations, totalPages);
            File.WriteAllText(outputHtmlPath, html);

            Console.WriteLine($"✅ HTML-rapport gemt som {outputHtmlPath}");
        }

        private string GenerateHtml(
            List<ViolationSummary> summaryByRule,
            List<ImpactSummary> summaryByImpact,
            int totalViolations,
            int totalPages)
        {
            var sb = new StringBuilder();
            sb.AppendLine("<!DOCTYPE html>");
            sb.AppendLine("<html lang='da'>");
            sb.AppendLine("<head>");
            sb.AppendLine("    <meta charset='UTF-8'>");
            sb.AppendLine("    <meta name='viewport' content='width=device-width, initial-scale=1.0'>");
            sb.AppendLine("    <title>WCAG Fejlrapport - Opsummering</title>");
            sb.AppendLine("    <style>");
            sb.AppendLine("        body { font-family: Arial, sans-serif; margin: 20px; color: #333; }");
            sb.AppendLine("        h1 { color: #2c3e50; }");
            sb.AppendLine("        h2 { color: #3498db; margin-top: 30px; }");
            sb.AppendLine("        .summary-card { background: #f8f9fa; border-radius: 8px; padding: 15px; margin: 15px 0; box-shadow: 0 2px 4px rgba(0,0,0,0.1); }");
            sb.AppendLine("        .impact-critical { background: #ffebee; border-left: 4px solid #e74c3c; }");
            sb.AppendLine("        .impact-serious { background: #fff3e0; border-left: 4px solid #f39c12; }");
            sb.AppendLine("        .impact-moderate { background: #e3f2fd; border-left: 4px solid #3498db; }");
            sb.AppendLine("        table { width: 100%; border-collapse: collapse; margin: 15px 0; }");
            sb.AppendLine("        th, td { padding: 12px; text-align: left; border-bottom: 1px solid #ddd; }");
            sb.AppendLine("        th { background: #3498db; color: white; }");
            sb.AppendLine("        tr:nth-child(even) { background: #f2f2f2; }");
            sb.AppendLine("        .total-box { background: #e8f4fc; padding: 15px; border-radius: 8px; margin: 20px 0; text-align: center; }");
            sb.AppendLine("        .total-number { font-size: 2em; font-weight: bold; color: #2c3e50; }");
            sb.AppendLine("        .chart-container { height: 300px; margin: 20px 0; }");
            sb.AppendLine("    </style>");
            sb.AppendLine("</head>");
            sb.AppendLine("<body>");
            sb.AppendLine("    <h1>WCAG Tilgængelighedsrapport - Opsummering</h1>");
            
            // Totaler
            sb.AppendLine("    <div class='summary-card'>");
            sb.AppendLine("        <h2>Overblik</h2>");
            sb.AppendLine("        <div style='display: flex; justify-content: space-around;'>");
            sb.AppendLine("            <div class='total-box'>");
            sb.AppendLine("                <div>Antal fejl i alt</div>");
            sb.AppendLine($
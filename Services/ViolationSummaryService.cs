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
        private readonly Dictionary<string, int> _impactOrder = new Dictionary<string, int>
        {
            { "critical", 0 },
            { "serious", 1 },
            { "moderate", 2 },
            { "minor", 3 }
        };

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
                .OrderBy(v => _impactOrder.GetValueOrDefault(v.Impact.ToLower(), 4))
                .ThenByDescending(x => x.Count)
                .ToList();

            var summaryByImpact = violations
                .GroupBy(v => v.Impact)
                .Select(g => new ImpactSummary
                {
                    Impact = g.Key,
                    Count = g.Count()
                })
                .OrderBy(i => _impactOrder.GetValueOrDefault(i.Impact.ToLower(), 4))
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
            sb.AppendLine("        .impact-minor { background: #f0f7f4; border-left: 4px solid #2ecc71; }");
            sb.AppendLine("        table { width: 100%; border-collapse: collapse; margin: 15px 0; }");
            sb.AppendLine("        th, td { padding: 12px; text-align: left; border-bottom: 1px solid #ddd; }");
            sb.AppendLine("        th { background: #3498db; color: white; }");
            sb.AppendLine("        tr:nth-child(even) { background: #f2f2f2; }");
            sb.AppendLine("        .total-box { background: #e8f4fc; padding: 15px; border-radius: 8px; margin: 20px 0; text-align: center; }");
            sb.AppendLine("        .total-number { font-size: 2em; font-weight: bold; color: #2c3e50; }");
            sb.AppendLine("        .progress-container { height: 30px; background: #ecf0f1; border-radius: 4px; margin: 10px 0; }");
            sb.AppendLine("        .progress-bar { height: 100%; border-radius: 4px; }");
            sb.AppendLine("        .progress-critical { background: #e74c3c; }");
            sb.AppendLine("        .progress-serious { background: #f39c12; }");
            sb.AppendLine("        .progress-moderate { background: #3498db; }");
            sb.AppendLine("        .progress-minor { background: #2ecc71; }");
            sb.AppendLine("        .impact-label { font-weight: bold; }");
            sb.AppendLine("    </style>");
            sb.AppendLine("</head>");
            sb.AppendLine("<body>");
            sb.AppendLine("    <h1>WCAG Tilgængelighedsrapport - Opsummering</h1>");
            
            // Overblik
            sb.AppendLine("    <div class='summary-card'>");
            sb.AppendLine("        <h2>Overblik</h2>");
            sb.AppendLine("        <div style='display: flex; justify-content: space-around;'>");
            sb.AppendLine("            <div class='total-box'>");
            sb.AppendLine("                <div>Antal fejl i alt</div>");
            sb.AppendLine($"                <div class='total-number'>{totalViolations}</div>");
            sb.AppendLine("            </div>");
            sb.AppendLine("            <div class='total-box'>");
            sb.AppendLine("                <div>Antal sider analyseret</div>");
            sb.AppendLine($"                <div class='total-number'>{totalPages}</div>");
            sb.AppendLine("            </div>");
            sb.AppendLine("        </div>");
            sb.AppendLine("    </div>");
            
            // Fordeling efter impact
            sb.AppendLine("    <div class='summary-card'>");
            sb.AppendLine("        <h2>Fejl fordelt på Impact</h2>");
            foreach (var impact in summaryByImpact)
            {
                var percentage = (double)impact.Count / totalViolations * 100;
                var className = impact.Impact.ToLower();
                sb.AppendLine($"        <div style='margin: 10px 0;'>");
                sb.AppendLine($"            <div class='impact-label'>{impact.Impact.ToUpper()}: {impact.Count} fejl</div>");
                sb.AppendLine($"            <div class='progress-container'>");
                sb.AppendLine($"                <div class='progress-bar progress-{className}' style='width: {percentage}%'></div>");
                sb.AppendLine($"            </div>");
                sb.AppendLine($"        </div>");
            }
            sb.AppendLine("    </div>");
            
            // Top fejltyper
            sb.AppendLine("    <div class='summary-card'>");
            sb.AppendLine("        <h2>Fejltyper</h2>");
            sb.AppendLine("        <table>");
            sb.AppendLine("            <tr><th>Regel</th><th>Antal</th><th>Impact</th><th>Eksempel URL</th><th>Beskrivelse</th></tr>");
            foreach (var item in summaryByRule)
            {
                sb.AppendLine("            <tr>");
                sb.AppendLine($"                <td>{item.Rule}</td>");
                sb.AppendLine($"                <td>{item.Count}</td>");
                sb.AppendLine($"                <td>{item.Impact}</td>");
                sb.AppendLine($"                <td><a href='{item.ExampleUrl}' target='_blank'>{item.ExampleUrl}</a></td>");
                sb.AppendLine($"                <td>{item.ExampleDescription}</td>");
                sb.AppendLine("            </tr>");
            }
            sb.AppendLine("        </table>");
            sb.AppendLine("    </div>");
            
            sb.AppendLine("    <div style='margin-top: 30px; text-align: center; color: #7f8c8d;'>");
            sb.AppendLine("        Rapport genereret: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            sb.AppendLine("    </div>");
            
            sb.AppendLine("</body>");
            sb.AppendLine("</html>");
            
            return sb.ToString();
        }
    }

    public class ViolationSummary
    {
        public string Rule { get; set; } = string.Empty;
        public int Count { get; set; }
        public string Impact { get; set; } = string.Empty;
        public string ExampleUrl { get; set; } = string.Empty;
        public string ExampleDescription { get; set; } = string.Empty;
    }

    public class ImpactSummary
    {
        public string Impact { get; set; } = string.Empty;
        public int Count { get; set; }
    }
}
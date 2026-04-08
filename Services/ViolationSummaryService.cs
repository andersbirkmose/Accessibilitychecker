using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using AccessibilityChecker.Models;
using CsvHelper;

namespace AccessibilityChecker.Services
{
    public class ViolationSummaryService
    {
        public void GenerateSummary(string inputCsvPath, string outputCsvPath)
        {
            List<AccessibilityViolation> violations;
            using (var reader = new StreamReader(inputCsvPath))
            using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
            {
                violations = csv.GetRecords<AccessibilityViolation>().ToList();
            }

            var summary = violations
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

            using (var writer = new StreamWriter(outputCsvPath))
            using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
            {
                csv.WriteRecords(summary);
            }

            Console.WriteLine($"✅ Opsummering gemt i {outputCsvPath}");
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
}
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AccessibilityChecker.Models;
using AccessibilityChecker.Utils;
using Microsoft.Extensions.Options;
using Microsoft.Playwright;

namespace AccessibilityChecker.Services
{
    public class CrawlerService
    {
        private readonly AppSettings _settings;
        private readonly AccessibilityAnalyzer _analyzer;
        private readonly DeadLinkCheckerService _deadLinkChecker;

        public List<AccessibilityViolation> AllViolations { get; } = new();
        public List<SkippedPage> SkippedPages { get; } = new();
        public List<DeadLink> AllDeadLinks { get; } = new();

        public CrawlerService(
            IOptions<AppSettings> settings,
            AccessibilityAnalyzer analyzer,
            DeadLinkCheckerService deadLinkChecker)
        {
            _settings = settings.Value;
            _analyzer = analyzer;
            _deadLinkChecker = deadLinkChecker;
        }

        public async Task CrawlAsync(string url, IBrowser browser, HashSet<string> visited, int depth = 0)
        {
            var normalizedUrl = UrlHelper.NormalizeUrl(url);

            if (depth > _settings.MaxDepth 
                || visited.Contains(normalizedUrl) 
                || IsExcluded(normalizedUrl) 
                || ExcludeFiles.IsFileUrl(normalizedUrl))
                return;

            if (visited.Count >= _settings.MaxPages)
                return;

            visited.Add(normalizedUrl);

            try
            {
                Console.WriteLine("\n[" + DateTime.Now.ToString("T") + "] - Analyserer: " + normalizedUrl);

                await using var context = await browser.NewContextAsync();
                var page = await context.NewPageAsync();

                // Check for dead links on this page
                var deadLinks = await _deadLinkChecker.CheckPageForDeadLinksAsync(normalizedUrl, page);
                AllDeadLinks.AddRange(deadLinks);
                if (deadLinks.Any())
                {
                    Console.WriteLine("   [DEAD LINKS] Fundet " + deadLinks.Count + " dode links pa " + normalizedUrl);
                }

                var (violations, skipReason) = await _analyzer.AnalyzeAsync(normalizedUrl, browser);

                if (!string.IsNullOrEmpty(skipReason))
                {
                    SkippedPages.Add(new SkippedPage
                    {
                        Url = normalizedUrl,
                        Reason = skipReason
                    });
                }
                else
                {
                    AllViolations.AddRange(violations);
                }

                await page.GotoAsync(normalizedUrl);

                var hrefs = await page.EvaluateAsync<string[]>(@"Array.from(document.querySelectorAll('a')).map(a => a.href)");

                foreach (var link in hrefs)
                {
                    var normalizedLink = UrlHelper.NormalizeUrl(link);
                    if (normalizedLink.StartsWith(_settings.TargetDomain)
                        && !ExcludeFiles.IsFileUrl(normalizedLink))
                    {
                        await CrawlAsync(normalizedLink, browser, visited, depth + 1);
                    }
                }

            }
            catch (Exception ex)
            {
                Console.WriteLine("[FEJL] Ved " + normalizedUrl + ": " + ex.Message);
            }
        }

        private bool IsExcluded(string url)
        {
            return _settings.ExcludedPaths.Any(p => url.Contains(p, StringComparison.OrdinalIgnoreCase));
        }
    }
}
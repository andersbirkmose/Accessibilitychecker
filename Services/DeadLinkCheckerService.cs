using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using AccessibilityChecker.Models;
using Microsoft.Playwright;

namespace AccessibilityChecker.Services
{
    public class DeadLinkCheckerService
    {
        private readonly HttpClient _httpClient;
        private readonly int _timeoutMs = 10000; // 10 sekunders timeout
        private readonly HashSet<string> _checkedLinks = new();

        public DeadLinkCheckerService()
        {
            _httpClient = new HttpClient();
            _httpClient.Timeout = TimeSpan.FromMilliseconds(_timeoutMs);
        }

        public async Task<List<DeadLink>> CheckPageForDeadLinksAsync(string pageUrl, IPage page)
        {
            var deadLinks = new List<DeadLink>();

            try
            {
                // Get all anchor elements on the page
                var links = await page.EvaluateAsync<LinkData[]>("() => {
                    return Array.from(document.querySelectorAll('a[href]')).map(a => ({
                        href: a.href,
                        text: a.innerText.trim().substring(0, 100)
                    }));
                }");

                // Filter out empty, anchor, and javascript links
                var validLinks = links
                    .Where(l => !string.IsNullOrEmpty(l.Href))
                    .Where(l => !l.Href.StartsWith("#"))
                    .Where(l => !l.Href.StartsWith("javascript:"))
                    .Where(l => !l.Href.StartsWith("mailto:"))
                    .Where(l => !l.Href.StartsWith("tel:"))
                    .Select(l => new { l.Href, l.Text })
                    .ToList();

                // Check each link only if not already checked
                foreach (var link in validLinks)
                {
                    var normalizedUrl = NormalizeUrl(link.Href);
                    if (!_checkedLinks.Contains(normalizedUrl))
                    {
                        _checkedLinks.Add(normalizedUrl);
                        var deadLink = await CheckLinkAsync(pageUrl, link.Href, link.Text);
                        if (deadLink != null)
                        {
                            deadLinks.Add(deadLink);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($
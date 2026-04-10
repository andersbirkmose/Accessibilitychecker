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
        private readonly int _timeoutMs = 10000;
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
                var links = await page.EvaluateAsync<LinkData[]>(
                    "() => Array.from(document.querySelectorAll('a[href]')).map(a => ({ href: a.href, text: a.innerText.trim().substring(0, 100) }))");

                Console.WriteLine("[DEBUG] Found " + links.Length + " links on page: " + pageUrl);

                var validLinks = links
                    .Where(l => !string.IsNullOrEmpty(l.Href))
                    .Where(l => !l.Href.StartsWith("#"))
                    .Where(l => !l.Href.StartsWith("javascript:"))
                    .Where(l => !l.Href.StartsWith("mailto:"))
                    .Where(l => !l.Href.StartsWith("tel:"))
                    .Select(l => new { l.Href, l.Text })
                    .ToList();

                Console.WriteLine("[DEBUG] " + validLinks.Count + " valid links to check after filtering");

                foreach (var link in validLinks)
                {
                    var normalizedUrl = NormalizeUrl(link.Href);
                    if (!_checkedLinks.Contains(normalizedUrl))
                    {
                        _checkedLinks.Add(normalizedUrl);
                        Console.WriteLine("[DEBUG] Checking link: " + normalizedUrl);
                        var deadLink = await CheckLinkAsync(pageUrl, link.Href, link.Text);
                        if (deadLink != null)
                        {
                            deadLinks.Add(deadLink);
                            Console.WriteLine("[DEBUG] Found dead link: " + deadLink.LinkUrl + " (" + deadLink.StatusCode + ": " + deadLink.Reason + ")");
                        }
                    }
                    else
                    {
                        Console.WriteLine("[DEBUG] Skipping already checked link: " + normalizedUrl);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("[FEJL] ved tjek af dode links pa " + pageUrl + ": " + ex.Message);
            }

            return deadLinks;
        }

        private string NormalizeUrl(string url)
        {
            if (string.IsNullOrEmpty(url))
                return url;

            try
            {
                // Handle URLs that might be relative or have fragments
                if (url.StartsWith("#"))
                    return url;

                var uri = new Uri(url, UriKind.RelativeOrAbsolute);
                if (!uri.IsAbsoluteUri)
                    return url.ToLower(); // For relative URLs, just use as-is for now

                // For absolute URLs, remove fragment and normalize to lowercase
                return new UriBuilder(uri) { Fragment = string.Empty }.Uri.ToString().ToLower();
            }
            catch
            {
                return url.ToLower();
            }
        }

        private async Task<DeadLink?> CheckLinkAsync(string pageUrl, string linkUrl, string linkText)
        {
            try
            {
                if (linkUrl.StartsWith("#"))
                    return null;

                if (linkUrl.StartsWith("mailto:") || linkUrl.StartsWith("tel:") || linkUrl.StartsWith("javascript:"))
                    return null;

                var uri = new Uri(linkUrl, UriKind.RelativeOrAbsolute);
                if (!uri.IsAbsoluteUri)
                {
                    var baseUri = new Uri(pageUrl);
                    uri = new Uri(baseUri, uri);
                    linkUrl = uri.ToString();
                }

                // Configure request to follow redirects
                var request = new HttpRequestMessage(HttpMethod.Head, uri);
                var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);

                // If HEAD is not allowed, try GET
                if (response.StatusCode == System.Net.HttpStatusCode.MethodNotAllowed)
                {
                    request = new HttpRequestMessage(HttpMethod.Get, uri);
                    response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
                }

                Console.WriteLine("[DEBUG] Link " + linkUrl + " status: " + (int)response.StatusCode);

                if ((int)response.StatusCode >= 400)
                {
                    return new DeadLink
                    {
                        PageUrl = pageUrl,
                        LinkUrl = linkUrl,
                        LinkText = string.IsNullOrEmpty(linkText) ? linkUrl : linkText,
                        StatusCode = (int)response.StatusCode,
                        Reason = response.ReasonPhrase ?? "Unknown error"
                    };
                }
            }
            catch (HttpRequestException ex)
            {
                Console.WriteLine("[DEBUG] HttpRequestException for " + linkUrl + ": " + ex.Message);
                return new DeadLink
                {
                    PageUrl = pageUrl,
                    LinkUrl = linkUrl,
                    LinkText = string.IsNullOrEmpty(linkText) ? linkUrl : linkText,
                    StatusCode = 0,
                    Reason = ex.Message
                };
            }
            catch (UriFormatException)
            {
                Console.WriteLine("[DEBUG] Invalid URL format: " + linkUrl);
                return null;
            }
            catch (TaskCanceledException)
            {
                Console.WriteLine("[DEBUG] Request timed out for: " + linkUrl);
                return new DeadLink
                {
                    PageUrl = pageUrl,
                    LinkUrl = linkUrl,
                    LinkText = string.IsNullOrEmpty(linkText) ? linkUrl : linkText,
                    StatusCode = 0,
                    Reason = "Request timed out"
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine("[DEBUG] General exception for " + linkUrl + ": " + ex.Message);
                return null;
            }

            return null;
        }

        internal class LinkData
        {
            public string Href { get; set; } = string.Empty;
            public string Text { get; set; } = string.Empty;
        }
    }
}
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using AccessibilityChecker.Models;
using Microsoft.Playwright;

namespace AccessibilityChecker.Services;

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
            Console.WriteLine($"⚠️ Fejl ved tjek af døde links på {pageUrl}: {ex.Message}");
        }

        return deadLinks;
    }

    private string NormalizeUrl(string url)
    {
        if (string.IsNullOrEmpty(url))
            return url;
        
        // Remove fragment
        var uri = new Uri(url, UriKind.RelativeOrAbsolute);
        if (!uri.IsAbsoluteUri)
            return url;
            
        return new UriBuilder(uri) { Fragment = string.Empty }.Uri.ToString();
    }

    private async Task<DeadLink?> CheckLinkAsync(string pageUrl, string linkUrl, string linkText)
    {
        try
        {
            // Skip if it's a same-page anchor
            if (linkUrl.StartsWith("#"))
                return null;

            // Skip mailto, tel, etc.
            if (linkUrl.StartsWith("mailto:") || linkUrl.StartsWith("tel:") || linkUrl.StartsWith("javascript:"))
                return null;

            // Use HttpClient for faster checks
            var uri = new Uri(linkUrl, UriKind.RelativeOrAbsolute);
            if (!uri.IsAbsoluteUri)
            {
                // Resolve relative URL
                var baseUri = new Uri(pageUrl);
                uri = new Uri(baseUri, uri);
            }

            var response = await _httpClient.SendAsync(new HttpRequestMessage(HttpMethod.Head, uri), HttpCompletionOption.ResponseHeadersRead);
            
            // If HEAD is not allowed, try GET
            if (response.StatusCode == System.Net.HttpStatusCode.MethodNotAllowed)
            {
                response = await _httpClient.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead);
            }

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
            // Invalid URL format, skip
            return null;
        }
        catch (TaskCanceledException)
        {
            // Timeout
            return new DeadLink
            {
                PageUrl = pageUrl,
                LinkUrl = linkUrl,
                LinkText = string.IsNullOrEmpty(linkText) ? linkUrl : linkText,
                StatusCode = 0,
                Reason = "Request timed out"
            };
        }
        catch (Exception)
        {
            // Other errors, skip
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
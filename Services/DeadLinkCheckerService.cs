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

                var validLinks = links
                    .Where(l => !string.IsNullOrEmpty(l.Href))
                    .Where(l => !l.Href.StartsWith("#"))
                    .Where(l => !l.Href.StartsWith("javascript:"))
                    .Where(l => !l.Href.StartsWith("mailto:"))
                    .Where(l => !l.Href.StartsWith("tel:"))
                    .Select(l => new { l.Href, l.Text })
                    .ToList();

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
                Console.WriteLine("FEJL ved tjek af dode links pa " + pageUrl + ": " + ex.Message);
            }

            return deadLinks;
        }

        private string NormalizeUrl(string url)
        {
            if (string.IsNullOrEmpty(url))
                return url;

            try
            {
                var uri = new Uri(url, UriKind.RelativeOrAbsolute);
                if (!uri.IsAbsoluteUri)
                    return url;

                return new UriBuilder(uri) { Fragment = string.Empty }.Uri.ToString();
            }
            catch
            {
                return url;
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
                }

                var response = await _httpClient.SendAsync(
                    new HttpRequestMessage(HttpMethod.Head, uri),
                    HttpCompletionOption.ResponseHeadersRead);

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
                return null;
            }
            catch (TaskCanceledException)
            {
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
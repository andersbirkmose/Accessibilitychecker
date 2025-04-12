using System.Xml.Linq;

namespace AccessibilityChecker.Utils;

public static class SitemapHelper
{
    public static async Task<List<string>> GetUrlsFromSitemapAsync(string sitemapUrl)
    {
        var urls = new List<string>();
        using var httpClient = new HttpClient();
        var xml = await httpClient.GetStringAsync(sitemapUrl);

        var doc = XDocument.Parse(xml);
        XNamespace ns = doc.Root?.Name.Namespace ?? "";

        urls = doc.Descendants(ns + "url")
          .Select(e => e.Element(ns + "loc")?.Value)
          .Where(u => !string.IsNullOrEmpty(u))
          .Select(u => u!) // 'u!' fortæller kompileren, at vi ved det ikke er null
          .Distinct()
          .ToList();


        return urls!;
    }
}

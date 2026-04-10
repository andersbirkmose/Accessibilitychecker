using System.Globalization;
using AccessibilityChecker.Models;
using AccessibilityChecker.Services;
using AccessibilityChecker.Utils;
using CsvHelper;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.Playwright;


var builder = Host.CreateApplicationBuilder(args);

// Tilføj secrets.json efter appsettings.json (hvis den findes)
builder.Configuration.AddJsonFile("secrets.json", optional: true, reloadOnChange: true);

// Konfiguration og services
builder.Services.Configure<AppSettings>(builder.Configuration);

builder.Services.Configure<EmailSettings>(builder.Configuration.GetSection("Email"));
builder.Services.AddSingleton<AccessibilityAnalyzer>();
builder.Services.AddSingleton<CrawlerService>();
builder.Services.AddSingleton<EmailService>();
builder.Services.AddSingleton<ViolationSummaryService>();
builder.Services.AddSingleton<DeadLinkCheckerService>();

var host = builder.Build();
var settings = host.Services.GetRequiredService<IOptions<AppSettings>>().Value;
var crawler = host.Services.GetRequiredService<CrawlerService>();
var summaryService = host.Services.GetRequiredService<ViolationSummaryService>();

// Start Playwright
using var playwright = await Playwright.CreateAsync();
await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
{
    Headless = true
});

// Bestem hvilke sider der skal crawles
List<string> urlsToScan;

if (settings.UseSitemap)
{
    var sitemapUrl = !string.IsNullOrEmpty(settings.SitemapUrl)
        ? settings.SitemapUrl
        : settings.TargetDomain.TrimEnd('/') + "/sitemap.xml";
    
    Console.WriteLine("[SITEMAP] Henter URLs fra sitemap: " + sitemapUrl);

    try
    {
        urlsToScan = await SitemapHelper.GetUrlsFromSitemapAsync(sitemapUrl);
        Console.WriteLine("[SITEMAP] Fundet " + urlsToScan.Count + " links i sitemap\n");
    }
    catch (Exception ex)
    {
        Console.WriteLine("[FEJL] Kunne ikke hente sitemap: " + ex.Message);
        return;
    }
}
else
{
    urlsToScan = new List<string> { settings.TargetDomain };
}

var visited = new HashSet<string>();

// Crawl alle sider
foreach (var url in urlsToScan)
{
    await crawler.CrawlAsync(url, browser, visited, 0);
}

// Forbered filnavne
var domainName = new Uri(settings.TargetDomain).Host;
var dateStamp = DateTime.Now.ToString("yyyy-MM-dd");
var violationsCsvFile = "violations-" + domainName + "-" + dateStamp + ".csv";
var skippedCsvFile = "skipped-" + domainName + "-" + dateStamp + ".csv";
var deadLinksCsvFile = "deadlinks-" + domainName + "-" + dateStamp + ".csv";
var summaryHtmlFile = "summary-" + domainName + "-" + dateStamp + ".html";
var attachments = new List<string>();

// Eksportér violations
var violations = crawler.AllViolations;

if (violations.Any())
{
    using (var writer = new StreamWriter(violationsCsvFile))
    using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
    {
        csv.WriteRecords(violations);
        Console.WriteLine("\n[OK] " + violations.Count + " WCAG-fejl gemt i " + violationsCsvFile);
    }

    attachments.Add(violationsCsvFile);

    // Generer HTML-rapport
    summaryService.GenerateHtmlReport(violationsCsvFile, summaryHtmlFile);
    attachments.Add(summaryHtmlFile);
}
else
{
    Console.WriteLine("\n[OK] Ingen WCAG-fejl fundet.");
}

// Eksportér døde links
var deadLinks = crawler.AllDeadLinks;
if (deadLinks.Any())
{
    using (var writer = new StreamWriter(deadLinksCsvFile))
    using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
    {
        csv.WriteRecords(deadLinks);
        Console.WriteLine("[OK] " + deadLinks.Count + " dode links gemt i " + deadLinksCsvFile);
    }

    attachments.Add(deadLinksCsvFile);
}
else
{
    Console.WriteLine("[OK] Ingen dode links fundet.");
}

// Eksportér skipped pages
if (crawler.SkippedPages.Any())
{
    using (var writer = new StreamWriter(skippedCsvFile))
    using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
    {
        csv.WriteRecords(crawler.SkippedPages);
        Console.WriteLine("[OK] " + crawler.SkippedPages.Count + " sider blev sprunget over og gemt i " + skippedCsvFile);
    }

    attachments.Add(skippedCsvFile);
}

// Send e-mail hvis der er noget at rapportere
if (attachments.Any() || deadLinks.Any() || crawler.SkippedPages.Any())
{
    var emailService = host.Services.GetRequiredService<EmailService>();
    
    // Make sure deadLinksCsvFile is in attachments if there are dead links
    if (deadLinks.Any() && !attachments.Contains(deadLinksCsvFile))
    {
        attachments.Add(deadLinksCsvFile);
    }
    
    await emailService.SendReportAsync(
        filePaths: attachments,
        subject: "WCAG-rapport - " + domainName,
        body: "Hej,\n\n" +
              "Rapport for domæne: " + domainName + "\n" +
              "Antal sider analyseret: " + visited.Count + "\n" +
              "Antal tilgængelighedsfejl fundet: " + violations.Count + "\n" +
              "Antal dode links fundet: " + deadLinks.Count + "\n" +
              "Antal sider sprunget over: " + crawler.SkippedPages.Count + "\n\n" +
              "Vedhæftede filer:\n" +
              (attachments.Contains(violationsCsvFile) ? "- " + Path.GetFileName(violationsCsvFile) + " (alle WCAG-fejl)\n" : "") +
              (attachments.Contains(summaryHtmlFile) ? "- " + Path.GetFileName(summaryHtmlFile) + " (HTML-opsummering)\n" : "") +
              (attachments.Contains(deadLinksCsvFile) ? "- " + Path.GetFileName(deadLinksCsvFile) + " (dode links)\n" : "") +
              (attachments.Contains(skippedCsvFile) ? "- " + Path.GetFileName(skippedCsvFile) + " (oversprungne sider)\n" : "") +
              "\n" +
              "Mvh,\n" +
              "Din automatiske WCAG-scanner");
}
else
{
    Console.WriteLine("[MAIL] Ingen filer genereret - e-mail springes over.");
}

Console.WriteLine("[DONE] Crawling afsluttet.");
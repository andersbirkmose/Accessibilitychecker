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

var host = builder.Build();
var settings = host.Services.GetRequiredService<IOptions<AppSettings>>().Value;
var crawler = host.Services.GetRequiredService<CrawlerService>();

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
    var sitemapUrl = $"{settings.TargetDomain.TrimEnd('/')}/sitemap.xml";
    Console.WriteLine($
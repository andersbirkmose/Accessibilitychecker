using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using AccessibilityChecker.Models;
using Microsoft.Extensions.Options;
using Microsoft.Playwright;

namespace AccessibilityChecker.Services;

public class AccessibilityAnalyzer
{
    private readonly string _axeScript;
    private readonly int _waitAfterLoadMs;

    public AccessibilityAnalyzer(IOptions<AppSettings> options)
    {
        _axeScript = File.ReadAllText("Resources/axe.min.js");
        _waitAfterLoadMs = options.Value.WaitAfterLoadMs;
    }

    public async Task<(List<AccessibilityViolation> Violations, string? SkipReason)> AnalyzeAsync(string url, IBrowser browser)
    {
        var violationsList = new List<AccessibilityViolation>();

        await using var context = await browser.NewContextAsync();
        var page = await context.NewPageAsync();

        page.Console += (_, msg) =>
        {
            if (msg.Type != "error") return;

            var text = msg.Text?.Trim() ?? "";
            if (text.Contains("%c%d") && text.Contains("NaN")) return;
            if (text.Contains("401 (Unauthorized)")) return;

            Console.WriteLine($"[JS ERROR] {text}");
        };

        try
        {
            await page.GotoAsync(url);
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

            if (_waitAfterLoadMs > 0)
            {
                Console.WriteLine($"⏱ Venter {_waitAfterLoadMs} ms for at sikre indholdet er loaded...");
                await page.WaitForTimeoutAsync(_waitAfterLoadMs);
            }

            var bodyContent = await page.InnerHTMLAsync("body");
            if (string.IsNullOrWhiteSpace(bodyContent) || bodyContent.Length < 100)
            {
                Console.WriteLine($"⚠️ DOM'en virker tom eller næsten tom på {url} – analyse springes over.");
                return (violationsList, "Tomme eller ubrugelige DOM-data");
            }

            await page.EvaluateAsync(_axeScript);

            var axeExists = await page.EvaluateAsync<bool>("() => typeof axe !== 'undefined'");
            if (!axeExists)
            {
                Console.WriteLine($"❌ axe.js blev ikke korrekt indlæst på {url}");
                return (violationsList, "axe.js blev ikke korrekt indlæst");
            }

            var result = await page.EvaluateAsync<JsonElement>("async () => await axe.run()");
            var violations = result.GetProperty("violations");

            if (violations.GetArrayLength() == 0)
            {
                Console.WriteLine($"✅ Ingen fejl på {url}");
            }
            else
            {
                Console.WriteLine($"❌ {violations.GetArrayLength()} fejl fundet på {url}");

                foreach (var violation in violations.EnumerateArray())
                {
                    violationsList.Add(new AccessibilityViolation
                    {
                        Url = url,
                        Rule = violation.GetProperty("id").GetString() ?? "",
                        Description = violation.GetProperty("help").GetString() ?? "",
                        Impact = violation.GetProperty("impact").GetString() ?? "",
                        AffectedNodes = violation.GetProperty("nodes").GetArrayLength(),
                    HelpUrl = violation.GetProperty("helpUrl").GetString() ?? ""
                    });
                }
            }

            return (violationsList, null);
        }
        catch (PlaywrightException ex)
        {
            Console.WriteLine($"⚠️ Playwright-fejl på {url}: {ex.Message}");
            return (violationsList, $"Fejl under analyse: {ex.Message}");
        }
    }
}

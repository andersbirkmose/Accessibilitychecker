# Accessibility Checker

Et .NET 8-baseret konsolprogram, der crawler et website og udfører automatisk WCAG 2.1 AA-analyse ved hjælp af axe-core og Playwright.

## Funktioner

- Automatisk crawling af hele websitet (eller sitemap-baseret)
- Analyse med axe-core i rigtig browser (Chromium via Playwright)
- CSV-rapporter over tilgængelighedsfejl og sider der ikke kunne analyseres
- Automatisk e-mail med vedhæftede rapporter
- Asynkron og robust arkitektur
- Konfigurerbar via `appsettings.json`

## Kom i gang

### 1. Installer Playwright-browser:

```bash
dotnet tool restore
dotnet playwright install

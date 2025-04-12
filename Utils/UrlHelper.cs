namespace AccessibilityChecker.Utils;

public static class UrlHelper
{
    public static string NormalizeUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return url;

        // Fjern trailing slash og hash
        url = url.TrimEnd('/', '#');

        return url;
    }
}

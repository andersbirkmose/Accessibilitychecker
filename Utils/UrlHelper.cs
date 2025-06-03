// Utils/UrlHelper.cs
using System;

namespace AccessibilityChecker.Utils
{
    public static class UrlHelper
    {
        public static string NormalizeUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return url;

            // Fjern fragment (#) hvis den findes
            int hashIndex = url.IndexOf('#');
            if (hashIndex >= 0)
                url = url.Substring(0, hashIndex);

            // Fjern query-parametre (inkl. evt. trailing '?')
            int queryIndex = url.IndexOf('?');
            if (queryIndex >= 0)
                url = url.Substring(0, queryIndex);

            // Fjern trailing slash (medmindre det kun er '/')
            if (url.Length > 1 && url.EndsWith("/"))
                url = url.TrimEnd('/');

            return url;
        }
    }
}


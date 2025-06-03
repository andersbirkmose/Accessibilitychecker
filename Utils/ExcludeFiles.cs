// Utils/ExcludeFiles.cs
using System;
using System.Linq;

namespace AccessibilityChecker.Utils
{
    public static class ExcludeFiles
    {
        private static readonly string[] FileExtensions =
        {
            ".pdf", ".jpg", ".jpeg", ".png", ".gif", ".svg", ".zip", ".rar", ".doc", ".docx",
            ".xls", ".xlsx", ".ppt", ".pptx", ".mp3", ".mp4", ".avi", ".mov", ".wmv", ".webm",
            ".ogg", ".ico", ".txt", ".csv", ".xml", ".json", ".js", ".css", ".woff", ".woff2",
            ".eot", ".ttf", ".otf", ".bmp", ".rtf", ".tar", ".gz", ".7z", ".ics", ".webp"
        };

        public static bool IsFileUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return false;

            try
            {
                var uri = new Uri(url);
                var path = uri.AbsolutePath.ToLowerInvariant();
                return FileExtensions.Any(ext => path.EndsWith(ext));
            }
            catch
            {
                // Hvis ikke en valid URL, antag at det ikke er en fil
                return false;
            }
        }
    }
}

namespace AccessibilityChecker.Models;

public class DeadLink
{
    public string PageUrl { get; set; } = string.Empty;
    public string LinkUrl { get; set; } = string.Empty;
    public string LinkText { get; set; } = string.Empty;
    public int StatusCode { get; set; }
    public string Reason { get; set; } = string.Empty;
}
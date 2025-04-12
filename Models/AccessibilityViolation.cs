namespace AccessibilityChecker.Models;

public class AccessibilityViolation
{
    public string Url { get; set; } = string.Empty;
    public string Rule { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Impact { get; set; } = string.Empty;
    public int AffectedNodes { get; set; }
}

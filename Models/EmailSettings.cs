namespace AccessibilityChecker.Models;

public class EmailSettings
{
    public string To { get; set; } = string.Empty;
    public string From { get; set; } = string.Empty;
    public string SmtpServer { get; set; } = "smtp.gmail.com";
    public int Port { get; set; } = 587;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

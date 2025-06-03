public class AppSettings
{
    public string TargetDomain { get; set; } = string.Empty;
    public List<string> ExcludedPaths { get; set; } = new();
    public int MaxDepth { get; set; } = 2;
    public bool UseSitemap { get; set; } = false;
    public int MaxPages { get; set; } 
    public int WaitAfterLoadMs { get; set; } = 0; // default: ingen ekstra ventetid


}

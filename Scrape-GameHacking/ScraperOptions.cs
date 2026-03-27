namespace GameHackingScraper
{
    internal sealed class ScraperOptions
    {
        public string OutDir      { get; init; } = Directory.GetCurrentDirectory();
        public string? System     { get; init; }
        public double Delay       { get; init; } = 1.0;
        public bool   DryRun      { get; init; }
        public bool   NoVerifySsl { get; init; }
        public int    Retries     { get; init; } = 3;
        public bool   Verbose     { get; init; }
        public string? Proxy      { get; init; }
        public bool   DumpHtml    { get; init; }
    }
}

using GameHackingScraper;

/*
 * Scrape-GameHacking (C# port)
 * ============================
 * Scrapes https://gamehacking.org/search for every game and its cheat codes,
 * then writes the CheatArchiver folder structure:
 *
 *   consoles/<Console>/<Region>/<Game Title>/cheats.yaml
 *
 * Uses RainbowSix.WebClient (https://github.com/TylerDev888/WebClient) for
 * all HTTP requests.
 *
 * Usage
 * -----
 *   Scrape-GameHacking [--out-dir /path/to/CheatArchiver]
 *                      [--system <system_id_or_name>]
 *                      [--delay 1.0]
 *                      [--dry-run]
 *                      [--no-verify-ssl]
 *                      [--retries 3]
 *                      [--verbose]
 *                      [--proxy <proxy_url>]
 *                      [--dump-html]
 *
 * Options
 * -------
 * --out-dir        Root of the CheatArchiver repo.  Defaults to the directory
 *                  that contains the executable.
 * --system         Filter to a single system by numeric ID or partial name.
 * --delay          Seconds to wait between HTTP requests (default 1.0).
 *                  A random jitter of +/- 30% is applied to avoid detection.
 * --dry-run        Print what would be written without creating any files.
 * --no-verify-ssl  Disable SSL certificate verification.
 * --retries        Number of times to retry a failed HTTP request (default 3).
 * --verbose        Enable verbose debug output for HTTP requests.
 * --proxy          HTTP/HTTPS proxy URL (e.g., http://127.0.0.1:8080).
 * --dump-html      Dump HTML pages to files for debugging.
 */

var opts = ParseArgs(args);
using var scraper = new GameHackingScraperEngine(opts);
await scraper.RunAsync();
return;

// ---------------------------------------------------------------------------
// Argument parsing (mirrors Python argparse)
// ---------------------------------------------------------------------------
static ScraperOptions ParseArgs(string[] args)
{
    // Default out-dir is the directory containing this executable's assembly
    var defaultOutDir = AppContext.BaseDirectory;

    string? outDir      = null;
    string? system      = null;
    double  delay       = 1.0;
    bool    dryRun      = false;
    bool    noVerifySsl = false;
    int     retries     = 3;
    bool    verbose     = false;
    string? proxy       = null;
    bool    dumpHtml    = false;

    for (var i = 0; i < args.Length; i++)
    {
        switch (args[i])
        {
            case "--out-dir":
                outDir = NextArg(args, ref i, "--out-dir");
                break;
            case "--system":
                system = NextArg(args, ref i, "--system");
                break;
            case "--delay":
                if (!double.TryParse(NextArg(args, ref i, "--delay"), out delay))
                    Exit("--delay must be a number.");
                break;
            case "--dry-run":
                dryRun = true;
                break;
            case "--no-verify-ssl":
                noVerifySsl = true;
                break;
            case "--retries":
                if (!int.TryParse(NextArg(args, ref i, "--retries"), out retries))
                    Exit("--retries must be an integer.");
                break;
            case "--verbose":
                verbose = true;
                break;
            case "--proxy":
                proxy = NextArg(args, ref i, "--proxy");
                break;
            case "--dump-html":
                dumpHtml = true;
                break;
            case "--help":
            case "-h":
                PrintHelp();
                Environment.Exit(0);
                break;
            default:
                Exit($"Unknown argument: {args[i]}");
                break;
        }
    }

    return new ScraperOptions
    {
        OutDir      = outDir ?? defaultOutDir,
        System      = system,
        Delay       = delay,
        DryRun      = dryRun,
        NoVerifySsl = noVerifySsl,
        Retries     = retries,
        Verbose     = verbose,
        Proxy       = proxy,
        DumpHtml    = dumpHtml,
    };
}

static string NextArg(string[] args, ref int i, string flag)
{
    if (i + 1 >= args.Length)
        Exit($"{flag} requires a value.");
    return args[++i];
}

static void Exit(string message)
{
    Console.Error.WriteLine($"ERROR: {message}");
    Console.Error.WriteLine("Run with --help for usage information.");
    Environment.Exit(1);
    throw new InvalidOperationException(); // unreachable; satisfies compiler
}

static void PrintHelp()
{
    Console.WriteLine("""
        Scrape-GameHacking (C# port)
        Scrapes https://gamehacking.org and writes cheats.yaml files into the
        CheatArchiver folder structure.

        Usage:
          Scrape-GameHacking [options]

        Options:
          --out-dir <path>    Root of CheatArchiver repo (default: executable directory)
          --system <name|id>  Filter to one system by ID or partial name
          --delay <seconds>   Delay between requests, default 1.0 (±30% jitter applied)
          --dry-run           Print paths without writing files
          --no-verify-ssl     Disable SSL certificate verification
          --retries <n>       Retry count for failed requests (default 3)
          --verbose           Verbose HTTP debug output
          --proxy <url>       Proxy URL, e.g. http://127.0.0.1:8080
          --dump-html         Dump raw HTML pages to files for debugging
          --help, -h          Show this help message
        """);
}

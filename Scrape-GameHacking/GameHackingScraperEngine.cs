using HtmlAgilityPack;
using RainbowSix.WebClient;
using System.Net;
using System.Text.RegularExpressions;

namespace GameHackingScraper
{
    internal sealed record SystemInfo(string Id, string Name);

    internal sealed record GameMeta(string Id, string Title, string Url);

    internal sealed record GameData(
        string Title,
        string Console,
        string Region,
        string Serial,
        string CheatType,
        string SourceUrl,
        IReadOnlyList<CheatEntry> Cheats);

    /// <summary>
    /// Scrapes https://gamehacking.org using the RainbowSix.WebClient HttpConnection.
    /// Mirrors the Python Scrape-GameHacking.py scraping logic.
    /// </summary>
    internal sealed class GameHackingScraperEngine : IDisposable
    {
        private const string BaseUrl = "https://gamehacking.org";

        private static readonly Regex GameAnchorRx    = new(@"^/game/(\d+)", RegexOptions.Compiled);
        private static readonly Regex CodePattern     = new(@"^[0-9A-Fa-f\s\-:]+$", RegexOptions.Compiled);
        private static readonly Regex HexPattern      = new(@"[0-9A-Fa-f]{2,}", RegexOptions.Compiled);
        private static readonly Regex HeadingDeviceRx = new(@"^(.+?)\s*(?:codes?|cheats?)?$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private readonly HttpConnection _http;
        private readonly ScraperOptions _opts;
        private readonly Random _rng = new();

        public GameHackingScraperEngine(ScraperOptions opts)
        {
            _opts = opts;

            IWebProxy? proxy = null;
            if (!string.IsNullOrEmpty(opts.Proxy))
                proxy = new WebProxy(opts.Proxy);

            _http = new HttpConnection(
                new ConsoleLogger(opts.Verbose),
                ignoreSSLCertErrors: opts.NoVerifySsl,
                proxy: proxy);

            // Set browser-like headers that match the Python cloudscraper configuration
            _http.SetRequestHeaders(new Dictionary<string, string>
            {
                ["User-Agent"]               = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/146.0.0.0 Safari/537.36",
                ["Accept"]                   = "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,image/apng,*/*;q=0.8,application/signed-exchange;v=b3;q=0.7",
                ["Accept-Language"]          = "en-US,en;q=0.9,ro;q=0.8,fr;q=0.7,es;q=0.6",
                ["Accept-Encoding"]          = "gzip, deflate, br",
                ["Connection"]               = "keep-alive",
                ["Upgrade-Insecure-Requests"]= "1",
                ["Sec-Fetch-Dest"]           = "document",
                ["Sec-Fetch-Mode"]           = "navigate",
                ["Sec-Fetch-Site"]           = "none",
                ["Sec-Fetch-User"]           = "?1",
                ["Sec-Ch-Ua"]                = "\"Chromium\";v=\"146\", \"Not-A.Brand\";v=\"24\", \"Google Chrome\";v=\"146\"",
                ["Sec-Ch-Ua-Mobile"]         = "?0",
                ["Sec-Ch-Ua-Platform"]       = "\"Windows\"",
                ["Cache-Control"]            = "max-age=0",
                ["DNT"]                      = "1",
                ["Priority"]                 = "u=0, i",
            });
        }

        // -----------------------------------------------------------------------
        // Public entry point
        // -----------------------------------------------------------------------

        public async Task RunAsync()
        {
            var consolesDir = Path.Combine(_opts.OutDir, "consoles");

            if (_opts.Verbose)
            {
                Console.WriteLine("Session configuration:");
                Console.WriteLine("  User-Agent: Mozilla/5.0 (Windows NT 10.0; Win64; x64) Chrome/146.0.0.0");
                Console.WriteLine($"  SSL verification: {!_opts.NoVerifySsl}");
                Console.WriteLine($"  Delay: {_opts.Delay}s (with random jitter)");
                Console.WriteLine($"  Retries: {_opts.Retries}");
                if (!string.IsNullOrEmpty(_opts.Proxy))
                    Console.WriteLine($"  Proxy: {_opts.Proxy}");
            }

            // Visit the homepage first to establish session cookies (mirrors Python behaviour)
            Console.WriteLine($"Initializing session with {BaseUrl} …");
            try
            {
                await _http.GetAsync(BaseUrl);
                await RandomDelayAsync(_opts.Delay);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  [WARN] Could not initialize session: {ex.Message}");
                Console.WriteLine("  Continuing anyway...");
            }

            Console.WriteLine($"Fetching system list from {BaseUrl}/search …");
            List<SystemInfo> systems;
            try
            {
                systems = await ScrapeSystemsAsync();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"ERROR: Could not fetch the system list.\n  {ex.Message}");
                Environment.Exit(1);
                return;
            }

            if (systems.Count == 0)
            {
                Console.Error.WriteLine("No systems found. The page layout may have changed.");
                Environment.Exit(1);
                return;
            }

            Console.WriteLine($"Found {systems.Count} system(s).");

            // Filter by --system if provided
            if (!string.IsNullOrEmpty(_opts.System))
            {
                var filterVal = _opts.System.ToLowerInvariant();
                systems = systems
                    .Where(s => s.Id.ToLowerInvariant().Contains(filterVal)
                             || s.Name.ToLowerInvariant().Contains(filterVal))
                    .ToList();

                if (systems.Count == 0)
                {
                    Console.Error.WriteLine($"No systems matched '{_opts.System}'.");
                    Environment.Exit(1);
                    return;
                }
                Console.WriteLine($"Filtered to {systems.Count} system(s).");
            }

            var totalWritten = 0;

            foreach (var system in systems)
            {
                Console.WriteLine($"\n=== System: {system.Name} (id={system.Id}) ===");

                List<GameMeta> games;
                try
                {
                    games = await ScrapeGameListAsync(system.Id);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"  ERROR fetching game list: {ex.Message}  Skipping system.");
                    continue;
                }
                Console.WriteLine($"  {games.Count} game(s) found.");

                foreach (var gameMeta in games)
                {
                    Console.Write($"  Scraping: {gameMeta.Title} … ");
                    GameData data;
                    try
                    {
                        data = await ScrapeGameAsync(gameMeta.Url);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"ERROR: {ex.Message}");
                        continue;
                    }

                    var console    = FileSystemHelper.SafeName(data.Console.Length > 0 ? data.Console : system.Name);
                    var region     = FileSystemHelper.SafeName(data.Region.Length > 0 ? data.Region : "Unknown");
                    var gameTitle  = FileSystemHelper.SafeName(data.Title.Length > 0 ? data.Title : gameMeta.Title);
                    var cheatType  = data.CheatType.Length > 0 ? data.CheatType : "RAW";

                    if (string.IsNullOrEmpty(console))   console   = FileSystemHelper.SafeName(system.Name);
                    if (string.IsNullOrEmpty(region))    region    = "Unknown";
                    if (string.IsNullOrEmpty(gameTitle)) gameTitle = $"game_{gameMeta.Id}";

                    var yamlContent = YamlBuilder.Build(
                        game:      data.Title.Length > 0 ? data.Title : gameMeta.Title,
                        console:   data.Console.Length > 0 ? data.Console : system.Name,
                        region:    data.Region.Length > 0 ? data.Region : "Unknown",
                        serial:    data.Serial,
                        cheatType: cheatType,
                        sourceUrl: data.SourceUrl,
                        cheats:    data.Cheats);

                    var yamlPath = Path.Combine(consolesDir, console, region, gameTitle, "cheats.yaml");
                    FileSystemHelper.EnsureYaml(yamlPath, yamlContent, _opts.DryRun);
                    totalWritten++;
                    Console.WriteLine($"OK ({data.Cheats.Count} cheats, type={cheatType})");
                }
            }

            Console.WriteLine($"\nDone. {totalWritten} game(s) processed.");
        }

        // -----------------------------------------------------------------------
        // Scraping helpers
        // -----------------------------------------------------------------------

        /// <summary>
        /// Fetches a URL with retry + exponential back-off and optional per-request headers.
        /// Mirrors the Python get_soup() function.
        /// </summary>
        private async Task<HtmlDocument> GetSoupAsync(
            string url,
            string? referer = null,
            string? dumpFilename = null)
        {
            if (_opts.Verbose)
            {
                Console.WriteLine($"  [DEBUG] Fetching {url}");
                if (referer != null)
                    Console.WriteLine($"  [DEBUG] Referer: {referer}");
            }

            Exception? lastExc = null;
            for (var attempt = 1; attempt <= _opts.Retries; attempt++)
            {
                try
                {
                    Dictionary<string, string>? perReqHeaders = null;
                    if (referer != null)
                    {
                        perReqHeaders = new Dictionary<string, string>
                        {
                            ["Referer"]       = referer,
                            ["Sec-Fetch-Site"]= "same-origin",
                        };
                    }

                    var html = await _http.GetAsync(url, perReqHeaders);

                    if (_opts.Verbose)
                        Console.WriteLine($"  [DEBUG] Content length: {html.Length} bytes");

                    if (_opts.DumpHtml && dumpFilename != null)
                    {
                        try
                        {
                            File.WriteAllText(dumpFilename, html, System.Text.Encoding.UTF8);
                            Console.WriteLine($"  [DEBUG] HTML dumped to: {dumpFilename}");
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"  [WARN] Could not dump HTML to file: {ex.Message}");
                        }
                    }

                    await RandomDelayAsync(_opts.Delay);

                    var doc = new HtmlDocument();
                    doc.LoadHtml(html);
                    return doc;
                }
                catch (HttpRequestException ex) when ((int?)ex.StatusCode < 500 && ex.StatusCode != null)
                {
                    // Non-transient HTTP errors (4xx) – not worth retrying
                    var status = (int)ex.StatusCode;
                    var msg = $"HTTP {status} error fetching {url}.";
                    if (status == 403)
                    {
                        msg += "\n  The site is actively blocking this request. This could be due to:"
                             + "\n  - Anti-bot protection (e.g., Cloudflare)"
                             + "\n  - Rate limiting (try increasing --delay)"
                             + "\n  - IP blocking (try using a VPN or proxy with --proxy)"
                             + "\n  - User-Agent detection (headers may need updating)";
                    }
                    else
                    {
                        msg += " The site may be blocking requests or the URL has changed.";
                    }
                    throw new HttpRequestException(msg, ex, ex.StatusCode);
                }
                catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or OperationCanceledException)
                {
                    lastExc = ex;
                    Console.WriteLine($"  [WARN] Request error on attempt {attempt}/{_opts.Retries} for {url}: {ex.Message}");
                    Console.Out.Flush();
                }

                if (attempt < _opts.Retries)
                {
                    var backoff = _opts.Delay * Math.Pow(2, attempt);
                    Console.WriteLine($"  [WARN] Retrying in {backoff:F1}s …");
                    await RandomDelayAsync(backoff);
                }
            }

            throw new Exception($"Failed to fetch {url} after {_opts.Retries} attempt(s).", lastExc);
        }

        /// <summary>
        /// Returns all systems from the search page.
        /// Mirrors the Python scrape_systems() function.
        /// </summary>
        private async Task<List<SystemInfo>> ScrapeSystemsAsync()
        {
            var doc = await GetSoupAsync(
                $"{BaseUrl}/search",
                referer: BaseUrl,
                dumpFilename: _opts.DumpHtml ? "debug_search_page.html" : null);

            var systems = new List<SystemInfo>();

            // Find the system <select> element
            var select = doc.DocumentNode.SelectSingleNode(
                "//select[@name and contains(translate(@name,'SYSTEM','system'),'system')]")
                ?? doc.DocumentNode.SelectSingleNode(
                "//select[@id and contains(translate(@id,'SYSTEM','system'),'system')]");

            if (select == null)
            {
                // Fallback: look for any <select> with many <option> children
                foreach (var sel in doc.DocumentNode.SelectNodes("//select") ?? Enumerable.Empty<HtmlNode>())
                {
                    var opts = sel.SelectNodes("option");
                    if (opts != null && opts.Count > 5)
                    {
                        select = sel;
                        break;
                    }
                }
            }

            if (select == null)
            {
                Console.WriteLine("[WARN] Could not find system selector on search page.");
                if (_opts.DumpHtml)
                    Console.WriteLine("  Check debug_search_page.html to see the actual page structure.");
                return systems;
            }

            foreach (var opt in select.SelectNodes("option") ?? Enumerable.Empty<HtmlNode>())
            {
                var val  = opt.GetAttributeValue("value", "").Trim();
                var name = HtmlEntity.DeEntitize(opt.InnerText).Trim();
                if (!string.IsNullOrEmpty(val) && val != "0")
                    systems.Add(new SystemInfo(val, name));
            }

            return systems;
        }

        /// <summary>
        /// Returns all games for a given system id, following pagination.
        /// Mirrors the Python scrape_game_list() function.
        /// </summary>
        private async Task<List<GameMeta>> ScrapeGameListAsync(string systemId)
        {
            var games   = new List<GameMeta>();
            var page    = 1;

            while (true)
            {
                var query    = $"system={Uri.EscapeDataString(systemId)}&page={page}";
                var pageUrl  = $"{BaseUrl}/search?{query}";
                var referer  = page == 1
                    ? BaseUrl
                    : $"{BaseUrl}/search?system={Uri.EscapeDataString(systemId)}&page={page - 1}";

                var doc = await GetSoupAsync(pageUrl, referer: referer);

                var foundAny = false;
                var anchors = doc.DocumentNode.SelectNodes("//a[@href]");
                if (anchors != null)
                {
                    foreach (var a in anchors)
                    {
                        var href = a.GetAttributeValue("href", "");
                        var m = GameAnchorRx.Match(href);
                        if (!m.Success)
                            continue;
                        var gameId = m.Groups[1].Value;
                        var title  = HtmlEntity.DeEntitize(a.InnerText).Trim();
                        games.Add(new GameMeta(gameId, title, $"{BaseUrl}{href}"));
                        foundAny = true;
                    }
                }

                // Stop if no games found or no "next page" link
                var nextLink = doc.DocumentNode.SelectSingleNode(
                    "//a[contains(translate(text(),'NEXT','next'),'next')]");
                if (!foundAny || nextLink == null)
                    break;
                page++;
            }

            return games;
        }

        /// <summary>
        /// Fetches a game page and extracts all cheat data.
        /// Mirrors the Python scrape_game() function.
        /// </summary>
        private async Task<GameData> ScrapeGameAsync(string gameUrl)
        {
            var doc = await GetSoupAsync(gameUrl, referer: $"{BaseUrl}/search");

            // --- Title ---
            var h1    = doc.DocumentNode.SelectSingleNode("//h1");
            var title = h1 != null ? HtmlEntity.DeEntitize(h1.InnerText).Trim() : "Unknown Game";

            // --- Metadata table (console, region, serial) ---
            var console = "";
            var region  = "";
            var serial  = "";

            foreach (var row in doc.DocumentNode.SelectNodes("//tr") ?? Enumerable.Empty<HtmlNode>())
            {
                var cells = row.SelectNodes("th|td");
                if (cells == null || cells.Count < 2)
                    continue;
                var label = HtmlEntity.DeEntitize(cells[0].InnerText).Trim().ToLowerInvariant();
                var value = HtmlEntity.DeEntitize(cells[1].InnerText).Trim();
                if (label.Contains("system") || label.Contains("console"))
                    console = value;
                else if (label.Contains("region"))
                    region = value;
                else if (label.Contains("serial") || label.Contains("id"))
                    serial = value;
            }

            // --- Cheat codes ---
            // Strategy: walk h2/h3 headings and tr rows; headings name the device,
            // rows following them contain codes.
            var cheats           = new List<CheatEntry>();
            var cheatTypesFound  = new HashSet<string>();
            var currentDevice    = "";

            var allNodes = doc.DocumentNode.SelectNodes("//h2|//h3|//tr");
            if (allNodes != null)
            {
                foreach (var tag in allNodes)
                {
                    if (tag.Name is "h2" or "h3")
                    {
                        var text = HtmlEntity.DeEntitize(tag.InnerText).Trim();
                        var m = HeadingDeviceRx.Match(text);
                        if (m.Success)
                            currentDevice = m.Groups[1].Value.Trim();
                    }
                    else if (tag.Name == "tr")
                    {
                        var cells = tag.SelectNodes("td");
                        if (cells == null || cells.Count < 2)
                            continue;

                        var codeText   = HtmlEntity.DeEntitize(
                            cells[0].InnerText.Replace("\n", " ")).Trim();
                        var descText   = HtmlEntity.DeEntitize(cells[1].InnerText).Trim();
                        var authorText = cells.Count > 2
                            ? HtmlEntity.DeEntitize(cells[2].InnerText).Trim()
                            : "";

                        // Validate that the first cell looks like a cheat code
                        if (codeText.Length >= 4
                            && CodePattern.IsMatch(codeText)
                            && HexPattern.IsMatch(codeText))
                        {
                            var codeLines = codeText
                                .Split('\n', StringSplitOptions.RemoveEmptyEntries)
                                .Select(l => l.Trim())
                                .Where(l => l.Length > 0)
                                .ToList();

                            if (codeLines.Count > 0)
                            {
                                cheatTypesFound.Add(currentDevice);
                                cheats.Add(new CheatEntry(
                                    Name:   string.IsNullOrEmpty(descText) ? "Unknown" : descText,
                                    Author: authorText,
                                    Notes:  "",
                                    Codes:  codeLines));
                            }
                        }
                    }
                }
            }

            var cheatType = CheatTypeDetector.ResolveFromSet(cheatTypesFound);

            return new GameData(
                Title:     title,
                Console:   console,
                Region:    region,
                Serial:    serial,
                CheatType: cheatType,
                SourceUrl: gameUrl,
                Cheats:    cheats);
        }

        // -----------------------------------------------------------------------
        // Utilities
        // -----------------------------------------------------------------------

        /// <summary>
        /// Waits for <paramref name="baseDelay"/> seconds with ±30 % random jitter.
        /// Mirrors the Python random_delay() function.
        /// </summary>
        private async Task RandomDelayAsync(double baseDelay)
        {
            var jitter  = _rng.NextDouble() * 0.6 - 0.3; // [-0.3, +0.3]
            var actual  = baseDelay * (1.0 + jitter);
            var ms      = (int)(Math.Max(0.1, actual) * 1000);
            await Task.Delay(ms);
        }

        public void Dispose() => _http.Dispose();
    }
}

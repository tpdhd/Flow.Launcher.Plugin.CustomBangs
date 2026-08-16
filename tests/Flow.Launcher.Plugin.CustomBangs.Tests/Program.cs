using Flow.Launcher.Plugin.CustomBangs.Core;
using Flow.Launcher.Plugin.CustomBangs.Models;
using Flow.Launcher.Plugin.CustomBangs.Services;

var tests = new (string Name, Action Run)[]
{
    ("Activator at start", ActivatorAtStart),
    ("Empty activator", EmptyActivator),
    ("Empty activator is the default", EmptyActivatorIsDefault),
    ("Reject bang in middle", RejectBangInMiddle),
    ("Multiple target URLs", MultipleTargets),
    ("Shortcut members form a search group", SearchGroupType),
    ("Groups sort before regular bangs", GroupsSortFirst),
    ("Invalid group members are rejected", InvalidGroups),
    ("No default means no action", NoDefaultMeansNoAction),
    ("Default URL", DefaultUrl),
    ("Alias resolution", AliasResolution),
    ("JavaScript-compatible encoding", JavaScriptEncoding),
    ("Validation", Validation),
    ("Chrome v6 JSON round trip", JsonRoundTrip),
    ("Chrome export omits Flow group extension", ChromeExportWithoutGroups),
    ("Search group JSON round trip", SearchGroupJsonRoundTrip),
    ("Published Flow list is valid and conflict-resistant", PublishedFlowList),
    ("Chrome v5 import", Version5Import),
    ("Two-hundred bang catalog", TwoHundredBangCatalog),
    ("Five-thousand bang indexed catalog", FiveThousandBangIndexedCatalog),
    ("Empty activator does not populate home", EmptyActivatorDoesNotPopulateHome),
    ("Chrome relative DuckDuckGo targets", ChromeRelativeDuckDuckGoTargets),
    ("Ten-thousand indexed queries", TenThousandIndexedQueries),
};

var failures = new List<string>();
foreach (var test in tests)
{
    try
    {
        test.Run();
        Console.WriteLine($"PASS  {test.Name}");
    }
    catch (Exception exception)
    {
        failures.Add($"{test.Name}: {exception.Message}");
        Console.Error.WriteLine($"FAIL  {test.Name}: {exception.Message}");
    }
}

Console.WriteLine($"{tests.Length - failures.Count}/{tests.Length} tests passed.");
if (failures.Count > 0)
{
    Environment.ExitCode = 1;
}

return;

static List<BangDefinition> Catalog() =>
[
    new BangDefinition
    {
        Keyword = "yt",
        DefaultUrl = "https://www.youtube.com/",
        Urls = ["https://www.youtube.com/results?search_query=%s"],
    },
    new BangDefinition
    {
        Keyword = "asd",
        Urls = ["https://www.amazon.de/s?k=%s"],
    },
    new BangDefinition
    {
        Keyword = "ede",
        Urls = ["https://www.ebay.de/sch/i.html?_nkw=%s"],
    },
    new BangDefinition
    {
        Keyword = "ali",
        Urls = ["https://www.aliexpress.com/wholesale?SearchText=%s"],
    },
    new BangDefinition
    {
        Keyword = "shop1",
        GroupMembers = ["asd", "ede", "ali"],
    },
    new BangDefinition
    {
        Keyword = "youtube",
        Alias = "yt",
    },
];

static void ActivatorAtStart()
{
    var result = BangParser.Parse("!yt linkin park", "!", Catalog(), ignoreCase: true);
    Equal("yt", result.Request?.Bang.Keyword);
    Equal("linkin park", result.Request?.QueryText);
}

static void EmptyActivator()
{
    var result = BangParser.Parse("yt linkin park", string.Empty, Catalog(), ignoreCase: true);
    Equal("yt", result.Request?.Bang.Keyword);
    Equal("linkin park", result.Request?.QueryText);
}

static void EmptyActivatorIsDefault()
{
    Equal(string.Empty, new PluginSettings().Activator);
}

static void RejectBangInMiddle()
{
    var result = BangParser.Parse("music !yt", "!", Catalog(), ignoreCase: true);
    True(result.Request is null && result.Suggestions.Count == 0, "A bang in the middle must not match.");
}

static void MultipleTargets()
{
    var catalog = Catalog();
    var group = catalog.Single(static bang => bang.Keyword == "shop1");
    var urls = BangResolver.ResolveUrls(group, "usb cable", catalog, ignoreCase: true);
    Equal(3, urls.Count);
    True(urls.All(static url => url.Contains("usb%20cable", StringComparison.Ordinal)), "Every target must receive the query.");
}

static void SearchGroupType()
{
    Equal("Group", Catalog().Single(static bang => bang.Keyword == "shop1").TypeLabel);
    Equal("Bang", Catalog()[0].TypeLabel);
    Equal("Alias", Catalog().Single(static bang => bang.Keyword == "youtube").TypeLabel);
}

static void GroupsSortFirst()
{
    var catalog = new BangCatalog(Catalog(), ignoreCase: true);
    Equal("shop1", catalog.Bangs[0].Keyword);
    Equal("shop1", catalog.Suggest(string.Empty, 20)[0].Keyword);
}

static void InvalidGroups()
{
    var missing = Catalog();
    missing.Single(static bang => bang.Keyword == "shop1").GroupMembers = ["asd", "missing"];
    True(BangValidator.Validate("!", missing, ignoreCase: true).Any(static error => error.Contains("missing shortcut", StringComparison.Ordinal)), "Missing members must be rejected.");

    var nested = Catalog();
    nested.Add(new BangDefinition { Keyword = "nested", GroupMembers = ["shop1", "asd"] });
    True(BangValidator.Validate("!", nested, ignoreCase: true).Any(static error => error.Contains("cannot include group", StringComparison.Ordinal)), "Nested groups must be rejected.");
}

static void NoDefaultMeansNoAction()
{
    var catalog = Catalog();
    var urls = BangResolver.ResolveUrls(catalog.Single(static bang => bang.Keyword == "shop1"), string.Empty, catalog, ignoreCase: true);
    Equal(0, urls.Count);
}

static void DefaultUrl()
{
    var catalog = Catalog();
    var urls = BangResolver.ResolveUrls(catalog[0], string.Empty, catalog, ignoreCase: true);
    Equal("https://www.youtube.com/", urls.Single());
}

static void AliasResolution()
{
    var catalog = Catalog();
    var alias = catalog.Single(static bang => bang.Keyword == "youtube");
    var urls = BangResolver.ResolveUrls(alias, "boids", catalog, ignoreCase: true);
    Equal("https://www.youtube.com/results?search_query=boids", urls.Single());
}

static void JavaScriptEncoding()
{
    Equal("M%C3%BCnchen%20!*'()", BangResolver.EncodeURIComponent("München !*'()"));
}

static void Validation()
{
    var catalog = Catalog();
    Equal(0, BangValidator.Validate("!", catalog, ignoreCase: true).Count);

    catalog.Add(new BangDefinition { Keyword = "YT", Alias = "youtube" });
    True(BangValidator.Validate("!", catalog, ignoreCase: true).Count > 0, "Case-insensitive duplicates must be rejected.");
}

static void JsonRoundTrip()
{
    var json = BangJsonSerializer.Export(Catalog());
    True(json.Contains("\"version\": 6", StringComparison.Ordinal), "Export must use version 6.");
    True(json.Contains("\"dontEncodeQuery\"", StringComparison.Ordinal), "Export must use the Chrome field names.");
    Equal(Catalog().Count, BangJsonSerializer.Import(json).Count);
}

static void ChromeExportWithoutGroups()
{
    var regularBangs = Catalog().Where(static bang => !bang.IsGroup).ToList();
    var json = BangJsonSerializer.Export(regularBangs);
    True(!json.Contains("\"groupMembers\"", StringComparison.Ordinal), "Chrome-compatible exports must not contain Flow's groupMembers extension.");
    Equal(regularBangs.Count, BangJsonSerializer.Import(json).Count);
}

static void SearchGroupJsonRoundTrip()
{
    var imported = BangJsonSerializer.Import(BangJsonSerializer.Export(Catalog()));
    var group = imported.Single(static bang => bang.Keyword == "shop1");
    Equal("Group", group.TypeLabel);
    Equal("asd, ede, ali", group.GroupMembersSummary);
    var urls = BangResolver.ResolveUrls(group, "noise cancelling headphones", imported, ignoreCase: true);
    Equal(3, urls.Count);
    True(urls.All(static url => url.Contains("noise%20cancelling%20headphones", StringComparison.Ordinal)), "Every imported group target must receive the same query.");
}

static void PublishedFlowList()
{
    var path = FindRepositoryFile("lists", "tpdhd-custom-bangs-v6.json");
    var imported = BangJsonSerializer.ImportFile(path);
    Equal(234, imported.Count);
    True(imported.All(static bang => bang.Keyword.Length > 1), "The public Flow list must not contain one-character shortcuts.");
    var group = imported.Single(static bang => bang.Keyword == "shop1");
    Equal("Group", group.TypeLabel);
    Equal("asd, ede, ali", group.GroupMembersSummary);
}

static void Version5Import()
{
    const string json = """
        {
          "version": 5,
          "options": { "ignoredDomains": [], "ignoreCase": true, "sortByAlpha": true },
          "bangs": [{ "bang": "docs", "urls": ["https://example.com/?q=%s"] }]
        }
        """;
    var imported = BangJsonSerializer.Import(json);
    Equal("docs", imported.Single().Keyword);
}

static string FindRepositoryFile(params string[] parts)
{
    var directory = new DirectoryInfo(AppContext.BaseDirectory);
    while (directory is not null)
    {
        var candidate = Path.Combine([directory.FullName, .. parts]);
        if (File.Exists(candidate))
        {
            return candidate;
        }

        directory = directory.Parent;
    }

    throw new FileNotFoundException($"Could not find repository file: {Path.Combine(parts)}");
}

static void TwoHundredBangCatalog()
{
    var catalog = Enumerable.Range(0, 200)
        .Select(index => new BangDefinition
        {
            Keyword = $"b{index}",
            Urls = ["https://example.com/search?q=%s"],
        })
        .ToList();
    var parsed = BangParser.Parse("!b199 final query", "!", catalog, ignoreCase: true);
    Equal("b199", parsed.Request?.Bang.Keyword);
    Equal("final query", parsed.Request?.QueryText);
}

static void FiveThousandBangIndexedCatalog()
{
    var bangs = Enumerable.Range(0, 5_000)
        .Select(index => new BangDefinition
        {
            Keyword = $"bang{index:D4}",
            Urls = ["https://example.com/search?q=%s"],
        })
        .ToArray();
    var catalog = new BangCatalog(bangs, ignoreCase: true);

    var parsed = BangParser.Parse("!BANG4999 final query", "!", catalog);
    Equal("bang4999", parsed.Request?.Bang.Keyword);
    Equal("final query", parsed.Request?.QueryText);

    var suggestions = BangParser.Parse("!bang49", "!", catalog).Suggestions;
    Equal(20, suggestions.Count);
    True(suggestions.All(static bang => bang.Keyword.StartsWith("bang49", StringComparison.OrdinalIgnoreCase)), "Indexed suggestions must respect the prefix.");
}

static void EmptyActivatorDoesNotPopulateHome()
{
    var result = BangParser.Parse(string.Empty, string.Empty, Catalog(), ignoreCase: true);
    True(result.Request is null && result.Suggestions.Count == 0, "An empty query must not add bang suggestions to Flow's home page.");
}

static void ChromeRelativeDuckDuckGoTargets()
{
    var bang = new BangDefinition { Keyword = "safeoff", Urls = ["/?q=%s&kp=-2"] };
    var catalog = new[] { bang };
    Equal(0, BangValidator.Validate(string.Empty, catalog, ignoreCase: true).Count);
    Equal(
        "https://duckduckgo.com/?q=privacy&kp=-2",
        BangResolver.ResolveUrls(bang, "privacy", catalog, ignoreCase: true).Single());
}

static void TenThousandIndexedQueries()
{
    var catalog = new BangCatalog(
        Enumerable.Range(0, 5_000).Select(index => new BangDefinition
        {
            Keyword = $"bang{index:D4}",
            Urls = ["https://example.com/?q=%s"],
        }),
        ignoreCase: true);
    var stopwatch = System.Diagnostics.Stopwatch.StartNew();

    for (var index = 0; index < 10_000; index++)
    {
        var parsed = BangParser.Parse("!bang4999 query", "!", catalog);
        True(parsed.Request is not null, "The indexed query should match.");
    }

    stopwatch.Stop();
    True(stopwatch.Elapsed < TimeSpan.FromSeconds(3), $"10,000 indexed queries took {stopwatch.Elapsed.TotalMilliseconds:N0} ms.");
}

static void Equal<T>(T expected, T actual)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
    {
        throw new InvalidOperationException($"Expected '{expected}', got '{actual}'.");
    }
}

static void True(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}

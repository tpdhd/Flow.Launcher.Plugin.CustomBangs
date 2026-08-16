using Flow.Launcher.Plugin.CustomBangs.Models;

namespace Flow.Launcher.Plugin.CustomBangs.Core;

public static class BangParser
{
    public static ParseResult Parse(
        string? input,
        string activator,
        IReadOnlyCollection<BangDefinition> bangs,
        bool ignoreCase,
        int suggestionLimit = 20) =>
        Parse(input, activator, new BangCatalog(bangs, ignoreCase), suggestionLimit);

    public static ParseResult Parse(
        string? input,
        string activator,
        BangCatalog catalog,
        int suggestionLimit = 20)
    {
        var text = input?.TrimStart() ?? string.Empty;
        activator ??= string.Empty;

        if (activator.Length == 0 && text.Length == 0)
        {
            return ParseResult.NoMatch;
        }

        if (activator.Length > 0)
        {
            if (!text.StartsWith(activator, StringComparison.Ordinal))
            {
                return ParseResult.NoMatch;
            }

            text = text[activator.Length..];
        }

        var separatorIndex = text.IndexOfAny([' ', '\t', '\r', '\n']);
        var keyword = separatorIndex < 0 ? text : text[..separatorIndex];
        var queryText = separatorIndex < 0 ? string.Empty : text[(separatorIndex + 1)..].Trim();
        var exact = catalog.Find(keyword);
        if (exact is not null)
        {
            return new ParseResult(new BangRequest(exact, queryText), []);
        }

        var suggestions = catalog.Suggest(keyword, suggestionLimit);

        return new ParseResult(null, suggestions);
    }
}

public sealed record ParseResult(BangRequest? Request, IReadOnlyList<BangDefinition> Suggestions)
{
    public static ParseResult NoMatch { get; } = new(null, []);
}

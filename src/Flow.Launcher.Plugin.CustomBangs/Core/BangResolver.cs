using System.Text;
using Flow.Launcher.Plugin.CustomBangs.Models;

namespace Flow.Launcher.Plugin.CustomBangs.Core;

public static class BangResolver
{
    private static readonly Uri DuckDuckGoBaseUri = new("https://duckduckgo.com/");

    public static IReadOnlyList<string> ResolveUrls(
        BangDefinition requestedBang,
        string queryText,
        IReadOnlyCollection<BangDefinition> catalog,
        bool ignoreCase) =>
        ResolveUrls(requestedBang, queryText, new BangCatalog(catalog, ignoreCase));

    public static IReadOnlyList<string> ResolveUrls(
        BangDefinition requestedBang,
        string queryText,
        BangCatalog catalog)
    {
        if (requestedBang.IsGroup)
        {
            if (string.IsNullOrWhiteSpace(queryText))
            {
                return [];
            }

            return requestedBang.GroupMembers
                .Select(catalog.Find)
                .Where(static member => member is not null && !member.IsGroup)
                .SelectMany(member => ResolveUrls(member!, queryText, catalog))
                .Distinct(StringComparer.Ordinal)
                .ToArray();
        }

        var bang = ResolveAlias(requestedBang, catalog);
        if (bang is null)
        {
            return [];
        }

        if (string.IsNullOrWhiteSpace(queryText))
        {
            return IsHttpUrl(bang.DefaultUrl) ? [bang.DefaultUrl] : [];
        }

        var replacement = bang.DontEncodeQuery ? queryText : EncodeURIComponent(queryText);
        return bang.Urls
            .Where(static url => !string.IsNullOrWhiteSpace(url))
            .Select(url => ResolveTargetUrl(url, replacement))
            .Where(static url => url is not null)
            .Select(static url => url!)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    public static BangDefinition? ResolveAlias(
        BangDefinition requestedBang,
        IReadOnlyCollection<BangDefinition> catalog,
        bool ignoreCase) =>
        ResolveAlias(requestedBang, new BangCatalog(catalog, ignoreCase));

    public static BangDefinition? ResolveAlias(BangDefinition requestedBang, BangCatalog catalog)
    {
        var current = requestedBang;
        var visited = new HashSet<string>(catalog.IgnoreCase ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);

        while (current.Alias is not null)
        {
            if (!visited.Add(current.Keyword))
            {
                return null;
            }

            current = catalog.Find(current.Alias);
            if (current is null)
            {
                return null;
            }
        }

        return current;
    }

    public static string EncodeURIComponent(string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        var builder = new StringBuilder(bytes.Length * 3);

        foreach (var valueByte in bytes)
        {
            var character = (char)valueByte;
            if ((character >= 'a' && character <= 'z') ||
                (character >= 'A' && character <= 'Z') ||
                (character >= '0' && character <= '9') ||
                character is '-' or '_' or '.' or '!' or '~' or '*' or '\'' or '(' or ')')
            {
                builder.Append(character);
            }
            else
            {
                builder.Append('%');
                builder.Append(valueByte.ToString("X2", System.Globalization.CultureInfo.InvariantCulture));
            }
        }

        return builder.ToString();
    }

    public static bool IsHttpUrl(string? value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
        (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);

    public static string? ResolveTargetUrl(string template, string replacement)
    {
        var candidate = template.Replace("%s", replacement, StringComparison.Ordinal).Trim();
        if (IsHttpUrl(candidate))
        {
            return candidate;
        }

        // Custom Bang Search exports DuckDuckGo's own relative bang routes
        // (for example /bang?q=%s and /?q=%s&kp=-2) without a host.
        return candidate.StartsWith("/", StringComparison.Ordinal) &&
               Uri.TryCreate(DuckDuckGoBaseUri, candidate, out var resolved) &&
               IsHttpUrl(resolved.AbsoluteUri)
            ? resolved.AbsoluteUri
            : null;
    }
}

using Flow.Launcher.Plugin.CustomBangs.Models;

namespace Flow.Launcher.Plugin.CustomBangs.Core;

public static class BangValidator
{
    public static IReadOnlyList<string> Validate(
        string activator,
        IReadOnlyCollection<BangDefinition> bangs,
        bool ignoreCase)
    {
        var errors = new List<string>();
        if (activator.Any(char.IsWhiteSpace))
        {
            errors.Add("The activator cannot contain whitespace.");
        }

        var comparer = ignoreCase ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
        var keywords = new HashSet<string>(comparer);

        foreach (var bang in bangs)
        {
            if (string.IsNullOrWhiteSpace(bang.Keyword))
            {
                errors.Add("Every bang needs a keyword.");
                continue;
            }

            if (bang.Keyword.Any(char.IsWhiteSpace))
            {
                errors.Add($"Bang '{bang.Keyword}' contains whitespace.");
            }

            if (!keywords.Add(bang.Keyword))
            {
                errors.Add($"Duplicate keyword: '{bang.Keyword}'.");
            }

            if (bang.Alias is not null)
            {
                if (comparer.Equals(bang.Keyword, bang.Alias))
                {
                    errors.Add($"Alias '{bang.Keyword}' points to itself.");
                }

                continue;
            }

            if (bang.IsGroup)
            {
                if (bang.GroupMembers.Count < 2)
                {
                    errors.Add($"Group '{bang.Keyword}' needs at least two shortcut members.");
                }

                if (bang.Urls.Count > 0 || bang.DefaultUrl.Length > 0)
                {
                    errors.Add($"Group '{bang.Keyword}' cannot contain URLs; use shortcut members instead.");
                }

                continue;
            }

            if (bang.DefaultUrl.Length > 0 && !BangResolver.IsHttpUrl(bang.DefaultUrl))
            {
                errors.Add($"Bang '{bang.Keyword}' has an invalid default URL.");
            }

            foreach (var url in bang.Urls.Where(static url => !string.IsNullOrWhiteSpace(url)))
            {
                if (BangResolver.ResolveTargetUrl(url, "query") is null)
                {
                    errors.Add($"Bang '{bang.Keyword}' has an invalid target URL: {url}");
                }
            }
        }

        var catalog = new BangCatalog(bangs, ignoreCase);
        foreach (var bang in bangs.Where(static bang => bang.Alias is not null))
        {
            if (!keywords.Contains(bang.Alias!))
            {
                errors.Add($"Alias '{bang.Keyword}' points to missing bang '{bang.Alias}'.");
            }
            else if (BangResolver.ResolveAlias(bang, catalog) is null)
            {
                errors.Add($"Alias cycle detected at '{bang.Keyword}'.");
            }
        }

        foreach (var group in bangs.Where(static bang => bang.IsGroup))
        {
            foreach (var member in group.GroupMembers)
            {
                if (!keywords.Contains(member))
                {
                    errors.Add($"Group '{group.Keyword}' references missing shortcut '{member}'.");
                    continue;
                }

                var target = catalog.Find(member);
                if (target?.IsGroup == true)
                {
                    errors.Add($"Group '{group.Keyword}' cannot include group '{member}'.");
                }
                else if (comparer.Equals(group.Keyword, member))
                {
                    errors.Add($"Group '{group.Keyword}' cannot include itself.");
                }
            }
        }

        return errors.Distinct(StringComparer.Ordinal).ToArray();
    }
}

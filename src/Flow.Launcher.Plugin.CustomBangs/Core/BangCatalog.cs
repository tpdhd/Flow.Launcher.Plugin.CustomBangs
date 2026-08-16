using Flow.Launcher.Plugin.CustomBangs.Models;

namespace Flow.Launcher.Plugin.CustomBangs.Core;

public sealed class BangCatalog
{
    private readonly StringComparer _comparer;
    private readonly StringComparison _comparison;
    private readonly Dictionary<string, BangDefinition> _byKeyword;
    private readonly BangDefinition[] _sorted;
    private readonly BangDefinition[] _groups;
    private readonly BangDefinition[] _regular;

    public BangCatalog(IEnumerable<BangDefinition> bangs, bool ignoreCase)
    {
        IgnoreCase = ignoreCase;
        _comparer = ignoreCase ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
        _comparison = ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        var all = bangs.ToArray();
        _groups = all.Where(static bang => bang.IsGroup).OrderBy(static bang => bang.Keyword, _comparer).ToArray();
        _regular = all.Where(static bang => !bang.IsGroup).OrderBy(static bang => bang.Keyword, _comparer).ToArray();
        _sorted = [.. _groups, .. _regular];
        _byKeyword = new Dictionary<string, BangDefinition>(_comparer);

        foreach (var bang in _sorted)
        {
            _byKeyword.TryAdd(bang.Keyword, bang);
        }
    }

    public bool IgnoreCase { get; }

    public IReadOnlyList<BangDefinition> Bangs => _sorted;

    public BangDefinition? Find(string keyword) =>
        _byKeyword.TryGetValue(keyword, out var bang) ? bang : null;

    public IReadOnlyList<BangDefinition> Suggest(string prefix, int limit)
    {
        if (limit <= 0 || _sorted.Length == 0)
        {
            return [];
        }

        if (prefix.Length == 0)
        {
            return _sorted.Take(limit).ToArray();
        }

        var matches = new List<BangDefinition>(Math.Min(limit, 20));
        AddPrefixMatches(_groups, prefix, limit, matches);
        AddPrefixMatches(_regular, prefix, limit, matches);
        return matches;
    }

    private void AddPrefixMatches(
        BangDefinition[] source,
        string prefix,
        int limit,
        List<BangDefinition> matches)
    {
        var low = 0;
        var high = source.Length;
        while (low < high)
        {
            var middle = low + ((high - low) / 2);
            if (_comparer.Compare(source[middle].Keyword, prefix) < 0)
            {
                low = middle + 1;
            }
            else
            {
                high = middle;
            }
        }

        for (var index = low; index < source.Length && matches.Count < limit; index++)
        {
            var bang = source[index];
            if (!bang.Keyword.StartsWith(prefix, _comparison))
            {
                break;
            }

            matches.Add(bang);
        }
    }
}

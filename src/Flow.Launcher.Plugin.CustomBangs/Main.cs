using System.Windows.Controls;
using Flow.Launcher.Plugin.CustomBangs.Core;
using Flow.Launcher.Plugin.CustomBangs.Models;
using Flow.Launcher.Plugin.CustomBangs.Services;
using Flow.Launcher.Plugin.CustomBangs.UI;

namespace Flow.Launcher.Plugin.CustomBangs;

public sealed class Main : IAsyncPlugin, ISettingProvider
{
    internal const string PluginId = "C6DDA7F7A7F64A9D9E5C2B6DC6EE5C2D";
    private const string IconPath = "Images\\app.png";

    private PluginInitContext? _context;
    private SettingsStore? _settingsStore;

    public Task InitAsync(PluginInitContext context)
    {
        _context = context;
        _settingsStore = new SettingsStore(context);
        return Task.CompletedTask;
    }

    public Task<List<Result>> QueryAsync(Query query, CancellationToken token)
    {
        if (_context is null || _settingsStore is null || token.IsCancellationRequested)
        {
            return Task.FromResult(new List<Result>());
        }

        var search = query.Search?.TrimStart() ?? string.Empty;
        var settingsResult = CreateSettingsResult(search);
        if (settingsResult is not null)
        {
            return Task.FromResult(new List<Result> { settingsResult });
        }

        var settings = _settingsStore.QuerySnapshot();
        var parsed = BangParser.Parse(search, settings.Activator, settings.Catalog);
        if (parsed.Request is not null)
        {
            var result = CreateBangResult(parsed.Request, settings);
            return Task.FromResult(result is null ? new List<Result>() : new List<Result> { result });
        }

        var suggestions = parsed.Suggestions
            .Select(bang => CreateSuggestionResult(bang, settings.Activator))
            .ToList();
        return Task.FromResult(suggestions);
    }

    public Control CreateSettingPanel()
    {
        if (_context is null || _settingsStore is null)
        {
            return new UserControl
            {
                Content = new TextBlock { Text = "Custom Bangs has not been initialized." },
            };
        }

        return new SettingsControl(_settingsStore, _context);
    }

    private Result? CreateSettingsResult(string search)
    {
        if (_context is null || search.Length < 5)
        {
            return null;
        }

        const string command = "bangs settings";
        if (!command.StartsWith(search, StringComparison.OrdinalIgnoreCase) &&
            !search.StartsWith(command, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return new Result
        {
            Title = "Bangs Settings",
            SubTitle = "Manage activator, custom bangs, aliases, targets, import and export",
            IcoPath = IconPath,
            Score = 10_000,
            QuerySuggestionText = command,
            Action = _ => OpenSettings(),
        };
    }

    private Result? CreateBangResult(BangRequest request, QuerySettingsSnapshot settings)
    {
        if (_context is null)
        {
            return null;
        }

        var urls = BangResolver.ResolveUrls(request.Bang, request.QueryText, settings.Catalog);
        if (urls.Count == 0)
        {
            return null;
        }

        var target = BangResolver.ResolveAlias(request.Bang, settings.Catalog) ?? request.Bang;
        var title = urls.Count == 1
            ? $"{settings.Activator}{request.Bang.Keyword}  {request.QueryText}".TrimEnd()
            : $"Open {urls.Count} targets with {settings.Activator}{request.Bang.Keyword}";
        var subtitle = urls.Count == 1
            ? urls[0]
            : string.Join("  •  ", urls.Select(static url => new Uri(url).Host));

        return new Result
        {
            Title = title,
            SubTitle = request.Bang.Alias is null ? subtitle : $"Alias of {target.Keyword}  •  {subtitle}",
            IcoPath = IconPath,
            Score = 9_000,
            ContextData = urls,
            Action = _ => OpenUrls(urls),
        };
    }

    private Result CreateSuggestionResult(BangDefinition bang, string activator)
    {
        var usage = $"{activator}{bang.Keyword} ";
        var description = bang.Alias is not null
            ? $"Alias of {bang.Alias}"
            : bang.IsGroup
                ? $"Search group • {bang.GroupMembersSummary}"
                : bang.UrlsSummary;

        return new Result
        {
            Title = usage.TrimEnd(),
            SubTitle = description,
            IcoPath = IconPath,
            Score = 5_000,
            QuerySuggestionText = usage,
            Action = _ =>
            {
                _context?.API.ChangeQuery(usage, requery: true);
                return false;
            },
        };
    }

    private bool OpenUrls(IReadOnlyList<string> urls)
    {
        if (_context is null)
        {
            return false;
        }

        try
        {
            foreach (var url in urls)
            {
                _context.API.OpenWebUrl(url);
            }

            return true;
        }
        catch (Exception exception)
        {
            _context.API.LogException(nameof(Main), "Could not open one or more bang targets.", exception);
            return false;
        }
    }

    private bool OpenSettings()
    {
        if (_context is null)
        {
            return false;
        }

        // Newer Flow builds can navigate directly to one plugin. Version 2.1.3
        // does not expose that method through its public NuGet interface yet,
        // so reflection preserves forward compatibility with a safe fallback.
        var directMethod = _context.API.GetType().GetMethod("OpenPluginSettingsWindow", [typeof(string)]);
        if (directMethod?.Invoke(_context.API, [PluginId]) is bool opened && opened)
        {
            return true;
        }

        _context.API.OpenSettingDialog();
        return true;
    }
}

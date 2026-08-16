using System.Collections.ObjectModel;
using System.IO;
using Flow.Launcher.Plugin.CustomBangs.Core;
using Flow.Launcher.Plugin.CustomBangs.Models;

namespace Flow.Launcher.Plugin.CustomBangs.Services;

public sealed class SettingsStore
{
    private readonly object _sync = new();
    private readonly PluginInitContext _context;
    private readonly PluginSettings _settings;
    private QuerySettingsSnapshot _querySnapshot;

    public SettingsStore(PluginInitContext context)
    {
        _context = context;
        _settings = context.API.LoadSettingJsonStorage<PluginSettings>();

        if (!_settings.InitializedFromCurated)
        {
            var curated = LoadCurated();
            _settings.Bangs = curated;
            _settings.InitializedFromCurated = true;
            context.API.SaveSettingJsonStorage<PluginSettings>();
        }

        _querySnapshot = CreateQuerySnapshot();
    }

    public PluginSettings Snapshot()
    {
        lock (_sync)
        {
            return _settings.Clone();
        }
    }

    public QuerySettingsSnapshot QuerySnapshot()
    {
        lock (_sync)
        {
            return _querySnapshot;
        }
    }

    public ObservableCollection<BangDefinition> LoadCurated()
    {
        var path = Path.Combine(_context.CurrentPluginMetadata.PluginDirectory, "curated-bangs.json");
        if (!File.Exists(path))
        {
            _context.API.LogWarn(nameof(SettingsStore), $"Curated bang file is missing: {path}");
            return [];
        }

        try
        {
            return BangJsonSerializer.ImportFile(path);
        }
        catch (Exception exception)
        {
            _context.API.LogException(nameof(SettingsStore), "Could not load curated-bangs.json.", exception);
            return [];
        }
    }

    public void Save(string activator, bool ignoreBangCase, IEnumerable<BangDefinition> bangs)
    {
        lock (_sync)
        {
            _settings.Activator = activator;
            _settings.IgnoreBangCase = ignoreBangCase;
            _settings.InitializedFromCurated = true;
            _settings.Bangs = new ObservableCollection<BangDefinition>(bangs.Select(static bang => bang.Clone()));
            _querySnapshot = CreateQuerySnapshot();
            _context.API.SaveSettingJsonStorage<PluginSettings>();
        }
    }

    private QuerySettingsSnapshot CreateQuerySnapshot()
    {
        var bangs = _settings.Bangs.Select(static bang => bang.Clone()).ToArray();
        return new QuerySettingsSnapshot(
            _settings.Activator,
            _settings.IgnoreBangCase,
            new BangCatalog(bangs, _settings.IgnoreBangCase));
    }
}

public sealed record QuerySettingsSnapshot(
    string Activator,
    bool IgnoreBangCase,
    BangCatalog Catalog);

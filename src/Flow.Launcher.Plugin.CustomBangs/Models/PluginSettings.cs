using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Flow.Launcher.Plugin.CustomBangs.Models;

public sealed class PluginSettings : INotifyPropertyChanged
{
    private string _activator = string.Empty;
    private bool _ignoreBangCase = true;
    private bool _initializedFromCurated;
    private ObservableCollection<BangDefinition> _bangs = [];

    public string Activator
    {
        get => _activator;
        set => SetField(ref _activator, value ?? string.Empty);
    }

    public bool IgnoreBangCase
    {
        get => _ignoreBangCase;
        set => SetField(ref _ignoreBangCase, value);
    }

    public bool InitializedFromCurated
    {
        get => _initializedFromCurated;
        set => SetField(ref _initializedFromCurated, value);
    }

    public ObservableCollection<BangDefinition> Bangs
    {
        get => _bangs;
        set => SetField(ref _bangs, value ?? []);
    }

    public PluginSettings Clone() => new()
    {
        Activator = Activator,
        IgnoreBangCase = IgnoreBangCase,
        InitializedFromCurated = InitializedFromCurated,
        Bangs = new ObservableCollection<BangDefinition>(Bangs.Select(static bang => bang.Clone())),
    };

    public event PropertyChangedEventHandler? PropertyChanged;

    private void SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

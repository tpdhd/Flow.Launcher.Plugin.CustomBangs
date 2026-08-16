using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace Flow.Launcher.Plugin.CustomBangs.Models;

public sealed class BangDefinition : INotifyPropertyChanged
{
    private string _keyword = string.Empty;
    private string? _alias;
    private string _defaultUrl = string.Empty;
    private List<string> _urls = [];
    private List<string> _groupMembers = [];
    private bool _dontEncodeQuery;

    [JsonPropertyName("keyword")]
    public string Keyword
    {
        get => _keyword;
        set => SetField(ref _keyword, value?.Trim() ?? string.Empty);
    }

    [JsonPropertyName("alias")]
    public string? Alias
    {
        get => _alias;
        set
        {
            var normalized = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
            if (SetField(ref _alias, normalized))
            {
                OnPropertyChanged(nameof(TypeLabel));
            }
        }
    }

    [JsonPropertyName("defaultUrl")]
    public string DefaultUrl
    {
        get => _defaultUrl;
        set => SetField(ref _defaultUrl, value?.Trim() ?? string.Empty);
    }

    [JsonPropertyName("urls")]
    public List<string> Urls
    {
        get => _urls;
        set
        {
            if (SetField(ref _urls, value ?? []))
            {
                OnPropertyChanged(nameof(UrlsSummary));
                OnPropertyChanged(nameof(TypeLabel));
            }
        }
    }

    [JsonPropertyName("groupMembers")]
    public List<string> GroupMembers
    {
        get => _groupMembers;
        set
        {
            if (SetField(ref _groupMembers, value ?? []))
            {
                OnPropertyChanged(nameof(GroupMembersSummary));
                OnPropertyChanged(nameof(IsGroup));
                OnPropertyChanged(nameof(TypeLabel));
            }
        }
    }

    [JsonPropertyName("dontEncodeQuery")]
    public bool DontEncodeQuery
    {
        get => _dontEncodeQuery;
        set => SetField(ref _dontEncodeQuery, value);
    }

    [JsonIgnore]
    public string UrlsSummary => Urls.Count switch
    {
        0 => "—",
        1 => Urls[0],
        _ => $"{Urls.Count} targets",
    };

    [JsonIgnore]
    public string GroupMembersSummary => string.Join(", ", GroupMembers);

    [JsonIgnore]
    public bool IsGroup => GroupMembers.Count > 0;

    [JsonIgnore]
    public string TypeLabel => Alias is not null
        ? "Alias"
        : IsGroup
            ? "Group"
            : "Bang";

    public BangDefinition Clone() => new()
    {
        Keyword = Keyword,
        Alias = Alias,
        DefaultUrl = DefaultUrl,
        Urls = [.. Urls],
        GroupMembers = [.. GroupMembers],
        DontEncodeQuery = DontEncodeQuery,
    };

    public event PropertyChangedEventHandler? PropertyChanged;

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Flow.Launcher.Plugin.CustomBangs.Models;

namespace Flow.Launcher.Plugin.CustomBangs.UI;

public partial class BangEditorDialog : Window
{
    private readonly BangEditorMode _mode;
    private readonly HashSet<string> _otherKeywords;
    private readonly HashSet<string> _groupKeywords;

    public BangEditorDialog(
        BangDefinition source,
        BangEditorMode mode,
        bool isDarkTheme,
        IEnumerable<BangDefinition> existingBangs,
        bool ignoreCase)
    {
        _mode = mode;
        var comparer = ignoreCase ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
        _otherKeywords = new HashSet<string>(
            existingBangs.Where(bang => !ReferenceEquals(bang, source)).Select(static bang => bang.Keyword),
            comparer);
        _groupKeywords = new HashSet<string>(
            existingBangs.Where(static bang => bang.IsGroup).Select(static bang => bang.Keyword),
            comparer);
        EditedBang = source.Clone();
        UrlRows = new ObservableCollection<UrlRow>(EditedBang.Urls.Select(static url => new UrlRow { Value = url }));
        while (UrlRows.Count < 1 && mode == BangEditorMode.Bang)
        {
            UrlRows.Add(new UrlRow());
        }

        InitializeComponent();
        DataContext = this;
        ApplyThemeFallbacks(isDarkTheme);
        Title = mode switch
        {
            BangEditorMode.Group => "Edit search group",
            BangEditorMode.Alias => "Edit bang alias",
            _ => "Edit custom bang",
        };
        KeywordTextBox.Text = EditedBang.Keyword;
        AliasTextBox.Text = EditedBang.Alias ?? string.Empty;
        GroupMembersTextBox.Text = string.Join(", ", EditedBang.GroupMembers);
        DefaultUrlTextBox.Text = EditedBang.DefaultUrl;
        DontEncodeCheckBox.IsChecked = EditedBang.DontEncodeQuery;

        var aliasVisibility = mode == BangEditorMode.Alias ? Visibility.Visible : Visibility.Collapsed;
        AliasLabel.Visibility = aliasVisibility;
        AliasTextBox.Visibility = aliasVisibility;
        GroupPanel.Visibility = mode == BangEditorMode.Group ? Visibility.Visible : Visibility.Collapsed;
        RegularBangPanel.Visibility = mode == BangEditorMode.Bang ? Visibility.Visible : Visibility.Collapsed;
    }

    public ObservableCollection<UrlRow> UrlRows { get; }

    public BangDefinition EditedBang { get; private set; }

    private void ApplyThemeFallbacks(bool isDarkTheme)
    {
        AddThemeFallback("PopuBGColor", isDarkTheme ? Color.FromRgb(31, 33, 38) : Color.FromRgb(250, 250, 250));
        AddThemeFallback("PopupTextColor", isDarkTheme ? Colors.White : Color.FromRgb(24, 24, 24));
        AddThemeFallback("Color00B", isDarkTheme ? Color.FromRgb(37, 39, 44) : Colors.White);
        AddThemeFallback("Color01B", isDarkTheme ? Color.FromRgb(31, 33, 38) : Color.FromRgb(247, 247, 247));
        AddThemeFallback("Color03B", isDarkTheme ? Color.FromRgb(98, 101, 109) : Color.FromRgb(165, 165, 165));
        AddThemeFallback("Color04B", isDarkTheme ? Color.FromRgb(190, 193, 201) : Color.FromRgb(76, 76, 76));
        AddThemeFallback("Color05B", isDarkTheme ? Colors.White : Color.FromRgb(20, 20, 20));
    }

    private void AddThemeFallback(string key, Color color)
    {
        if (Application.Current?.TryFindResource(key) is null)
        {
            Resources[key] = new SolidColorBrush(color);
        }
    }

    private void AddUrl_Click(object sender, RoutedEventArgs e)
    {
        var row = new UrlRow();
        UrlRows.Add(row);
        UrlsDataGrid.SelectedItem = row;
        UrlsDataGrid.ScrollIntoView(row);
        UrlsDataGrid.BeginEdit();
    }

    private void RemoveUrl_Click(object sender, RoutedEventArgs e)
    {
        if (UrlsDataGrid.SelectedItem is UrlRow selected)
        {
            UrlRows.Remove(selected);
        }
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        UrlsDataGrid.CommitEdit(DataGridEditingUnit.Cell, exitEditingMode: true);
        UrlsDataGrid.CommitEdit(DataGridEditingUnit.Row, exitEditingMode: true);

        var keyword = KeywordTextBox.Text.Trim();
        if (keyword.Length == 0 || keyword.Any(char.IsWhiteSpace))
        {
            ValidationTextBlock.Text = "The keyword is required and cannot contain whitespace.";
            return;
        }

        if (_otherKeywords.Contains(keyword))
        {
            ValidationTextBlock.Text = $"The command '{keyword}' already exists.";
            return;
        }

        if (_mode == BangEditorMode.Alias && string.IsNullOrWhiteSpace(AliasTextBox.Text))
        {
            ValidationTextBlock.Text = "An alias target is required.";
            return;
        }

        var urls = UrlRows
            .Select(static row => row.Value.Trim())
            .Where(static value => value.Length > 0)
            .ToList();

        var groupMembers = GroupMembersTextBox.Text
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(static member => member.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (_mode == BangEditorMode.Group && groupMembers.Count < 2)
        {
            ValidationTextBlock.Text = "A search group needs at least two shortcut members, separated by commas.";
            return;
        }

        if (_mode == BangEditorMode.Group)
        {
            var missing = groupMembers.FirstOrDefault(member => !_otherKeywords.Contains(member));
            if (missing is not null)
            {
                ValidationTextBlock.Text = $"Shortcut '{missing}' does not exist. Create it first, then add it to the group.";
                return;
            }

            var nested = groupMembers.FirstOrDefault(_groupKeywords.Contains);
            if (nested is not null)
            {
                ValidationTextBlock.Text = $"'{nested}' is another group. Add only regular shortcuts or aliases.";
                return;
            }
        }

        EditedBang = new BangDefinition
        {
            Keyword = keyword,
            Alias = _mode == BangEditorMode.Alias ? AliasTextBox.Text.Trim() : null,
            DefaultUrl = _mode == BangEditorMode.Bang ? DefaultUrlTextBox.Text.Trim() : string.Empty,
            Urls = _mode == BangEditorMode.Bang ? urls : [],
            GroupMembers = _mode == BangEditorMode.Group ? groupMembers : [],
            DontEncodeQuery = _mode == BangEditorMode.Bang && DontEncodeCheckBox.IsChecked == true,
        };

        DialogResult = true;
    }
}

public enum BangEditorMode
{
    Bang,
    Group,
    Alias,
}

public sealed class UrlRow : INotifyPropertyChanged
{
    private string _value = string.Empty;

    public string Value
    {
        get => _value;
        set
        {
            if (_value == value)
            {
                return;
            }

            _value = value ?? string.Empty;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Value)));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}

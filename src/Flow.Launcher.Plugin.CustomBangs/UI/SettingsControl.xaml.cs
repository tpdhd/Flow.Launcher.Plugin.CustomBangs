using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Flow.Launcher.Plugin.CustomBangs.Core;
using Flow.Launcher.Plugin.CustomBangs.Models;
using Flow.Launcher.Plugin.CustomBangs.Services;
using Microsoft.Win32;

namespace Flow.Launcher.Plugin.CustomBangs.UI;

public partial class SettingsControl : UserControl
{
    private readonly SettingsStore _settingsStore;
    private readonly PluginInitContext _context;
    private readonly DispatcherTimer _filterTimer;
    private ObservableCollection<BangDefinition> _workingBangs;
    private bool _suppressSettingChanges = true;

    public SettingsControl(SettingsStore settingsStore, PluginInitContext context)
    {
        _settingsStore = settingsStore;
        _context = context;

        InitializeComponent();

        var snapshot = settingsStore.Snapshot();
        _workingBangs = snapshot.Bangs;
        BangsView = CollectionViewSource.GetDefaultView(_workingBangs);
        BangsView.Filter = FilterBang;
        ApplyGroupFirstSort(BangsView);
        DataContext = this;
        ActivatorTextBox.Text = snapshot.Activator;
        IgnoreCaseCheckBox.IsChecked = snapshot.IgnoreBangCase;
        _filterTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(180) };
        _filterTimer.Tick += (_, _) =>
        {
            _filterTimer.Stop();
            BangsView.Refresh();
            RefreshCount();
        };
        _suppressSettingChanges = false;
        RefreshCount();
    }

    public ICollectionView BangsView { get; private set; }

    private void AddBang_Click(object sender, RoutedEventArgs e) => OpenEditor(new BangDefinition(), BangEditorMode.Bang, add: true);

    private void AddGroup_Click(object sender, RoutedEventArgs e) => OpenEditor(new BangDefinition(), BangEditorMode.Group, add: true);

    private void Edit_Click(object sender, RoutedEventArgs e)
    {
        if (BangsDataGrid.SelectedItem is BangDefinition selected)
        {
            OpenEditor(selected, GetEditorMode(selected), add: false);
        }
    }

    private void DeleteRow_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { CommandParameter: BangDefinition selected })
        {
            return;
        }

        _workingBangs.Remove(selected);
        BangsView.Refresh();
        RefreshCount();
        SaveCurrentSettings($"Deleted '{selected.Keyword}' and saved.", showSuccess: true);
    }

    private void DeleteSelected_Click(object sender, RoutedEventArgs e) => DeleteSelectedBangs();

    private void BangsDataGrid_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Delete && BangsDataGrid.SelectedItems.Count > 0)
        {
            DeleteSelectedBangs();
            e.Handled = true;
        }
    }

    private void BangsDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DeleteSelectedButton is null)
        {
            return;
        }

        var count = BangsDataGrid.SelectedItems.Count;
        DeleteSelectedButton.IsEnabled = count > 0;
        DeleteSelectedButton.Content = count > 1 ? $"Delete selected ({count})" : "Delete selected";
    }

    private void DeleteSelectedBangs()
    {
        var selected = BangsDataGrid.SelectedItems.Cast<BangDefinition>().ToArray();
        if (selected.Length == 0)
        {
            return;
        }

        var message = selected.Length == 1
            ? $"Delete '{selected[0].Keyword}'?"
            : $"Delete the {selected.Length} selected entries?";
        if (ShowConfirmation(message, "Delete entries") != MessageBoxResult.Yes)
        {
            return;
        }

        foreach (var bang in selected)
        {
            _workingBangs.Remove(bang);
        }

        BangsView.Refresh();
        RefreshCount();
        SaveCurrentSettings($"Deleted {selected.Length} selected {(selected.Length == 1 ? "entry" : "entries")} and saved.", showSuccess: true);
    }

    private void BangsDataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e) => Edit_Click(sender, e);

    private void FilterTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_filterTimer is null)
        {
            return;
        }

        _filterTimer.Stop();
        _filterTimer.Start();
    }

    private void ActivatorTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!_suppressSettingChanges)
        {
            SaveCurrentSettings("Activator saved automatically.", showSuccess: true);
        }
    }

    private void IgnoreCaseCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        if (!_suppressSettingChanges)
        {
            SaveCurrentSettings("Case preference saved automatically.", showSuccess: true);
        }
    }

    private void Import_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Import Custom Bang Search JSON",
            Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
            CheckFileExists = true,
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        if (ShowConfirmation(
                "Import replaces the entire current bang list. Continue?",
                "Replace bang list") != MessageBoxResult.Yes)
        {
            return;
        }

        try
        {
            var imported = BangJsonSerializer.ImportFile(dialog.FileName);
            var validationErrors = BangValidator.Validate(
                ActivatorTextBox.Text ?? string.Empty,
                imported,
                IgnoreCaseCheckBox.IsChecked == true);
            if (validationErrors.Count > 0)
            {
                SetStatus($"Import rejected: {validationErrors[0]}", isError: true);
                return;
            }

            ReplaceWorkingBangs(imported);
            SaveCurrentSettings($"Imported and saved {_workingBangs.Count} bangs.", showSuccess: true);
        }
        catch (Exception exception)
        {
            _context.API.LogException(nameof(SettingsControl), "Bang import failed.", exception);
            SetStatus($"Import failed: {exception.Message}", isError: true);
        }
    }

    private void Export_Click(object sender, RoutedEventArgs e)
    {
        var errors = BangValidator.Validate(
            ActivatorTextBox.Text,
            _workingBangs,
            IgnoreCaseCheckBox.IsChecked == true);
        if (errors.Count > 0)
        {
            SetStatus(errors[0], isError: true);
            return;
        }

        var dialog = new SaveFileDialog
        {
            Title = "Export Custom Bang Search JSON",
            Filter = "JSON files (*.json)|*.json",
            FileName = "custombangs.json",
            DefaultExt = ".json",
            AddExtension = true,
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        try
        {
            File.WriteAllText(dialog.FileName, BangJsonSerializer.Export(_workingBangs));
            SetStatus($"Exported {_workingBangs.Count} bangs.", isError: false);
        }
        catch (Exception exception)
        {
            _context.API.LogException(nameof(SettingsControl), "Bang export failed.", exception);
            SetStatus($"Export failed: {exception.Message}", isError: true);
        }
    }

    private void ResetCurated_Click(object sender, RoutedEventArgs e)
    {
        if (ShowConfirmation(
                "Replace the current list with the curated list bundled with this plugin?",
                "Reset bang list") != MessageBoxResult.Yes)
        {
            return;
        }

        ReplaceWorkingBangs(_settingsStore.LoadCurated());
        SaveCurrentSettings($"Loaded and saved {_workingBangs.Count} curated bangs.", showSuccess: true);
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        SaveCurrentSettings($"Saved {_workingBangs.Count} bangs.", showSuccess: true);
    }

    private bool SaveCurrentSettings(string successMessage, bool showSuccess)
    {
        var activator = ActivatorTextBox.Text ?? string.Empty;
        var ignoreCase = IgnoreCaseCheckBox.IsChecked == true;
        var errors = BangValidator.Validate(activator, _workingBangs, ignoreCase);
        if (errors.Count > 0)
        {
            SetStatus(string.Join(Environment.NewLine, errors.Take(3)), isError: true);
            return false;
        }

        try
        {
            _settingsStore.Save(activator, ignoreCase, _workingBangs);
            if (showSuccess)
            {
                SetStatus(successMessage, isError: false);
            }

            return true;
        }
        catch (Exception exception)
        {
            _context.API.LogException(nameof(SettingsControl), "Could not save settings.", exception);
            SetStatus($"Save failed: {exception.Message}", isError: true);
            return false;
        }
    }

    private void OpenEditor(BangDefinition source, BangEditorMode mode, bool add)
    {
        var dialog = new BangEditorDialog(
            source,
            mode,
            _context.API.IsApplicationDarkTheme(),
            _workingBangs,
            IgnoreCaseCheckBox.IsChecked == true)
        {
            Owner = Window.GetWindow(this),
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        if (add)
        {
            _workingBangs.Add(dialog.EditedBang);
        }
        else
        {
            var index = _workingBangs.IndexOf(source);
            if (index >= 0)
            {
                _workingBangs[index] = dialog.EditedBang;
            }
        }

        BangsView.Refresh();
        RefreshCount();
        SaveCurrentSettings(add ? "Bang added and saved." : "Bang updated and saved.", showSuccess: true);
    }

    private static BangEditorMode GetEditorMode(BangDefinition bang) => bang.Alias is not null
        ? BangEditorMode.Alias
        : bang.IsGroup
            ? BangEditorMode.Group
            : BangEditorMode.Bang;

    private bool FilterBang(object item)
    {
        if (item is not BangDefinition bang)
        {
            return false;
        }

        var filter = FilterTextBox?.Text?.Trim();
        if (string.IsNullOrEmpty(filter))
        {
            return true;
        }

        return bang.Keyword.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
               (bang.Alias?.Contains(filter, StringComparison.OrdinalIgnoreCase) ?? false) ||
               bang.GroupMembers.Any(member => member.Contains(filter, StringComparison.OrdinalIgnoreCase)) ||
               bang.DefaultUrl.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
               bang.Urls.Any(url => url.Contains(filter, StringComparison.OrdinalIgnoreCase));
    }

    private void ReplaceWorkingBangs(ObservableCollection<BangDefinition> replacement)
    {
        _workingBangs = replacement;
        BangsView = CollectionViewSource.GetDefaultView(_workingBangs);
        BangsView.Filter = FilterBang;
        ApplyGroupFirstSort(BangsView);
        BangsDataGrid.ItemsSource = BangsView;
        RefreshCount();
    }

    private static void ApplyGroupFirstSort(ICollectionView view)
    {
        view.SortDescriptions.Clear();
        view.SortDescriptions.Add(new SortDescription(nameof(BangDefinition.IsGroup), ListSortDirection.Descending));
        view.SortDescriptions.Add(new SortDescription(nameof(BangDefinition.Keyword), ListSortDirection.Ascending));
    }

    private void RefreshCount()
    {
        if (CountTextBlock is null || BangsView is null)
        {
            return;
        }

        var visible = BangsView.Cast<object>().Count();
        CountTextBlock.Text = visible == _workingBangs.Count
            ? $"{_workingBangs.Count} bangs"
            : $"{visible} of {_workingBangs.Count} bangs";
    }

    private void SetStatus(string message, bool isError)
    {
        StatusTextBlock.Text = message;
        StatusTextBlock.Foreground = isError ? Brushes.IndianRed : Brushes.SeaGreen;
    }

    private MessageBoxResult ShowConfirmation(string message, string caption)
    {
        var owner = Window.GetWindow(this);
        return owner is null
            ? MessageBox.Show(message, caption, MessageBoxButton.YesNo, MessageBoxImage.Question)
            : MessageBox.Show(owner, message, caption, MessageBoxButton.YesNo, MessageBoxImage.Question);
    }
}

using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using JustConvert.Core;
using JustConvert.Core.Windows;
using Microsoft.Win32;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace JustConvert.Cli.UI;

public partial class SettingsWindow : FluentWindow
{
    private AppSettings _settings;
    private readonly ConverterRegistry _registry = new();

    private static readonly string[] AllVideoFormats =
    [
        "mp4-h264", "mp4-h264-nvenc", "mp4-h264-qsv", "mp4-h264-amf",
        "mp4-h265", "mp4-h265-nvenc", "mp4-h265-qsv", "mp4-h265-amf",
        "webm-vp9", "webm-vp9-qsv",
        "webm-av1", "webm-av1-nvenc", "webm-av1-qsv", "webm-av1-amf",
        "mov-prores422", "mov-prores4444",
        "remux-mp4", "remux-mkv",
        "frames",
        "mp3", "wav", "flac", "aac",
        "reencode"
    ];

    private static readonly string[] AllAudioFormats =
    [
        "mp3", "aac", "m4a", "wav", "flac", "ogg", "reencode"
    ];

    private static readonly string[] AllImageFormats =
    [
        "png", "jpg", "webp", "ico", "bmp", "gif", "jp2", "tiff", "tga", "pcx", "ppm", "avif", "reencode"
    ];

    public SettingsWindow()
    {
        InitializeComponent();

        ApplicationThemeManager.ApplySystemTheme();
        ApplicationAccentColorManager.ApplySystemAccent();
        ApplicationThemeManager.Apply(this);
        SystemThemeWatcher.Watch(this);

        _settings = AppSettings.Load();
        RefreshProfilesList();
    }

    private void RefreshProfilesList()
    {
        CmbProfiles.ItemsSource = null;
        CmbProfiles.ItemsSource = _settings.Profiles;
        CmbProfiles.SelectedItem = _settings.GetActiveProfile();
    }

    private void OnProfileSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (CmbProfiles.SelectedItem is not MenuProfile profile) return;

        _settings.ActiveProfileId = profile.Id;

        var isReadOnly = profile.IsReadOnly;
        BtnRename.IsEnabled = !isReadOnly;
        BtnDelete.IsEnabled = !isReadOnly;
        TxtProfileNotice.Text = isReadOnly ? I18n.T("ProfileReadOnlyNotice") : I18n.T("ProfileCustomNotice");

        PopulateCategoryPanels(profile);
    }

    private void PopulateCategoryPanels(MenuProfile profile)
    {
        PopulateFormatList(PanelVideoFormats, AllVideoFormats, profile.VideoFormats, profile.IsReadOnly);
        PopulateFormatList(PanelAudioFormats, AllAudioFormats, profile.AudioFormats, profile.IsReadOnly);
        PopulateFormatList(PanelImageFormats, AllImageFormats, profile.ImageFormats, profile.IsReadOnly);
    }

    private void PopulateFormatList(StackPanel targetPanel, string[] allFormats, List<string> activeFormats, bool isReadOnly)
    {
        targetPanel.Children.Clear();

        var available = ClassicContextMenuManager.FilterAvailableFormats(allFormats);

        foreach (var format in available)
        {
            var checkBox = new System.Windows.Controls.CheckBox
            {
                Content = I18n.GetSubMenuTitle(format),
                IsChecked = activeFormats.Contains(format, StringComparer.OrdinalIgnoreCase),
                IsEnabled = !isReadOnly,
                Margin = new Thickness(0, 4, 0, 4),
                Tag = format
            };

            checkBox.Checked += (s, _) =>
            {
                if (!isReadOnly && !activeFormats.Contains(format, StringComparer.OrdinalIgnoreCase))
                {
                    activeFormats.Add(format);
                }
            };

            checkBox.Unchecked += (s, _) =>
            {
                if (!isReadOnly)
                {
                    activeFormats.RemoveAll(f => string.Equals(f, format, StringComparison.OrdinalIgnoreCase));
                }
            };

            targetPanel.Children.Add(checkBox);
        }
    }

    private void OnCategoryTabChanged(object sender, SelectionChangedEventArgs e)
    {
    }

    private void OnDuplicateProfileClick(object sender, RoutedEventArgs e)
    {
        if (CmbProfiles.SelectedItem is not MenuProfile current) return;

        var defaultName = $"{current.Name} ({I18n.T("ProfileCopySuffix")})";
        var newName = InputDialog.Show(this, I18n.T("PromptNewProfileName"), I18n.T("SettingsTitle"), defaultName);
        if (string.IsNullOrWhiteSpace(newName)) return;

        var clone = current.Clone(newName.Trim());

        _settings.Profiles.Add(clone);
        _settings.ActiveProfileId = clone.Id;
        _settings.Save();

        RefreshProfilesList();
    }

    private void OnRenameProfileClick(object sender, RoutedEventArgs e)
    {
        if (CmbProfiles.SelectedItem is not MenuProfile current || current.IsReadOnly) return;

        var result = InputDialog.Show(this, I18n.T("PromptRenameProfile"), I18n.T("SettingsTitle"), current.Name);
        if (!string.IsNullOrWhiteSpace(result) && result != current.Name)
        {
            current.Name = result.Trim();
            _settings.Save();
            RefreshProfilesList();
        }
    }

    private void OnDeleteProfileClick(object sender, RoutedEventArgs e)
    {
        if (CmbProfiles.SelectedItem is not MenuProfile current || current.IsReadOnly) return;

        var confirm = System.Windows.MessageBox.Show(
            string.Format(I18n.T("ConfirmDeleteProfile"), current.Name),
            I18n.T("SettingsTitle"),
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Question);

        if (confirm == System.Windows.MessageBoxResult.Yes)
        {
            _settings.Profiles.Remove(current);
            _settings.ActiveProfileId = "default";
            _settings.Save();
            RefreshProfilesList();
        }
    }

    private void OnExportClick(object sender, RoutedEventArgs e)
    {
        var sfd = new SaveFileDialog
        {
            Filter = "JSON Files (*.json)|*.json|All Files (*.*)|*.*",
            FileName = "JustConvert-Settings.json",
            Title = I18n.T("BtnExport")
        };

        if (sfd.ShowDialog() == true)
        {
            try
            {
                _settings.Export(sfd.FileName);
                TxtApplyStatus.Text = I18n.T("SettingsExportSuccess");
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(ex.Message, I18n.T("TitleError"), System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }
    }

    private void OnImportClick(object sender, RoutedEventArgs e)
    {
        var ofd = new OpenFileDialog
        {
            Filter = "JSON Files (*.json)|*.json|All Files (*.*)|*.*",
            Title = I18n.T("BtnImport")
        };

        if (ofd.ShowDialog() == true)
        {
            try
            {
                var imported = AppSettings.Import(ofd.FileName);
                _settings = imported;
                _settings.Save();
                RefreshProfilesList();
                ApplySettingsToSystem();
                TxtApplyStatus.Text = I18n.T("SettingsImportSuccess");
            }
            catch
            {
                System.Windows.MessageBox.Show(I18n.T("SettingsImportError"), I18n.T("TitleError"), System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }
    }

    private void OnApplyClick(object sender, RoutedEventArgs e)
    {
        _settings.Save();
        ApplySettingsToSystem();
        TxtApplyStatus.Text = I18n.T("SettingsAppliedSuccess");
    }

    private void ApplySettingsToSystem()
    {
        try
        {
            var exePath = Environment.ProcessPath ?? Path.Combine(AppContext.BaseDirectory, "just-convert.exe");
            new ClassicContextMenuManager(_registry).Register(exePath);
        }
        catch { }
    }

    private void OnScrollViewerPreviewMouseWheel(object sender, System.Windows.Input.MouseWheelEventArgs e)
    {
        if (sender is not ScrollViewer sv || sv.ScrollableHeight <= 0) return;

        var offset = sv.VerticalOffset - (e.Delta * 0.25);
        sv.ScrollToVerticalOffset(Math.Clamp(offset, 0, sv.ScrollableHeight));
        e.Handled = true;
    }

    private void OnCloseClick(object sender, RoutedEventArgs e)
    {
        Close();
    }
}

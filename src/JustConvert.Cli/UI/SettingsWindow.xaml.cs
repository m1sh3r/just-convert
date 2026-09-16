using System.Collections.ObjectModel;
using System.ComponentModel;
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
        "mp4", "webm", "mkv", "mov",
        "remux-mp4", "remux-mkv",
        "frames", "gif",
        "mp3", "wav", "flac", "aac", "m4a", "opus",
        "reencode"
    ];

    private static readonly string[] AllAudioFormats =
    [
        "mp3", "aac", "m4a", "wav", "flac", "ogg", "opus", "aiff", "reencode"
    ];

    private static readonly string[] AllImageFormats =
    [
        "png", "jpg", "webp", "ico", "bmp", "gif", "jp2", "tiff", "tga", "pcx", "ppm", "avif", "reencode"
    ];

    internal AppSettings CurrentSettings => _settings;

    public SettingsWindow() : this(AppSettings.Load())
    {
    }

    internal SettingsWindow(AppSettings settings)
    {
        _settings = settings;
        _settings.EnsureDefaultProfile();
        _suppressQualityEvents = true;

        InitializeComponent();

        Title = I18n.T("SettingsTitle");
        AppTitleBar.Title = Title;

        if (!DesignerProperties.GetIsInDesignMode(this))
        {
            ApplicationThemeManager.ApplySystemTheme();
            ApplicationAccentColorManager.ApplySystemAccent();
            ApplicationThemeManager.Apply(this);
            SystemThemeWatcher.Watch(this);

            RefreshProfilesList();
            RefreshQualityControls();
            RefreshPresetsList();
            _suppressQualityEvents = false;
        }
        else
        {
            _settings = new AppSettings();
            PopulateFormatList(PanelVideoFormats, ["mp4", "webm", "mkv"], ["mp4"], false);
            PopulateFormatList(PanelAudioFormats, ["mp3", "wav", "flac"], ["mp3"], false);
            PopulateFormatList(PanelImageFormats, ["png", "jpg", "webp"], ["png"], false);
            TxtQualityStatusJpg.Text = string.Format(I18n.T("QualityStatusRemembered"), 92);
            TxtQualityStatusWebp.Text = I18n.T("QualityStatusAsk");
            TxtQualityStatusAvif.Text = I18n.T("QualityStatusAsk");
            TxtQualityStatusJp2.Text = I18n.T("QualityStatusAsk");
            BtnResetQualityJpg.Content = I18n.T("BtnResetQuality");
            BtnResetQualityWebp.Content = I18n.T("BtnResetQuality");
            BtnResetQualityAvif.Content = I18n.T("BtnResetQuality");
            BtnResetQualityJp2.Content = I18n.T("BtnResetQuality");
            BtnCreatePreset.Content = I18n.T("BtnCreatePreset");
            BtnEditPreset.Content = I18n.T("BtnEditPreset");
            BtnDeletePreset.Content = I18n.T("BtnDeletePreset");
            ListPresets.ItemsSource = new List<CustomPreset>
            {
                new() { Name = "YouTube 1080p (MP4)", Category = "video", ContainerFormat = "mp4" },
                new() { Name = "Podcast Audio (MP3)", Category = "audio", ContainerFormat = "mp3" }
            };
        }
    }

    private void RefreshProfilesList()
    {
        CmbProfiles.ItemsSource = null;
        CmbProfiles.ItemsSource = _settings.Profiles;
        CmbProfiles.SelectedItem = _settings.GetActiveProfile();
    }

    private void OnProfileSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_settings == null || CmbProfiles.SelectedItem is not MenuProfile profile) return;

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
                RefreshQualityControls();
                ApplySettingsToSystem();
                TxtApplyStatus.Text = I18n.T("SettingsImportSuccess");
            }
            catch
            {
                System.Windows.MessageBox.Show(I18n.T("SettingsImportError"), I18n.T("TitleError"), System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }
    }

    private bool _suppressQualityEvents;

    private void RefreshQualityControls()
    {
        _suppressQualityEvents = true;
        try
        {
            ChkAppendQualitySuffix.IsChecked = _settings.AppendQualitySuffix;
            UpdateQualityRow("jpg", SliderQualityJpg, TxtQualityStatusJpg);
            UpdateQualityRow("webp", SliderQualityWebp, TxtQualityStatusWebp);
            UpdateQualityRow("avif", SliderQualityAvif, TxtQualityStatusAvif);
            UpdateQualityRow("jp2", SliderQualityJp2, TxtQualityStatusJp2);
            RefreshRemuxControls();
        }
        finally
        {
            _suppressQualityEvents = false;
        }
    }

    private void RefreshRemuxControls()
    {
        var remux = _settings.GetEffectiveRemuxSetting();
        if (CmbSettingsRemuxContainer.Items.Count == 0)
        {
            CmbSettingsRemuxContainer.Items.Add("mp4");
            CmbSettingsRemuxContainer.Items.Add("mkv");
            CmbSettingsRemuxContainer.Items.Add("mov");
            CmbSettingsRemuxContainer.Items.Add("webm");
        }

        CmbSettingsRemuxContainer.SelectedItem = remux.TargetContainer;
        ChkSettingsRemuxFastStart.IsChecked = remux.FastStart;
        ChkSettingsRemuxSubtitles.IsChecked = remux.CopySubtitles;

        TxtRemuxStatus.Text = remux.IsRemembered
            ? string.Format(I18n.T("RemuxStatusRemembered"), remux.TargetContainer.ToUpperInvariant())
            : I18n.T("QualityStatusAsk");
    }

    private void OnResetRemuxClick(object sender, RoutedEventArgs e)
    {
        _settings.ResetRemuxSetting();
        _suppressQualityEvents = true;
        try
        {
            RefreshRemuxControls();
        }
        finally
        {
            _suppressQualityEvents = false;
        }
    }

    private void OnSettingsRemuxOptionChanged(object sender, RoutedEventArgs e)
    {
        if (_suppressQualityEvents) return;
        var remux = _settings.GetEffectiveRemuxSetting();
        if (CmbSettingsRemuxContainer.SelectedItem is string c)
        {
            remux.TargetContainer = c;
        }
        remux.FastStart = ChkSettingsRemuxFastStart.IsChecked == true;
        remux.CopySubtitles = ChkSettingsRemuxSubtitles.IsChecked == true;
        _settings.SetRemuxSetting(remux);
    }

    private void UpdateQualityRow(string format, Slider slider, System.Windows.Controls.TextBlock statusBlock)
    {
        var isSaved = _settings.TryGetSavedQuality(format, out var quality);
        slider.Value = quality;
        statusBlock.Text = isSaved
            ? string.Format(I18n.T("QualityStatusRemembered"), quality)
            : I18n.T("QualityStatusAsk");
    }

    private void OnQualitySliderValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_suppressQualityEvents || sender is not Slider slider || slider.Tag is not string format) return;

        var val = (int)e.NewValue;
        _settings.SetQuality(format, val, true);

        var statusBlock = GetQualityStatusBlock(format);
        if (statusBlock != null)
        {
            statusBlock.Text = string.Format(I18n.T("QualityStatusRemembered"), val);
        }
    }

    private void OnResetQualityClick(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement el || el.Tag is not string format) return;

        _settings.ResetQuality(format);
        var defaultVal = AppSettings.GetDefaultQuality(format);

        _suppressQualityEvents = true;
        try
        {
            var slider = GetQualitySlider(format);
            if (slider != null) slider.Value = defaultVal;

            var statusBlock = GetQualityStatusBlock(format);
            if (statusBlock != null) statusBlock.Text = I18n.T("QualityStatusAsk");
        }
        finally
        {
            _suppressQualityEvents = false;
        }
    }

    private void OnAppendQualitySuffixChanged(object sender, RoutedEventArgs e)
    {
        if (_suppressQualityEvents) return;
        _settings.AppendQualitySuffix = ChkAppendQualitySuffix.IsChecked == true;
    }

    private Slider? GetQualitySlider(string format) => format switch
    {
        "jpg" => SliderQualityJpg,
        "webp" => SliderQualityWebp,
        "avif" => SliderQualityAvif,
        "jp2" => SliderQualityJp2,
        _ => null
    };

    private System.Windows.Controls.TextBlock? GetQualityStatusBlock(string format) => format switch
    {
        "jpg" => TxtQualityStatusJpg,
        "webp" => TxtQualityStatusWebp,
        "avif" => TxtQualityStatusAvif,
        "jp2" => TxtQualityStatusJp2,
        _ => null
    };

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

    private void RefreshPresetsList()
    {
        ListPresets.ItemsSource = null;
        ListPresets.ItemsSource = _settings.CustomPresets;
        UpdatePresetButtonStates();
    }

    private void UpdatePresetButtonStates()
    {
        var hasSelection = ListPresets.SelectedItem is CustomPreset;
        BtnEditPreset.IsEnabled = hasSelection;
        BtnDeletePreset.IsEnabled = hasSelection;
    }

    private void OnPresetSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdatePresetButtonStates();
    }

    private void OnPresetDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (ListPresets.SelectedItem is CustomPreset)
        {
            OnEditPresetClick(sender, e);
        }
    }

    private void OnCreatePresetClick(object sender, RoutedEventArgs e)
    {
        var dialog = new PresetEditorDialog(null) { Owner = this };
        if (dialog.ShowDialog() == true)
        {
            _settings.AddPreset(dialog.Preset);
            _settings.Save();
            RefreshPresetsList();
        }
    }

    private void OnEditPresetClick(object sender, RoutedEventArgs e)
    {
        if (ListPresets.SelectedItem is not CustomPreset selected) return;

        var dialog = new PresetEditorDialog(selected) { Owner = this };
        if (dialog.ShowDialog() == true)
        {
            _settings.UpdatePreset(dialog.Preset);
            _settings.Save();
            RefreshPresetsList();
        }
    }

    private void OnDeletePresetClick(object sender, RoutedEventArgs e)
    {
        if (ListPresets.SelectedItem is not CustomPreset selected) return;

        var msg = string.Format(I18n.T("ConfirmDeletePreset"), selected.Name);
        var result = System.Windows.MessageBox.Show(this, msg, I18n.T("BtnDeletePreset"), System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Question);
        if (result == System.Windows.MessageBoxResult.Yes)
        {
            _settings.DeletePreset(selected.Id);
            _settings.Save();
            RefreshPresetsList();
        }
    }

    private void OnCloseClick(object sender, RoutedEventArgs e)
    {
        Close();
    }
}

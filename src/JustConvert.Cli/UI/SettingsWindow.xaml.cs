using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
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

    public Wpf.Ui.Controls.MenuItem BtnRename => MenuRename;
    public Wpf.Ui.Controls.MenuItem BtnDelete => MenuDelete;
    public Wpf.Ui.Controls.MenuItem BtnResetDefaults => MenuResetDefaults;
    public Wpf.Ui.Controls.MenuItem BtnDuplicate => MenuDuplicate;
    public Wpf.Ui.Controls.MenuItem BtnExport => MenuExport;
    public Wpf.Ui.Controls.MenuItem BtnImport => MenuImport;

    public SettingsWindow() : this(AppSettings.Load())
    {
    }

    internal SettingsWindow(AppSettings settings)
    {
        _settings = settings;
        _settings.EnsureDefaultProfile();

        InitializeComponent();

        CmbProfiles.ContextMenu = BtnProfileMenu.ContextMenu;

        Title = I18n.T("SettingsTitle");
        AppTitleBar.Title = Title;

        if (!DesignerProperties.GetIsInDesignMode(this))
        {
            ApplicationThemeManager.ApplySystemTheme();
            ApplicationAccentColorManager.ApplySystemAccent();
            ApplicationThemeManager.Apply(this);
            SystemThemeWatcher.Watch(this);

            RefreshProfilesList();
            ChkAppendQualitySuffix.IsChecked = _settings.AppendQualitySuffix;
        }
        else
        {
            _settings = new AppSettings();
            PopulateFormatList(PanelVideoFormats, "video", ["mp4", "webm", "mkv"], ["mp4"]);
            PopulateFormatList(PanelAudioFormats, "audio", ["mp3", "wav", "flac"], ["mp3"]);
            PopulateFormatList(PanelImageFormats, "image", ["png", "jpg", "webp"], ["png"]);
            MenuResetDefaults.Header = I18n.T("BtnResetDefaults");
        }
    }

    private void RefreshProfilesList()
    {
        CmbProfiles.ItemsSource = null;
        CmbProfiles.ItemsSource = _settings.Profiles;
        CmbProfiles.SelectedItem = _settings.GetActiveProfile();
    }

    private void OnProfileMenuClick(object sender, RoutedEventArgs e)
    {
        if (BtnProfileMenu.ContextMenu != null)
        {
            BtnProfileMenu.ContextMenu.PlacementTarget = BtnProfileMenu;
            BtnProfileMenu.ContextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
            BtnProfileMenu.ContextMenu.IsOpen = true;
        }
    }

    private void OnProfileSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_settings == null || CmbProfiles.SelectedItem is not MenuProfile profile) return;

        _settings.ActiveProfileId = profile.Id;

        var isDefault = profile.Id == "default";
        MenuRename.IsEnabled = !isDefault;
        MenuDelete.IsEnabled = !isDefault && _settings.Profiles.Count > 1;
        PopulateCategoryPanels(profile);
    }

    private void PopulateCategoryPanels(MenuProfile profile)
    {
        PopulateFormatList(PanelVideoFormats, "video", AllVideoFormats, profile.VideoFormats);
        PopulateFormatList(PanelAudioFormats, "audio", AllAudioFormats, profile.AudioFormats);
        PopulateFormatList(PanelImageFormats, "image", AllImageFormats, profile.ImageFormats);
    }

    private void PopulateFormatList(Panel targetPanel, string category, string[] allFormats, List<string> activeFormats)
    {
        targetPanel.Children.Clear();

        var available = ClassicContextMenuManager.FilterAvailableFormats(allFormats);

        foreach (var format in available)
        {
            var isConfigurable = HasConfigurableSettings(category, format);

            var row = new Grid
            {
                Margin = new Thickness(0, 3, 8, 3)
            };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var contentStack = new StackPanel
            {
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 8, 0)
            };

            var checkBox = new System.Windows.Controls.CheckBox
            {
                Content = I18n.GetSubMenuTitle(format),
                IsChecked = activeFormats.Contains(format, StringComparer.OrdinalIgnoreCase),
                Tag = format
            };

            checkBox.Checked += (s, _) =>
            {
                if (!activeFormats.Contains(format, StringComparer.OrdinalIgnoreCase))
                {
                    activeFormats.Add(format);
                }
            };

            checkBox.Unchecked += (s, _) =>
            {
                activeFormats.RemoveAll(f => string.Equals(f, format, StringComparison.OrdinalIgnoreCase));
            };

            contentStack.Children.Add(checkBox);

            System.Windows.Controls.TextBlock? statusText = null;
            if (isConfigurable)
            {
                statusText = new System.Windows.Controls.TextBlock
                {
                    Text = GetFormatStatus(category, format),
                    FontSize = 11,
                    Foreground = (TryFindResource("TextFillColorSecondaryBrush") as System.Windows.Media.Brush) ?? System.Windows.Media.Brushes.Gray,
                    Margin = new Thickness(26, 2, 0, 0),
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    Tag = format
                };
                contentStack.Children.Add(statusText);
            }

            Grid.SetColumn(contentStack, 0);
            row.Children.Add(contentStack);

            if (isConfigurable)
            {
                var editButton = new Wpf.Ui.Controls.Button
                {
                    Icon = new Wpf.Ui.Controls.SymbolIcon { Symbol = Wpf.Ui.Controls.SymbolRegular.Edit20 },
                    ToolTip = I18n.T("BtnEditFormatSettings"),
                    Padding = new Thickness(6, 4, 6, 4),
                    VerticalAlignment = VerticalAlignment.Center,
                    Tag = format
                };
                editButton.Click += (s, _) =>
                {
                    OnEditFormatSettings(category, format, statusText);
                };
                Grid.SetColumn(editButton, 1);
                row.Children.Add(editButton);
            }

            targetPanel.Children.Add(row);
        }
    }

    private void OnNavItemClick(object sender, RoutedEventArgs e)
    {
        if (sender is NavigationViewItem item && int.TryParse(item.Tag?.ToString(), out var index))
        {
            TabsCategory.SelectedIndex = index;
        }
    }

    private void OnNavViewSelectionChanged(NavigationView sender, RoutedEventArgs args)
    {
        if (TabsCategory == null) return;
        if (sender.SelectedItem is NavigationViewItem item && int.TryParse(item.Tag?.ToString(), out var index))
        {
            if (TabsCategory.SelectedIndex != index)
            {
                TabsCategory.SelectedIndex = index;
            }
        }
    }

    private void OnCategoryTabChanged(object sender, SelectionChangedEventArgs e)
    {
        if (e.Source != TabsCategory) return;
        if (NavView?.MenuItems == null) return;
        var index = TabsCategory.SelectedIndex;
        for (var i = 0; i < NavView.MenuItems.Count; i++)
        {
            if (NavView.MenuItems[i] is NavigationViewItem item)
            {
                item.IsActive = (i == index);
            }
        }
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
        if (CmbProfiles.SelectedItem is not MenuProfile current || current.Id == "default") return;

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
        if (CmbProfiles.SelectedItem is not MenuProfile current || current.Id == "default" || _settings.Profiles.Count <= 1) return;

        var message = string.Format(I18n.T("ConfirmDeleteProfile"), current.Name);
        if (MessageDialog.ShowConfirm(this, message, I18n.T("SettingsTitle")))
        {
            _settings.Profiles.Remove(current);
            _settings.ActiveProfileId = "default";
            _settings.Save();
            RefreshProfilesList();
        }
    }

    private void OnResetDefaultsClick(object sender, RoutedEventArgs e)
    {
        if (CmbProfiles.SelectedItem is not MenuProfile current) return;

        if (!MessageDialog.ShowConfirm(this, I18n.T("ConfirmResetDefaults"), I18n.T("SettingsTitle"))) return;

        ResetProfileToDefaults(current);
    }

    internal void ResetProfileToDefaults(MenuProfile profile)
    {
        var factoryDefault = AppSettings.CreateDefaultProfile();
        profile.VideoFormats = [.. factoryDefault.VideoFormats];
        profile.AudioFormats = [.. factoryDefault.AudioFormats];
        profile.ImageFormats = [.. factoryDefault.ImageFormats];
        _settings.Save();
        PopulateCategoryPanels(profile);
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
                MessageDialog.ShowError(this, ex.Message, I18n.T("TitleError"));
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
                ChkAppendQualitySuffix.IsChecked = _settings.AppendQualitySuffix;
                ApplySettingsToSystem();
                TxtApplyStatus.Text = I18n.T("SettingsImportSuccess");
            }
            catch
            {
                MessageDialog.ShowError(this, I18n.T("SettingsImportError"), I18n.T("TitleError"));
            }
        }
    }

    private void OnAppendQualitySuffixChanged(object sender, RoutedEventArgs e)
    {
        _settings.AppendQualitySuffix = ChkAppendQualitySuffix.IsChecked == true;
    }

    private static bool HasConfigurableSettings(string category, string format)
    {
        var fmt = format.TrimStart('.').ToLowerInvariant();
        if (category == "video")
        {
            return fmt is "mp4" or "webm" or "mkv" or "mov" or "reencode" or "frames" or "mp3" or "aac" or "m4a" or "opus";
        }
        if (category == "audio")
        {
            return fmt is "mp3" or "aac" or "m4a" or "ogg" or "opus" or "reencode";
        }
        if (category == "image")
        {
            return AppSettings.SupportsQuality(fmt);
        }
        return false;
    }

    private static string GetCodecDisplayName(string codec) => codec.ToLowerInvariant() switch
    {
        "h264" => I18n.T("CodecH264"),
        "h265" => I18n.T("CodecH265"),
        "vp9" => I18n.T("CodecVp9"),
        "av1" => I18n.T("CodecAv1"),
        "prores422" => I18n.T("CodecProRes"),
        "copy" => I18n.T("CodecCopy"),
        _ => codec.ToUpperInvariant()
    };

    internal string GetFormatStatus(string category, string format)
    {
        var fmt = format.TrimStart('.').ToLowerInvariant();

        if (category == "video")
        {
            if (fmt == "frames")
            {
                return _settings.FramesSetting.IsRemembered
                    ? string.Format(I18n.T("FramesStatusRemembered"), _settings.FramesSetting.ImageFormat.ToUpperInvariant())
                    : I18n.T("QualityStatusAsk");
            }
            if (fmt is "mp3" or "aac" or "m4a" or "opus")
            {
                if (_settings.TryGetSavedAudioQuality(fmt, out var bitrate))
                {
                    return bitrate <= 0
                        ? I18n.T("QualityStatusAudioAuto")
                        : string.Format(I18n.T("QualityStatusAudio"), bitrate);
                }
                return I18n.T("QualityStatusAsk");
            }
            if (_settings.TryGetSavedVideoQuality(fmt, out var vq))
            {
                var codecName = GetCodecDisplayName(vq.VideoCodec);
                if (string.Equals(vq.VideoCodec, "copy", StringComparison.OrdinalIgnoreCase))
                {
                    return string.Format(I18n.T("QualityStatusVideoCopy"), codecName);
                }
                if (string.Equals(vq.RateControl, "cbr", StringComparison.OrdinalIgnoreCase))
                {
                    return string.Format(I18n.T("QualityStatusVideoCbr"), codecName, vq.VideoBitrateKbps);
                }
                if (string.Equals(vq.RateControl, "vbr", StringComparison.OrdinalIgnoreCase))
                {
                    return string.Format(I18n.T("QualityStatusVideoVbr"), codecName, vq.VideoBitrateKbps);
                }
                return string.Format(I18n.T("QualityStatusVideo"), codecName, vq.VideoQualityCq);
            }
            return I18n.T("QualityStatusAsk");
        }

        if (category == "audio")
        {
            if (_settings.TryGetSavedAudioQuality(fmt, out var bitrate))
            {
                return bitrate <= 0
                    ? I18n.T("QualityStatusAudioAuto")
                    : string.Format(I18n.T("QualityStatusAudio"), bitrate);
            }
            return I18n.T("QualityStatusAsk");
        }

        if (category == "image")
        {
            if (_settings.TryGetSavedQuality(fmt, out var quality))
            {
                return string.Format(I18n.T("QualityStatusRemembered"), quality);
            }
            return I18n.T("QualityStatusAsk");
        }

        return string.Empty;
    }

    private void OnEditFormatSettings(string category, string format, System.Windows.Controls.TextBlock? statusBlock)
    {
        var fmt = format.TrimStart('.').ToLowerInvariant();
        ConversionOptionsDialog dialog;

        if (category == "video")
        {
            if (fmt == "frames")
            {
                dialog = new ConversionOptionsDialog("frames", "frames", _settings.GetEffectiveFramesSetting(), null, _settings.AppendQualitySuffix, isSettingsMode: true);
            }
            else if (fmt is "mp3" or "aac" or "m4a" or "opus")
            {
                dialog = new ConversionOptionsDialog(fmt, "audio", _settings.GetEffectiveAudioQuality(fmt), null, _settings.AppendQualitySuffix, isSettingsMode: true);
            }
            else
            {
                dialog = new ConversionOptionsDialog(fmt, "video", _settings.GetEffectiveVideoQuality(fmt), null, _settings.AppendQualitySuffix, isSettingsMode: true);
            }
        }
        else if (category == "audio")
        {
            dialog = new ConversionOptionsDialog(fmt, "audio", _settings.GetEffectiveAudioQuality(fmt), null, _settings.AppendQualitySuffix, isSettingsMode: true);
        }
        else
        {
            dialog = new ConversionOptionsDialog(fmt, "image", _settings.GetEffectiveQuality(fmt), null, _settings.AppendQualitySuffix, isSettingsMode: true);
        }

        dialog.Owner = this;
        if (dialog.ShowDialog() == true)
        {
            ApplyDialogResult(category, fmt, dialog);
            if (statusBlock != null)
            {
                statusBlock.Text = GetFormatStatus(category, fmt);
            }
        }
    }

    internal void ApplyDialogResult(string category, string fmt, ConversionOptionsDialog dialog)
    {
        if (category == "video")
        {
            if (fmt == "frames")
            {
                if (dialog.IsResetRequested || !dialog.RememberChoice)
                {
                    _settings.ResetFramesSetting();
                }
                else
                {
                    var s = dialog.SelectedFramesSetting;
                    s.IsRemembered = true;
                    _settings.SetFramesSetting(s);
                }
            }
            else if (fmt is "mp3" or "aac" or "m4a" or "opus")
            {
                if (dialog.IsResetRequested || !dialog.RememberChoice)
                {
                    _settings.ResetAudioQuality(fmt);
                }
                else
                {
                    _settings.SetAudioQuality(fmt, dialog.SelectedAudioBitrate, true);
                }
            }
            else
            {
                if (dialog.IsResetRequested || !dialog.RememberChoice)
                {
                    _settings.ResetVideoQuality(fmt);
                }
                else
                {
                    var s = dialog.SelectedVideoQuality;
                    s.IsRemembered = true;
                    _settings.SetVideoQuality(fmt, s);
                }
            }
        }
        else if (category == "audio")
        {
            if (dialog.IsResetRequested || !dialog.RememberChoice)
            {
                _settings.ResetAudioQuality(fmt);
            }
            else
            {
                _settings.SetAudioQuality(fmt, dialog.SelectedAudioBitrate, true);
            }
        }
        else if (category == "image")
        {
            if (dialog.IsResetRequested || !dialog.RememberChoice)
            {
                _settings.ResetQuality(fmt);
            }
            else
            {
                _settings.SetQuality(fmt, dialog.SelectedImageQuality, true);
            }
        }

        _settings.AppendQualitySuffix = dialog.AppendQualitySuffix;
        ChkAppendQualitySuffix.IsChecked = _settings.AppendQualitySuffix;
        _settings.Save();
    }

    internal void EditFormatSettings(string category, string format, ConversionOptionsDialog dialog)
    {
        ApplyDialogResult(category, format, dialog);
        var panel = GetCategoryPanel(category);
        var statusBlock = FindFormatStatusBlock(panel, format);
        if (statusBlock != null)
        {
            statusBlock.Text = GetFormatStatus(category, format);
        }
    }

    internal Panel GetCategoryPanel(string category) => category switch
    {
        "video" => PanelVideoFormats,
        "audio" => PanelAudioFormats,
        _ => PanelImageFormats
    };

    internal System.Windows.Controls.CheckBox? FindFormatCheckBox(Panel panel, string format)
    {
        return FindDescendants<System.Windows.Controls.CheckBox>(panel)
            .FirstOrDefault(cb => string.Equals(cb.Tag as string, format, StringComparison.OrdinalIgnoreCase));
    }

    internal Wpf.Ui.Controls.Button? FindFormatEditButton(Panel panel, string format)
    {
        return FindDescendants<Wpf.Ui.Controls.Button>(panel)
            .FirstOrDefault(b => string.Equals(b.Tag as string, format, StringComparison.OrdinalIgnoreCase));
    }

    internal System.Windows.Controls.TextBlock? FindFormatStatusBlock(Panel panel, string format)
    {
        return FindDescendants<System.Windows.Controls.TextBlock>(panel)
            .FirstOrDefault(tb => string.Equals(tb.Tag as string, format, StringComparison.OrdinalIgnoreCase));
    }

    internal static IEnumerable<T> FindDescendants<T>(DependencyObject parent) where T : DependencyObject
    {
        if (parent is Panel panel)
        {
            foreach (UIElement child in panel.Children)
            {
                if (child is T match) yield return match;
                foreach (var desc in FindDescendants<T>(child)) yield return desc;
            }
        }
        else if (parent is ContentControl cc && cc.Content is DependencyObject contentChild)
        {
            if (contentChild is T match) yield return match;
            foreach (var desc in FindDescendants<T>(contentChild)) yield return desc;
        }
        else if (parent is Decorator dec && dec.Child is DependencyObject decChild)
        {
            if (decChild is T match) yield return match;
            foreach (var desc in FindDescendants<T>(decChild)) yield return desc;
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

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (!e.Handled && e.Key == Key.Escape)
        {
            Close();
            e.Handled = true;
        }
    }

    private void OnCloseClick(object sender, RoutedEventArgs e)
    {
        Close();
    }
}

using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using JustConvert.Core;
using JustConvert.Core.Logging;
using JustConvert.Core.Scanning;
using JustConvert.Core.Windows;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace JustConvert.Cli.UI;

public sealed record FormatChoice(string Format, string DisplayName, bool IsSeparator = false)
{
    public override string ToString() => DisplayName;
}

public partial class FolderBatchWindow : FluentWindow
{
    private readonly string _folderPath;
    private readonly AppSettings _settings;
    private readonly bool _isCustomSettings;
    private FolderScanResult? _scanResult;
    private bool _isScanning;

    private static readonly string[] FallbackImageFormats =
    [
        "png", "jpg", "webp", "ico", "bmp", "gif", "jp2", "tiff", "tga", "pcx", "ppm", "avif", "reencode"
    ];

    private static readonly string[] FallbackVideoFormats =
    [
        "mp4", "webm", "mkv", "mov", "gif", "frames", "mp3", "wav", "flac", "aac", "m4a", "opus", "reencode"
    ];

    private static readonly string[] FallbackAudioFormats =
    [
        "mp3", "aac", "m4a", "wav", "flac", "ogg", "opus", "aiff", "reencode"
    ];

    internal AppSettings CurrentSettings => _settings;

    public FolderBatchWindow() : this(string.Empty)
    {
    }

    public FolderBatchWindow(string folderPath) : this(folderPath, AppSettings.Load(), false)
    {
    }

    public FolderBatchWindow(string folderPath, AppSettings settings) : this(folderPath, settings, true)
    {
    }

    private FolderBatchWindow(string folderPath, AppSettings settings, bool isCustomSettings)
    {
        InitializeComponent();
        _folderPath = folderPath;
        _settings = settings;
        _isCustomSettings = isCustomSettings;
        _settings.EnsureDefaultProfile();

        if (DesignerProperties.GetIsInDesignMode(this))
        {
            Title = I18n.T("FolderBatchTitle");
            AppTitleBar.Title = Title;
            TxtFolderName.Text = "Sample Folder";
            TxtFolderPath.Text = @"C:\Sample Folder";
            TxtScanSummary.Text = I18n.T("LabelScanSummary", 18);
            ChkImages.Content = I18n.T("CategoryImagesBatch", 12, "jpg, png");
            ChkVideo.Content = I18n.T("CategoryVideoBatch", 4, "mp4, mov");
            ChkAudio.Content = I18n.T("CategoryAudioBatch", 2, "wav, flac");
            RbCustomFolder.Content = I18n.T("OptionDestCustomFolder");
            BtnBrowseCustomFolder.Content = I18n.T("BtnBrowseFolder");
            BtnCancel.Content = I18n.T("BtnCancel");
            BtnStart.Content = I18n.T("BtnStartBatch");
            return;
        }

        FluentThemeService.Watch(this);

        Title = I18n.T("FolderBatchTitle");
        AppTitleBar.Title = Title;

        InitFormatComboBoxes();

        CmbImageFormats.AddHandler(UIElement.PreviewMouseLeftButtonDownEvent, new MouseButtonEventHandler(OnComboBoxPreviewMouseButton), true);
        CmbImageFormats.AddHandler(UIElement.PreviewMouseLeftButtonUpEvent, new MouseButtonEventHandler(OnComboBoxPreviewMouseButton), true);
        CmbVideoFormats.AddHandler(UIElement.PreviewMouseLeftButtonDownEvent, new MouseButtonEventHandler(OnComboBoxPreviewMouseButton), true);
        CmbVideoFormats.AddHandler(UIElement.PreviewMouseLeftButtonUpEvent, new MouseButtonEventHandler(OnComboBoxPreviewMouseButton), true);
        CmbAudioFormats.AddHandler(UIElement.PreviewMouseLeftButtonDownEvent, new MouseButtonEventHandler(OnComboBoxPreviewMouseButton), true);
        CmbAudioFormats.AddHandler(UIElement.PreviewMouseLeftButtonUpEvent, new MouseButtonEventHandler(OnComboBoxPreviewMouseButton), true);

        TxtFolderName.Text = Path.GetFileName(folderPath);
        TxtFolderPath.Text = folderPath;

        ChkIncludeSubfolders.Checked += (_, _) => TriggerScan();
        ChkIncludeSubfolders.Unchecked += (_, _) => TriggerScan();

        Loaded += (_, _) => TriggerScan();
        Activated += (_, _) =>
        {
            if (!_isCustomSettings)
            {
                var refreshed = AppSettings.Load();
                refreshed.EnsureDefaultProfile();
                var activeProfile = refreshed.GetActiveProfile();
                var currentProfile = _settings.GetActiveProfile();
                currentProfile.ImageFormats = [.. activeProfile.ImageFormats];
                currentProfile.VideoFormats = [.. activeProfile.VideoFormats];
                currentProfile.AudioFormats = [.. activeProfile.AudioFormats];
            }
            UpdateFormatComboBoxes(_scanResult);
        };
    }

    private void InitFormatComboBoxes()
    {
        UpdateFormatComboBoxes(null);
    }

    internal static IReadOnlyList<string> GetCategoryCandidateFormats(
        IEnumerable<string> profileFormats,
        IReadOnlyList<string> fallbackFormats,
        string? category = null)
    {
        var profileList = profileFormats.ToList();
        var formatsToProcess = profileList.Count > 0 ? profileList : fallbackFormats.ToList();
        var hasExplicitSeparators = formatsToProcess.Any(ClassicContextMenuManager.IsSeparator);
        var result = new List<string>();
        var seenFormats = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (hasExplicitSeparators)
        {
            foreach (var raw in formatsToProcess)
            {
                if (ClassicContextMenuManager.IsSeparator(raw))
                {
                    result.Add("separator");
                    continue;
                }

                var clean = raw.TrimStart('.').ToLowerInvariant();
                if (seenFormats.Add(clean))
                {
                    result.Add(clean);
                }
            }
        }
        else
        {
            int? prevGroup = null;
            foreach (var raw in formatsToProcess)
            {
                var clean = raw.TrimStart('.').ToLowerInvariant();
                if (seenFormats.Add(clean))
                {
                    if (category != null)
                    {
                        var group = ClassicContextMenuManager.GetFormatGroup(clean, category);
                        if (prevGroup.HasValue && group != prevGroup.Value)
                        {
                            result.Add("separator");
                        }
                        prevGroup = group;
                    }
                    result.Add(clean);
                }
            }
        }

        return seenFormats.Count > 0 ? result : fallbackFormats;
    }

    private void UpdateFormatComboBoxes(FolderScanResult? result)
    {
        var profile = _settings.GetActiveProfile();

        var imageCandidates = GetCategoryCandidateFormats(profile.ImageFormats, FallbackImageFormats, "image");
        UpdateCategoryComboBox(
            CmbImageFormats,
            imageCandidates,
            result?.Images?.UniqueExtensions);

        var videoCandidates = GetCategoryCandidateFormats(profile.VideoFormats, FallbackVideoFormats, "video");
        UpdateCategoryComboBox(
            CmbVideoFormats,
            videoCandidates,
            result?.Video?.UniqueExtensions);

        var audioCandidates = GetCategoryCandidateFormats(profile.AudioFormats, FallbackAudioFormats, "audio");
        UpdateCategoryComboBox(
            CmbAudioFormats,
            audioCandidates,
            result?.Audio?.UniqueExtensions);
    }

    private static void UpdateCategoryComboBox(ComboBox comboBox, IReadOnlyList<string> candidates, IReadOnlyList<string>? uniqueExtensions)
    {
        var available = new List<string>();
        foreach (var item in candidates)
        {
            if (ClassicContextMenuManager.IsSeparator(item))
            {
                available.Add(item);
                continue;
            }

            if (uniqueExtensions == null || uniqueExtensions.Count == 0 || !uniqueExtensions.All(ext => ClassicContextMenuManager.IsSameFormat(ext, item)))
            {
                available.Add(item);
            }
        }

        if (!available.Any(f => !ClassicContextMenuManager.IsSeparator(f)))
        {
            available = candidates.ToList();
        }

        var items = new List<FormatChoice>();
        var pendingSeparator = false;

        foreach (var item in available)
        {
            if (ClassicContextMenuManager.IsSeparator(item))
            {
                if (items.Count > 0)
                {
                    pendingSeparator = true;
                }
                continue;
            }

            if (pendingSeparator)
            {
                items.Add(new FormatChoice(string.Empty, string.Empty, IsSeparator: true));
                pendingSeparator = false;
            }

            items.Add(new FormatChoice(item, I18n.GetSubMenuTitle(item)));
        }

        var previousSelected = (comboBox.SelectedItem as FormatChoice)?.Format;
        comboBox.ItemsSource = items;

        var newIndex = items.FindIndex(i => !i.IsSeparator && i.Format == previousSelected);
        if (newIndex < 0)
        {
            newIndex = items.FindIndex(i => !i.IsSeparator);
        }
        comboBox.SelectedIndex = newIndex >= 0 ? newIndex : -1;
    }

    private static void OnComboBoxPreviewMouseButton(object sender, MouseButtonEventArgs e)
    {
        var container = FindAncestor<ComboBoxItem>(e.OriginalSource as DependencyObject);
        if (container?.DataContext is FormatChoice { IsSeparator: true })
        {
            e.Handled = true;
        }
    }

    private static T? FindAncestor<T>(DependencyObject? current) where T : DependencyObject
    {
        while (current != null)
        {
            if (current is T match) return match;
            var parent = current is Visual or System.Windows.Media.Media3D.Visual3D ? VisualTreeHelper.GetParent(current) : null;
            current = parent ?? LogicalTreeHelper.GetParent(current);
        }
        return null;
    }

    private void OnFormatComboBoxSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is not ComboBox comboBox) return;
        if (comboBox.SelectedItem is not FormatChoice { IsSeparator: true }) return;

        if (comboBox.ItemsSource is not IList<FormatChoice> items) return;

        var oldItem = e.RemovedItems.OfType<FormatChoice>().FirstOrDefault(i => !i.IsSeparator);
        var oldIndex = oldItem != null ? items.IndexOf(oldItem) : -1;
        var currentIndex = comboBox.SelectedIndex;

        if (oldIndex >= 0 && currentIndex > oldIndex)
        {
            var nextIndex = -1;
            for (var i = currentIndex + 1; i < items.Count; i++)
            {
                if (!items[i].IsSeparator) { nextIndex = i; break; }
            }
            comboBox.SelectedIndex = nextIndex >= 0 ? nextIndex : oldIndex;
        }
        else if (oldIndex >= 0 && currentIndex < oldIndex)
        {
            var prevIndex = -1;
            for (var i = currentIndex - 1; i >= 0; i--)
            {
                if (!items[i].IsSeparator) { prevIndex = i; break; }
            }
            comboBox.SelectedIndex = prevIndex >= 0 ? prevIndex : oldIndex;
        }
        else
        {
            var firstIndex = -1;
            for (var i = 0; i < items.Count; i++)
            {
                if (!items[i].IsSeparator) { firstIndex = i; break; }
            }
            comboBox.SelectedIndex = firstIndex >= 0 ? firstIndex : -1;
        }
    }

    private async void TriggerScan()
    {
        if (_isScanning || string.IsNullOrEmpty(_folderPath)) return;
        _isScanning = true;

        TxtScanSummary.Text = I18n.T("LabelScanning");
        BtnStart.IsEnabled = false;

        var recursive = ChkIncludeSubfolders.IsChecked == true;
        var result = await Task.Run(() => FolderScanner.Scan(_folderPath, recursive));

        _scanResult = result;
        _isScanning = false;

        UpdateScanUI(result);
    }

    internal void UpdateScanUI(FolderScanResult result)
    {
        UpdateFormatComboBoxes(result);
        TxtScanSummary.Text = I18n.T("LabelScanSummary", result.TotalCount);

        if (!result.HasFiles)
        {
            TxtNoFiles.Visibility = Visibility.Visible;
            PanelCategories.Visibility = Visibility.Collapsed;
            BtnStart.IsEnabled = false;
            return;
        }

        TxtNoFiles.Visibility = Visibility.Collapsed;
        PanelCategories.Visibility = Visibility.Visible;

        if (result.Images != null && result.Images.Count > 0)
        {
            RowImages.Visibility = Visibility.Visible;
            ChkImages.Content = I18n.T("CategoryImagesBatch", result.Images.Count, string.Join(", ", result.Images.UniqueExtensions));
        }
        else
        {
            RowImages.Visibility = Visibility.Collapsed;
            ChkImages.IsChecked = false;
        }

        if (result.Video != null && result.Video.Count > 0)
        {
            RowVideo.Visibility = Visibility.Visible;
            ChkVideo.Content = I18n.T("CategoryVideoBatch", result.Video.Count, string.Join(", ", result.Video.UniqueExtensions));
        }
        else
        {
            RowVideo.Visibility = Visibility.Collapsed;
            ChkVideo.IsChecked = false;
        }

        if (result.Audio != null && result.Audio.Count > 0)
        {
            RowAudio.Visibility = Visibility.Visible;
            ChkAudio.Content = I18n.T("CategoryAudioBatch", result.Audio.Count, string.Join(", ", result.Audio.UniqueExtensions));
        }
        else
        {
            RowAudio.Visibility = Visibility.Collapsed;
            ChkAudio.IsChecked = false;
        }

        BtnStart.IsEnabled = (ChkImages.IsChecked == true && result.Images != null)
            || (ChkVideo.IsChecked == true && result.Video != null)
            || (ChkAudio.IsChecked == true && result.Audio != null);
    }

    private void OnDestinationChanged(object sender, RoutedEventArgs e)
    {
        if (PanelCustomFolder == null || RbCustomFolder == null) return;
        PanelCustomFolder.Visibility = RbCustomFolder.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
    }

    private void BtnBrowseCustomFolder_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = I18n.T("BrowseFolderTitle")
        };
        if (dialog.ShowDialog(this) == true)
        {
            TxtCustomFolder.Text = dialog.FolderName;
        }
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

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void BtnStart_Click(object sender, RoutedEventArgs e)
    {
        if (_scanResult == null || !_scanResult.HasFiles) return;

        DestinationMode destMode;
        string? customDir = null;

        if (RbCustomFolder.IsChecked == true)
        {
            customDir = TxtCustomFolder.Text?.Trim();
            var (isValid, errorMessage) = FolderBatchPlanner.ValidateDestinationDirectory(customDir ?? "");
            if (!isValid)
            {
                MessageDialog.ShowWarning(this, errorMessage ?? I18n.T("ErrorFolderInvalid", ""), I18n.T("ConversionOptionsTitle"));
                return;
            }
            destMode = DestinationMode.CustomFolder;
        }
        else if (RbSubfolder.IsChecked == true)
        {
            destMode = DestinationMode.Subfolder;
        }
        else
        {
            destMode = DestinationMode.InPlace;
        }

        var categoryPlans = new Dictionary<MediaCategory, BatchCategoryPlan>();
        var settings = _settings;

        if (ChkImages.IsChecked == true && _scanResult.Images != null)
        {
            var target = (CmbImageFormats.SelectedItem as FormatChoice)?.Format;
            if (string.IsNullOrEmpty(target) || ClassicContextMenuManager.IsSeparator(target))
            {
                target = "png";
            }
            var hasSvg = _scanResult.Images.UniqueExtensions.Contains("svg", StringComparer.OrdinalIgnoreCase);
            if (hasSvg && !settings.SvgSetting.IsRemembered)
            {
                var dialog = new ConversionOptionsDialog(
                    target,
                    "image",
                    settings.GetEffectiveSvgSetting(),
                    null,
                    settings.GetEffectiveAppendQualitySuffix("svg", target),
                    _scanResult.Images.Files.Count,
                    CalculateCategoryTotalSizeBytes(_scanResult.Images),
                    sourceFormat: "svg")
                {
                    Owner = this
                };
                if (dialog.ShowDialog() != true)
                {
                    return;
                }

                var svgSet = dialog.SelectedSvgSetting;
                svgSet.IsRemembered = dialog.RememberChoice;
                svgSet.AppendQualitySuffix = dialog.AppendQualitySuffix;
                settings.SvgSetting = svgSet;
                if (AppSettings.SupportsQuality(target))
                {
                    settings.SetQuality(target, dialog.SelectedImageQuality, dialog.RememberChoice, dialog.AppendQualitySuffix);
                }
                settings.SetAppendQualitySuffix("svg", target, dialog.AppendQualitySuffix);
                settings.Save();
            }
            else if (AppSettings.SupportsQuality(target))
            {
                if (!settings.TryGetSavedQuality(target, out _))
                {
                    var dialog = new ConversionOptionsDialog(
                        target,
                        "image",
                        settings.GetEffectiveQuality(target),
                        null,
                        settings.GetEffectiveAppendQualitySuffix("image", target),
                        _scanResult.Images.Files.Count,
                        CalculateCategoryTotalSizeBytes(_scanResult.Images))
                    {
                        Owner = this
                    };
                    if (dialog.ShowDialog() != true)
                    {
                        return;
                    }

                    settings.SetQuality(target, dialog.SelectedImageQuality, dialog.RememberChoice, dialog.AppendQualitySuffix);
                    settings.SetAppendQualitySuffix("image", target, dialog.AppendQualitySuffix);
                    settings.Save();
                }
            }

            categoryPlans[MediaCategory.Image] = new BatchCategoryPlan(MediaCategory.Image, true, target);
        }

        if (ChkVideo.IsChecked == true && _scanResult.Video != null)
        {
            var target = (CmbVideoFormats.SelectedItem as FormatChoice)?.Format;
            if (string.IsNullOrEmpty(target) || ClassicContextMenuManager.IsSeparator(target))
            {
                target = "mp4";
            }
            if (target == "frames")
            {
                if (!settings.FramesSetting.IsRemembered)
                {
                    var dialog = new ConversionOptionsDialog(
                        "frames",
                        "frames",
                        settings.GetEffectiveFramesSetting(),
                        null,
                        settings.GetEffectiveAppendQualitySuffix("frames", "frames"),
                        _scanResult.Video.Files.Count,
                        CalculateCategoryTotalSizeBytes(_scanResult.Video))
                    {
                        Owner = this
                    };
                    if (dialog.ShowDialog() != true)
                    {
                        return;
                    }

                    var fs = dialog.SelectedFramesSetting;
                    fs.AppendQualitySuffix = dialog.AppendQualitySuffix;
                    settings.SetFramesSetting(fs);
                    settings.SetAppendQualitySuffix("frames", "frames", dialog.AppendQualitySuffix);
                    settings.Save();
                    target = dialog.SelectedFramesTargetFormat;
                }
                else
                {
                    target = $"frames-{settings.FramesSetting.ImageFormat}";
                }
            }
            else if (target is "mp4" or "webm" or "mkv" or "mov")
            {
                if (!settings.TryGetSavedVideoQuality(target, out _))
                {
                    var dialog = new ConversionOptionsDialog(
                        target,
                        "video",
                        settings.GetEffectiveVideoQuality(target),
                        null,
                        settings.GetEffectiveAppendQualitySuffix("video", target),
                        _scanResult.Video.Files.Count,
                        CalculateCategoryTotalSizeBytes(_scanResult.Video))
                    {
                        Owner = this
                    };
                    if (dialog.ShowDialog() != true)
                    {
                        return;
                    }

                    var sel = dialog.SelectedVideoQuality;
                    sel.IsRemembered = dialog.RememberChoice;
                    sel.AppendQualitySuffix = dialog.AppendQualitySuffix;
                    settings.SetVideoQuality(target, sel);
                    settings.SetAppendQualitySuffix("video", target, dialog.AppendQualitySuffix);
                    settings.Save();
                }
            }
            else if (target is "mp3" or "aac" or "m4a" or "ogg" or "opus")
            {
                if (!settings.TryGetSavedAudioQuality(target, out _))
                {
                    var dialog = new ConversionOptionsDialog(
                        target,
                        "audio",
                        settings.GetEffectiveAudioQuality(target),
                        null,
                        settings.GetEffectiveAppendQualitySuffix("audio", target),
                        _scanResult.Video.Files.Count,
                        CalculateCategoryTotalSizeBytes(_scanResult.Video))
                    {
                        Owner = this
                    };
                    if (dialog.ShowDialog() != true)
                    {
                        return;
                    }

                    settings.SetAudioQuality(target, dialog.SelectedAudioBitrate, dialog.RememberChoice, dialog.AppendQualitySuffix);
                    settings.SetAppendQualitySuffix("audio", target, dialog.AppendQualitySuffix);
                    settings.Save();
                }
            }

            categoryPlans[MediaCategory.Video] = new BatchCategoryPlan(MediaCategory.Video, true, target);
        }

        if (ChkAudio.IsChecked == true && _scanResult.Audio != null)
        {
            var target = (CmbAudioFormats.SelectedItem as FormatChoice)?.Format;
            if (string.IsNullOrEmpty(target) || ClassicContextMenuManager.IsSeparator(target))
            {
                target = "mp3";
            }
            if (target is "mp3" or "aac" or "m4a" or "ogg" or "opus")
            {
                if (!settings.TryGetSavedAudioQuality(target, out _))
                {
                    var dialog = new ConversionOptionsDialog(
                        target,
                        "audio",
                        settings.GetEffectiveAudioQuality(target),
                        null,
                        settings.GetEffectiveAppendQualitySuffix("audio", target),
                        _scanResult.Audio.Files.Count,
                        CalculateCategoryTotalSizeBytes(_scanResult.Audio))
                    {
                        Owner = this
                    };
                    if (dialog.ShowDialog() != true)
                    {
                        return;
                    }

                    settings.SetAudioQuality(target, dialog.SelectedAudioBitrate, dialog.RememberChoice, dialog.AppendQualitySuffix);
                    settings.SetAppendQualitySuffix("audio", target, dialog.AppendQualitySuffix);
                    settings.Save();
                }
            }

            categoryPlans[MediaCategory.Audio] = new BatchCategoryPlan(MediaCategory.Audio, true, target);
        }

        if (categoryPlans.Count == 0) return;

        var processingWindow = new QueueProcessingWindow();
        processingWindow.Show();
        Hide();

        _ = Task.Run(() =>
        {
            try
            {
                var items = FolderBatchPlanner.Plan(_scanResult, categoryPlans, destMode, customDir);
                if (items.Count == 0 || processingWindow.IsCancelled)
                {
                    Dispatcher.Invoke(() =>
                    {
                        processingWindow.Close();
                        Close();
                    });
                    return;
                }

                Dispatcher.Invoke(() =>
                {
                    var progressWindow = new ConversionProgressWindow(items);
                    if (Application.Current != null)
                    {
                        Application.Current.MainWindow = progressWindow;
                    }
                    progressWindow.Show();
                    processingWindow.Close();
                    Close();
                });
            }
            catch (Exception ex)
            {
                AppLogger.Error("Error planning batch in FolderBatchWindow", ex);
                Dispatcher.Invoke(() =>
                {
                    processingWindow.Close();
                    Close();
                });
            }
        });
    }

    private static long? CalculateCategoryTotalSizeBytes(CategoryScanResult? category)
    {
        if (category == null || category.Files.Count == 0) return null;
        long total = 0;
        foreach (var file in category.Files)
        {
            try
            {
                var fi = new FileInfo(file.FullPath);
                if (fi.Exists) total += fi.Length;
            }
            catch
            {
            }
        }
        return total;
    }
}

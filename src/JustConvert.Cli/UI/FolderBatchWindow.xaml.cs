using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using JustConvert.Core;
using JustConvert.Core.Scanning;
using JustConvert.Core.Windows;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace JustConvert.Cli.UI;

public sealed record FormatChoice(string Format, string DisplayName)
{
    public override string ToString() => DisplayName;
}

public partial class FolderBatchWindow : FluentWindow
{
    private readonly string _folderPath;
    private FolderScanResult? _scanResult;
    private bool _isScanning;

    public FolderBatchWindow() : this(string.Empty)
    {
    }

    public FolderBatchWindow(string folderPath)
    {
        InitializeComponent();
        _folderPath = folderPath;

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

        ApplicationThemeManager.ApplySystemTheme();
        ApplicationAccentColorManager.ApplySystemAccent();
        ApplicationThemeManager.Apply(this);
        SystemThemeWatcher.Watch(this);

        Title = I18n.T("FolderBatchTitle");
        AppTitleBar.Title = Title;

        InitFormatComboBoxes();

        TxtFolderName.Text = Path.GetFileName(folderPath);
        TxtFolderPath.Text = folderPath;

        ChkIncludeSubfolders.Checked += (_, _) => TriggerScan();
        ChkIncludeSubfolders.Unchecked += (_, _) => TriggerScan();

        Loaded += (_, _) => TriggerScan();
    }

    private void InitFormatComboBoxes()
    {
        UpdateFormatComboBoxes(null);
    }

    private void UpdateFormatComboBoxes(FolderScanResult? result)
    {
        UpdateCategoryComboBox(
            CmbImageFormats,
            ["jpg", "png", "webp", "avif", "gif", "ico", "bmp", "tiff"],
            result?.Images?.UniqueExtensions);

        var videoCandidateFormats = new[] { "mp4", "mkv", "mov", "webm", "gif", "remux", "frames" };
        var videoAvailable = ClassicContextMenuManager.FilterAvailableFormats(videoCandidateFormats);
        UpdateCategoryComboBox(
            CmbVideoFormats,
            videoAvailable,
            result?.Video?.UniqueExtensions);

        UpdateCategoryComboBox(
            CmbAudioFormats,
            ["mp3", "m4a", "aac", "wav", "flac", "opus", "ogg"],
            result?.Audio?.UniqueExtensions);
    }

    private static void UpdateCategoryComboBox(ComboBox comboBox, IReadOnlyList<string> candidates, IReadOnlyList<string>? uniqueExtensions)
    {
        var filtered = candidates.Where(fmt =>
        {
            if (uniqueExtensions == null || uniqueExtensions.Count == 0) return true;
            return !uniqueExtensions.All(ext => ClassicContextMenuManager.IsSameFormat(ext, fmt));
        }).ToList();

        if (filtered.Count == 0)
        {
            filtered = candidates.ToList();
        }

        var previousSelected = (comboBox.SelectedItem as FormatChoice)?.Format;
        var items = filtered.Select(f => new FormatChoice(f, I18n.GetSubMenuTitle(f))).ToList();
        comboBox.ItemsSource = items;

        var newIndex = items.FindIndex(i => i.Format == previousSelected);
        comboBox.SelectedIndex = newIndex >= 0 ? newIndex : 0;
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
                System.Windows.MessageBox.Show(this, errorMessage ?? I18n.T("ErrorFolderInvalid", ""), I18n.T("ConversionOptionsTitle"), System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
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
        var settings = AppSettings.Load();

        if (ChkImages.IsChecked == true && _scanResult.Images != null)
        {
            var target = (CmbImageFormats.SelectedItem as FormatChoice)?.Format ?? "png";
            if (AppSettings.SupportsQuality(target))
            {
                if (!settings.TryGetSavedQuality(target, out _))
                {
                    var dialog = new ConversionOptionsDialog(
                        target,
                        "image",
                        settings.GetEffectiveQuality(target),
                        null,
                        settings.AppendQualitySuffix,
                        _scanResult.Images.Files.Count,
                        CalculateCategoryTotalSizeBytes(_scanResult.Images))
                    {
                        Owner = this
                    };
                    if (dialog.ShowDialog() != true)
                    {
                        return;
                    }

                    settings.SetQuality(target, dialog.SelectedImageQuality, dialog.RememberChoice);
                    settings.AppendQualitySuffix = dialog.AppendQualitySuffix;
                    settings.Save();
                }
            }

            categoryPlans[MediaCategory.Image] = new BatchCategoryPlan(MediaCategory.Image, true, target);
        }

        if (ChkVideo.IsChecked == true && _scanResult.Video != null)
        {
            var target = (CmbVideoFormats.SelectedItem as FormatChoice)?.Format ?? "mp4";
            if (target == "frames")
            {
                if (!settings.FramesSetting.IsRemembered)
                {
                    var dialog = new ConversionOptionsDialog(
                        "frames",
                        "frames",
                        settings.GetEffectiveFramesSetting(),
                        null,
                        settings.AppendQualitySuffix,
                        _scanResult.Video.Files.Count,
                        CalculateCategoryTotalSizeBytes(_scanResult.Video))
                    {
                        Owner = this
                    };
                    if (dialog.ShowDialog() != true)
                    {
                        return;
                    }

                    settings.SetFramesSetting(dialog.SelectedFramesSetting);
                    settings.AppendQualitySuffix = dialog.AppendQualitySuffix;
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
                        settings.AppendQualitySuffix,
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
                    settings.SetVideoQuality(target, sel);
                    settings.AppendQualitySuffix = dialog.AppendQualitySuffix;
                    settings.Save();
                }
            }

            categoryPlans[MediaCategory.Video] = new BatchCategoryPlan(MediaCategory.Video, true, target);
        }

        if (ChkAudio.IsChecked == true && _scanResult.Audio != null)
        {
            var target = (CmbAudioFormats.SelectedItem as FormatChoice)?.Format ?? "mp3";
            if (target is "mp3" or "aac" or "m4a" or "ogg" or "opus")
            {
                if (!settings.TryGetSavedAudioQuality(target, out _))
                {
                    var dialog = new ConversionOptionsDialog(
                        target,
                        "audio",
                        settings.GetEffectiveAudioQuality(target),
                        null,
                        settings.AppendQualitySuffix,
                        _scanResult.Audio.Files.Count,
                        CalculateCategoryTotalSizeBytes(_scanResult.Audio))
                    {
                        Owner = this
                    };
                    if (dialog.ShowDialog() != true)
                    {
                        return;
                    }

                    settings.SetAudioQuality(target, dialog.SelectedAudioBitrate, dialog.RememberChoice);
                    settings.AppendQualitySuffix = dialog.AppendQualitySuffix;
                    settings.Save();
                }
            }

            categoryPlans[MediaCategory.Audio] = new BatchCategoryPlan(MediaCategory.Audio, true, target);
        }

        if (categoryPlans.Count == 0) return;

        var items = FolderBatchPlanner.Plan(_scanResult, categoryPlans, destMode, customDir);

        if (items.Count == 0) return;

        var progressWindow = new ConversionProgressWindow(items);
        if (Application.Current != null)
        {
            Application.Current.MainWindow = progressWindow;
        }
        progressWindow.Show();
        Close();
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

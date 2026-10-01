using System.Windows;
using System.Windows.Input;
using JustConvert.Cli.UI;
using JustConvert.Core;
using JustConvert.Core.Scanning;
using Xunit;

namespace JustConvert.Tests;

public class UiFolderBatchWindowTests
{
    [Fact]
    public void Construct_EmptyFolder_InitializesDefaults()
    {
        StaTestRunner.Run(() =>
        {
            var window = new FolderBatchWindow();

            Assert.NotNull(window.TxtFolderName);
            Assert.NotNull(window.TxtFolderPath);
            Assert.True(window.RbInPlace.IsChecked);
            Assert.False(window.RbSubfolder.IsChecked);
            Assert.False(window.RbCustomFolder.IsChecked);
            Assert.True(window.ChkIncludeSubfolders.IsChecked);
            Assert.Equal(Visibility.Collapsed, window.PanelCustomFolder.Visibility);

            var imageItems = window.CmbImageFormats.ItemsSource as IReadOnlyList<FormatChoice>;
            var videoItems = window.CmbVideoFormats.ItemsSource as IReadOnlyList<FormatChoice>;
            var audioItems = window.CmbAudioFormats.ItemsSource as IReadOnlyList<FormatChoice>;

            Assert.NotNull(imageItems);
            Assert.NotEmpty(imageItems);
            Assert.NotNull(videoItems);
            Assert.NotEmpty(videoItems);
            Assert.NotNull(audioItems);
            Assert.NotEmpty(audioItems);

            Assert.Equal(I18n.T("FolderBatchTitle"), window.Title);
        });
    }

    [Fact]
    public void Construct_WithFolderPath_SetsFolderTexts()
    {
        StaTestRunner.Run(() =>
        {
            var path = @"C:\MediaDirectory\TargetFolder";
            var window = new FolderBatchWindow(path);

            Assert.Equal("TargetFolder", window.TxtFolderName.Text);
            Assert.Equal(path, window.TxtFolderPath.Text);
        });
    }

    [Fact]
    public void DestinationRadioButtons_ToggleCustomFolderVisibility()
    {
        StaTestRunner.Run(() =>
        {
            var window = new FolderBatchWindow();

            window.RbCustomFolder.IsChecked = true;
            Assert.Equal(Visibility.Visible, window.PanelCustomFolder.Visibility);

            window.RbSubfolder.IsChecked = true;
            Assert.Equal(Visibility.Collapsed, window.PanelCustomFolder.Visibility);

            window.RbInPlace.IsChecked = true;
            Assert.Equal(Visibility.Collapsed, window.PanelCustomFolder.Visibility);
        });
    }

    [Fact]
    public void UpdateScanUI_WithNoFiles_ShowsNoFilesAndHidesCategories()
    {
        StaTestRunner.Run(() =>
        {
            var window = new FolderBatchWindow();
            var emptyResult = new FolderScanResult(@"C:\EmptyFolder", false, Array.Empty<ScannedFile>(), null, null, null);

            window.UpdateScanUI(emptyResult);

            Assert.Equal(Visibility.Visible, window.TxtNoFiles.Visibility);
            Assert.Equal(Visibility.Collapsed, window.PanelCategories.Visibility);
            Assert.False(window.BtnStart.IsEnabled);
        });
    }

    [Fact]
    public void UpdateScanUI_WithImagesOnly_ShowsOnlyImagesRow()
    {
        StaTestRunner.Run(() =>
        {
            var window = new FolderBatchWindow();
            var files = new[]
            {
                new ScannedFile(@"C:\Test\photo1.png", "photo1.png", "png", MediaCategory.Image),
                new ScannedFile(@"C:\Test\photo2.jpg", "photo2.jpg", "jpg", MediaCategory.Image)
            };
            var imgResult = new CategoryScanResult(MediaCategory.Image, files, new[] { "png", "jpg" });
            var scanResult = new FolderScanResult(@"C:\Test", false, files, imgResult, null, null);

            window.UpdateScanUI(scanResult);

            Assert.Equal(Visibility.Collapsed, window.TxtNoFiles.Visibility);
            Assert.Equal(Visibility.Visible, window.PanelCategories.Visibility);
            Assert.Equal(Visibility.Visible, window.RowImages.Visibility);
            Assert.True(window.ChkImages.IsChecked);
            Assert.Equal(Visibility.Collapsed, window.RowVideo.Visibility);
            Assert.False(window.ChkVideo.IsChecked);
            Assert.Equal(Visibility.Collapsed, window.RowAudio.Visibility);
            Assert.False(window.ChkAudio.IsChecked);
            Assert.True(window.BtnStart.IsEnabled);
        });
    }

    [Fact]
    public void UpdateScanUI_WithAllCategories_ShowsAllRows()
    {
        StaTestRunner.Run(() =>
        {
            var window = new FolderBatchWindow();
            var imgFiles = new[] { new ScannedFile(@"C:\Test\photo.png", "photo.png", "png", MediaCategory.Image) };
            var vidFiles = new[] { new ScannedFile(@"C:\Test\clip.mp4", "clip.mp4", "mp4", MediaCategory.Video) };
            var audFiles = new[] { new ScannedFile(@"C:\Test\song.mp3", "song.mp3", "mp3", MediaCategory.Audio) };
            var allFiles = imgFiles.Concat(vidFiles).Concat(audFiles).ToArray();

            var scanResult = new FolderScanResult(
                @"C:\Test",
                false,
                allFiles,
                new CategoryScanResult(MediaCategory.Image, imgFiles, new[] { "png" }),
                new CategoryScanResult(MediaCategory.Video, vidFiles, new[] { "mp4" }),
                new CategoryScanResult(MediaCategory.Audio, audFiles, new[] { "mp3" }));

            window.UpdateScanUI(scanResult);

            Assert.Equal(Visibility.Visible, window.RowImages.Visibility);
            Assert.Equal(Visibility.Visible, window.RowVideo.Visibility);
            Assert.Equal(Visibility.Visible, window.RowAudio.Visibility);
            Assert.True(window.ChkImages.IsChecked);
            Assert.True(window.ChkVideo.IsChecked);
            Assert.True(window.ChkAudio.IsChecked);
            Assert.True(window.BtnStart.IsEnabled);
        });
    }

    [Fact]
    public void FolderBatchWindow_BtnCancel_HasIsCancelSet()
    {
        StaTestRunner.Run(() =>
        {
            var window = new FolderBatchWindow();
            Assert.True(window.BtnCancel.IsCancel);
        });
    }

    [Fact]
    public void FolderBatchWindow_PressEscape_HandlesEvent()
    {
        StaTestRunner.Run(() =>
        {
            var window = new FolderBatchWindow();
            var keyArgs = new KeyEventArgs(
                Keyboard.PrimaryDevice,
                new System.Windows.Interop.HwndSource(0, 0, 0, 0, 0, "", IntPtr.Zero),
                0,
                Key.Escape)
            {
                RoutedEvent = Keyboard.KeyDownEvent
            };
            window.RaiseEvent(keyArgs);
            Assert.True(keyArgs.Handled);
        });
    }

    [Fact]
    public void GetCategoryCandidateFormats_PreservesSeparatorsAndOrder()
    {
        var input = new[] { "png", "separator:1", "webp", "SEPARATOR", "jpg", ".png" };
        var fallback = new[] { "default" };

        var result = FolderBatchWindow.GetCategoryCandidateFormats(input, fallback);

        Assert.Equal(5, result.Count);
        Assert.Equal("png", result[0]);
        Assert.Equal("separator", result[1]);
        Assert.Equal("webp", result[2]);
        Assert.Equal("separator", result[3]);
        Assert.Equal("jpg", result[4]);
    }

    [Fact]
    public void Construct_WithCustomSettings_SyncsCandidateFormatsAndSeparatorsFromActiveProfile()
    {
        StaTestRunner.Run(() =>
        {
            var settings = new AppSettings();
            settings.EnsureDefaultProfile();
            var profile = settings.GetActiveProfile();
            profile.ImageFormats = ["webp", "separator", "png"];
            profile.VideoFormats = ["webm", "separator", "mp4"];
            profile.AudioFormats = ["opus", "separator", "flac"];

            var window = new FolderBatchWindow(@"C:\TestFolder", settings);

            var imageItems = (window.CmbImageFormats.ItemsSource as IReadOnlyList<FormatChoice>)!;
            var videoItems = (window.CmbVideoFormats.ItemsSource as IReadOnlyList<FormatChoice>)!;
            var audioItems = (window.CmbAudioFormats.ItemsSource as IReadOnlyList<FormatChoice>)!;

            Assert.Equal(3, imageItems.Count);
            Assert.Equal("webp", imageItems[0].Format);
            Assert.False(imageItems[0].IsSeparator);
            Assert.True(imageItems[1].IsSeparator);
            Assert.Equal("png", imageItems[2].Format);
            Assert.False(imageItems[2].IsSeparator);
            Assert.Equal("webp", (window.CmbImageFormats.SelectedItem as FormatChoice)?.Format);

            Assert.Equal(3, videoItems.Count);
            Assert.Equal("webm", videoItems[0].Format);
            Assert.False(videoItems[0].IsSeparator);
            Assert.True(videoItems[1].IsSeparator);
            Assert.Equal("mp4", videoItems[2].Format);
            Assert.False(videoItems[2].IsSeparator);
            Assert.Equal("webm", (window.CmbVideoFormats.SelectedItem as FormatChoice)?.Format);

            Assert.Equal(3, audioItems.Count);
            Assert.Equal("opus", audioItems[0].Format);
            Assert.False(audioItems[0].IsSeparator);
            Assert.True(audioItems[1].IsSeparator);
            Assert.Equal("flac", audioItems[2].Format);
            Assert.False(audioItems[2].IsSeparator);
            Assert.Equal("opus", (window.CmbAudioFormats.SelectedItem as FormatChoice)?.Format);
        });
    }

    [Fact]
    public void UpdateScanUI_WithCustomProfile_ExcludesSameFormatAndOmitsLeadingSeparator()
    {
        StaTestRunner.Run(() =>
        {
            var settings = new AppSettings();
            settings.EnsureDefaultProfile();
            var profile = settings.GetActiveProfile();
            profile.ImageFormats = ["webp", "separator", "png", "separator", "jpg", "separator"];

            var window = new FolderBatchWindow(@"C:\TestFolder", settings);

            var files = new[]
            {
                new ScannedFile(@"C:\TestFolder\image.webp", "image.webp", "webp", MediaCategory.Image)
            };
            var imgResult = new CategoryScanResult(MediaCategory.Image, files, new[] { "webp" });
            var scanResult = new FolderScanResult(@"C:\TestFolder", false, files, imgResult, null, null);

            window.UpdateScanUI(scanResult);

            var imageItems = (window.CmbImageFormats.ItemsSource as IReadOnlyList<FormatChoice>)!;
            Assert.Equal(3, imageItems.Count);
            Assert.Equal("png", imageItems[0].Format);
            Assert.False(imageItems[0].IsSeparator);
            Assert.True(imageItems[1].IsSeparator);
            Assert.Equal("jpg", imageItems[2].Format);
            Assert.False(imageItems[2].IsSeparator);
            Assert.Equal("png", (window.CmbImageFormats.SelectedItem as FormatChoice)?.Format);
        });
    }

    [Fact]
    public void Construct_WithDefaultProfile_GeneratesSeparatorsBetweenGroups()
    {
        StaTestRunner.Run(() =>
        {
            var settings = new AppSettings();
            settings.EnsureDefaultProfile();

            var window = new FolderBatchWindow(@"C:\TestFolder", settings);

            var imageItems = (window.CmbImageFormats.ItemsSource as IReadOnlyList<FormatChoice>)!;
            var videoItems = (window.CmbVideoFormats.ItemsSource as IReadOnlyList<FormatChoice>)!;
            var audioItems = (window.CmbAudioFormats.ItemsSource as IReadOnlyList<FormatChoice>)!;

            Assert.Contains(imageItems, i => i.IsSeparator);
            Assert.Contains(videoItems, i => i.IsSeparator);
            Assert.Contains(audioItems, i => i.IsSeparator);

            Assert.False(imageItems[0].IsSeparator);
            Assert.False(videoItems[0].IsSeparator);
            Assert.False(audioItems[0].IsSeparator);
        });
    }

    [Fact]
    public void SelectionChanged_WhenSelectingSeparator_SkipsToAdjacentFormat()
    {
        StaTestRunner.Run(() =>
        {
            var settings = new AppSettings();
            settings.EnsureDefaultProfile();
            var profile = settings.GetActiveProfile();
            profile.ImageFormats = ["png", "separator", "webp"];

            var window = new FolderBatchWindow(@"C:\TestFolder", settings);

            Assert.Equal("png", (window.CmbImageFormats.SelectedItem as FormatChoice)?.Format);

            window.CmbImageFormats.SelectedIndex = 1;

            Assert.Equal("webp", (window.CmbImageFormats.SelectedItem as FormatChoice)?.Format);
        });
    }

    [Fact]
    public void Separator_PreviewMouseLeftButtonDown_IsHandled()
    {
        StaTestRunner.Run(() =>
        {
            var settings = new AppSettings();
            settings.EnsureDefaultProfile();
            var profile = settings.GetActiveProfile();
            profile.ImageFormats = ["png", "separator", "webp"];

            var window = new FolderBatchWindow(@"C:\TestFolder", settings);
            var cmb = window.CmbImageFormats;
            var sepChoice = ((IList<FormatChoice>)cmb.ItemsSource)[1];
            Assert.True(sepChoice.IsSeparator);

            var item = new System.Windows.Controls.ComboBoxItem { DataContext = sepChoice };
            var childBorder = new System.Windows.Controls.Border();
            item.Content = childBorder;

            var args = new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left)
            {
                RoutedEvent = UIElement.PreviewMouseLeftButtonDownEvent,
                Source = childBorder
            };

            cmb.RaiseEvent(args);
            Assert.True(args.Handled);
        });
    }
}

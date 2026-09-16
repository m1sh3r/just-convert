using System.Windows;
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
}

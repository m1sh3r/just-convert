using System.IO;
using System.Windows;
using JustConvert.Cli.UI;
using JustConvert.Core;
using JustConvert.Core.Converters.Tools;

namespace JustConvert.Tests;

public class UiConversionOptionsDialogTests
{
    [Fact]
    public void Construct_VideoCategory_InitializesPropertiesAndControls()
    {
        StaTestRunner.Run(() =>
        {
            var dialog = new ConversionOptionsDialog("mp4", "video", new VideoQualitySetting { VideoQualityCq = 22 }, null, true);

            Assert.Equal(Visibility.Visible, dialog.PanelVideoOptions.Visibility);
            Assert.Equal(Visibility.Collapsed, dialog.PanelAudioOptions.Visibility);
            Assert.Equal(Visibility.Collapsed, dialog.PanelImageOptions.Visibility);
            Assert.Equal(Visibility.Collapsed, dialog.PanelRemuxOptions.Visibility);

            Assert.Equal(22, dialog.SliderVideoCq.Value);
            Assert.Equal("CQ 22", dialog.TxtVideoCqValue.Text);
            Assert.False(string.IsNullOrWhiteSpace(dialog.TxtVideoQualityDescription.Text));
            Assert.True(dialog.AppendQualitySuffix);
            Assert.False(dialog.RememberChoice);
        });
    }

    [Fact]
    public void Construct_VideoCategory_WithMediaInfo_PopulatesSourceInfo()
    {
        StaTestRunner.Run(() =>
        {
            var vInfo = new VideoStreamInfo(false, "hevc", "yuv420p", 1920, 1080, 120.0, 60.0, 15_000_000);
            var aInfo = new AudioStreamInfo(192, false, "aac", false, 16, 48000, 2, 120.0, 15_000_000);
            var mediaInfo = new MediaStreamInfo(aInfo, vInfo, 120.0, 15_000_000, "video.mp4");

            var dialog = new ConversionOptionsDialog("mp4", "video", null, mediaInfo, true);

            Assert.Equal(Visibility.Visible, dialog.TxtSourceInfo.Visibility);
            Assert.Contains("H.265/HEVC", dialog.TxtSourceInfo.Text);
            Assert.Contains("1920x1080", dialog.TxtSourceInfo.Text);
            Assert.Contains("60", dialog.TxtSourceInfo.Text);
            Assert.False(string.IsNullOrWhiteSpace(dialog.TxtEstimatedSize.Text));
        });
    }

    [Fact]
    public void Change_VideoCqSlider_UpdatesQualityDescriptionAndEstimate()
    {
        StaTestRunner.Run(() =>
        {
            var dialog = new ConversionOptionsDialog("mp4", "video", new VideoQualitySetting { VideoQualityCq = 23 }, null, true);

            dialog.SliderVideoCq.Value = 18;
            Assert.Equal("CQ 18", dialog.TxtVideoCqValue.Text);
            Assert.Equal(18, dialog.SelectedVideoQuality.VideoQualityCq);

            dialog.SliderVideoCq.Value = 30;
            Assert.Equal("CQ 30", dialog.TxtVideoCqValue.Text);
            Assert.Equal(30, dialog.SelectedVideoQuality.VideoQualityCq);
        });
    }

    [Fact]
    public void Construct_RemuxCategory_InitializesControlsAndContainers()
    {
        StaTestRunner.Run(() =>
        {
            var remuxSetting = new RemuxSetting
            {
                TargetContainer = "mkv",
                CopyVideo = true,
                CopyAudio = true,
                CopySubtitles = false,
                FastStart = true
            };

            var dialog = new ConversionOptionsDialog("remux", "remux", remuxSetting, null, true);

            Assert.Equal(Visibility.Visible, dialog.PanelRemuxOptions.Visibility);
            Assert.Equal(Visibility.Collapsed, dialog.PanelVideoOptions.Visibility);
            Assert.True(dialog.CmbRemuxContainer.Items.Count >= 4);
            Assert.True(dialog.ChkRemuxVideo.IsChecked == true);
            Assert.True(dialog.ChkRemuxAudio.IsChecked == true);
            Assert.False(dialog.ChkRemuxSubtitles.IsChecked == true);
            Assert.True(dialog.ChkRemuxFastStart.IsChecked == true);
        });
    }

    [Fact]
    public void Change_RemuxOptions_UpdatesSelectedRemuxSetting()
    {
        StaTestRunner.Run(() =>
        {
            var dialog = new ConversionOptionsDialog("remux", "remux", null, null, true);

            dialog.ChkRemuxSubtitles.IsChecked = true;
            dialog.ChkRemuxFastStart.IsChecked = false;
            dialog.ChkRemember.IsChecked = true;

            Assert.True(dialog.SelectedRemuxSetting.CopySubtitles);
            Assert.False(dialog.SelectedRemuxSetting.FastStart);
            Assert.True(dialog.RememberChoice);
        });
    }

    [Fact]
    public void Construct_AudioCategory_InitializesAudioControls()
    {
        StaTestRunner.Run(() =>
        {
            var dialog = new ConversionOptionsDialog("mp3", "audio", 256, null, false);

            Assert.Equal(Visibility.Visible, dialog.PanelAudioOptions.Visibility);
            Assert.Equal(Visibility.Collapsed, dialog.PanelVideoOptions.Visibility);
            Assert.Equal(Visibility.Collapsed, dialog.PanelImageOptions.Visibility);

            Assert.Equal(256, dialog.SliderAudioBitrate.Value);
            Assert.Equal(256, dialog.SelectedAudioBitrate);
            Assert.Contains("256", dialog.TxtAudioBitrateValue.Text);
            Assert.False(dialog.AppendQualitySuffix);
        });
    }

    [Fact]
    public void Change_AudioBitrate_UpdatesDescriptionAndEstimate()
    {
        StaTestRunner.Run(() =>
        {
            var aInfo = new AudioStreamInfo(1411, false, "pcm_s16le", true, 16, 44100, 2, 180.0, 50_000_000);
            var mediaInfo = new MediaStreamInfo(aInfo, null, 180.0, 50_000_000, "track.wav");

            var dialog = new ConversionOptionsDialog("mp3", "audio", 192, mediaInfo, true);

            dialog.SliderAudioBitrate.Value = 320;
            Assert.Equal(320, dialog.SelectedAudioBitrate);
            Assert.Contains("320", dialog.TxtAudioBitrateValue.Text);
            Assert.Contains("~", dialog.TxtEstimatedSize.Text);
        });
    }

    [Fact]
    public void Construct_ImageCategory_InitializesImageControls()
    {
        StaTestRunner.Run(() =>
        {
            var dialog = new ConversionOptionsDialog("jpg", "image", 85, null, true);

            Assert.Equal(Visibility.Visible, dialog.PanelImageOptions.Visibility);
            Assert.Equal(Visibility.Collapsed, dialog.PanelVideoOptions.Visibility);
            Assert.Equal(Visibility.Collapsed, dialog.PanelAudioOptions.Visibility);

            Assert.Equal(85, dialog.SliderImageQuality.Value);
            Assert.Equal(85, dialog.SelectedImageQuality);
            Assert.Equal("85%", dialog.TxtImageQualityValue.Text);

            dialog.SliderImageQuality.Value = 95;
            Assert.Equal(95, dialog.SelectedImageQuality);
            Assert.Equal("95%", dialog.TxtImageQualityValue.Text);
        });
    }

    [Fact]
    public void Checkboxes_RememberChoiceAndAppendSuffix_ToggleProperly()
    {
        StaTestRunner.Run(() =>
        {
            var dialog = new ConversionOptionsDialog("png", "image", null, null, false);

            Assert.False(dialog.RememberChoice);
            Assert.False(dialog.AppendQualitySuffix);

            dialog.ChkRemember.IsChecked = true;
            dialog.ChkAppendSuffix.IsChecked = true;

            Assert.True(dialog.RememberChoice);
            Assert.True(dialog.AppendQualitySuffix);
        });
    }

    [Fact]
    public void Construct_BatchVideo_PopulatesBatchFormatAndSourceInfo()
    {
        StaTestRunner.Run(() =>
        {
            var dialog = new ConversionOptionsDialog("mp4", "video", null, null, false, 3, 314572800);

            Assert.Equal("mp4", dialog.SelectedTargetFormat);
            Assert.Contains("3", dialog.TxtFormatPrompt.Text);
            Assert.Contains("3", dialog.TxtSourceInfo.Text);
            Assert.Equal(Visibility.Visible, dialog.TxtSourceInfo.Visibility);
        });
    }

    [Fact]
    public void Construct_Video_WithAudioStreamInfo_ResolvesMatchingAudioBitrate()
    {
        StaTestRunner.Run(() =>
        {
            var aInfo = new AudioStreamInfo(128, false, "aac", false, 16, 44100, 2, 60.0, 10_000_000);
            var vInfo = new VideoStreamInfo(false, "h264", "yuv420p", 1920, 1080, 60.0, 30.0, 10_000_000);
            var mediaInfo = new MediaStreamInfo(aInfo, vInfo, 60.0, 10_000_000, "video.mp4");

            var dialog = new ConversionOptionsDialog("mp4", "video", new VideoQualitySetting { AudioBitrateKbps = 320 }, mediaInfo, false);

            Assert.Equal(128, dialog.SelectedVideoQuality.AudioBitrateKbps);
        });
    }

    [Fact]
    public void Construct_Audio_WithAudioStreamInfo_ResolvesMatchingAudioBitrate()
    {
        StaTestRunner.Run(() =>
        {
            var aInfo = new AudioStreamInfo(128, false, "aac", false, 16, 44100, 2, 60.0, 10_000_000);
            var mediaInfo = new MediaStreamInfo(aInfo, null, 60.0, 10_000_000, "audio.aac");

            var dialog = new ConversionOptionsDialog("mp3", "audio", 320, mediaInfo, false);

            Assert.Equal(128, dialog.SelectedAudioBitrate);
            Assert.Equal(128, dialog.SliderAudioBitrate.Value);
        });
    }

    [Fact]
    public void AddBatchFiles_UpdatesBatchCountAndSourceInfo()
    {
        StaTestRunner.Run(() =>
        {
            var dialog = new ConversionOptionsDialog("mp4", "video", null, null, false, 2, 20_000_000);
            Assert.Equal(2, dialog.BatchCount);

            dialog.AddBatchFiles(["test3.mp4", "test4.mp4"]);
            Assert.Equal(4, dialog.BatchCount);
            Assert.Contains("4", dialog.TxtFormatPrompt.Text);
            Assert.Contains("4", dialog.TxtSourceInfo.Text);
        });
    }

    [Fact]
    public void UpdateMediaInfo_ResolvesAudioBitrateAndUpdatesSourceInfo()
    {
        StaTestRunner.Run(() =>
        {
            var dialog = new ConversionOptionsDialog("mp4", "video", new VideoQualitySetting { AudioBitrateKbps = 320 }, null, false, 1, null);
            Assert.Equal(320, dialog.SelectedVideoQuality.AudioBitrateKbps);

            var aInfo = new AudioStreamInfo(128, false, "aac", false, 16, 44100, 2, 60.0, 10_000_000);
            var vInfo = new VideoStreamInfo(false, "h264", "yuv420p", 1920, 1080, 60.0, 30.0, 10_000_000);
            var mediaInfo = new MediaStreamInfo(aInfo, vInfo, 60.0, 10_000_000, "sample.mp4");

            dialog.UpdateMediaInfo(mediaInfo);
            Assert.Equal(128, dialog.SelectedVideoQuality.AudioBitrateKbps);
            Assert.Equal(Visibility.Visible, dialog.TxtSourceInfo.Visibility);
            Assert.Contains("1920x1080", dialog.TxtSourceInfo.Text);
        });
    }

    [Fact]
    public void Construct_BatchVideo_DisplaysBatchEstimatedTotalSize()
    {
        StaTestRunner.Run(() =>
        {
            var aInfo = new AudioStreamInfo(128, false, "aac", false, 16, 44100, 2, 60.0, 10_000_000);
            var vInfo = new VideoStreamInfo(false, "h264", "yuv420p", 1920, 1080, 60.0, 30.0, 10_000_000);
            var mediaInfo = new MediaStreamInfo(aInfo, vInfo, 60.0, 10_000_000, "sample.mp4");

            var singleDialog = new ConversionOptionsDialog("mp4", "video", null, mediaInfo, false, 1, 10_000_000);
            var batchDialog = new ConversionOptionsDialog("mp4", "video", null, mediaInfo, false, 3, 30_000_000);

            var singlePrefix = I18n.T("EstimatedFileSizeLabel").Split('~')[0].TrimEnd();
            var batchPrefix = I18n.T("EstimatedBatchSizeLabel").Split('~')[0].TrimEnd();

            Assert.Contains(singlePrefix, singleDialog.TxtEstimatedSize.Text);
            Assert.Contains(batchPrefix, batchDialog.TxtEstimatedSize.Text);
            Assert.DoesNotContain(singlePrefix, batchDialog.TxtEstimatedSize.Text);
        });
    }
}

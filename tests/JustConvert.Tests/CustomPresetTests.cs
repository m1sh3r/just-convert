using System.IO;
using System.Windows;
using System.Windows.Controls.Primitives;
using JustConvert.Cli.UI;
using JustConvert.Core;
using JustConvert.Core.Converters.Tools;
using JustConvert.Core.Windows;
using Xunit;

namespace JustConvert.Tests;

public class CustomPresetTests
{
    [Fact]
    public async Task CustomPresetValidator_EmptyArgs_ReturnsInvalid()
    {
        var result = await CustomPresetValidator.ValidateFfmpegArgumentsAsync("   ", "video");
        Assert.False(result.IsValid);
        Assert.Equal(I18n.T("PresetValidationEmptyArgs"), result.Message);
    }

    [Fact]
    public async Task CustomPresetValidator_InvalidArguments_ReturnsFailure()
    {
        var ffmpeg = ToolLocator.FindFfmpegPath();
        if (ffmpeg == null) return;

        var result = await CustomPresetValidator.ValidateFfmpegArgumentsAsync("-c:v totally_non_existent_codec_999", "video");
        Assert.False(result.IsValid);
        Assert.NotEmpty(result.Message);
    }

    [Fact]
    public async Task CustomPresetValidator_ValidArguments_ReturnsSuccess()
    {
        var ffmpeg = ToolLocator.FindFfmpegPath();
        if (ffmpeg == null) return;

        var result = await CustomPresetValidator.ValidateFfmpegArgumentsAsync("-c:v libx264 -crf 23", "video");
        Assert.True(result.IsValid);
        Assert.Equal(I18n.T("PresetValidationSuccess"), result.Message);
    }

    [Fact]
    public void CustomPreset_CreateFromCurrentSettings_UsesUserPreferences()
    {
        var settings = new AppSettings();
        settings.SetVideoQuality("mp4", new VideoQualitySetting
        {
            VideoCodec = "h265",
            Encoder = "cpu",
            VideoQualityCq = 20,
            AudioCodec = "aac",
            AudioBitrateKbps = 256
        });
        settings.AppendQualitySuffix = false;

        var preset = CustomPreset.CreateFromCurrentSettings(settings, "video", "mp4");

        Assert.Equal("video", preset.Category);
        Assert.Equal("mp4", preset.ContainerFormat);
        Assert.Equal("h265", preset.VideoCodec);
        Assert.Equal("cpu", preset.Encoder);
        Assert.Equal(20, preset.VideoQualityCq);
        Assert.Equal(256, preset.AudioBitrateKbps);
        Assert.False(preset.AppendSuffix);
        Assert.Equal("quick", preset.PresetType);
        Assert.False(preset.IsCustomCommand);
    }

    [Fact]
    public void CustomPreset_Badges_ReflectTypeAndMode()
    {
        var quickPreset = new CustomPreset
        {
            PresetType = "quick",
            IsCustomCommand = false
        };
        Assert.Equal(I18n.T("PresetBadgeQuick"), quickPreset.PresetTypeDisplayName);
        Assert.Equal(I18n.T("PresetBadgeQuick"), quickPreset.PresetBadgeText);

        var templatePreset = new CustomPreset
        {
            PresetType = "template",
            IsCustomCommand = true
        };
        Assert.Equal(I18n.T("PresetBadgeTemplate"), templatePreset.PresetTypeDisplayName);
        Assert.Contains(I18n.T("PresetBadgeCustomCommand"), templatePreset.PresetBadgeText);
    }

    [Fact]
    public void ClassicContextMenuManager_IsPresetApplicableToExtension_WorksCorrectly()
    {
        var presetAll = new CustomPreset
        {
            InputExtensions = ""
        };
        Assert.True(ClassicContextMenuManager.IsPresetApplicableToExtension(presetAll, "mp4"));
        Assert.True(ClassicContextMenuManager.IsPresetApplicableToExtension(presetAll, "mkv"));

        var presetSpecific = new CustomPreset
        {
            InputExtensions = "mp4, mkv, mov"
        };
        Assert.True(ClassicContextMenuManager.IsPresetApplicableToExtension(presetSpecific, "mp4"));
        Assert.True(ClassicContextMenuManager.IsPresetApplicableToExtension(presetSpecific, ".mkv"));
        Assert.True(ClassicContextMenuManager.IsPresetApplicableToExtension(presetSpecific, "MOV"));
        Assert.False(ClassicContextMenuManager.IsPresetApplicableToExtension(presetSpecific, "webm"));
        Assert.False(ClassicContextMenuManager.IsPresetApplicableToExtension(presetSpecific, "avi"));
    }

    [Fact]
    public void AppSettings_AddPreset_AssignsIncrementalOrder()
    {
        var settings = new AppSettings();
        var p1 = new CustomPreset { Name = "First" };
        var p2 = new CustomPreset { Name = "Second" };

        settings.AddPreset(p1);
        settings.AddPreset(p2);

        Assert.Equal(0, p1.Order);
        Assert.Equal(1, p2.Order);
    }

    [Fact]
    public void PresetEditorDialog_CustomCommandMode_ConfiguresPreset()
    {
        StaTestRunner.Run(() =>
        {
            var initial = new CustomPreset
            {
                Name = "720p Slow",
                Category = "video",
                ContainerFormat = "mp4",
                PresetType = "template",
                IsCustomCommand = true,
                CustomArguments = "-vf scale=1280:720 -c:v libx264",
                InputExtensions = "mp4, mkv"
            };

            var dialog = new PresetEditorDialog(initial);

            Assert.True(dialog.RadioPresetTemplate.IsChecked);
            Assert.True(dialog.RadioModeCustom.IsChecked);
            Assert.Equal("-vf scale=1280:720 -c:v libx264", dialog.TxtCustomArgs.Text);
            Assert.True(dialog.RadioExtCustom.IsChecked);
            Assert.Equal("mp4, mkv", dialog.TxtInputExtensions.Text);

            dialog.TxtPresetName.Text = "Edited Preset";
            dialog.RadioPresetQuick.IsChecked = true;
            dialog.TxtCustomArgs.Text = "-c:v libx265 -crf 24";
            dialog.BtnSave.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));

            Assert.Equal("Edited Preset", dialog.Preset.Name);
            Assert.Equal("quick", dialog.Preset.PresetType);
            Assert.True(dialog.Preset.IsCustomCommand);
            Assert.Equal("-c:v libx265 -crf 24", dialog.Preset.CustomArguments);
            Assert.Equal("mp4, mkv", dialog.Preset.InputExtensions);
        });
    }

    [Fact]
    public void ConversionOptionsDialog_SaveAsPresetButton_VisibleForMediaCategories()
    {
        StaTestRunner.Run(() =>
        {
            var videoDialog = new ConversionOptionsDialog("mp4", "video", new VideoQualitySetting(), null, true);
            Assert.Equal(Visibility.Visible, videoDialog.BtnSaveAsPreset.Visibility);

            var audioDialog = new ConversionOptionsDialog("mp3", "audio", null, null, true);
            Assert.Equal(Visibility.Visible, audioDialog.BtnSaveAsPreset.Visibility);

            var remuxDialog = new ConversionOptionsDialog("remux", "remux", null, null, true);
            Assert.Equal(Visibility.Collapsed, remuxDialog.BtnSaveAsPreset.Visibility);
        });
    }
}

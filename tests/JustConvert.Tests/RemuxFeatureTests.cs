using System.IO;
using JustConvert.Core;
using JustConvert.Core.Converters;
using JustConvert.Core.Converters.Tools;
using JustConvert.Core.Windows;

namespace JustConvert.Tests;

public class RemuxFeatureTests
{
    [Fact]
    public void BuildRemuxArguments_Mp4Default_IncludesAllExpectedFlags()
    {
        var setting = new RemuxSetting
        {
            TargetContainer = "mp4",
            CopyVideo = true,
            CopyAudio = true,
            CopySubtitles = true,
            FastStart = true
        };

        var args = VideoConverter.BuildRemuxArguments("in.mkv", "out.mp4", setting);

        Assert.Contains("-map 0:v?", args);
        Assert.Contains("-map 0:a?", args);
        Assert.Contains("-map 0:s?", args);
        Assert.Contains("-c copy", args);
        Assert.Contains("-movflags +faststart", args);
        Assert.Contains("-map_metadata 0", args);
    }

    [Fact]
    public void BuildRemuxArguments_MkvNoSubtitlesNoFastStart_BuildsCorrectArguments()
    {
        var setting = new RemuxSetting
        {
            TargetContainer = "mkv",
            CopyVideo = true,
            CopyAudio = true,
            CopySubtitles = false,
            FastStart = false
        };

        var args = VideoConverter.BuildRemuxArguments("in.mp4", "out.mkv", setting);

        Assert.Contains("-map 0:v?", args);
        Assert.Contains("-map 0:a?", args);
        Assert.DoesNotContain("-map 0:s?", args);
        Assert.Contains("-c copy", args);
        Assert.DoesNotContain("-movflags +faststart", args);
    }

    [Fact]
    public void CanConvert_VideoToSameContainer_ReturnsTrue()
    {
        var converter = new VideoConverter();
        Assert.True(converter.CanConvert("mp4", "mp4"));
        Assert.True(converter.CanConvert("mkv", "mkv"));
        Assert.True(converter.CanConvert("mov", "mov"));
        Assert.True(converter.CanConvert("webm", "webm"));
    }

    [Fact]
    public void CanConvert_RemuxTarget_ReturnsTrueForVideo()
    {
        var converter = new VideoConverter();
        Assert.True(converter.CanConvert("mp4", "remux"));
        Assert.True(converter.CanConvert("mkv", "remux"));
        Assert.True(converter.CanConvert("avi", "remux"));
        Assert.False(converter.CanConvert("mp3", "remux"));
    }

    [Fact]
    public void IsSameFormat_VideoFormats_DoNotFilterOutSameContainer()
    {
        Assert.False(ClassicContextMenuManager.IsSameFormat("mp4", "mp4"));
        Assert.False(ClassicContextMenuManager.IsSameFormat("mkv", "mkv"));
        Assert.False(ClassicContextMenuManager.IsSameFormat("mov", "mov"));
        Assert.False(ClassicContextMenuManager.IsSameFormat("webm", "webm"));
        Assert.False(ClassicContextMenuManager.IsSameFormat("mp4", "remux"));
    }

    [Fact]
    public void IsSameFormat_ImageFormats_StillFiltersSameFormat()
    {
        Assert.True(ClassicContextMenuManager.IsSameFormat("jpg", "jpg"));
        Assert.True(ClassicContextMenuManager.IsSameFormat("png", "png"));
    }

    [Theory]
    [InlineData("h264", "H.264")]
    [InlineData("avc1", "H.264")]
    [InlineData("hevc", "H.265/HEVC")]
    [InlineData("h265", "H.265/HEVC")]
    [InlineData("av1", "AV1")]
    [InlineData("vp9", "VP9")]
    [InlineData("aac", "AAC")]
    [InlineData("flac", "FLAC")]
    [InlineData("mp3", "MP3")]
    public void FormatCodecName_FormatsKnownCodecsProperly(string input, string expected)
    {
        var formatted = QualityEstimator.FormatCodecName(input);
        Assert.Equal(expected, formatted);
    }

    [Fact]
    public void AppSettings_RemuxSetting_SaveAndResetWork()
    {
        var settings = new AppSettings();
        var effective = settings.GetEffectiveRemuxSetting();
        Assert.Equal("mp4", effective.TargetContainer);
        Assert.True(effective.CopyVideo);
        Assert.True(effective.CopyAudio);
        Assert.True(effective.FastStart);
        Assert.False(effective.IsRemembered);

        settings.SetRemuxSetting(new RemuxSetting
        {
            TargetContainer = "mkv",
            CopySubtitles = false,
            IsRemembered = true
        });

        var updated = settings.GetEffectiveRemuxSetting();
        Assert.Equal("mkv", updated.TargetContainer);
        Assert.False(updated.CopySubtitles);
        Assert.True(updated.IsRemembered);

        settings.ResetRemuxSetting();
        var reset = settings.GetEffectiveRemuxSetting();
        Assert.Equal("mp4", reset.TargetContainer);
        Assert.False(reset.IsRemembered);
    }

    [Fact]
    public void ClassicContextMenuManager_Mp4Targets_ContainsMp4()
    {
        var settings = AppSettings.Load();
        var active = settings.GetActiveProfile();
        var videoTargets = ClassicContextMenuManager.FilterAvailableFormats(active.VideoFormats);
        var targets = videoTargets.Where(t => !ClassicContextMenuManager.IsSameFormat("mp4", t)).ToList();
        Assert.Contains("mp4", targets);
    }

    [Fact]
    public void ConversionOptionsDialog_AllCategories_ConstructsWithoutException()
    {
        StaTestRunner.Run(() =>
        {
            var d1 = new JustConvert.Cli.UI.ConversionOptionsDialog("mp4", "video", null, null, true);
            var d2 = new JustConvert.Cli.UI.ConversionOptionsDialog("remux", "remux", null, null, true);
            var d3 = new JustConvert.Cli.UI.ConversionOptionsDialog("mp3", "audio", null, null, true);
            var d4 = new JustConvert.Cli.UI.ConversionOptionsDialog("jpg", "image", null, null, true);

            Assert.NotNull(d1);
            Assert.NotNull(d2);
            Assert.NotNull(d3);
            Assert.NotNull(d4);
        });
    }
}

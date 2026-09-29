using System.IO;
using JustConvert.Core;
using JustConvert.Core.Converters;
using JustConvert.Core.Converters.Tools;

namespace JustConvert.Tests;

public class OutputFileNameHelperTests : IDisposable
{
    private readonly string _tempDir;

    public OutputFileNameHelperTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"jc_name_tests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, true);
        }
    }

    [Theory]
    [InlineData("mp4", "H.264 - CQ 23")]
    [InlineData("mp4-h264", "H.264 - CQ 23")]
    [InlineData("mp4-h264-nvenc", "H.264 NVENC - CQ 23")]
    [InlineData("mp4-h265", "H.265 - CQ 23")]
    [InlineData("mp4-h265-qsv", "H.265 QSV - CQ 23")]
    [InlineData("webm-vp9", "VP9 - CQ 23")]
    [InlineData("av1", "AV1 - CQ 23")]
    [InlineData("webm-av1-amf", "AV1 AMF - CQ 23")]
    [InlineData("mov-prores422", "ProRes 422")]
    [InlineData("mov-prores4444", "ProRes 4444")]
    [InlineData("frames", "Frames")]
    [InlineData("frames-jpg", "Frames")]
    [InlineData("gif", null)]
    public void BuildVideoSuffix_ReturnsExpectedSuffix(string targetFormat, string? expectedSuffix)
    {
        var suffix = OutputFileNameHelper.BuildVideoSuffix(targetFormat);
        Assert.Equal(expectedSuffix, suffix);
    }

    [Fact]
    public void BuildVideoSuffix_WithVbrOrCbrSetting_ReturnsBitrateSuffix()
    {
        var vbrSetting = new VideoQualitySetting
        {
            VideoCodec = "h264",
            RateControl = "vbr",
            VideoBitrateKbps = 6000
        };
        var cbrSetting = new VideoQualitySetting
        {
            VideoCodec = "hevc",
            RateControl = "cbr",
            VideoBitrateKbps = 8000
        };

        Assert.Equal("H.264 - VBR 6000k", OutputFileNameHelper.BuildVideoSuffix("mp4", vbrSetting));
        Assert.Equal("H.265 - CBR 8000k", OutputFileNameHelper.BuildVideoSuffix("mp4", cbrSetting));
    }

    [Theory]
    [InlineData("mp3", "320k")]
    [InlineData("aac", "320k")]
    [InlineData("ogg", "Vorbis - 320k")]
    [InlineData("flac", "Lossless")]
    [InlineData("wav", "PCM 16-bit")]
    [InlineData("aiff", "PCM 16-bit")]
    [InlineData("opus", "256k")]
    public void BuildAudioSuffix_WithoutSourceInfo_ReturnsExpectedDefaults(string targetFormat, string? expectedSuffix)
    {
        var suffix = OutputFileNameHelper.BuildAudioSuffix(targetFormat);
        Assert.Equal(expectedSuffix, suffix);
    }

    [Fact]
    public void BuildAudioSuffix_With24BitSource_Returns24BitSuffix()
    {
        var audioInfo = new AudioStreamInfo(
            BitrateKbps: 1500,
            HasAttachedPic: false,
            Codec: "flac",
            IsLossless: true,
            BitsPerSample: 24,
            SampleRate: 96000,
            Channels: 2);

        var flacSuffix = OutputFileNameHelper.BuildAudioSuffix("flac", audioInfo);
        var wavSuffix = OutputFileNameHelper.BuildAudioSuffix("wav", audioInfo);
        var aiffSuffix = OutputFileNameHelper.BuildAudioSuffix("aiff", audioInfo);
        var m4aSuffix = OutputFileNameHelper.BuildAudioSuffix("m4a", audioInfo);

        Assert.Equal("24-bit", flacSuffix);
        Assert.Equal("PCM 24-bit", wavSuffix);
        Assert.Equal("PCM 24-bit", aiffSuffix);
        Assert.Equal("ALAC - Lossless", m4aSuffix);
    }

    [Fact]
    public void BuildAudioSuffix_WithMonoOpus_Returns192k()
    {
        var audioInfo = new AudioStreamInfo(
            BitrateKbps: 128,
            HasAttachedPic: false,
            Codec: "opus",
            IsLossless: false,
            BitsPerSample: 16,
            SampleRate: 48000,
            Channels: 1);

        var opusSuffix = OutputFileNameHelper.BuildAudioSuffix("opus", audioInfo);
        Assert.Equal("192k", opusSuffix);
    }

    [Theory]
    [InlineData("png", null)]
    [InlineData("bmp", null)]
    [InlineData("jpg", "Q92")]
    [InlineData("jpeg", "Q92")]
    [InlineData("webp", "Q85")]
    [InlineData("avif", "Q80")]
    [InlineData("jp2", "Q85")]
    [InlineData("ico", "Multi-layer")]
    [InlineData("tiff", "LZW")]
    [InlineData("reencode", "Reencode")]
    public void BuildImageSuffix_WithDefaultQuality_ReturnsExpectedSuffix(string targetFormat, string? expectedSuffix)
    {
        var suffix = OutputFileNameHelper.BuildImageSuffix(targetFormat);
        Assert.Equal(expectedSuffix, suffix);
    }

    [Theory]
    [InlineData("jpg", 75, "Q75")]
    [InlineData("webp", 90, "Q90")]
    [InlineData("avif", 60, "Q60")]
    [InlineData("jp2", 50, "Q50")]
    public void BuildImageSuffix_WithCustomQuality_ReturnsCustomSuffix(string targetFormat, int quality, string expectedSuffix)
    {
        var suffix = OutputFileNameHelper.BuildImageSuffix(targetFormat, quality);
        Assert.Equal(expectedSuffix, suffix);
    }

    [Theory]
    [InlineData("jpg", null)]
    [InlineData("webp", null)]
    [InlineData("avif", null)]
    [InlineData("jp2", null)]
    [InlineData("ico", "Multi-layer")]
    [InlineData("tiff", "LZW")]
    [InlineData("reencode", "Reencode")]
    public void BuildImageSuffix_WhenQualitySuffixDisabled_ReturnsNullForLossyFormats(string targetFormat, string? expectedSuffix)
    {
        var suffix = OutputFileNameHelper.BuildImageSuffix(targetFormat, quality: null, appendQualitySuffix: false);
        Assert.Equal(expectedSuffix, suffix);
    }

    [Theory]
    [InlineData("png", "svg", 1024, true, "1024px")]
    [InlineData("jpg", "svg", 2048, true, "2048px - Q92")]
    [InlineData("png", "svg", 0, true, null)]
    [InlineData("png", "svg", 1024, false, null)]
    public void BuildImageSuffix_WithSvgSource_ReturnsExpectedSvgSuffix(string targetFormat, string sourceExt, int svgWidth, bool appendSuffix, string? expectedSuffix)
    {
        var suffix = OutputFileNameHelper.BuildImageSuffix(targetFormat, quality: null, appendQualitySuffix: appendSuffix, sourceExt: sourceExt, svgWidth: svgWidth);
        Assert.Equal(expectedSuffix, suffix);
    }

    [Fact]
    public void GetUniquePath_WhenFileDoesNotExist_ReturnsDirectPath()
    {
        var path = OutputFileNameHelper.GetUniquePath(_tempDir, "video", "H.264 - CQ 23", ".mp4");
        var expected = Path.Combine(_tempDir, "video [H.264 - CQ 23].mp4");

        Assert.Equal(expected, path);
    }

    [Fact]
    public void GetUniquePath_WithoutSuffix_ReturnsDirectPath()
    {
        var path = OutputFileNameHelper.GetUniquePath(_tempDir, "photo", null, ".png");
        var expected = Path.Combine(_tempDir, "photo.png");

        Assert.Equal(expected, path);
    }

    [Fact]
    public void GetUniquePath_WhenFileExists_AppendsCounter()
    {
        var initial = Path.Combine(_tempDir, "song [320k].mp3");
        File.WriteAllText(initial, "dummy");

        var path1 = OutputFileNameHelper.GetUniquePath(_tempDir, "song", "320k", ".mp3");
        var expected1 = Path.Combine(_tempDir, "song [320k] (1).mp3");
        Assert.Equal(expected1, path1);

        File.WriteAllText(path1, "dummy");

        var path2 = OutputFileNameHelper.GetUniquePath(_tempDir, "song", "320k", ".mp3");
        var expected2 = Path.Combine(_tempDir, "song [320k] (2).mp3");
        Assert.Equal(expected2, path2);
    }

    [Fact]
    public void GetUniquePath_WithoutSuffix_WhenFileExists_AppendsCounter()
    {
        var initial = Path.Combine(_tempDir, "photo.png");
        File.WriteAllText(initial, "dummy");

        var path1 = OutputFileNameHelper.GetUniquePath(_tempDir, "photo", null, ".png");
        var expected1 = Path.Combine(_tempDir, "photo (1).png");
        Assert.Equal(expected1, path1);
    }

    [Fact]
    public void GetUniquePath_ForDirectory_WhenDirectoryExists_AppendsCounter()
    {
        var initialDir = Path.Combine(_tempDir, "video [Frames]");
        Directory.CreateDirectory(initialDir);

        var path1 = OutputFileNameHelper.GetUniquePath(_tempDir, "video", "Frames", "", isDirectory: true);
        var expected1 = Path.Combine(_tempDir, "video [Frames] (1)");
        Assert.Equal(expected1, path1);
    }

    [Theory]
    [InlineData("Fast/HD", "Fast-HD")]
    [InlineData("Preset:1080p", "Preset-1080p")]
    [InlineData("Quality*Best?", "Quality-Best-")]
    [InlineData("ValidName", "ValidName")]
    [InlineData("Special !@#$^&()_+~=;, ' 🎵 音楽 тест [2026]", "Special !@#$^&()_+~=;, ' 🎵 音楽 тест [2026]")]
    public void SanitizeFileName_ReplacesInvalidCharacters_PreservesValidSpecialCharacters(string input, string expected)
    {
        var result = OutputFileNameHelper.SanitizeFileName(input);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void GetUniquePath_WithInvalidCharactersInSuffix_SanitizesAndCreatesValidPath()
    {
        var path = OutputFileNameHelper.GetUniquePath(_tempDir, "movie", "Custom/1080p:HQ", ".mp4");
        var expected = Path.Combine(_tempDir, "movie [Custom-1080p-HQ].mp4");
        Assert.Equal(expected, path);
    }
}

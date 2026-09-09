using JustConvert.Core;
using JustConvert.Core.Converters;

namespace JustConvert.Tests;

public class FormatRegistryTests
{
    private readonly ConverterRegistry _registry = new();

    private static readonly string[] VideoExtensions =
    [
        "mp4", "mkv", "avi", "mov", "webm", "wmv", "flv", "m4v"
    ];

    private static readonly string[] AudioExtensions =
    [
        "mp3", "wav", "flac", "aac", "ogg", "m4a", "wma", "opus", "aiff", "m4b"
    ];

    private static readonly string[] ImageExtensions =
    [
        "png", "jpg", "jpeg", "webp", "bmp", "gif", "tiff", "tif", "tga", "ico", "pcx", "ppm", "jp2", "heic"
    ];

    private static readonly string[] ExpectedVideoTargets =
    [
        "mp4-h264", "mp4-h265", "webm-vp9", "webm-av1",
        "mov-prores422", "mov-prores4444",
        "remux-mp4", "remux-mkv",
        "frames",
        "mp3", "wav", "flac", "aac",
        "reencode"
    ];

    private static readonly string[] ExpectedAudioTargets =
    [
        "mp3", "aac", "m4a", "wav", "flac", "ogg", "reencode"
    ];

    private static readonly string[] ExpectedImageTargets =
    [
        "png", "jpg", "webp", "ico", "bmp", "gif", "jp2", "tiff", "tga", "pcx", "ppm", "avif", "reencode"
    ];

    [Theory]
    [InlineData("mp4")]
    [InlineData("mov")]
    [InlineData("mkv")]
    [InlineData("webm")]
    [InlineData("avi")]
    public void GetAvailableTargetFormats_ForVideoExtensions_ReturnsExpectedTargets(string ext)
    {
        var targets = _registry.GetAvailableTargetFormats(ext);
        foreach (var expected in ExpectedVideoTargets)
        {
            Assert.Contains(expected, targets, StringComparer.OrdinalIgnoreCase);
        }
    }

    [Theory]
    [InlineData("mp3")]
    [InlineData("wav")]
    [InlineData("flac")]
    [InlineData("aac")]
    [InlineData("ogg")]
    [InlineData("m4a")]
    public void GetAvailableTargetFormats_ForAudioExtensions_ReturnsExpectedTargets(string ext)
    {
        var targets = _registry.GetAvailableTargetFormats(ext);
        foreach (var expected in ExpectedAudioTargets)
        {
            if (!string.Equals(expected, ext, StringComparison.OrdinalIgnoreCase))
            {
                Assert.Contains(expected, targets, StringComparer.OrdinalIgnoreCase);
            }
        }
    }

    [Theory]
    [InlineData("png")]
    [InlineData("jpg")]
    [InlineData("jpeg")]
    [InlineData("webp")]
    [InlineData("bmp")]
    [InlineData("gif")]
    [InlineData("tiff")]
    [InlineData("ico")]
    public void GetAvailableTargetFormats_ForImageExtensions_ReturnsExpectedTargets(string ext)
    {
        var targets = _registry.GetAvailableTargetFormats(ext);
        foreach (var expected in ExpectedImageTargets)
        {
            if (!IsSameImageFormat(ext, expected))
            {
                Assert.Contains(expected, targets, StringComparer.OrdinalIgnoreCase);
            }
        }
    }

    [Theory]
    [MemberData(nameof(GetAllVideoTargetCombinations))]
    public void CanConvert_ForVideoFormats_ReturnsTrue(string sourceExt, string targetFormat)
    {
        var result = _registry.CanConvert(sourceExt, targetFormat);
        Assert.True(result);
    }

    [Theory]
    [MemberData(nameof(GetAllAudioTargetCombinations))]
    public void CanConvert_ForAudioFormats_ReturnsTrue(string sourceExt, string targetFormat)
    {
        var result = _registry.CanConvert(sourceExt, targetFormat);
        Assert.True(result);
    }

    [Theory]
    [MemberData(nameof(GetAllImageTargetCombinations))]
    public void CanConvert_ForImageFormats_ReturnsTrue(string sourceExt, string targetFormat)
    {
        var result = _registry.CanConvert(sourceExt, targetFormat);
        Assert.True(result);
    }

    public static TheoryData<string, string> GetAllVideoTargetCombinations()
    {
        var data = new TheoryData<string, string>();
        foreach (var src in VideoExtensions)
        {
            foreach (var tgt in ExpectedVideoTargets)
            {
                data.Add(src, tgt);
            }
        }
        return data;
    }

    public static TheoryData<string, string> GetAllAudioTargetCombinations()
    {
        var data = new TheoryData<string, string>();
        foreach (var src in AudioExtensions)
        {
            foreach (var tgt in ExpectedAudioTargets)
            {
                if (tgt == "reencode" || !string.Equals(src, tgt, StringComparison.OrdinalIgnoreCase))
                {
                    data.Add(src, tgt);
                }
            }
        }
        return data;
    }

    public static TheoryData<string, string> GetAllImageTargetCombinations()
    {
        var data = new TheoryData<string, string>();
        foreach (var src in ImageExtensions)
        {
            foreach (var tgt in ExpectedImageTargets)
            {
                if (src == "heic" && tgt == "reencode") continue;
                if (tgt == "reencode" || !IsSameImageFormat(src, tgt))
                {
                    data.Add(src, tgt);
                }
            }
        }
        return data;
    }

    private static bool IsSameImageFormat(string src, string tgt)
    {
        var s = src.TrimStart('.').ToLowerInvariant();
        var t = tgt.TrimStart('.').ToLowerInvariant();
        if (s == t) return true;
        if (s is "jpg" or "jpeg" && t is "jpg" or "jpeg") return true;
        if (s is "tiff" or "tif" && t is "tiff" or "tif") return true;
        if (s is "jp2" or "jpeg2000" && t is "jp2" or "jpeg2000") return true;
        return false;
    }
}

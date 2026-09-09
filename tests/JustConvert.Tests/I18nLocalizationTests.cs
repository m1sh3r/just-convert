using JustConvert.Core;

namespace JustConvert.Tests;

public class I18nLocalizationTests
{
    private static readonly string[] AllFormatIdentifiers =
    [
        "mp4-h264", "mp4-h265", "webm-vp9", "webm-av1",
        "mp4-h264-nvenc", "mp4-h265-nvenc", "webm-av1-nvenc",
        "mp4-h264-qsv", "mp4-h265-qsv", "webm-vp9-qsv", "webm-av1-qsv",
        "mp4-h264-amf", "mp4-h265-amf", "webm-av1-amf",
        "mov-prores422", "mov-prores4444",
        "remux-mp4", "remux-mkv",
        "frames", "reencode",
        "mp3", "aac", "m4a", "wav", "flac", "ogg",
        "png", "jpg", "webp", "ico", "bmp", "gif", "jp2", "tiff", "tga", "pcx", "ppm", "avif"
    ];

    [Theory]
    [InlineData("en")]
    [InlineData("ru")]
    public void AllFormatIdentifiers_HaveMeaningfulTitles(string lang)
    {
        var originalCulture = System.Globalization.CultureInfo.CurrentUICulture;
        try
        {
            System.Globalization.CultureInfo.CurrentUICulture = new System.Globalization.CultureInfo(lang);
            foreach (var fmt in AllFormatIdentifiers)
            {
                var title = I18n.GetSubMenuTitle(fmt);
                Assert.False(string.IsNullOrWhiteSpace(title));
                Assert.DoesNotContain("{0}", title);

                var tooltip = I18n.GetSubMenuToolTip(fmt);
                Assert.False(string.IsNullOrWhiteSpace(tooltip));
            }
        }
        finally
        {
            System.Globalization.CultureInfo.CurrentUICulture = originalCulture;
        }
    }

    [Fact]
    public void Dictionaries_HaveMatchingKeys()
    {
        var enKeys = I18n.Strings["en"].Keys.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var ruKeys = I18n.Strings["ru"].Keys.ToHashSet(StringComparer.OrdinalIgnoreCase);

        var missingInRu = enKeys.Except(ruKeys).ToList();
        var missingInEn = ruKeys.Except(enKeys).ToList();

        Assert.Empty(missingInRu);
        Assert.Empty(missingInEn);
    }

    [Fact]
    public void I18n_T_FormatsArgumentsCorrectly()
    {
        var originalCulture = System.Globalization.CultureInfo.CurrentUICulture;
        try
        {
            System.Globalization.CultureInfo.CurrentUICulture = new System.Globalization.CultureInfo("en");
            var resultEn = I18n.T("MenuConvertTo", "MP4");
            Assert.Equal("Convert to MP4", resultEn);

            System.Globalization.CultureInfo.CurrentUICulture = new System.Globalization.CultureInfo("ru");
            var resultRu = I18n.T("MenuConvertTo", "MP4");
            Assert.Equal("Конвертировать в MP4", resultRu);
        }
        finally
        {
            System.Globalization.CultureInfo.CurrentUICulture = originalCulture;
        }
    }
}

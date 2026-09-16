using System.IO;
using JustConvert.Core;

namespace JustConvert.Tests;

public class AppSettingsAndProfilesTests
{
    [Fact]
    public void CreateDefaultProfile_ContainsAllExpectedCategories()
    {
        var profile = AppSettings.CreateDefaultProfile();

        Assert.Equal("default", profile.Id);
        Assert.True(profile.IsReadOnly);
        Assert.NotEmpty(profile.VideoFormats);
        Assert.NotEmpty(profile.AudioFormats);
        Assert.NotEmpty(profile.ImageFormats);

        Assert.Contains("mp4", profile.VideoFormats);
        Assert.Contains("remux", profile.VideoFormats);
        Assert.Contains("mp3", profile.AudioFormats);
        Assert.Contains("png", profile.ImageFormats);
    }

    [Fact]
    public void MenuProfile_Clone_CreatesDetachedEditableCopy()
    {
        var defaultProfile = AppSettings.CreateDefaultProfile();
        var clone = defaultProfile.Clone("My Custom Profile");

        Assert.NotEqual(defaultProfile.Id, clone.Id);
        Assert.Equal("My Custom Profile", clone.Name);
        Assert.False(clone.IsReadOnly);
        Assert.Equal(defaultProfile.VideoFormats.Count, clone.VideoFormats.Count);

        clone.VideoFormats.RemoveAt(0);
        Assert.NotEqual(defaultProfile.VideoFormats.Count, clone.VideoFormats.Count);
    }

    [Fact]
    public void AppSettings_ExportAndImport_PreservesProfiles()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"jc_settings_test_{Guid.NewGuid():N}.json");
        try
        {
            var settings = new AppSettings();
            settings.EnsureDefaultProfile();

            var custom = settings.GetActiveProfile().Clone("Export Test");
            custom.AudioFormats.Clear();
            custom.AudioFormats.Add("wav");
            settings.Profiles.Add(custom);
            settings.ActiveProfileId = custom.Id;

            settings.Export(tempFile);
            Assert.True(File.Exists(tempFile));

            var imported = AppSettings.Import(tempFile);
            Assert.Equal(2, imported.Profiles.Count);
            Assert.Equal(custom.Id, imported.ActiveProfileId);

            var importedCustom = imported.Profiles.First(p => p.Id == custom.Id);
            Assert.Equal("Export Test", importedCustom.Name);
            Assert.Single(importedCustom.AudioFormats);
            Assert.Contains("wav", importedCustom.AudioFormats);
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [Fact]
    public void LastVideoCodec_PersistsAndPropagatesToEffectiveQuality()
    {
        var settings = new AppSettings();
        Assert.Equal("h264", settings.LastVideoCodec);

        settings.SetVideoQuality("mp4", new VideoQualitySetting { VideoCodec = "h265", VideoQualityCq = 20 });
        Assert.Equal("h265", settings.LastVideoCodec);

        var mkvQuality = settings.GetEffectiveVideoQuality("mkv");
        Assert.Equal("h265", mkvQuality.VideoCodec);

        var mp4Quality = settings.GetEffectiveVideoQuality("mp4");
        Assert.Equal("h265", mp4Quality.VideoCodec);
    }

    [Fact]
    public void LastVideoCodec_AdaptsToWebmAndNonWebmFormats()
    {
        var settings = new AppSettings { LastVideoCodec = "h265" };

        var webmQuality = settings.GetEffectiveVideoQuality("webm");
        Assert.Equal("vp9", webmQuality.VideoCodec);

        settings.LastVideoCodec = "av1";
        var webmAv1 = settings.GetEffectiveVideoQuality("webm");
        Assert.Equal("av1", webmAv1.VideoCodec);

        var mp4Av1 = settings.GetEffectiveVideoQuality("mp4");
        Assert.Equal("av1", mp4Av1.VideoCodec);

        settings.LastVideoCodec = "vp9";
        var mp4Vp9 = settings.GetEffectiveVideoQuality("mp4");
        Assert.Equal("h264", mp4Vp9.VideoCodec);
    }
}

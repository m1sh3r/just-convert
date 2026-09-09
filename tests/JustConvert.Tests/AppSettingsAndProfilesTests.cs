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

        Assert.Contains("mp4-h264", profile.VideoFormats);
        Assert.Contains("remux-mp4", profile.VideoFormats);
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
}

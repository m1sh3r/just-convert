using System.IO;
using System.Text.Json;

namespace JustConvert.Core;

public class MenuProfile
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = string.Empty;
    public bool IsReadOnly { get; set; }
    public List<string> VideoFormats { get; set; } = [];
    public List<string> AudioFormats { get; set; } = [];
    public List<string> ImageFormats { get; set; } = [];

    public MenuProfile Clone(string newName)
    {
        return new MenuProfile
        {
            Id = Guid.NewGuid().ToString("N"),
            Name = newName,
            IsReadOnly = false,
            VideoFormats = [.. VideoFormats],
            AudioFormats = [.. AudioFormats],
            ImageFormats = [.. ImageFormats]
        };
    }
}

public class AppSettings
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public string ActiveProfileId { get; set; } = "default";
    public List<MenuProfile> Profiles { get; set; } = [];

    public static string SettingsFilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "m1sh3r",
        "Just Convert",
        "settings.json"
    );

    public static AppSettings Load()
    {
        try
        {
            var path = SettingsFilePath;
            if (File.Exists(path))
            {
                var json = File.ReadAllText(path);
                var settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions);
                if (settings != null)
                {
                    settings.EnsureDefaultProfile();
                    return settings;
                }
            }
        }
        catch { }

        var defaultSettings = new AppSettings();
        defaultSettings.EnsureDefaultProfile();
        return defaultSettings;
    }

    public void Save()
    {
        try
        {
            var path = SettingsFilePath;
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            var json = JsonSerializer.Serialize(this, JsonOptions);
            File.WriteAllText(path, json);
        }
        catch { }
    }

    public void Export(string filePath)
    {
        var json = JsonSerializer.Serialize(this, JsonOptions);
        File.WriteAllText(filePath, json);
    }

    public static AppSettings Import(string filePath)
    {
        var json = File.ReadAllText(filePath);
        var settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions)
            ?? throw new InvalidOperationException("Invalid settings file format.");
        settings.EnsureDefaultProfile();
        return settings;
    }

    public MenuProfile GetActiveProfile()
    {
        return Profiles.FirstOrDefault(p => p.Id == ActiveProfileId)
            ?? Profiles.FirstOrDefault(p => p.IsReadOnly)
            ?? Profiles.First();
    }

    public void EnsureDefaultProfile()
    {
        var defaultProfile = Profiles.FirstOrDefault(p => p.Id == "default");
        var factoryDefault = CreateDefaultProfile();

        if (defaultProfile == null)
        {
            Profiles.Insert(0, factoryDefault);
        }
        else
        {
            defaultProfile.IsReadOnly = true;
            defaultProfile.Name = I18n.T("ProfileDefaultName");
            defaultProfile.VideoFormats = factoryDefault.VideoFormats;
            defaultProfile.AudioFormats = factoryDefault.AudioFormats;
            defaultProfile.ImageFormats = factoryDefault.ImageFormats;
        }

        if (string.IsNullOrEmpty(ActiveProfileId) || !Profiles.Any(p => p.Id == ActiveProfileId))
        {
            ActiveProfileId = "default";
        }
    }

    public static MenuProfile CreateDefaultProfile()
    {
        var video = new List<string> { "mp4-h264" };
        if (HardwareAccelerationDetector.HasNvencH264) video.Add("mp4-h264-nvenc");
        if (HardwareAccelerationDetector.HasQsvH264) video.Add("mp4-h264-qsv");
        if (HardwareAccelerationDetector.HasAmfH264) video.Add("mp4-h264-amf");

        video.Add("mp4-h265");
        if (HardwareAccelerationDetector.HasNvencHevc) video.Add("mp4-h265-nvenc");
        if (HardwareAccelerationDetector.HasQsvHevc) video.Add("mp4-h265-qsv");
        if (HardwareAccelerationDetector.HasAmfHevc) video.Add("mp4-h265-amf");

        video.Add("webm-vp9");
        if (HardwareAccelerationDetector.HasQsvVp9) video.Add("webm-vp9-qsv");

        video.Add("webm-av1");
        if (HardwareAccelerationDetector.HasNvencAv1) video.Add("webm-av1-nvenc");
        if (HardwareAccelerationDetector.HasQsvAv1) video.Add("webm-av1-qsv");
        if (HardwareAccelerationDetector.HasAmfAv1) video.Add("webm-av1-amf");

        video.Add("mov-prores422");
        video.Add("mov-prores4444");
        video.Add("remux-mp4");
        video.Add("remux-mkv");
        video.Add("frames");
        video.Add("mp3");
        video.Add("wav");
        video.Add("flac");
        video.Add("aac");
        video.Add("reencode");

        return new MenuProfile
        {
            Id = "default",
            Name = I18n.T("ProfileDefaultName"),
            IsReadOnly = true,
            VideoFormats = video,
            AudioFormats = ["mp3", "aac", "m4a", "wav", "flac", "ogg", "reencode"],
            ImageFormats = ["png", "jpg", "webp", "ico", "bmp", "gif", "jp2", "tiff", "tga", "pcx", "ppm", "avif", "reencode"]
        };
    }
}

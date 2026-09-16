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

public class ImageQualitySetting
{
    public int Quality { get; set; }
    public bool IsRemembered { get; set; }
}

public class VideoQualitySetting
{
    public string VideoCodec { get; set; } = "h264";
    public string Encoder { get; set; } = "auto";
    public int VideoQualityCq { get; set; } = 23;
    public string AudioCodec { get; set; } = "aac";
    public int AudioBitrateKbps { get; set; } = 192;
    public bool IsRemembered { get; set; }
}

public class AudioQualitySetting
{
    public int AudioBitrateKbps { get; set; } = 192;
    public bool IsRemembered { get; set; }
}

public class RemuxSetting
{
    public string TargetContainer { get; set; } = "mp4";
    public bool CopyVideo { get; set; } = true;
    public bool CopyAudio { get; set; } = true;
    public bool CopySubtitles { get; set; } = true;
    public bool FastStart { get; set; } = true;
    public bool IsRemembered { get; set; }
}

public class CustomPreset
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = "video";
    public string ContainerFormat { get; set; } = "mp4";
    public string VideoCodec { get; set; } = "h264";
    public string Encoder { get; set; } = "auto";
    public int VideoQualityCq { get; set; } = 23;
    public string AudioCodec { get; set; } = "aac";
    public int AudioBitrateKbps { get; set; } = 192;
    public int ImageQuality { get; set; } = 90;
    public bool AppendSuffix { get; set; } = true;
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
    public bool AppendQualitySuffix { get; set; } = true;
    public List<CustomPreset> CustomPresets { get; set; } = [];
    public Dictionary<string, VideoQualitySetting> VideoQualitySettings { get; set; } = [];
    public Dictionary<string, AudioQualitySetting> AudioQualitySettings { get; set; } = [];
    public Dictionary<string, ImageQualitySetting> ImageQualitySettings { get; set; } = [];
    public RemuxSetting RemuxSetting { get; set; } = new();
    public string LastVideoCodec { get; set; } = "h264";

    public static string NormalizeQualityFormat(string format)
    {
        var fmt = format.TrimStart('.').ToLowerInvariant();
        return fmt switch
        {
            "jpeg" => "jpg",
            "jpeg2000" => "jp2",
            _ => fmt
        };
    }

    public static bool SupportsQuality(string format)
    {
        var fmt = NormalizeQualityFormat(format);
        return fmt is "jpg" or "webp" or "avif" or "jp2";
    }

    public static int GetDefaultQuality(string format)
    {
        var fmt = NormalizeQualityFormat(format);
        return fmt switch
        {
            "jpg" => 92,
            "webp" => 85,
            "avif" => 80,
            "jp2" => 85,
            _ => 90
        };
    }

    public bool TryGetSavedQuality(string format, out int quality)
    {
        var fmt = NormalizeQualityFormat(format);
        if (ImageQualitySettings.TryGetValue(fmt, out var setting) && setting.IsRemembered)
        {
            quality = Math.Clamp(setting.Quality, 1, 100);
            return true;
        }

        quality = GetDefaultQuality(fmt);
        return false;
    }

    public int GetEffectiveQuality(string format)
    {
        var fmt = NormalizeQualityFormat(format);
        if (ImageQualitySettings.TryGetValue(fmt, out var setting))
        {
            return Math.Clamp(setting.Quality, 1, 100);
        }

        return GetDefaultQuality(fmt);
    }

    public void SetQuality(string format, int quality, bool remember)
    {
        var fmt = NormalizeQualityFormat(format);
        ImageQualitySettings[fmt] = new ImageQualitySetting
        {
            Quality = Math.Clamp(quality, 1, 100),
            IsRemembered = remember
        };
    }

    public void ResetQuality(string format)
    {
        var fmt = NormalizeQualityFormat(format);
        ImageQualitySettings.Remove(fmt);
    }

    public bool TryGetSavedVideoQuality(string format, out VideoQualitySetting setting)
    {
        var fmt = format.TrimStart('.').ToLowerInvariant();
        if (VideoQualitySettings.TryGetValue(fmt, out var s) && s.IsRemembered)
        {
            setting = s;
            return true;
        }

        setting = GetEffectiveVideoQuality(fmt);
        return false;
    }

    public VideoQualitySetting GetEffectiveVideoQuality(string format)
    {
        var fmt = format.TrimStart('.').ToLowerInvariant();
        if (VideoQualitySettings.TryGetValue(fmt, out var s))
        {
            if (!string.IsNullOrEmpty(LastVideoCodec) && !s.IsRemembered)
            {
                s.VideoCodec = AdaptCodecForFormat(fmt, LastVideoCodec);
            }
            return s;
        }

        var defaultCodec = AdaptCodecForFormat(fmt, LastVideoCodec);
        return new VideoQualitySetting
        {
            VideoCodec = defaultCodec,
            Encoder = "auto",
            VideoQualityCq = 23,
            AudioCodec = fmt == "webm" ? "opus" : "aac",
            AudioBitrateKbps = fmt == "webm" ? 128 : 192
        };
    }

    private static string AdaptCodecForFormat(string format, string? codec)
    {
        var targetCodec = !string.IsNullOrEmpty(codec) ? codec.ToLowerInvariant() : "h264";
        if (format == "webm")
        {
            return targetCodec is "vp9" or "av1" ? targetCodec : "vp9";
        }
        if (targetCodec == "vp9")
        {
            return "h264";
        }
        return targetCodec;
    }

    public void SetVideoQuality(string format, VideoQualitySetting setting)
    {
        var fmt = format.TrimStart('.').ToLowerInvariant();
        VideoQualitySettings[fmt] = setting;
        if (!string.IsNullOrEmpty(setting.VideoCodec))
        {
            LastVideoCodec = setting.VideoCodec;
        }
    }

    public void ResetVideoQuality(string format)
    {
        var fmt = format.TrimStart('.').ToLowerInvariant();
        VideoQualitySettings.Remove(fmt);
    }

    public bool TryGetSavedAudioQuality(string format, out int bitrateKbps)
    {
        var fmt = format.TrimStart('.').ToLowerInvariant();
        if (AudioQualitySettings.TryGetValue(fmt, out var s) && s.IsRemembered)
        {
            bitrateKbps = s.AudioBitrateKbps;
            return true;
        }

        bitrateKbps = GetEffectiveAudioQuality(fmt);
        return false;
    }

    public int GetEffectiveAudioQuality(string format)
    {
        var fmt = format.TrimStart('.').ToLowerInvariant();
        if (AudioQualitySettings.TryGetValue(fmt, out var s))
        {
            return s.AudioBitrateKbps;
        }

        return fmt switch
        {
            "mp3" => 320,
            "ogg" => 256,
            _ => 192
        };
    }

    public void SetAudioQuality(string format, int bitrateKbps, bool remember)
    {
        var fmt = format.TrimStart('.').ToLowerInvariant();
        AudioQualitySettings[fmt] = new AudioQualitySetting
        {
            AudioBitrateKbps = Math.Clamp(bitrateKbps, 32, 512),
            IsRemembered = remember
        };
    }

    public void ResetAudioQuality(string format)
    {
        var fmt = format.TrimStart('.').ToLowerInvariant();
        AudioQualitySettings.Remove(fmt);
    }

    public RemuxSetting GetEffectiveRemuxSetting()
    {
        return new RemuxSetting
        {
            TargetContainer = string.IsNullOrWhiteSpace(RemuxSetting.TargetContainer) ? "mp4" : RemuxSetting.TargetContainer.TrimStart('.').ToLowerInvariant(),
            CopyVideo = RemuxSetting.CopyVideo,
            CopyAudio = RemuxSetting.CopyAudio,
            CopySubtitles = RemuxSetting.CopySubtitles,
            FastStart = RemuxSetting.FastStart,
            IsRemembered = RemuxSetting.IsRemembered
        };
    }

    public void SetRemuxSetting(RemuxSetting setting)
    {
        RemuxSetting = setting;
    }

    public void ResetRemuxSetting()
    {
        RemuxSetting = new RemuxSetting();
    }

    public void AddPreset(CustomPreset preset)
    {
        CustomPresets.Add(preset);
    }

    public void UpdatePreset(CustomPreset preset)
    {
        var idx = CustomPresets.FindIndex(p => p.Id == preset.Id);
        if (idx >= 0)
        {
            CustomPresets[idx] = preset;
        }
        else
        {
            CustomPresets.Add(preset);
        }
    }

    public void DeletePreset(string presetId)
    {
        CustomPresets.RemoveAll(p => p.Id == presetId);
    }

    public CustomPreset? FindPreset(string presetId)
    {
        return CustomPresets.FirstOrDefault(p => p.Id == presetId);
    }

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
        return new MenuProfile
        {
            Id = "default",
            Name = I18n.T("ProfileDefaultName"),
            IsReadOnly = true,
            VideoFormats = ["mp4", "webm", "mkv", "mov", "gif", "frames", "mp3", "wav", "flac", "aac", "m4a", "opus", "remux", "reencode"],
            AudioFormats = ["mp3", "aac", "m4a", "wav", "flac", "ogg", "opus", "aiff", "reencode"],
            ImageFormats = ["png", "jpg", "webp", "ico", "bmp", "gif", "jp2", "tiff", "tga", "pcx", "ppm", "avif", "reencode"]
        };
    }
}

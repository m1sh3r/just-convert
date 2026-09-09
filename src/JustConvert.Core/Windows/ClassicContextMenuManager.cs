using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Microsoft.Win32;

namespace JustConvert.Core.Windows;

public enum InstallScope
{
    CurrentUser,
    AllUsers
}

[SupportedOSPlatform("windows")]
public class ClassicContextMenuManager
{
    private const string VerbRoot = "JustConvert";

    private static readonly string[] KnownExtensions =
    [
        "png", "jpg", "jpeg", "webp", "bmp", "gif", "tiff", "tif", "tga", "ico", "pcx", "ppm", "jp2", "heic",
        "mp4", "mkv", "avi", "mov", "webm", "wmv", "flv", "m4v",
        "mp3", "wav", "flac", "aac", "ogg", "m4a", "wma", "opus", "aiff", "aif", "m4b"
    ];

    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        "png", "jpg", "jpeg", "webp", "bmp", "gif", "tiff", "tif", "tga", "ico", "pcx", "ppm", "jp2", "heic"
    };

    private static readonly HashSet<string> VideoExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        "mp4", "mkv", "avi", "mov", "webm", "wmv", "flv", "m4v"
    };

    private static readonly HashSet<string> AudioExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        "mp3", "wav", "flac", "aac", "ogg", "m4a", "wma", "opus", "aiff", "aif", "m4b"
    };

    private static readonly Dictionary<string, Func<IReadOnlyList<string>>> CategoryTargetFormats = new(StringComparer.OrdinalIgnoreCase)
    {
        ["audio"] = () => ["mp3", "aac", "m4a", "wav", "flac", "ogg", "reencode"],
        ["video"] = GetVideoTargetFormats,
        ["image"] = () => ["png", "jpg", "webp", "ico", "bmp", "gif", "jp2", "tiff", "tga", "pcx", "ppm", "avif", "reencode"]
    };

    private static IReadOnlyList<string> GetVideoTargetFormats()
    {
        var list = new List<string> { "mp4-h264" };
        if (HardwareAccelerationDetector.HasNvencH264) list.Add("mp4-h264-nvenc");
        if (HardwareAccelerationDetector.HasQsvH264) list.Add("mp4-h264-qsv");
        if (HardwareAccelerationDetector.HasAmfH264) list.Add("mp4-h264-amf");

        list.Add("mp4-h265");
        if (HardwareAccelerationDetector.HasNvencHevc) list.Add("mp4-h265-nvenc");
        if (HardwareAccelerationDetector.HasQsvHevc) list.Add("mp4-h265-qsv");
        if (HardwareAccelerationDetector.HasAmfHevc) list.Add("mp4-h265-amf");

        list.Add("webm-vp9");
        if (HardwareAccelerationDetector.HasQsvVp9) list.Add("webm-vp9-qsv");

        list.Add("webm-av1");
        if (HardwareAccelerationDetector.HasNvencAv1) list.Add("webm-av1-nvenc");
        if (HardwareAccelerationDetector.HasQsvAv1) list.Add("webm-av1-qsv");
        if (HardwareAccelerationDetector.HasAmfAv1) list.Add("webm-av1-amf");

        list.Add("mov-prores422");
        list.Add("mov-prores4444");
        list.Add("remux-mp4");
        list.Add("remux-mkv");
        list.Add("frames");
        list.Add("mp3");
        list.Add("wav");
        list.Add("flac");
        list.Add("aac");
        list.Add("reencode");
        return list;
    }

    private readonly ConverterRegistry _registry;

    public ClassicContextMenuManager(ConverterRegistry registry)
    {
        _registry = registry;
    }

    public void Register(string executablePath, InstallScope scope = InstallScope.CurrentUser)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return;

        using var classesRoot = scope == InstallScope.AllUsers
            ? Registry.LocalMachine.CreateSubKey(@"Software\Classes", true)
            : Registry.CurrentUser.CreateSubKey(@"Software\Classes", true);

        if (classesRoot == null) return;

        var settings = AppSettings.Load();
        var activeProfile = settings.GetActiveProfile();

        var videoTargets = FilterAvailableFormats(activeProfile.VideoFormats);
        var audioTargets = FilterAvailableFormats(activeProfile.AudioFormats);
        var imageTargets = FilterAvailableFormats(activeProfile.ImageFormats);

        RegisterKey(classesRoot, $@"SystemFileAssociations\video\shell\{VerbRoot}", videoTargets, executablePath);
        RegisterKey(classesRoot, $@"SystemFileAssociations\audio\shell\{VerbRoot}", audioTargets, executablePath);
        RegisterKey(classesRoot, $@"SystemFileAssociations\image\shell\{VerbRoot}", imageTargets, executablePath);

        foreach (var ext in KnownExtensions)
        {
            IReadOnlyList<string> targets;
            if (ImageExtensions.Contains(ext))
            {
                targets = imageTargets;
            }
            else if (AudioExtensions.Contains(ext))
            {
                targets = audioTargets;
            }
            else if (VideoExtensions.Contains(ext))
            {
                targets = videoTargets;
            }
            else
            {
                targets = _registry.GetAvailableTargetFormats(ext);
            }

            if (targets.Count == 0) continue;

            var cleanExt = "." + ext.TrimStart('.').ToLowerInvariant();
            RegisterKey(classesRoot, $@"SystemFileAssociations\{cleanExt}\shell\{VerbRoot}", targets, executablePath);
        }

        CreateStartMenuShortcut(executablePath, scope);
        NotifyShell();
    }

    public static List<string> FilterAvailableFormats(IEnumerable<string> formats)
    {
        var result = new List<string>();
        foreach (var fmt in formats)
        {
            var f = fmt.TrimStart('.').ToLowerInvariant();
            if (f is "mp4-h264-nvenc" or "mp4-nvenc-h264" && !HardwareAccelerationDetector.HasNvencH264) continue;
            if (f is "mp4-h265-nvenc" or "mp4-hevc-nvenc" or "mp4-nvenc-h265" or "mp4-nvenc-hevc" && !HardwareAccelerationDetector.HasNvencHevc) continue;
            if (f is "webm-av1-nvenc" or "webm-nvenc-av1" or "mp4-av1-nvenc" or "mp4-nvenc-av1" && !HardwareAccelerationDetector.HasNvencAv1) continue;

            if (f is "mp4-h264-qsv" or "mp4-qsv-h264" && !HardwareAccelerationDetector.HasQsvH264) continue;
            if (f is "mp4-h265-qsv" or "mp4-hevc-qsv" or "mp4-qsv-h265" or "mp4-qsv-hevc" && !HardwareAccelerationDetector.HasQsvHevc) continue;
            if (f is "webm-vp9-qsv" or "webm-qsv-vp9" && !HardwareAccelerationDetector.HasQsvVp9) continue;
            if (f is "webm-av1-qsv" or "webm-qsv-av1" or "mp4-av1-qsv" or "mp4-qsv-av1" && !HardwareAccelerationDetector.HasQsvAv1) continue;

            if (f is "mp4-h264-amf" or "mp4-amf-h264" && !HardwareAccelerationDetector.HasAmfH264) continue;
            if (f is "mp4-h265-amf" or "mp4-hevc-amf" or "mp4-amf-h265" or "mp4-amf-hevc" && !HardwareAccelerationDetector.HasAmfHevc) continue;
            if (f is "webm-av1-amf" or "webm-amf-av1" or "mp4-av1-amf" or "mp4-amf-av1" && !HardwareAccelerationDetector.HasAmfAv1) continue;

            result.Add(fmt);
        }
        return result;
    }

    private static void RegisterKey(RegistryKey classesRoot, string shellPath, IReadOnlyList<string> targetFormats, string exePath)
    {
        using (var key = classesRoot.CreateSubKey(shellPath, true))
        {
            if (key == null) return;

            key.SetValue("MUIVerb", I18n.MenuTitle);
            key.SetValue("Icon", $"\"{exePath}\",0");
            key.SetValue("SubCommands", "");
            key.SetValue("MultiSelectModel", "Player");
        }

        try
        {
            classesRoot.DeleteSubKeyTree($@"{shellPath}\Shell", false);
        }
        catch { }

        var subCommandsRoot = $@"{shellPath}\Shell";
        int? previousGroup = null;
        for (int i = 0; i < targetFormats.Count; i++)
        {
            var target = targetFormats[i];
            var verbKey = $@"{subCommandsRoot}\{i:D2}_To_{target.ToUpperInvariant()}";
            using var subKey = classesRoot.CreateSubKey(verbKey, true);
            if (subKey == null) continue;

            var title = I18n.GetSubMenuTitle(target);

            subKey.SetValue("MUIVerb", title);
            subKey.SetValue("MultiSelectModel", "Player");

            var currentGroup = GetFormatGroup(target);
            if (previousGroup.HasValue && currentGroup != previousGroup.Value)
            {
                subKey.SetValue("SeparatorBefore", "");
                subKey.SetValue("CommandFlags", 0x20, RegistryValueKind.DWord);
            }
            previousGroup = currentGroup;

            using var commandKey = subKey.CreateSubKey("command", true);
            commandKey?.SetValue("", $"\"{exePath}\" convert \"%1\" --to {target}");
        }
    }

    private static int GetFormatGroup(string format)
    {
        var fmt = format.TrimStart('.').ToLowerInvariant();
        return fmt switch
        {
            "mp4-h264" or "h264" or "mp4" or "mp4-h264-nvenc" or "mp4-nvenc-h264" or "mp4-h264-qsv" or "mp4-qsv-h264" or "mp4-h264-amf" or "mp4-amf-h264" => 1,
            "mp4-h265" or "h265" or "hevc" or "mp4-hevc" or "mp4-h265-nvenc" or "mp4-hevc-nvenc" or "mp4-nvenc-h265" or "mp4-nvenc-hevc" or "mp4-h265-qsv" or "mp4-hevc-qsv" or "mp4-qsv-h265" or "mp4-qsv-hevc" or "mp4-h265-amf" or "mp4-hevc-amf" or "mp4-amf-h265" or "mp4-amf-hevc" => 2,
            "webm-vp9" or "vp9" or "webm-vp9-qsv" or "webm-qsv-vp9" => 3,
            "webm-av1" or "av1" or "webm" or "webm-av1-nvenc" or "webm-nvenc-av1" or "webm-av1-qsv" or "webm-qsv-av1" or "webm-av1-amf" or "webm-amf-av1" or "mp4-av1-nvenc" or "mp4-nvenc-av1" or "mp4-av1-qsv" or "mp4-qsv-av1" or "mp4-av1-amf" or "mp4-amf-av1" or "mp4-av1" => 4,
            "mov-prores422" or "mov-prores4444" or "prores422" or "prores4444" => 5,
            "remux-mp4" or "remux-mkv" or "mp4-remux" or "mkv-remux" => 6,
            "frames" or "frames-png" or "frames-jpg" => 7,

            "png" or "jpg" or "jpeg" or "webp" => 10,
            "ico" or "bmp" or "gif" => 11,
            "tiff" or "tif" or "tga" or "avif" or "heic" => 12,

            "mp3" or "aac" or "m4a" => 20,
            "wav" or "flac" => 21,
            "ogg" or "opus" => 22,

            _ => 99
        };
    }

    public void Unregister(InstallScope scope = InstallScope.CurrentUser)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return;

        using var classesRoot = scope == InstallScope.AllUsers
            ? Registry.LocalMachine.OpenSubKey(@"Software\Classes", true)
            : Registry.CurrentUser.OpenSubKey(@"Software\Classes", true);

        if (classesRoot == null) return;

        foreach (var category in CategoryTargetFormats.Keys)
        {
            var shellPath = $@"SystemFileAssociations\{category}\shell\{VerbRoot}";
            try
            {
                classesRoot.DeleteSubKeyTree(shellPath, false);
            }
            catch { }
        }

        foreach (var ext in KnownExtensions)
        {
            var cleanExt = "." + ext.TrimStart('.').ToLowerInvariant();
            var shellPath = $@"SystemFileAssociations\{cleanExt}\shell\{VerbRoot}";
            try
            {
                classesRoot.DeleteSubKeyTree(shellPath, false);
            }
            catch { }
        }

        RemoveStartMenuShortcut(scope);
        NotifyShell();
    }

    public static void CreateStartMenuShortcut(string exePath, InstallScope scope)
    {
        try
        {
            var programsFolder = scope == InstallScope.AllUsers
                ? Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms)
                : Environment.GetFolderPath(Environment.SpecialFolder.Programs);

            var shortcutPath = Path.Combine(programsFolder, "Just Convert.lnk");
            var workDir = Path.GetDirectoryName(exePath) ?? "";

            var script = $"$ws = New-Object -ComObject WScript.Shell; $s = $ws.CreateShortcut('{shortcutPath.Replace("'", "''")}'); $s.TargetPath = '{exePath.Replace("'", "''")}'; $s.WorkingDirectory = '{workDir.Replace("'", "''")}'; $s.IconLocation = '{exePath.Replace("'", "''")},0'; $s.Description = 'Just Convert'; $s.Save()";

            using var proc = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -ExecutionPolicy Bypass -Command \"{script}\"",
                CreateNoWindow = true,
                UseShellExecute = false
            });
            proc?.WaitForExit();
        }
        catch { }
    }

    public static void RemoveStartMenuShortcut(InstallScope scope)
    {
        try
        {
            var programsFolder = scope == InstallScope.AllUsers
                ? Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms)
                : Environment.GetFolderPath(Environment.SpecialFolder.Programs);

            var shortcutPath = Path.Combine(programsFolder, "Just Convert.lnk");
            if (File.Exists(shortcutPath))
            {
                File.Delete(shortcutPath);
            }
        }
        catch { }
    }

    [DllImport("shell32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern void SHChangeNotify(int wEventId, uint uFlags, IntPtr dwItem1, IntPtr dwItem2);

    private static void NotifyShell()
    {
        try
        {
            SHChangeNotify(0x08000000, 0x0000, IntPtr.Zero, IntPtr.Zero);
        }
        catch { }
    }
}

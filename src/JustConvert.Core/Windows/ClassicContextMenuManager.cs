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
        "png", "jpg", "jpeg", "webp", "bmp", "gif", "tiff", "tif", "tga", "ico",
        "mp4", "mkv", "avi", "mov", "webm", "wmv", "flv", "m4v",
        "mp3", "wav", "flac", "aac", "ogg", "m4a", "wma", "opus"
    ];

    private static readonly Dictionary<string, string[]> CategoryTargetFormats = new(StringComparer.OrdinalIgnoreCase)
    {
        ["audio"] = ["mp3", "aac", "m4a", "wav", "flac", "ogg"],
        ["video"] = ["mp4-h264", "mp4-h265", "mov-prores422", "mov-prores4444", "frames"],
        ["image"] = ["png", "jpg", "webp", "ico", "bmp", "gif", "tiff", "tga", "avif", "heic"]
    };

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

        foreach (var (category, formats) in CategoryTargetFormats)
        {
            RegisterKey(classesRoot, $@"SystemFileAssociations\{category}\shell\{VerbRoot}", formats, executablePath);
        }

        foreach (var ext in KnownExtensions)
        {
            var targets = _registry.GetAvailableTargetFormats(ext);
            if (targets.Count == 0) continue;

            var cleanExt = "." + ext.TrimStart('.').ToLowerInvariant();
            RegisterKey(classesRoot, $@"SystemFileAssociations\{cleanExt}\shell\{VerbRoot}", targets, executablePath);
        }
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
            "mp4-h264" or "mp4-h265" or "mp4" or "h264" or "h265" => 1,
            "mov-prores422" or "mov-prores4444" or "prores422" or "prores4444" => 2,
            "frames" or "frames-png" or "frames-jpg" => 3,

            "png" or "jpg" or "jpeg" or "webp" => 10,
            "ico" or "bmp" or "gif" => 11,
            "tiff" or "tif" or "tga" or "avif" or "heic" => 12,

            "mp3" or "aac" or "m4a" => 20,
            "wav" or "flac" => 21,
            "ogg" => 22,

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
    }
}

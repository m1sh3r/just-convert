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

        foreach (var ext in KnownExtensions)
        {
            var targets = _registry.GetAvailableTargetFormats(ext);
            if (targets.Count == 0) continue;

            RegisterForExtension(classesRoot, ext, targets, executablePath);
        }
    }

    private static void RegisterForExtension(RegistryKey classesRoot, string ext, IReadOnlyList<string> targetFormats, string exePath)
    {
        var cleanExt = "." + ext.TrimStart('.').ToLowerInvariant();
        var shellPath = $@"SystemFileAssociations\{cleanExt}\shell\{VerbRoot}";

        using (var key = classesRoot.CreateSubKey(shellPath, true))
        {
            if (key == null) return;

            key.SetValue("MUIVerb", I18n.MenuTitle);
            key.SetValue("Icon", $"\"{exePath}\",0");
            key.SetValue("SubCommands", "");
        }

        var subCommandsRoot = $@"{shellPath}\Shell";
        foreach (var target in targetFormats)
        {
            var verbKey = $@"{subCommandsRoot}\To_{target.ToUpperInvariant()}";
            using var subKey = classesRoot.CreateSubKey(verbKey, true);
            if (subKey == null) continue;

            var title = I18n.GetSubMenuTitle(target);

            subKey.SetValue("MUIVerb", title);
            
            using var commandKey = subKey.CreateSubKey("command", true);
            commandKey?.SetValue("", $"\"{exePath}\" convert \"%1\" --to {target}");
        }
    }

    public void Unregister(InstallScope scope = InstallScope.CurrentUser)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return;

        using var classesRoot = scope == InstallScope.AllUsers
            ? Registry.LocalMachine.OpenSubKey(@"Software\Classes", true)
            : Registry.CurrentUser.OpenSubKey(@"Software\Classes", true);

        if (classesRoot == null) return;

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

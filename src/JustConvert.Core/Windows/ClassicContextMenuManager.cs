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
        "png", "jpg", "jpeg", "webp", "bmp", "gif", "tiff", "tif", "tga", "ico", "pcx", "ppm", "jp2", "heic", "svg", "psd", "dng", "cr2", "cr3", "nef", "arw",
        "mp4", "mkv", "avi", "mov", "webm", "wmv", "flv", "m4v",
        "mp3", "wav", "flac", "aac", "ogg", "m4a", "wma", "opus", "aiff", "aif", "m4b", "alac", "ape", "wv"
    ];

    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        "png", "jpg", "jpeg", "webp", "bmp", "gif", "tiff", "tif", "tga", "ico", "pcx", "ppm", "jp2", "heic", "svg", "psd", "dng", "cr2", "cr3", "nef", "arw"
    };

    private static readonly HashSet<string> VideoExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        "mp4", "mkv", "avi", "mov", "webm", "wmv", "flv", "m4v"
    };

    private static readonly HashSet<string> AudioExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        "mp3", "wav", "flac", "aac", "ogg", "m4a", "wma", "opus", "aiff", "aif", "m4b", "alac", "ape", "wv"
    };

    private static readonly Dictionary<string, Func<IReadOnlyList<string>>> CategoryTargetFormats = new(StringComparer.OrdinalIgnoreCase)
    {
        ["audio"] = () => ["mp3", "aac", "m4a", "wav", "flac", "ogg", "opus", "aiff", "reencode"],
        ["video"] = GetVideoTargetFormats,
        ["image"] = () => ["png", "jpg", "webp", "ico", "bmp", "gif", "jp2", "tiff", "tga", "pcx", "ppm", "avif", "reencode"]
    };

    private static IReadOnlyList<string> GetVideoTargetFormats()
    {
        return
        [
            "mp4", "webm", "mkv", "mov", "gif", "frames",
            "mp3", "wav", "flac", "aac", "m4a", "opus",
            "reencode"
        ];
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
            IReadOnlyList<string> baseTargets;
            if (ImageExtensions.Contains(ext))
            {
                baseTargets = imageTargets;
            }
            else if (AudioExtensions.Contains(ext))
            {
                baseTargets = audioTargets;
            }
            else if (VideoExtensions.Contains(ext))
            {
                baseTargets = videoTargets;
            }
            else
            {
                baseTargets = _registry.GetAvailableTargetFormats(ext);
            }

            var targets = baseTargets.Where(t => !IsSameFormat(ext, t)).ToList();

            if (targets.Count == 0) continue;

            var cleanExt = "." + ext.TrimStart('.').ToLowerInvariant();
            RegisterKey(classesRoot, $@"SystemFileAssociations\{cleanExt}\shell\{VerbRoot}", targets, executablePath);
        }

        RegisterFolderMenu(classesRoot, executablePath);

        CreateStartMenuShortcut(executablePath, scope);
        NotifyShell();
    }

    public static List<string> FilterAvailableFormats(IEnumerable<string> formats)
    {
        return formats.Select(f => f.TrimStart('.').ToLowerInvariant()).ToList();
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
            var safeTarget = target.Replace(':', '_').ToUpperInvariant();
            var verbKey = $@"{subCommandsRoot}\{i:D2}_To_{safeTarget}";
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

    private static void RegisterFolderMenu(RegistryKey rootKey, string executablePath)
    {
        using var folderKey = rootKey.CreateSubKey(@"Directory\shell\JustConvertFolder", true);
        if (folderKey == null) return;

        folderKey.SetValue("", I18n.T("MenuConvertFolder"));
        folderKey.SetValue("MUIVerb", I18n.T("MenuConvertFolder"));
        folderKey.SetValue("Icon", $"\"{executablePath}\",0");

        using var commandKey = folderKey.CreateSubKey("command", true);
        commandKey?.SetValue("", $"\"{executablePath}\" \"%1\"");
    }

    public static bool IsSameFormat(string sourceExt, string targetFormat)
    {
        var src = sourceExt.TrimStart('.').ToLowerInvariant();
        var tgt = targetFormat.TrimStart('.').ToLowerInvariant();

        if (tgt is "reencode" or "frames" or "compress")
        {
            return false;
        }

        if (VideoExtensions.Contains(src) && VideoExtensions.Contains(tgt))
        {
            return false;
        }

        if (src == tgt) return true;

        if (src is "jpg" or "jpeg" && tgt is "jpg" or "jpeg") return true;
        if (src is "tiff" or "tif" && tgt is "tiff" or "tif") return true;
        if (src is "jp2" or "jpeg2000" && tgt is "jp2" or "jpeg2000") return true;
        if (src is "aiff" or "aif" && tgt is "aiff" or "aif") return true;

        return false;
    }

    private static int GetFormatGroup(string format)
    {
        var fmt = format.TrimStart('.').ToLowerInvariant();
        return fmt switch
        {
            "mp4" or "mkv" or "mov" or "webm" or "gif" => 1,
            "frames" or "frames-png" or "frames-jpg" or "frames-webp" or "frames-bmp" or "frames-tiff" => 10,
            "mp3" or "m4a" or "aac" or "wav" or "flac" or "opus" or "ogg" or "aiff" or "aif" => 20,
            "jpg" or "jpeg" or "png" or "webp" or "avif" => 30,
            "ico" or "bmp" => 31,
            "tiff" or "tif" or "jp2" or "jpeg2000" or "tga" or "pcx" or "ppm" or "heic" => 32,
            "reencode" => 40,
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

        try
        {
            classesRoot.DeleteSubKeyTree(@"Directory\shell\JustConvertFolder", false);
        }
        catch { }

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

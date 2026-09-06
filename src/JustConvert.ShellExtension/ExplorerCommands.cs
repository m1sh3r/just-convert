using System.Diagnostics;
using System.Runtime.InteropServices;
using JustConvert.Core;

namespace JustConvert.ShellExtension;

[ComVisible(true)]
[Guid("78C3D0E2-51E2-4C18-9364-8846C74D2E5B")]
public class TopLevelExplorerCommand : IExplorerCommand
{
    private static readonly ConverterRegistry Registry = new();

    public int GetTitle(IntPtr psiItemArray, out string ppszName)
    {
        ppszName = I18n.MenuTitle;
        return 0;
    }

    public int GetIcon(IntPtr psiItemArray, out string ppszIcon)
    {
        var exePath = Path.Combine(AppContext.BaseDirectory, "just-convert.exe");
        ppszIcon = File.Exists(exePath) ? $"{exePath},0" : "shell32.dll,301";
        return 0;
    }

    public int GetToolTip(IntPtr psiItemArray, out string ppszInfotip)
    {
        ppszInfotip = I18n.MenuToolTip;
        return 0;
    }

    public int GetCanonicalName(out Guid pguidCommandName)
    {
        pguidCommandName = typeof(TopLevelExplorerCommand).GUID;
        return 0;
    }

    public int GetState(IntPtr psiItemArray, bool fOkToBeSlow, out EXPCMDSTATE pCmdState)
    {
        var filePath = GetFirstFilePath(psiItemArray);
        if (string.IsNullOrEmpty(filePath))
        {
            pCmdState = EXPCMDSTATE.ECS_HIDDEN;
            return 0;
        }

        var ext = Path.GetExtension(filePath).TrimStart('.').ToLowerInvariant();
        var available = Registry.GetAvailableTargetFormats(ext);

        pCmdState = available.Count > 0 ? EXPCMDSTATE.ECS_ENABLED : EXPCMDSTATE.ECS_HIDDEN;
        return 0;
    }

    public int Invoke(IntPtr psiItemArray, IntPtr pbc) => 0;

    public int GetFlags(out EXPCMDFLAGS pFlags)
    {
        pFlags = EXPCMDFLAGS.ECF_HASSUBCOMMANDS;
        return 0;
    }

    public int EnumSubCommands(out IEnumExplorerCommand ppEnum)
    {
        ppEnum = new DynamicEnumExplorerCommand();
        return 0;
    }

    public static string? GetFirstFilePath(IntPtr psiItemArray)
    {
        if (psiItemArray == IntPtr.Zero) return null;

        try
        {
            var array = (IShellItemArray)Marshal.GetObjectForIUnknown(psiItemArray);
            if (array.GetCount(out var count) == 0 && count > 0)
            {
                if (array.GetItemAt(0, out var item) == 0 && item != null)
                {
                    const uint SIGDN_FILESYSPATH = 0x80058000;
                    if (item.GetDisplayName(SIGDN_FILESYSPATH, out var path) == 0)
                    {
                        return path;
                    }
                }
            }
        }
        catch { }

        return null;
    }
}

[ComVisible(true)]
[Guid("89D4E1F3-62F3-4D29-A475-9957D85E3F6C")]
public class SubFormatExplorerCommand : IExplorerCommand
{
    private static readonly ConverterRegistry Registry = new();
    private readonly string _targetFormat;
    private readonly bool _hasSeparatorBefore;

    public SubFormatExplorerCommand() : this("png", false) { }

    public SubFormatExplorerCommand(string targetFormat, bool hasSeparatorBefore = false)
    {
        _targetFormat = targetFormat;
        _hasSeparatorBefore = hasSeparatorBefore;
    }

    public int GetTitle(IntPtr psiItemArray, out string ppszName)
    {
        ppszName = I18n.GetSubMenuTitle(_targetFormat);
        return 0;
    }

    public int GetIcon(IntPtr psiItemArray, out string ppszIcon)
    {
        ppszIcon = "";
        return 0;
    }

    public int GetToolTip(IntPtr psiItemArray, out string ppszInfotip)
    {
        ppszInfotip = I18n.GetSubMenuToolTip(_targetFormat);
        return 0;
    }

    public int GetCanonicalName(out Guid pguidCommandName)
    {
        pguidCommandName = typeof(SubFormatExplorerCommand).GUID;
        return 0;
    }

    public int GetState(IntPtr psiItemArray, bool fOkToBeSlow, out EXPCMDSTATE pCmdState)
    {
        var filePath = TopLevelExplorerCommand.GetFirstFilePath(psiItemArray);
        if (string.IsNullOrEmpty(filePath))
        {
            pCmdState = EXPCMDSTATE.ECS_HIDDEN;
            return 0;
        }

        var ext = Path.GetExtension(filePath).TrimStart('.').ToLowerInvariant();
        var converter = Registry.FindConverter(ext, _targetFormat);
        pCmdState = converter != null ? EXPCMDSTATE.ECS_ENABLED : EXPCMDSTATE.ECS_HIDDEN;
        return 0;
    }

    public int Invoke(IntPtr psiItemArray, IntPtr pbc)
    {
        var filePath = TopLevelExplorerCommand.GetFirstFilePath(psiItemArray);
        if (string.IsNullOrEmpty(filePath)) return 0;

        var exePath = Path.Combine(AppContext.BaseDirectory, "just-convert.exe");
        if (!File.Exists(exePath))
        {
            exePath = "just-convert.exe";
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = exePath,
            Arguments = $"convert \"{filePath}\" --to {_targetFormat}",
            UseShellExecute = true
        };

        try
        {
            Process.Start(startInfo);
        }
        catch { }

        return 0;
    }

    public int GetFlags(out EXPCMDFLAGS pFlags)
    {
        pFlags = _hasSeparatorBefore ? EXPCMDFLAGS.ECF_SEPARATORBEFORE : EXPCMDFLAGS.ECF_DEFAULT;
        return 0;
    }

    public int EnumSubCommands(out IEnumExplorerCommand ppEnum)
    {
        ppEnum = null!;
        return 1;
    }
}

[ComVisible(true)]
[Guid("B89C3241-1188-4F71-B536-47B259EE34D9")]
public class SeparatorExplorerCommand : IExplorerCommand
{
    private static readonly ConverterRegistry Registry = new();
    private readonly string[] _beforeFormats;
    private readonly string[] _afterFormats;
    private readonly Guid _guid = Guid.NewGuid();

    public SeparatorExplorerCommand(string[] beforeFormats, string[] afterFormats)
    {
        _beforeFormats = beforeFormats;
        _afterFormats = afterFormats;
    }

    public int GetTitle(IntPtr psiItemArray, out string ppszName)
    {
        ppszName = null!;
        return 0;
    }

    public int GetIcon(IntPtr psiItemArray, out string ppszIcon)
    {
        ppszIcon = string.Empty;
        return 0;
    }

    public int GetToolTip(IntPtr psiItemArray, out string ppszInfotip)
    {
        ppszInfotip = string.Empty;
        return 0;
    }

    public int GetCanonicalName(out Guid pguidCommandName)
    {
        pguidCommandName = _guid;
        return 0;
    }

    public int GetState(IntPtr psiItemArray, bool fOkToBeSlow, out EXPCMDSTATE pCmdState)
    {
        var filePath = TopLevelExplorerCommand.GetFirstFilePath(psiItemArray);
        if (string.IsNullOrEmpty(filePath))
        {
            pCmdState = EXPCMDSTATE.ECS_HIDDEN;
            return 0;
        }

        var ext = Path.GetExtension(filePath).TrimStart('.').ToLowerInvariant();
        var hasBefore = _beforeFormats.Any(f => Registry.FindConverter(ext, f) != null);
        var hasAfter = _afterFormats.Any(f => Registry.FindConverter(ext, f) != null);

        pCmdState = (hasBefore && hasAfter) ? EXPCMDSTATE.ECS_ENABLED : EXPCMDSTATE.ECS_HIDDEN;
        return 0;
    }

    public int Invoke(IntPtr psiItemArray, IntPtr pbc) => 0;

    public int GetFlags(out EXPCMDFLAGS pFlags)
    {
        pFlags = EXPCMDFLAGS.ECF_ISSEPARATOR;
        return 0;
    }

    public int EnumSubCommands(out IEnumExplorerCommand ppEnum)
    {
        ppEnum = null!;
        return 1;
    }
}

[ComVisible(true)]
[Guid("9AE5F204-7304-4E3A-B586-AA68E96F407D")]
public class DynamicEnumExplorerCommand : IEnumExplorerCommand
{
    private readonly List<IExplorerCommand> _commands;
    private int _index;

    public DynamicEnumExplorerCommand()
    {
        string[] videoGroup1 = ["mp4-h264", "mp4-h265"];
        string[] videoGroup2 = ["mov-prores422", "mov-prores4444"];
        string[] videoGroup3 = ["frames"];

        string[] imageGroup1 = ["png", "jpg", "webp"];
        string[] imageGroup2 = ["ico", "bmp", "gif"];
        string[] imageGroup3 = ["tiff", "tga", "avif", "heic"];

        string[] audioGroup1 = ["mp3", "aac", "m4a"];
        string[] audioGroup2 = ["wav", "flac"];
        string[] audioGroup3 = ["ogg"];

        _commands =
        [
            // Video
            new SubFormatExplorerCommand("mp4-h264"),
            new SubFormatExplorerCommand("mp4-h265"),
            new SeparatorExplorerCommand(videoGroup1, videoGroup2),
            new SubFormatExplorerCommand("mov-prores422", hasSeparatorBefore: true),
            new SubFormatExplorerCommand("mov-prores4444"),
            new SeparatorExplorerCommand(videoGroup2, videoGroup3),
            new SubFormatExplorerCommand("frames", hasSeparatorBefore: true),

            // Images
            new SubFormatExplorerCommand("png"),
            new SubFormatExplorerCommand("jpg"),
            new SubFormatExplorerCommand("webp"),
            new SeparatorExplorerCommand(imageGroup1, imageGroup2),
            new SubFormatExplorerCommand("ico", hasSeparatorBefore: true),
            new SubFormatExplorerCommand("bmp"),
            new SubFormatExplorerCommand("gif"),
            new SeparatorExplorerCommand(imageGroup2, imageGroup3),
            new SubFormatExplorerCommand("tiff", hasSeparatorBefore: true),
            new SubFormatExplorerCommand("tga"),
            new SubFormatExplorerCommand("avif"),
            new SubFormatExplorerCommand("heic"),

            // Audio
            new SubFormatExplorerCommand("mp3"),
            new SubFormatExplorerCommand("aac"),
            new SubFormatExplorerCommand("m4a"),
            new SeparatorExplorerCommand(audioGroup1, audioGroup2),
            new SubFormatExplorerCommand("wav", hasSeparatorBefore: true),
            new SubFormatExplorerCommand("flac"),
            new SeparatorExplorerCommand(audioGroup2, audioGroup3),
            new SubFormatExplorerCommand("ogg", hasSeparatorBefore: true)
        ];
        _index = 0;
    }

    public int Next(uint celt, out IExplorerCommand[] apUICommand, out uint pceltFetched)
    {
        var list = new List<IExplorerCommand>();
        while (_index < _commands.Count && list.Count < celt)
        {
            list.Add(_commands[_index++]);
        }

        apUICommand = list.ToArray();
        pceltFetched = (uint)list.Count;

        return pceltFetched == celt ? 0 : 1;
    }

    public int Skip(uint celt)
    {
        _index += (int)celt;
        return _index <= _commands.Count ? 0 : 1;
    }

    public int Reset()
    {
        _index = 0;
        return 0;
    }

    public int Clone(out IEnumExplorerCommand ppenum)
    {
        ppenum = new DynamicEnumExplorerCommand();
        return 0;
    }
}

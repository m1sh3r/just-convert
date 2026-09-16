using System.IO;

namespace JustConvert.Core.Converters.Tools;

public static class ToolLocator
{
    public static string? FindFfmpegPath()
    {
        return FindToolPath("ffmpeg.exe", "FFMPEG_PATH", "FFMPEG_HOME");
    }

    public static string? FindFfprobePath()
    {
        var ffmpeg = FindFfmpegPath();
        if (ffmpeg != null)
        {
            var dir = Path.GetDirectoryName(ffmpeg);
            if (!string.IsNullOrEmpty(dir))
            {
                var probeNearFfmpeg = Path.Combine(dir, "ffprobe.exe");
                if (File.Exists(probeNearFfmpeg)) return probeNearFfmpeg;
            }
        }

        return FindToolPath("ffprobe.exe", "FFPROBE_PATH");
    }

    public static string? FindMagickPath()
    {
        return FindToolPath("magick.exe", "MAGICK_PATH", "MAGICK_HOME");
    }

    public static string? FindToolPath(string executableName, params string[] environmentVariables)
    {
        var localExe = Path.Combine(AppContext.BaseDirectory, executableName);
        if (File.Exists(localExe)) return localExe;

        var localBinExe = Path.Combine(AppContext.BaseDirectory, "bin", executableName);
        if (File.Exists(localBinExe)) return localBinExe;

        string[] vendorLocations =
        [
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "m1sh3r", "Just Convert", executableName),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "m1sh3r", "JustConvert", executableName),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "m1sh3r", "Just Convert", executableName),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "m1sh3r", "JustConvert", executableName),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "m1sh3r", "Just Convert", executableName),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "m1sh3r", "JustConvert", executableName),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "Just Convert", executableName),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "JustConvert", executableName)
        ];

        foreach (var loc in vendorLocations)
        {
            if (File.Exists(loc)) return loc;
        }

        foreach (var envVar in environmentVariables)
        {
            var customPath = Environment.GetEnvironmentVariable(envVar);
            if (!string.IsNullOrEmpty(customPath))
            {
                if (File.Exists(customPath)) return customPath;

                var candidate = Path.Combine(customPath, executableName);
                if (File.Exists(candidate)) return candidate;

                var binCandidate = Path.Combine(customPath, "bin", executableName);
                if (File.Exists(binCandidate)) return binCandidate;
            }
        }

        var pathEnv = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        var paths = pathEnv.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var p in paths)
        {
            try
            {
                var cleanDir = p.Trim('\"');
                var target = Path.Combine(cleanDir, executableName);
                if (File.Exists(target)) return target;
            }
            catch { }
        }

        var wingetLinks = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Microsoft\WinGet\Links", executableName);
        if (File.Exists(wingetLinks)) return wingetLinks;

        return null;
    }
}

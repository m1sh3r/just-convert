using System.Diagnostics;
using System.IO;

namespace JustConvert.Core.Converters;

public class ImageConverter : IFormatConverter
{
    public string Name => "Image Converter (ImageMagick)";

    private static readonly HashSet<string> SupportedFormats = new(StringComparer.OrdinalIgnoreCase)
    {
        "png", "jpg", "jpeg", "webp", "ico", "bmp", "gif", "jp2", "jpeg2000", "tiff", "tif", "tga", "pcx", "ppm", "avif", "heic"
    };

    private static readonly string[] FormatsOrder =
    [
        "png", "jpg", "webp",
        "ico", "bmp", "gif", "jp2",
        "tiff", "tga", "pcx", "ppm", "avif"
    ];

    public static string? FindMagickPath()
    {
        var localExe = Path.Combine(AppContext.BaseDirectory, "magick.exe");
        if (File.Exists(localExe)) return localExe;

        string[] appLocations =
        [
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "m1sh3r", "Just Convert", "magick.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "m1sh3r", "Just Convert", "magick.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "m1sh3r", "JustConvert", "magick.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "m1sh3r", "JustConvert", "magick.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "Just Convert", "magick.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "JustConvert", "magick.exe")
        ];

        foreach (var loc in appLocations)
        {
            if (File.Exists(loc)) return loc;
        }

        var customPath = Environment.GetEnvironmentVariable("MAGICK_HOME") ?? Environment.GetEnvironmentVariable("MAGICK_PATH");
        if (!string.IsNullOrEmpty(customPath))
        {
            if (File.Exists(customPath)) return customPath;
            var binPath = Path.Combine(customPath, "magick.exe");
            if (File.Exists(binPath)) return binPath;
            binPath = Path.Combine(customPath, "bin", "magick.exe");
            if (File.Exists(binPath)) return binPath;
        }

        var paths = (Environment.GetEnvironmentVariable("PATH") ?? "")
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries);

        foreach (var p in paths)
        {
            try
            {
                var target = Path.Combine(p.Trim('\"'), "magick.exe");
                if (File.Exists(target)) return target;
            }
            catch { }
        }

        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        string[] knownLocations =
        [
            Path.Combine(localAppData, @"Microsoft\WinGet\Links\magick.exe"),
            Path.Combine(userProfile, @"scoop\shims\magick.exe"),
            Path.Combine(userProfile, @"scoop\apps\imagemagick\current\magick.exe"),
            @"C:\ProgramData\chocolatey\bin\magick.exe",
            @"C:\Program Files\ImageMagick\magick.exe"
        ];

        foreach (var loc in knownLocations)
        {
            if (File.Exists(loc)) return loc;
        }

        var progFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        if (Directory.Exists(progFiles))
        {
            try
            {
                var dirs = Directory.GetDirectories(progFiles, "ImageMagick*");
                foreach (var d in dirs)
                {
                    var exe = Path.Combine(d, "magick.exe");
                    if (File.Exists(exe)) return exe;
                }
            }
            catch { }
        }

        var wingetPackagesDir = Path.Combine(localAppData, @"Microsoft\WinGet\Packages");
        if (Directory.Exists(wingetPackagesDir))
        {
            try
            {
                var matches = Directory.GetFiles(wingetPackagesDir, "magick.exe", SearchOption.AllDirectories);
                if (matches.Length > 0) return matches[0];
            }
            catch { }
        }

        return null;
    }

    public bool CanConvert(string sourceExtension, string targetExtension)
    {
        var src = sourceExtension.TrimStart('.').ToLowerInvariant();
        var tgt = targetExtension.TrimStart('.').ToLowerInvariant();

        if (tgt == "reencode")
        {
            return SupportedFormats.Contains(src) && src is not "heic";
        }

        if (src is "heic" && tgt is "heic") return false;

        return SupportedFormats.Contains(src) && SupportedFormats.Contains(tgt) && !src.Equals(tgt, StringComparison.OrdinalIgnoreCase) && tgt is not "heic";
    }

    public IReadOnlyList<string> GetSupportedTargetFormats(string sourceExtension)
    {
        var src = sourceExtension.TrimStart('.').ToLowerInvariant();
        if (!SupportedFormats.Contains(src)) return [];

        var list = FormatsOrder
            .Where(f => !f.Equals(src, StringComparison.OrdinalIgnoreCase)
                && !(src == "jpg" && f == "jpeg")
                && !(src == "jpeg" && f == "jpg")
                && !(src == "tiff" && f == "tif")
                && !(src == "tif" && f == "tiff")
                && !(src == "jp2" && f == "jpeg2000")
                && !(src == "jpeg2000" && f == "jp2"))
            .ToList();

        if (src is not "heic")
        {
            list.Add("reencode");
        }

        return list;
    }

    public async Task<ConversionResult> ConvertAsync(
        string inputPath,
        string targetExtension,
        string? outputPath = null,
        IProgress<ConversionProgress>? progress = null,
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        var magick = FindMagickPath();

        if (magick == null)
        {
            return new ConversionResult(
                false,
                null,
                I18n.T("ImageMagickNotFound"),
                null,
                sw.Elapsed
            );
        }

        var sourceExt = Path.GetExtension(inputPath).TrimStart('.').ToLowerInvariant();
        var isReencode = targetExtension.TrimStart('.').Equals("reencode", StringComparison.OrdinalIgnoreCase);
        var targetExt = isReencode ? sourceExt : targetExtension.TrimStart('.').ToLowerInvariant();

        var outputExt = targetExt switch
        {
            "jpeg" => "jpg",
            "jpeg2000" => "jp2",
            "tif" => "tiff",
            _ => targetExt
        };

        if (string.IsNullOrWhiteSpace(outputPath))
        {
            var dir = Path.GetDirectoryName(inputPath) ?? "";
            var fileNameWithoutExt = Path.GetFileNameWithoutExtension(inputPath);
            outputPath = Path.Combine(dir, $"{fileNameWithoutExt}.{outputExt}");

            int counter = 1;
            while (File.Exists(outputPath))
            {
                outputPath = Path.Combine(dir, $"{fileNameWithoutExt}_{counter}.{outputExt}");
                counter++;
            }
        }

        try
        {
            progress?.Report(new ConversionProgress(20, I18n.T("ImageLoading")));

            var arguments = BuildArguments(inputPath, outputPath, targetExt, isReencode);
            var startInfo = new ProcessStartInfo
            {
                FileName = magick,
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            };

            using var proc = new Process { StartInfo = startInfo };
            var fullLog = new System.Text.StringBuilder();

            proc.ErrorDataReceived += (sender, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    lock (fullLog)
                    {
                        fullLog.AppendLine(e.Data);
                    }
                }
            };

            progress?.Report(new ConversionProgress(60, I18n.T("EncodingTo", targetExt)));

            proc.Start();
            proc.BeginErrorReadLine();

            using var registration = ct.Register(() =>
            {
                try
                {
                    if (!proc.HasExited)
                    {
                        proc.Kill();
                    }
                }
                catch { }
            });

            await proc.WaitForExitAsync(CancellationToken.None);
            sw.Stop();

            var logs = fullLog.ToString();

            ct.ThrowIfCancellationRequested();

            if (proc.ExitCode == 0 && File.Exists(outputPath))
            {
                progress?.Report(new ConversionProgress(100, I18n.T("StatusDone")));
                return new ConversionResult(true, outputPath, null, logs, sw.Elapsed);
            }

            try
            {
                if (outputPath != null && File.Exists(outputPath))
                {
                    File.Delete(outputPath);
                }
            }
            catch { }

            return new ConversionResult(false, null, I18n.T("ImageMagickExitError", proc.ExitCode), logs, sw.Elapsed);
        }
        catch (OperationCanceledException)
        {
            sw.Stop();
            try { if (outputPath != null && File.Exists(outputPath)) File.Delete(outputPath); } catch { }
            return new ConversionResult(false, null, I18n.T("StatusCancelled"), null, sw.Elapsed);
        }
        catch (Exception ex)
        {
            sw.Stop();
            try { if (outputPath != null && File.Exists(outputPath)) File.Delete(outputPath); } catch { }
            return new ConversionResult(false, null, ex.Message, ex.ToString(), sw.Elapsed);
        }
    }

    private static string BuildArguments(string input, string output, string targetExt, bool isReencode = false)
    {
        if (isReencode)
        {
            return targetExt switch
            {
                "png" => $"\"{input}\" -strip -quality 95 \"{output}\"",
                "jpg" or "jpeg" => $"\"{input}\" -strip -quality 92 \"{output}\"",
                "webp" => $"\"{input}\" -quality 85 \"{output}\"",
                "avif" => $"\"{input}\" -quality 80 \"{output}\"",
                "tiff" or "tif" => $"\"{input}\" -compress lzw \"{output}\"",
                "jp2" or "jpeg2000" => $"\"{input}\" -quality 85 \"{output}\"",
                _ => $"\"{input}\" \"{output}\""
            };
        }

        return targetExt switch
        {
            "png" => $"\"{input}\" -quality 95 \"{output}\"",
            "jpg" or "jpeg" => $"\"{input}\" -background white -flatten -quality 92 \"{output}\"",
            "webp" => $"\"{input}\" -quality 85 \"{output}\"",
            "avif" => $"\"{input}\" -quality 80 \"{output}\"",
            "ico" => $"\"{input}\" -resize 256x256 \"{output}\"",
            "jp2" or "jpeg2000" => $"\"{input}\" -quality 85 \"{output}\"",
            "tiff" or "tif" => $"\"{input}\" -compress lzw \"{output}\"",
            _ => $"\"{input}\" \"{output}\""
        };
    }
}

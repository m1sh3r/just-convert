using System.Diagnostics;
using System.IO;
using JustConvert.Core.Converters.Tools;
using JustConvert.Core.Logging;

namespace JustConvert.Core.Converters;

public class ImageConverter : IFormatConverter
{
    public string Name => "Image Converter (ImageMagick)";

    private static readonly HashSet<string> RawFormats = new(StringComparer.OrdinalIgnoreCase)
    {
        "dng", "cr2", "cr3", "nef", "arw"
    };

    private static readonly HashSet<string> SupportedFormats = new(StringComparer.OrdinalIgnoreCase)
    {
        "png", "jpg", "jpeg", "webp", "ico", "bmp", "gif", "jp2", "jpeg2000", "tiff", "tif", "tga", "pcx", "ppm", "avif", "heic",
        "svg", "psd", "dng", "cr2", "cr3", "nef", "arw"
    };

    private static readonly string[] FormatsOrder =
    [
        "png", "jpg", "webp",
        "ico", "bmp", "gif", "jp2",
        "tiff", "tga", "pcx", "ppm", "avif"
    ];

    public bool CanConvert(string sourceExtension, string targetExtension)
    {
        var src = sourceExtension.TrimStart('.').ToLowerInvariant();
        var tgt = targetExtension.TrimStart('.').ToLowerInvariant();

        if (tgt == "reencode")
        {
            return SupportedFormats.Contains(src) && src is not "heic" and not "svg" and not "psd" && !RawFormats.Contains(src);
        }

        if (src is "heic" && tgt is "heic") return false;

        return SupportedFormats.Contains(src) && FormatsOrder.Contains(tgt, StringComparer.OrdinalIgnoreCase) && !src.Equals(tgt, StringComparison.OrdinalIgnoreCase);
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

        if (src is not "heic" and not "svg" and not "psd" && !RawFormats.Contains(src))
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
        CancellationToken ct = default,
        IConversionController? controller = null)
    {
        var sw = Stopwatch.StartNew();
        var magick = ToolLocator.FindMagickPath();

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

        var settings = AppSettings.Load();
        var effectiveQuality = settings.GetEffectiveQuality(targetExt);
        var appendQualitySuffix = settings.AppendQualitySuffix;

        if (string.IsNullOrWhiteSpace(outputPath))
        {
            var dir = Path.GetDirectoryName(inputPath) ?? "";
            var fileNameWithoutExt = Path.GetFileNameWithoutExtension(inputPath);
            var suffix = OutputFileNameHelper.BuildImageSuffix(targetExt, effectiveQuality, appendQualitySuffix);
            outputPath = OutputFileNameHelper.GetUniquePath(dir, fileNameWithoutExt, suffix, $".{outputExt}");
        }
        else
        {
            if (string.IsNullOrEmpty(Path.GetExtension(outputPath)))
            {
                outputPath = $"{outputPath}.{outputExt}";
            }
        }

        var outDir = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(outDir) && !Directory.Exists(outDir))
        {
            Directory.CreateDirectory(outDir);
        }

        try
        {
            progress?.Report(new ConversionProgress(20, I18n.T("ImageLoading")));

            var arguments = BuildArguments(inputPath, outputPath, targetExt, sourceExt, isReencode, effectiveQuality);
            AppLogger.Info($"[ImageConverter] Conversion starting: \"{inputPath}\" -> \"{outputPath}\" (target: {targetExt})");
            AppLogger.Info($"[ImageConverter] Command: magick {arguments}");

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
            controller?.OnProcessStarted(proc);
            proc.BeginErrorReadLine();

            using var registration = ct.Register(() =>
            {
                try
                {
                    Windows.ProcessSuspender.Resume(proc);
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
            AppLogger.LogProcess("magick", arguments, proc.ExitCode, sw.Elapsed, proc.ExitCode != 0 ? logs : null);

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

            var diagMsg = ExtractDiagnosticMessage(logs, proc.ExitCode);
            AppLogger.Error($"[ImageConverter] Conversion failed for \"{inputPath}\": {diagMsg}");
            return new ConversionResult(false, null, diagMsg, logs, sw.Elapsed);
        }
        catch (OperationCanceledException)
        {
            sw.Stop();
            AppLogger.Warn($"[ImageConverter] Conversion cancelled for \"{inputPath}\"");
            try { if (outputPath != null && File.Exists(outputPath)) File.Delete(outputPath); } catch { }
            return new ConversionResult(false, null, I18n.T("StatusCancelled"), null, sw.Elapsed);
        }
        catch (Exception ex)
        {
            sw.Stop();
            AppLogger.Error($"[ImageConverter] Unexpected exception for \"{inputPath}\"", ex);
            try { if (outputPath != null && File.Exists(outputPath)) File.Delete(outputPath); } catch { }
            return new ConversionResult(false, null, ex.Message, ex.ToString(), sw.Elapsed);
        }
    }

    private static string BuildArguments(string input, string output, string targetExt, string sourceExt, bool isReencode = false, int quality = 90)
    {
        var inputSpecifier = sourceExt switch
        {
            "psd" => $"\"{input}[0]\"",
            _ when RawFormats.Contains(sourceExt) => $"\"{input}[0]\"",
            _ => $"\"{input}\""
        };

        if (isReencode)
        {
            return targetExt switch
            {
                "png" => $"{inputSpecifier} -auto-orient -strip -colorspace sRGB -quality 95 \"{output}\"",
                "jpg" or "jpeg" => $"{inputSpecifier} -auto-orient -strip -colorspace sRGB -quality {quality} \"{output}\"",
                "webp" => $"{inputSpecifier} -auto-orient -strip -colorspace sRGB -quality {quality} \"{output}\"",
                "avif" => $"{inputSpecifier} -auto-orient -strip -colorspace sRGB -quality {quality} \"{output}\"",
                "tiff" or "tif" => $"{inputSpecifier} -auto-orient -colorspace sRGB -compress lzw \"{output}\"",
                "jp2" or "jpeg2000" => $"{inputSpecifier} -auto-orient -colorspace sRGB -quality {quality} \"{output}\"",
                _ => $"{inputSpecifier} -auto-orient -colorspace sRGB \"{output}\""
            };
        }

        return targetExt switch
        {
            "png" => $"{inputSpecifier} -auto-orient -colorspace sRGB -quality 95 \"{output}\"",
            "jpg" or "jpeg" => $"{inputSpecifier} -auto-orient -background white -flatten -colorspace sRGB -quality {quality} \"{output}\"",
            "webp" => $"{inputSpecifier} -auto-orient -colorspace sRGB -quality {quality} \"{output}\"",
            "avif" => $"{inputSpecifier} -auto-orient -colorspace sRGB -quality {quality} \"{output}\"",
            "ico" => $"{inputSpecifier} -auto-orient -background transparent -define icon:auto-resize=256,128,64,48,32,16 \"{output}\"",
            "jp2" or "jpeg2000" => $"{inputSpecifier} -auto-orient -colorspace sRGB -quality {quality} \"{output}\"",
            "tiff" or "tif" => $"{inputSpecifier} -auto-orient -colorspace sRGB -compress lzw \"{output}\"",
            _ => $"{inputSpecifier} -auto-orient -colorspace sRGB \"{output}\""
        };
    }

    public static string ExtractDiagnosticMessage(string? logs, int exitCode)
    {
        if (string.IsNullOrWhiteSpace(logs))
        {
            return I18n.T("ImageMagickExitError", exitCode);
        }

        var lines = logs.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        for (int i = lines.Length - 1; i >= 0; i--)
        {
            var line = lines[i];
            if (line.StartsWith("magick:", StringComparison.OrdinalIgnoreCase) ||
                line.Contains("error", StringComparison.OrdinalIgnoreCase) ||
                line.Contains("unable to", StringComparison.OrdinalIgnoreCase) ||
                line.Contains("failed", StringComparison.OrdinalIgnoreCase) ||
                line.Contains("invalid", StringComparison.OrdinalIgnoreCase) ||
                line.Contains("no such", StringComparison.OrdinalIgnoreCase))
            {
                return line;
            }
        }

        return lines.Length > 0 ? lines[^1] : I18n.T("ImageMagickExitError", exitCode);
    }
}

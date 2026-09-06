using System.Diagnostics;
using System.IO;

namespace JustConvert.Core.Converters;

public class ImageConverter : IFormatConverter
{
    public string Name => "Image Converter (FFmpeg)";

    private static readonly HashSet<string> SupportedFormats = new(StringComparer.OrdinalIgnoreCase)
    {
        "png", "jpg", "jpeg", "webp", "bmp", "gif", "tiff", "tif", "tga", "ico", "avif", "heic"
    };

    public bool CanConvert(string sourceExtension, string targetExtension)
    {
        var src = sourceExtension.TrimStart('.').ToLowerInvariant();
        var tgt = targetExtension.TrimStart('.').ToLowerInvariant();

        return SupportedFormats.Contains(src) && SupportedFormats.Contains(tgt) && !src.Equals(tgt, StringComparison.OrdinalIgnoreCase);
    }

    public IReadOnlyList<string> GetSupportedTargetFormats(string sourceExtension)
    {
        var src = sourceExtension.TrimStart('.').ToLowerInvariant();
        if (!SupportedFormats.Contains(src)) return [];

        return SupportedFormats
            .Where(f => !f.Equals(src, StringComparison.OrdinalIgnoreCase)
                && !(src == "jpg" && f == "jpeg")
                && !(src == "jpeg" && f == "jpg")
                && !(src == "tiff" && f == "tif")
                && !(src == "tif" && f == "tiff"))
            .ToList();
    }

    public async Task<ConversionResult> ConvertAsync(
        string inputPath,
        string targetExtension,
        string? outputPath = null,
        IProgress<ConversionProgress>? progress = null,
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        var ffmpeg = MediaConverter.FindFfmpegPath();

        if (ffmpeg == null)
        {
            return new ConversionResult(
                false,
                null,
                I18n.T("FfmpegNotFound"),
                null,
                sw.Elapsed
            );
        }

        var targetExt = targetExtension.TrimStart('.').ToLowerInvariant();
        var outputExt = (targetExt == "jpeg" ? "jpg" : targetExt == "tif" ? "tiff" : targetExt);

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

            var arguments = BuildArguments(inputPath, outputPath, targetExt);
            var startInfo = new ProcessStartInfo
            {
                FileName = ffmpeg,
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

            await proc.WaitForExitAsync(ct);
            sw.Stop();

            var logs = fullLog.ToString();

            if (proc.ExitCode == 0 && File.Exists(outputPath))
            {
                progress?.Report(new ConversionProgress(100, I18n.T("StatusDone")));
                return new ConversionResult(true, outputPath, null, logs, sw.Elapsed);
            }

            return new ConversionResult(false, null, I18n.T("FfmpegExitError", proc.ExitCode), logs, sw.Elapsed);
        }
        catch (Exception ex)
        {
            sw.Stop();
            return new ConversionResult(false, null, ex.Message, ex.ToString(), sw.Elapsed);
        }
    }

    private static string BuildArguments(string input, string output, string targetExt)
    {
        return targetExt switch
        {
            "jpg" or "jpeg" => $"-y -i \"{input}\" -q:v 2 \"{output}\"",
            "webp" => $"-y -i \"{input}\" -c:v libwebp -quality 85 \"{output}\"",
            "ico" => $"-y -i \"{input}\" -vf \"scale=256:256:force_original_aspect_ratio=decrease\" \"{output}\"",
            _ => $"-y -i \"{input}\" \"{output}\""
        };
    }
}

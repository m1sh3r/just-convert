using System.Diagnostics;
using System.IO;

namespace JustConvert.Core.Converters;

public class ImageConverter : IFormatConverter
{
    public string Name => "Image Converter (FFmpeg)";

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

        var sourceExt = Path.GetExtension(inputPath).TrimStart('.').ToLowerInvariant();
        var targetExt = targetExtension.TrimStart('.').ToLowerInvariant();
        if (targetExt == "reencode")
        {
            targetExt = sourceExt;
        }

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

            return new ConversionResult(false, null, I18n.T("FfmpegExitError", proc.ExitCode), logs, sw.Elapsed);
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

    private static string BuildArguments(string input, string output, string targetExt)
    {
        return targetExt switch
        {
            "jpg" or "jpeg" => $"-y -i \"{input}\" -sws_flags +accurate_rnd+full_chroma_int+bitexact -vf \"split[s0][s1];[s0]drawbox=c=white:t=fill[bg];[bg][s1]overlay=format=auto\" -pix_fmt yuvj444p -q:v 2 -map_metadata 0 \"{output}\"",
            "webp" => $"-y -i \"{input}\" -sws_flags +accurate_rnd+full_chroma_int+bitexact -c:v libwebp -quality 85 -map_metadata 0 \"{output}\"",
            "ico" => $"-y -i \"{input}\" -sws_flags +accurate_rnd+full_chroma_int+bitexact -vf \"scale=256:256:force_original_aspect_ratio=decrease\" -map_metadata 0 \"{output}\"",
            "jp2" or "jpeg2000" => $"-y -i \"{input}\" -sws_flags +accurate_rnd+full_chroma_int+bitexact -c:v libopenjpeg -map_metadata 0 \"{output}\"",
            _ => $"-y -i \"{input}\" -sws_flags +accurate_rnd+full_chroma_int+bitexact -map_metadata 0 \"{output}\""
        };
    }
}

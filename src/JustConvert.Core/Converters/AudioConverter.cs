using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using JustConvert.Core.Converters.Tools;
using JustConvert.Core.Logging;

namespace JustConvert.Core.Converters;

public class AudioConverter : IFormatConverter
{
    public string Name => "Audio Converter (FFmpeg)";

    private static readonly HashSet<string> AudioSourceFormats = new(StringComparer.OrdinalIgnoreCase)
    {
        "mp3", "wav", "flac", "aac", "ogg", "m4a", "wma", "opus", "aiff", "aif", "m4b", "alac", "ape", "wv"
    };

    private static readonly HashSet<string> AudioTargetFormats = new(StringComparer.OrdinalIgnoreCase)
    {
        "mp3", "aac", "m4a", "wav", "flac", "ogg", "opus", "aiff", "reencode"
    };

    private static readonly string[] FormatsOrder =
    [
        "mp3", "aac", "m4a", "wav", "flac", "ogg", "opus", "aiff"
    ];

    public bool CanConvert(string sourceExtension, string targetExtension)
    {
        var src = sourceExtension.TrimStart('.').ToLowerInvariant();
        var tgt = targetExtension.TrimStart('.').ToLowerInvariant();

        if (tgt == "reencode")
        {
            return AudioSourceFormats.Contains(src);
        }

        if (src == tgt) return false;

        return AudioSourceFormats.Contains(src) && AudioTargetFormats.Contains(tgt);
    }

    public IReadOnlyList<string> GetSupportedTargetFormats(string sourceExtension)
    {
        var src = sourceExtension.TrimStart('.').ToLowerInvariant();
        if (!AudioSourceFormats.Contains(src)) return [];

        var list = FormatsOrder
            .Where(f => !f.Equals(src, StringComparison.OrdinalIgnoreCase) && !(src is "aiff" or "aif" && f is "aiff"))
            .ToList();

        list.Add("reencode");
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
        var ffmpeg = ToolLocator.FindFfmpegPath();

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
        var rawTargetExt = targetExtension.TrimStart('.').ToLowerInvariant();
        var isReencode = rawTargetExt == "reencode";
        var targetExt = isReencode ? (sourceExt is "aif" ? "aiff" : sourceExt) : (rawTargetExt is "aif" ? "aiff" : rawTargetExt);

        var outputExt = $".{targetExt}";

        MediaStreamInfo? mediaInfo = null;
        try
        {
            mediaInfo = await MediaProbe.ProbeAsync(ffmpeg, inputPath, ct);
        }
        catch { }

        var settings = AppSettings.Load();
        var effectiveBitrate = settings.GetEffectiveAudioQuality(targetExt);
        var appendSuffix = settings.AppendQualitySuffix;
        int? customBitrate = effectiveBitrate > 0 ? effectiveBitrate : null;
        var resolvedBitrate = customBitrate ?? MediaProbe.ResolveAudioBitrate(mediaInfo?.Audio, 320, 320);

        if (string.IsNullOrWhiteSpace(outputPath))
        {
            var dir = Path.GetDirectoryName(inputPath) ?? "";
            var fileNameWithoutExt = Path.GetFileNameWithoutExtension(inputPath);
            var suffix = OutputFileNameHelper.BuildAudioSuffix(targetExt, mediaInfo?.Audio, resolvedBitrate, appendSuffix: appendSuffix);
            outputPath = OutputFileNameHelper.GetUniquePath(dir, fileNameWithoutExt, suffix, outputExt);
        }
        else
        {
            if (string.IsNullOrEmpty(Path.GetExtension(outputPath)))
            {
                outputPath += outputExt;
            }
        }

        var outDir = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(outDir) && !Directory.Exists(outDir))
        {
            Directory.CreateDirectory(outDir);
        }

        try
        {
            var arguments = BuildAudioArguments(inputPath, outputPath, targetExt, mediaInfo?.Audio, isReencode, customBitrate);
            AppLogger.Info($"[AudioConverter] Conversion starting: \"{inputPath}\" -> \"{outputPath}\" (target: {targetExt})");
            AppLogger.Info($"[AudioConverter] Command: ffmpeg {arguments}");

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

            TimeSpan totalDuration = TimeSpan.Zero;
            var durationRegex = new Regex(@"Duration:\s*(\d{2}):(\d{2}):(\d{2}\.\d+)", RegexOptions.Compiled);
            var timeRegex = new Regex(@"time=(\d{2}):(\d{2}):(\d{2}\.\d+)", RegexOptions.Compiled);

            proc.ErrorDataReceived += (sender, e) =>
            {
                if (string.IsNullOrEmpty(e.Data)) return;

                lock (fullLog)
                {
                    fullLog.AppendLine(e.Data);
                }

                if (totalDuration == TimeSpan.Zero)
                {
                    var match = durationRegex.Match(e.Data);
                    if (match.Success && TimeSpan.TryParse(match.Groups[1].Value + ":" + match.Groups[2].Value + ":" + match.Groups[3].Value, CultureInfo.InvariantCulture, out var parsed))
                    {
                        totalDuration = parsed;
                    }
                }

                var timeMatch = timeRegex.Match(e.Data);
                if (timeMatch.Success && TimeSpan.TryParse(timeMatch.Groups[1].Value + ":" + timeMatch.Groups[2].Value + ":" + timeMatch.Groups[3].Value, CultureInfo.InvariantCulture, out var currentTime))
                {
                    if (totalDuration > TimeSpan.Zero)
                    {
                        var pct = Math.Clamp((currentTime.TotalSeconds / totalDuration.TotalSeconds) * 100.0, 0, 99);
                        var timeFormat = totalDuration.TotalHours >= 1 ? @"hh\:mm\:ss" : @"mm\:ss";
                        progress?.Report(new ConversionProgress(pct, I18n.T("MediaConverting"), $"{currentTime.ToString(timeFormat)} / {totalDuration.ToString(timeFormat)}"));
                    }
                    else
                    {
                        var timeLabel = I18n.T("TimeLabel");
                        var timeFormat = currentTime.TotalHours >= 1 ? @"hh\:mm\:ss" : @"mm\:ss";
                        progress?.Report(new ConversionProgress(50, I18n.T("FfmpegProcessing"), $"{timeLabel}{currentTime.ToString(timeFormat)}"));
                    }
                }
            };

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
            AppLogger.LogProcess("ffmpeg", arguments, proc.ExitCode, sw.Elapsed, proc.ExitCode != 0 ? logs : null);

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

            var diagMsg = MediaProbe.ExtractDiagnosticMessage(logs, proc.ExitCode);
            AppLogger.Error($"[AudioConverter] Conversion failed for \"{inputPath}\": {diagMsg}");
            return new ConversionResult(false, null, diagMsg, logs, sw.Elapsed);
        }
        catch (OperationCanceledException)
        {
            sw.Stop();
            AppLogger.Warn($"[AudioConverter] Conversion cancelled for \"{inputPath}\"");
            try { if (outputPath != null && File.Exists(outputPath)) File.Delete(outputPath); } catch { }
            return new ConversionResult(false, null, I18n.T("StatusCancelled"), null, sw.Elapsed);
        }
        catch (Exception ex)
        {
            sw.Stop();
            AppLogger.Error($"[AudioConverter] Unexpected exception for \"{inputPath}\"", ex);
            try { if (outputPath != null && File.Exists(outputPath)) File.Delete(outputPath); } catch { }
            return new ConversionResult(false, null, ex.Message, ex.ToString(), sw.Elapsed);
        }
    }

    public static string BuildAudioArguments(string input, string output, string targetExt, AudioStreamInfo? audioInfo = null, bool isReencode = false, int? customBitrate = null)
    {
        var hasAttachedPic = audioInfo?.HasAttachedPic ?? false;
        var isLosslessSource = audioInfo?.IsLossless ?? false;

        if (targetExt is "m4a")
        {
            if (isLosslessSource)
            {
                if (hasAttachedPic)
                {
                    return $"-y -i \"{input}\" -map 0:a:0 -map 0:v? -c:a alac -c:v copy -disposition:v:0 attached_pic -map_metadata 0 \"{output}\"";
                }
                return $"-y -i \"{input}\" -map 0:a:0 -c:a alac -map_metadata 0 \"{output}\"";
            }

            var bitrate = customBitrate ?? MediaProbe.ResolveAudioBitrate(audioInfo, 320, 320);
            var aacCodec = (!isReencode && string.Equals(audioInfo?.Codec, "aac", StringComparison.OrdinalIgnoreCase))
                ? "-c:a copy"
                : $"-c:a aac -b:a {bitrate}k";

            if (hasAttachedPic)
            {
                return $"-y -i \"{input}\" -map 0:a:0 -map 0:v? {aacCodec} -c:v copy -disposition:v:0 attached_pic -map_metadata 0 \"{output}\"";
            }
            return $"-y -i \"{input}\" -map 0:a:0 {aacCodec} -map_metadata 0 \"{output}\"";
        }

        if (targetExt is "mp3")
        {
            var bitrate = customBitrate ?? MediaProbe.ResolveAudioBitrate(audioInfo, 320, 320);
            if (hasAttachedPic)
            {
                return $"-y -i \"{input}\" -map 0:a:0 -map 0:v? -c:a libmp3lame -b:a {bitrate}k -c:v copy -disposition:v:0 attached_pic -id3v2_version 3 -metadata:s:v title=\"Album cover\" -metadata:s:v comment=\"Cover (front)\" -map_metadata 0 \"{output}\"";
            }
            return $"-y -i \"{input}\" -map 0:a:0 -c:a libmp3lame -b:a {bitrate}k -id3v2_version 3 -map_metadata 0 \"{output}\"";
        }

        if (targetExt is "wav")
        {
            var pcmCodec = (audioInfo?.BitsPerSample is >= 24) ? "pcm_s24le" : "pcm_s16le";
            return $"-y -i \"{input}\" -map 0:a:0 -c:a {pcmCodec} -map_metadata 0 \"{output}\"";
        }

        if (targetExt is "flac")
        {
            if (hasAttachedPic)
            {
                return $"-y -i \"{input}\" -map 0:a:0 -map 0:v? -c:a flac -c:v copy -disposition:v:0 attached_pic -map_metadata 0 \"{output}\"";
            }
            return $"-y -i \"{input}\" -map 0:a:0 -c:a flac -map_metadata 0 \"{output}\"";
        }

        if (targetExt is "aac")
        {
            var bitrate = customBitrate ?? MediaProbe.ResolveAudioBitrate(audioInfo, 320, 320);
            var aacCodec = (!isReencode && string.Equals(audioInfo?.Codec, "aac", StringComparison.OrdinalIgnoreCase))
                ? "-c:a copy"
                : $"-c:a aac -b:a {bitrate}k";
            return $"-y -i \"{input}\" -map 0:a:0 {aacCodec} -map_metadata 0 \"{output}\"";
        }

        if (targetExt is "opus")
        {
            var maxBitrate = (audioInfo?.Channels == 1) ? 192 : 256;
            var bitrate = customBitrate ?? Math.Min(MediaProbe.ResolveAudioBitrate(audioInfo, 192, maxBitrate), maxBitrate);
            return $"-y -i \"{input}\" -map 0:a:0 -c:a libopus -b:a {bitrate}k -vbr on -map_metadata 0 \"{output}\"";
        }

        if (targetExt is "aiff")
        {
            var aiffCodec = (audioInfo?.BitsPerSample is >= 24) ? "pcm_s24be" : "pcm_s16be";
            if (hasAttachedPic)
            {
                return $"-y -i \"{input}\" -map 0:a:0 -map 0:v? -c:a {aiffCodec} -c:v copy -disposition:v:0 attached_pic -map_metadata 0 \"{output}\"";
            }
            return $"-y -i \"{input}\" -map 0:a:0 -c:a {aiffCodec} -map_metadata 0 \"{output}\"";
        }

        if (targetExt is "ogg")
        {
            var vorbisQuality = customBitrate.HasValue
                ? (customBitrate.Value switch { >= 320 => 10, >= 256 => 8, >= 192 => 6, >= 128 => 4, _ => 2 })
                : MediaProbe.ResolveVorbisQuality(audioInfo);
            return $"-y -i \"{input}\" -map 0:a:0 -c:a libvorbis -q:a {vorbisQuality} -map_metadata 0 \"{output}\"";
        }

        if (targetExt is "wma")
        {
            var bitrate = customBitrate ?? MediaProbe.ResolveAudioBitrate(audioInfo, 192, 320);
            return $"-y -i \"{input}\" -map 0:a:0 -c:a wmav2 -b:a {bitrate}k -map_metadata 0 \"{output}\"";
        }

        return $"-y -i \"{input}\" \"{output}\"";
    }
}

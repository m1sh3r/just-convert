using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using JustConvert.Core.Converters.Tools;
using JustConvert.Core.Logging;

namespace JustConvert.Core.Converters;

public class VideoConverter : IFormatConverter
{
    public string Name => "Video Converter (FFmpeg)";

    private static readonly HashSet<string> VideoFormats = new(StringComparer.OrdinalIgnoreCase)
    {
        "mp4", "mkv", "avi", "mov", "webm", "wmv", "flv", "m4v"
    };

    private static readonly HashSet<string> VideoTargetFormats = new(StringComparer.OrdinalIgnoreCase)
    {
        "mp4", "mkv", "mov", "webm", "gif", "remux", "remux-mp4", "remux-mkv", "frames", "frames-png", "frames-jpg", "frames-webp", "frames-bmp", "frames-tiff",
        "mp4-h264", "mp4-h265", "mp4-hevc", "h264", "h265", "hevc", "webm-vp9", "vp9", "webm-av1", "av1", "mp4-av1", "mov-prores422", "mov-prores4444",
        "mp4-h264-nvenc", "mp4-nvenc-h264", "mp4-h265-nvenc", "mp4-hevc-nvenc", "mp4-nvenc-h265", "mp4-nvenc-hevc", "webm-av1-nvenc", "webm-nvenc-av1", "mp4-av1-nvenc", "mp4-nvenc-av1",
        "mp4-h264-qsv", "mp4-qsv-h264", "mp4-h265-qsv", "mp4-hevc-qsv", "mp4-qsv-h265", "mp4-qsv-hevc", "webm-vp9-qsv", "webm-qsv-vp9", "webm-av1-qsv", "webm-qsv-av1", "mp4-av1-qsv", "mp4-qsv-av1",
        "mp4-h264-amf", "mp4-amf-h264", "mp4-h265-amf", "mp4-hevc-amf", "mp4-amf-h265", "mp4-amf-hevc", "webm-av1-amf", "webm-amf-av1", "mp4-av1-amf", "mp4-amf-av1",
        "mp3", "wav", "flac", "aac", "ogg", "m4a", "opus", "aiff"
    };

    private static readonly HashSet<string> AudioExtractionTargets = new(StringComparer.OrdinalIgnoreCase)
    {
        "mp3", "wav", "flac", "aac", "ogg", "m4a", "opus", "aiff"
    };

    public bool CanConvert(string sourceExtension, string targetExtension)
    {
        var src = sourceExtension.TrimStart('.').ToLowerInvariant();
        var tgt = targetExtension.TrimStart('.').ToLowerInvariant();

        if (tgt is "reencode" or "remux")
        {
            return VideoFormats.Contains(src);
        }

        if (!VideoFormats.Contains(src)) return false;
        if (tgt.StartsWith("preset:")) return true;
        if (!VideoTargetFormats.Contains(tgt)) return false;

        if (tgt is "mp4-h264-nvenc" or "mp4-nvenc-h264" && !HardwareAccelerationDetector.HasNvencH264) return false;
        if (tgt is "mp4-h265-nvenc" or "mp4-hevc-nvenc" or "mp4-nvenc-h265" or "mp4-nvenc-hevc" && !HardwareAccelerationDetector.HasNvencHevc) return false;
        if (tgt is "webm-av1-nvenc" or "webm-nvenc-av1" or "mp4-av1-nvenc" or "mp4-nvenc-av1" && !HardwareAccelerationDetector.HasNvencAv1) return false;

        if (tgt is "mp4-h264-qsv" or "mp4-qsv-h264" && !HardwareAccelerationDetector.HasQsvH264) return false;
        if (tgt is "mp4-h265-qsv" or "mp4-hevc-qsv" or "mp4-qsv-h265" or "mp4-qsv-hevc" && !HardwareAccelerationDetector.HasQsvHevc) return false;
        if (tgt is "webm-vp9-qsv" or "webm-qsv-vp9" && !HardwareAccelerationDetector.HasQsvVp9) return false;
        if (tgt is "webm-av1-qsv" or "webm-qsv-av1" or "mp4-av1-qsv" or "mp4-qsv-av1" && !HardwareAccelerationDetector.HasQsvAv1) return false;

        if (tgt is "mp4-h264-amf" or "mp4-amf-h264" && !HardwareAccelerationDetector.HasAmfH264) return false;
        if (tgt is "mp4-h265-amf" or "mp4-hevc-amf" or "mp4-amf-h265" or "mp4-amf-hevc" && !HardwareAccelerationDetector.HasAmfHevc) return false;
        if (tgt is "webm-av1-amf" or "webm-amf-av1" or "mp4-av1-amf" or "mp4-amf-av1" && !HardwareAccelerationDetector.HasAmfAv1) return false;

        return true;
    }

    public IReadOnlyList<string> GetSupportedTargetFormats(string sourceExtension)
    {
        var src = sourceExtension.TrimStart('.').ToLowerInvariant();
        if (!VideoFormats.Contains(src)) return [];

        return
        [
            "mp4", "mkv", "mov", "webm", "gif", "frames",
            "mp3", "m4a", "aac", "wav", "flac", "opus",
            "remux", "reencode"
        ];
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
        var isRemux = rawTargetExt == "remux";
        var targetExt = isReencode ? sourceExt : rawTargetExt;

        var isExtractFrames = targetExt is "frames" or "frames-png" or "frames-jpg" or "frames-webp" or "frames-bmp" or "frames-tiff";
        var isCompress = targetExt is "compress" or "compressed";

        var settings = AppSettings.Load();
        CustomPreset? preset = null;
        VideoQualitySetting videoSetting;
        RemuxSetting? remuxSetting = null;

        string outputExt;
        if (isRemux)
        {
            remuxSetting = settings.GetEffectiveRemuxSetting();
            var container = remuxSetting.TargetContainer.TrimStart('.').ToLowerInvariant();
            outputExt = $".{container}";
            videoSetting = settings.GetEffectiveVideoQuality(container);
        }
        else if (targetExt.StartsWith("preset:"))
        {
            var presetId = targetExt.Substring(7);
            preset = settings.FindPreset(presetId);
            if (preset != null)
            {
                outputExt = $".{preset.ContainerFormat.TrimStart('.').ToLowerInvariant()}";
                videoSetting = new VideoQualitySetting
                {
                    VideoCodec = preset.VideoCodec,
                    Encoder = preset.Encoder,
                    VideoQualityCq = preset.VideoQualityCq,
                    AudioCodec = preset.AudioCodec,
                    AudioBitrateKbps = preset.AudioBitrateKbps
                };
            }
            else
            {
                outputExt = ".mp4";
                videoSetting = settings.GetEffectiveVideoQuality("mp4");
            }
        }
        else
        {
            if (targetExt is "remux-mp4" or "mp4-remux" or "mp4-copy") outputExt = ".mp4";
            else if (targetExt is "remux-mkv" or "mkv-remux" or "mkv-copy") outputExt = ".mkv";
            else if (targetExt.StartsWith("mp4", StringComparison.OrdinalIgnoreCase) && targetExt != "mp4-av1") outputExt = ".mp4";
            else if (targetExt.StartsWith("mov", StringComparison.OrdinalIgnoreCase)) outputExt = ".mov";
            else if (targetExt.StartsWith("webm", StringComparison.OrdinalIgnoreCase) || targetExt is "vp9" or "av1") outputExt = ".webm";
            else if (targetExt.StartsWith("mkv", StringComparison.OrdinalIgnoreCase)) outputExt = ".mkv";
            else if (targetExt is "h264" or "h265" or "hevc" or "mp4-av1" or "compress" or "compressed") outputExt = ".mp4";
            else outputExt = $".{targetExt}";

            videoSetting = settings.GetEffectiveVideoQuality(targetExt);
        }

        var appendSuffix = preset != null ? preset.AppendSuffix : settings.AppendQualitySuffix;

        MediaStreamInfo? mediaInfo = null;
        try
        {
            mediaInfo = await MediaProbe.ProbeAsync(ffmpeg, inputPath, ct);
        }
        catch { }

        if (string.IsNullOrWhiteSpace(outputPath))
        {
            var dir = Path.GetDirectoryName(inputPath) ?? "";
            var fileNameWithoutExt = Path.GetFileNameWithoutExtension(inputPath);

            var suffix = AudioExtractionTargets.Contains(targetExt)
                ? OutputFileNameHelper.BuildAudioSuffix(targetExt, mediaInfo?.Audio, appendSuffix: appendSuffix)
                : (preset != null && appendSuffix ? preset.Name : OutputFileNameHelper.BuildVideoSuffix(targetExt, videoSetting, appendSuffix: appendSuffix));

            if (isExtractFrames)
            {
                outputPath = OutputFileNameHelper.GetUniquePath(dir, fileNameWithoutExt, suffix, "", isDirectory: true);
                Directory.CreateDirectory(outputPath);
            }
            else
            {
                outputPath = OutputFileNameHelper.GetUniquePath(dir, fileNameWithoutExt, suffix, outputExt);
            }
        }
        else
        {
            if (!isExtractFrames && string.IsNullOrEmpty(Path.GetExtension(outputPath)))
            {
                outputPath += outputExt;
            }
        }

        if (isExtractFrames && !string.IsNullOrEmpty(outputPath) && !Directory.Exists(outputPath))
        {
            Directory.CreateDirectory(outputPath);
        }

        try
        {
            var arguments = BuildVideoArguments(inputPath, outputPath, targetExt, mediaInfo?.Audio, isReencode, mediaInfo?.Video, videoSetting, remuxSetting);
            AppLogger.Info($"[VideoConverter] Conversion starting: \"{inputPath}\" -> \"{outputPath}\" (target: {targetExt})");
            AppLogger.Info($"[VideoConverter] Command: ffmpeg {arguments}");

            var (exitCode, logs) = await ExecuteFfmpegAsync(ffmpeg, arguments, progress, controller, ct, isCompress, isExtractFrames);

            var isCpuFallback = false;
            if (exitCode != 0 && !ct.IsCancellationRequested && UsesHardwareEncoder(arguments))
            {
                var diagMsg = MediaProbe.ExtractDiagnosticMessage(logs, exitCode);
                AppLogger.Warn($"[VideoConverter] Hardware encoder failed: {diagMsg}. Falling back to CPU encoder...");

                progress?.Report(new ConversionProgress(0, I18n.T("StatusGpuFailedCpuFallback")));

                if (arguments.Contains("_nvenc")) HardwareAccelerationDetector.DisableNvenc();
                if (arguments.Contains("_qsv")) HardwareAccelerationDetector.DisableQsv();
                if (arguments.Contains("_amf")) HardwareAccelerationDetector.DisableAmf();

                DeleteOutputArtifacts(outputPath, isExtractFrames);

                var fallbackTargetExt = MapToCpuTarget(targetExt);
                var fallbackSetting = videoSetting != null ? new VideoQualitySetting
                {
                    VideoCodec = videoSetting.VideoCodec,
                    Encoder = "cpu",
                    VideoQualityCq = videoSetting.VideoQualityCq,
                    AudioCodec = videoSetting.AudioCodec,
                    AudioBitrateKbps = videoSetting.AudioBitrateKbps,
                    IsRemembered = videoSetting.IsRemembered
                } : null;
                arguments = BuildVideoArguments(inputPath, outputPath, fallbackTargetExt, mediaInfo?.Audio, isReencode, mediaInfo?.Video, fallbackSetting, remuxSetting);

                AppLogger.Info($"[VideoConverter] Fallback Command: ffmpeg {arguments}");
                (exitCode, logs) = await ExecuteFfmpegAsync(ffmpeg, arguments, progress, controller, ct, isCompress, isExtractFrames);
                isCpuFallback = true;
            }

            sw.Stop();
            ct.ThrowIfCancellationRequested();

            if (exitCode == 0)
            {
                progress?.Report(new ConversionProgress(100, isCpuFallback ? I18n.T("StatusDoneCpuFallback") : I18n.T("StatusDone")));
                return new ConversionResult(true, outputPath, null, logs, sw.Elapsed, CpuFallback: isCpuFallback);
            }

            DeleteOutputArtifacts(outputPath, isExtractFrames);

            var finalDiagMsg = MediaProbe.ExtractDiagnosticMessage(logs, exitCode);
            AppLogger.Error($"[VideoConverter] Conversion failed for \"{inputPath}\": {finalDiagMsg}");
            return new ConversionResult(false, null, finalDiagMsg, logs, sw.Elapsed);
        }
        catch (OperationCanceledException)
        {
            sw.Stop();
            AppLogger.Warn($"[VideoConverter] Conversion cancelled for \"{inputPath}\"");
            DeleteOutputArtifacts(outputPath, isExtractFrames);
            return new ConversionResult(false, null, I18n.T("StatusCancelled"), null, sw.Elapsed);
        }
        catch (Exception ex)
        {
            sw.Stop();
            AppLogger.Error($"[VideoConverter] Unexpected exception for \"{inputPath}\"", ex);
            DeleteOutputArtifacts(outputPath, isExtractFrames);
            return new ConversionResult(false, null, ex.Message, ex.ToString(), sw.Elapsed);
        }
    }

    private static bool UsesHardwareEncoder(string arguments) =>
        arguments.Contains("_nvenc") || arguments.Contains("_qsv") || arguments.Contains("_amf");

    private static string MapToCpuTarget(string targetExt) => targetExt switch
    {
        "mp4-h264-nvenc" or "mp4-nvenc-h264" or "mp4-h264-qsv" or "mp4-qsv-h264" or "mp4-h264-amf" or "mp4-amf-h264" => "mp4-h264",
        "mp4-h265-nvenc" or "mp4-hevc-nvenc" or "mp4-nvenc-h265" or "mp4-nvenc-hevc" or "mp4-h265-qsv" or "mp4-hevc-qsv" or "mp4-qsv-h265" or "mp4-qsv-hevc" or "mp4-h265-amf" or "mp4-hevc-amf" or "mp4-amf-h265" or "mp4-amf-hevc" => "mp4-hevc",
        "webm-av1-nvenc" or "webm-nvenc-av1" or "webm-av1-qsv" or "webm-qsv-av1" or "webm-av1-amf" or "webm-amf-av1" => "webm-av1",
        "mp4-av1-nvenc" or "mp4-nvenc-av1" or "mp4-av1-qsv" or "mp4-qsv-av1" or "mp4-av1-amf" or "mp4-amf-av1" => "mp4-av1",
        "webm-vp9-qsv" or "webm-qsv-vp9" => "webm-vp9",
        _ => targetExt
    };

    private static void DeleteOutputArtifacts(string? outputPath, bool isExtractFrames)
    {
        try
        {
            if (outputPath != null)
            {
                if (isExtractFrames && Directory.Exists(outputPath))
                {
                    Directory.Delete(outputPath, true);
                }
                else if (File.Exists(outputPath))
                {
                    File.Delete(outputPath);
                }
            }
        }
        catch { }
    }

    private static async Task<(int ExitCode, string Logs)> ExecuteFfmpegAsync(
        string ffmpeg,
        string arguments,
        IProgress<ConversionProgress>? progress,
        IConversionController? controller,
        CancellationToken ct,
        bool isCompress,
        bool isExtractFrames)
    {
        var sw = Stopwatch.StartNew();
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
                    var status = isCompress ? I18n.T("VideoCompressing") : isExtractFrames ? I18n.T("VideoExtractingFrames") : I18n.T("MediaConverting");
                    var timeFormat = totalDuration.TotalHours >= 1 ? @"hh\:mm\:ss" : @"mm\:ss";
                    progress?.Report(new ConversionProgress(pct, status, $"{currentTime.ToString(timeFormat)} / {totalDuration.ToString(timeFormat)}"));
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
        return (proc.ExitCode, logs);
    }

    public static string BuildRemuxArguments(string input, string output, RemuxSetting? setting = null)
    {
        var s = setting ?? new RemuxSetting();
        var outExt = Path.GetExtension(output).TrimStart('.').ToLowerInvariant();
        var sb = new StringBuilder();
        sb.Append("-y -i \"").Append(input).Append("\"");
        if (s.CopyVideo) sb.Append(" -map 0:v?");
        if (s.CopyAudio) sb.Append(" -map 0:a?");
        if (s.CopySubtitles && outExt is "mkv" or "mp4") sb.Append(" -map 0:s?");
        sb.Append(" -c copy");
        if (s.FastStart && outExt is "mp4" or "mov") sb.Append(" -movflags +faststart");
        sb.Append(" -map_metadata 0 \"").Append(output).Append("\"");
        return sb.ToString();
    }

    internal static string BuildVideoArguments(string input, string output, string targetExt, AudioStreamInfo? audioInfo = null, bool isReencode = false, VideoStreamInfo? videoInfo = null, VideoQualitySetting? videoSetting = null, RemuxSetting? remuxSetting = null)
    {
        if (targetExt == "remux")
        {
            return BuildRemuxArguments(input, output, remuxSetting);
        }

        if (AudioExtractionTargets.Contains(targetExt))
        {
            return AudioConverter.BuildAudioArguments(input, output, targetExt, audioInfo, isReencode);
        }

        var tonemapFilter = (videoInfo?.IsHdrOrWideGamut == true)
            ? "-vf \"scale=out_color_matrix=bt709:out_range=tv,format=yuv420p,setparams=color_primaries=bt709:color_trc=bt709:colorspace=bt709\" -color_primaries:v bt709 -color_trc:v bt709 -colorspace:v bt709 "
            : "";

        if (targetExt is "frames" or "frames-png")
        {
            var pattern = Path.Combine(output, "frame_%04d.png");
            return string.IsNullOrEmpty(tonemapFilter)
                ? $"-y -i \"{input}\" \"{pattern}\""
                : $"-y -i \"{input}\" {tonemapFilter}\"{pattern}\"";
        }

        if (targetExt == "frames-jpg")
        {
            var pattern = Path.Combine(output, "frame_%04d.jpg");
            return string.IsNullOrEmpty(tonemapFilter)
                ? $"-y -i \"{input}\" -qscale:v 2 \"{pattern}\""
                : $"-y -i \"{input}\" {tonemapFilter}-qscale:v 2 \"{pattern}\"";
        }

        if (targetExt == "frames-webp")
        {
            var pattern = Path.Combine(output, "frame_%04d.webp");
            return string.IsNullOrEmpty(tonemapFilter)
                ? $"-y -i \"{input}\" -qscale:v 85 \"{pattern}\""
                : $"-y -i \"{input}\" {tonemapFilter}-qscale:v 85 \"{pattern}\"";
        }

        if (targetExt == "frames-bmp")
        {
            var pattern = Path.Combine(output, "frame_%04d.bmp");
            return string.IsNullOrEmpty(tonemapFilter)
                ? $"-y -i \"{input}\" \"{pattern}\""
                : $"-y -i \"{input}\" {tonemapFilter}\"{pattern}\"";
        }

        if (targetExt == "frames-tiff")
        {
            var pattern = Path.Combine(output, "frame_%04d.tiff");
            return string.IsNullOrEmpty(tonemapFilter)
                ? $"-y -i \"{input}\" \"{pattern}\""
                : $"-y -i \"{input}\" {tonemapFilter}\"{pattern}\"";
        }

        if (targetExt is "remux-mp4" or "mp4-remux" or "mp4-copy")
        {
            return $"-y -i \"{input}\" -map 0:v? -map 0:a? -c copy -movflags +faststart -map_metadata 0 \"{output}\"";
        }

        if (targetExt is "remux-mkv" or "mkv-remux" or "mkv-copy")
        {
            return $"-y -i \"{input}\" -map 0:v? -map 0:a? -map 0:s? -c copy -map_metadata 0 \"{output}\"";
        }

        if (targetExt is "compress" or "compressed")
        {
            return $"-y -i \"{input}\" -map 0:v:0 -map 0:a? {tonemapFilter}-c:v libx264 -crf 28 -preset medium -c:a aac -b:a 128k -movflags +faststart -sws_flags spline+accurate_rnd+full_chroma_int -map_metadata 0 \"{output}\"";
        }

        if (videoSetting != null && (targetExt is "mp4" or "webm" or "mkv" or "mov" || targetExt.StartsWith("preset:")))
        {
            return BuildSettingBasedVideoArguments(input, output, targetExt, videoSetting, tonemapFilter, audioInfo);
        }

        if (targetExt is "mp4" or "mp4-h264" or "h264")
        {
            return $"-y -i \"{input}\" -map 0:v:0 -map 0:a? {tonemapFilter}-c:v libx264 -crf 23 -preset medium -c:a aac -b:a 192k -movflags +faststart -sws_flags spline+accurate_rnd+full_chroma_int -map_metadata 0 \"{output}\"";
        }

        if (targetExt is "mp4-hevc" or "mp4-h265" or "h265" or "hevc")
        {
            return $"-y -i \"{input}\" -map 0:v:0 -map 0:a? -c:v libx265 -crf 23 -preset medium -tag:v hvc1 -c:a aac -b:a 192k -movflags +faststart -sws_flags spline+accurate_rnd+full_chroma_int -map_metadata 0 \"{output}\"";
        }

        if (targetExt is "mov-prores422" or "prores422" or "prores")
        {
            return $"-y -i \"{input}\" -map 0:v:0 -map 0:a? -c:v prores_ks -profile:v 2 -c:a pcm_s16le -sws_flags spline+accurate_rnd+full_chroma_int -map_metadata 0 \"{output}\"";
        }

        if (targetExt is "mov-prores4444" or "prores4444")
        {
            return $"-y -i \"{input}\" -map 0:v:0 -map 0:a? -c:v prores_ks -profile:v 4 -pix_fmt yuva444p10le -c:a pcm_s16le -sws_flags spline+accurate_rnd+full_chroma_int -map_metadata 0 \"{output}\"";
        }

        if (targetExt is "mov" or "mov-h264")
        {
            return $"-y -i \"{input}\" -map 0:v:0 -map 0:a? {tonemapFilter}-c:v libx264 -crf 23 -preset medium -c:a aac -b:a 192k -sws_flags spline+accurate_rnd+full_chroma_int -map_metadata 0 \"{output}\"";
        }

        if (targetExt is "webm-vp9" or "vp9" or "webm")
        {
            return $"-y -i \"{input}\" -map 0:v:0 -map 0:a? {tonemapFilter}-c:v libvpx-vp9 -crf 23 -b:v 0 -deadline good -cpu-used 2 -row-mt 1 -c:a libopus -b:a 128k -sws_flags spline+accurate_rnd+full_chroma_int -map_metadata 0 \"{output}\"";
        }

        if (targetExt is "webm-av1" or "av1")
        {
            return $"-y -i \"{input}\" -map 0:v:0 -map 0:a? {tonemapFilter}-c:v libsvtav1 -crf 23 -preset 6 -svtav1-params tune=0 -c:a libopus -b:a 128k -sws_flags spline+accurate_rnd+full_chroma_int -map_metadata 0 \"{output}\"";
        }

        if (targetExt == "mp4-av1")
        {
            return $"-y -i \"{input}\" -map 0:v:0 -map 0:a? {tonemapFilter}-c:v libsvtav1 -crf 23 -preset 6 -svtav1-params tune=0 -c:a aac -b:a 192k -movflags +faststart -sws_flags spline+accurate_rnd+full_chroma_int -map_metadata 0 \"{output}\"";
        }

        if (targetExt is "mp4-h264-nvenc" or "mp4-nvenc-h264")
        {
            return $"-y -i \"{input}\" -map 0:v:0 -map 0:a? {tonemapFilter}-c:v h264_nvenc -cq:v 23 -preset p5 -tune hq -multipass fullres -rc-lookahead 20 -spatial-aq 1 -aq-strength 7 -temporal-aq 1 -b_ref_mode middle -c:a aac -b:a 192k -movflags +faststart -sws_flags spline+accurate_rnd+full_chroma_int -map_metadata 0 \"{output}\"";
        }

        if (targetExt is "mp4-h265-nvenc" or "mp4-hevc-nvenc" or "mp4-nvenc-h265" or "mp4-nvenc-hevc")
        {
            return $"-y -i \"{input}\" -map 0:v:0 -map 0:a? -c:v hevc_nvenc -cq:v 23 -preset p5 -tune hq -multipass fullres -rc-lookahead 20 -spatial-aq 1 -aq-strength 7 -temporal-aq 1 -b_ref_mode middle -tag:v hvc1 -c:a aac -b:a 192k -movflags +faststart -sws_flags spline+accurate_rnd+full_chroma_int -map_metadata 0 \"{output}\"";
        }

        if (targetExt is "webm-av1-nvenc" or "webm-nvenc-av1")
        {
            return $"-y -i \"{input}\" -map 0:v:0 -map 0:a? {tonemapFilter}-c:v av1_nvenc -cq:v 23 -preset p5 -tune hq -multipass fullres -rc-lookahead 20 -spatial-aq 1 -aq-strength 7 -temporal-aq 1 -c:a libopus -b:a 128k -sws_flags spline+accurate_rnd+full_chroma_int -map_metadata 0 \"{output}\"";
        }

        if (targetExt is "mp4-av1-nvenc" or "mp4-nvenc-av1")
        {
            return $"-y -i \"{input}\" -map 0:v:0 -map 0:a? {tonemapFilter}-c:v av1_nvenc -cq:v 23 -preset p5 -tune hq -multipass fullres -rc-lookahead 20 -spatial-aq 1 -aq-strength 7 -temporal-aq 1 -c:a aac -b:a 192k -movflags +faststart -sws_flags spline+accurate_rnd+full_chroma_int -map_metadata 0 \"{output}\"";
        }

        if (targetExt is "mp4-h264-qsv" or "mp4-qsv-h264")
        {
            return $"-y -i \"{input}\" -map 0:v:0 -map 0:a? {tonemapFilter}-c:v h264_qsv -global_quality:v 23 -preset medium -adaptive_i 1 -adaptive_b 1 -c:a aac -b:a 192k -movflags +faststart -sws_flags spline+accurate_rnd+full_chroma_int -map_metadata 0 \"{output}\"";
        }

        if (targetExt is "mp4-h265-qsv" or "mp4-hevc-qsv" or "mp4-qsv-h265" or "mp4-qsv-hevc")
        {
            return $"-y -i \"{input}\" -map 0:v:0 -map 0:a? -c:v hevc_qsv -global_quality:v 23 -preset medium -adaptive_i 1 -adaptive_b 1 -tag:v hvc1 -c:a aac -b:a 192k -movflags +faststart -sws_flags spline+accurate_rnd+full_chroma_int -map_metadata 0 \"{output}\"";
        }

        if (targetExt is "webm-vp9-qsv" or "webm-qsv-vp9")
        {
            return $"-y -i \"{input}\" -map 0:v:0 -map 0:a? {tonemapFilter}-c:v vp9_qsv -global_quality:v 23 -adaptive_i 1 -adaptive_b 1 -c:a libopus -b:a 128k -sws_flags spline+accurate_rnd+full_chroma_int -map_metadata 0 \"{output}\"";
        }

        if (targetExt is "webm-av1-qsv" or "webm-qsv-av1")
        {
            return $"-y -i \"{input}\" -map 0:v:0 -map 0:a? {tonemapFilter}-c:v av1_qsv -global_quality:v 23 -preset medium -adaptive_i 1 -adaptive_b 1 -c:a libopus -b:a 128k -sws_flags spline+accurate_rnd+full_chroma_int -map_metadata 0 \"{output}\"";
        }

        if (targetExt is "mp4-av1-qsv" or "mp4-qsv-av1")
        {
            return $"-y -i \"{input}\" -map 0:v:0 -map 0:a? {tonemapFilter}-c:v av1_qsv -global_quality:v 23 -preset medium -adaptive_i 1 -adaptive_b 1 -c:a aac -b:a 192k -movflags +faststart -sws_flags spline+accurate_rnd+full_chroma_int -map_metadata 0 \"{output}\"";
        }

        if (targetExt is "mp4-h264-amf" or "mp4-amf-h264")
        {
            return $"-y -i \"{input}\" -map 0:v:0 -map 0:a? {tonemapFilter}-c:v h264_amf -rc cqp -qp_i 23 -qp_p 23 -qp_b 23 -quality balanced -c:a aac -b:a 192k -movflags +faststart -sws_flags spline+accurate_rnd+full_chroma_int -map_metadata 0 \"{output}\"";
        }

        if (targetExt is "mp4-h265-amf" or "mp4-hevc-amf" or "mp4-amf-h265" or "mp4-amf-hevc")
        {
            return $"-y -i \"{input}\" -map 0:v:0 -map 0:a? -c:v hevc_amf -rc cqp -qp_i 23 -qp_p 23 -qp_b 23 -quality balanced -tag:v hvc1 -c:a aac -b:a 192k -movflags +faststart -sws_flags spline+accurate_rnd+full_chroma_int -map_metadata 0 \"{output}\"";
        }

        if (targetExt is "webm-av1-amf" or "webm-amf-av1")
        {
            return $"-y -i \"{input}\" -map 0:v:0 -map 0:a? {tonemapFilter}-c:v av1_amf -rc cqp -qp_i 23 -qp_p 23 -qp_b 23 -quality balanced -c:a libopus -b:a 128k -sws_flags spline+accurate_rnd+full_chroma_int -map_metadata 0 \"{output}\"";
        }

        if (targetExt is "mp4-av1-amf" or "mp4-amf-av1")
        {
            return $"-y -i \"{input}\" -map 0:v:0 -map 0:a? {tonemapFilter}-c:v av1_amf -rc cqp -qp_i 23 -qp_p 23 -qp_b 23 -quality balanced -c:a aac -b:a 192k -movflags +faststart -sws_flags spline+accurate_rnd+full_chroma_int -map_metadata 0 \"{output}\"";
        }

        if (targetExt == "mkv")
        {
            return $"-y -i \"{input}\" -map 0:v:0 -map 0:a? {tonemapFilter}-c:v libx264 -crf 23 -preset medium -c:a aac -b:a 192k -sws_flags spline+accurate_rnd+full_chroma_int -map_metadata 0 \"{output}\"";
        }

        if (targetExt == "avi")
        {
            return $"-y -i \"{input}\" -map 0:v:0 -map 0:a? {tonemapFilter}-c:v mpeg4 -qscale:v 3 -c:a mp3 -b:a 192k -sws_flags spline+accurate_rnd+full_chroma_int -map_metadata 0 \"{output}\"";
        }

        if (targetExt == "wmv")
        {
            return $"-y -i \"{input}\" -map 0:v:0 -map 0:a? {tonemapFilter}-c:v wmv2 -b:v 2M -c:a wmav2 -b:a 192k -map_metadata 0 \"{output}\"";
        }

        if (targetExt == "flv")
        {
            return $"-y -i \"{input}\" -map 0:v:0 -map 0:a? {tonemapFilter}-c:v flv1 -qscale:v 3 -c:a mp3 -b:a 128k -map_metadata 0 \"{output}\"";
        }

        if (targetExt == "m4v")
        {
            return $"-y -i \"{input}\" -map 0:v:0 -map 0:a? {tonemapFilter}-c:v libx264 -crf 23 -preset medium -c:a aac -b:a 192k -movflags +faststart -map_metadata 0 \"{output}\"";
        }

        if (targetExt == "gif")
        {
            return $"-y -i \"{input}\" -vf \"fps=15,scale=480:-1:flags=lanczos,split[s0][s1];[s0]palettegen[p];[s1][p]paletteuse\" \"{output}\"";
        }

        return $"-y -i \"{input}\" \"{output}\"";
    }

    private static string BuildSettingBasedVideoArguments(
        string input,
        string output,
        string targetExt,
        VideoQualitySetting setting,
        string tonemapFilter,
        AudioStreamInfo? audioInfo = null)
    {
        var codecArg = BuildVideoCodecArgument(setting);
        var audioArg = BuildAudioCodecArgument(setting, audioInfo);
        var container = targetExt.StartsWith("preset:") ? Path.GetExtension(output).TrimStart('.').ToLowerInvariant() : targetExt;
        var movflags = (container is "mp4" or "mov")
            ? "-movflags +faststart "
            : "";

        return $"-y -i \"{input}\" -map 0:v:0 -map 0:a? {tonemapFilter}{codecArg} {audioArg} {movflags}-sws_flags spline+accurate_rnd+full_chroma_int -map_metadata 0 \"{output}\"";
    }

    private static string BuildVideoCodecArgument(VideoQualitySetting setting)
    {
        var codec = setting.VideoCodec.ToLowerInvariant();
        var encoder = setting.Encoder.ToLowerInvariant();
        var cq = setting.VideoQualityCq;

        if (codec == "copy") return "-c:v copy";
        if (codec == "prores422") return "-c:v prores_ks -profile:v 2";
        if (codec == "prores4444") return "-c:v prores_ks -profile:v 4 -pix_fmt yuva444p10le";

        if (encoder == "auto")
        {
            if (codec == "h264")
            {
                if (HardwareAccelerationDetector.HasNvencH264) encoder = "nvenc";
                else if (HardwareAccelerationDetector.HasQsvH264) encoder = "qsv";
                else if (HardwareAccelerationDetector.HasAmfH264) encoder = "amf";
                else encoder = "cpu";
            }
            else if (codec is "h265" or "hevc")
            {
                if (HardwareAccelerationDetector.HasNvencHevc) encoder = "nvenc";
                else if (HardwareAccelerationDetector.HasQsvHevc) encoder = "qsv";
                else if (HardwareAccelerationDetector.HasAmfHevc) encoder = "amf";
                else encoder = "cpu";
            }
            else if (codec == "av1")
            {
                if (HardwareAccelerationDetector.HasNvencAv1) encoder = "nvenc";
                else if (HardwareAccelerationDetector.HasQsvAv1) encoder = "qsv";
                else if (HardwareAccelerationDetector.HasAmfAv1) encoder = "amf";
                else encoder = "cpu";
            }
            else if (codec == "vp9")
            {
                if (HardwareAccelerationDetector.HasQsvVp9) encoder = "qsv";
                else encoder = "cpu";
            }
            else
            {
                encoder = "cpu";
            }
        }

        return encoder switch
        {
            "nvenc" => codec switch
            {
                "h264" => $"-c:v h264_nvenc -cq:v {cq} -preset p5 -tune hq -multipass fullres -rc-lookahead 20 -spatial-aq 1 -aq-strength 7 -temporal-aq 1 -b_ref_mode middle",
                "h265" or "hevc" => $"-c:v hevc_nvenc -cq:v {cq} -preset p5 -tune hq -multipass fullres -rc-lookahead 20 -spatial-aq 1 -aq-strength 7 -temporal-aq 1 -b_ref_mode middle -tag:v hvc1",
                "av1" => $"-c:v av1_nvenc -cq:v {cq} -preset p5 -tune hq -multipass fullres -rc-lookahead 20 -spatial-aq 1 -aq-strength 7 -temporal-aq 1",
                _ => $"-c:v libx264 -crf {cq} -preset medium"
            },
            "qsv" => codec switch
            {
                "h264" => $"-c:v h264_qsv -global_quality:v {cq} -preset medium -adaptive_i 1 -adaptive_b 1",
                "h265" or "hevc" => $"-c:v hevc_qsv -global_quality:v {cq} -preset medium -adaptive_i 1 -adaptive_b 1 -tag:v hvc1",
                "av1" => $"-c:v av1_qsv -global_quality:v {cq} -preset medium -adaptive_i 1 -adaptive_b 1",
                "vp9" => $"-c:v vp9_qsv -global_quality:v {cq} -adaptive_i 1 -adaptive_b 1",
                _ => $"-c:v libx264 -crf {cq} -preset medium"
            },
            "amf" => codec switch
            {
                "h264" => $"-c:v h264_amf -rc cqp -qp_i {cq} -qp_p {cq} -qp_b {cq} -quality balanced",
                "h265" or "hevc" => $"-c:v hevc_amf -rc cqp -qp_i {cq} -qp_p {cq} -qp_b {cq} -quality balanced -tag:v hvc1",
                "av1" => $"-c:v av1_amf -rc cqp -qp_i {cq} -qp_p {cq} -qp_b {cq} -quality balanced",
                _ => $"-c:v libx264 -crf {cq} -preset medium"
            },
            _ => codec switch
            {
                "h264" => $"-c:v libx264 -crf {cq} -preset medium",
                "h265" or "hevc" => $"-c:v libx265 -crf {cq} -preset medium -tag:v hvc1",
                "av1" => $"-c:v libsvtav1 -crf {cq} -preset 6 -svtav1-params tune=0",
                "vp9" => $"-c:v libvpx-vp9 -crf {cq} -b:v 0 -deadline good -cpu-used 2 -row-mt 1",
                _ => $"-c:v libx264 -crf {cq} -preset medium"
            }
        };
    }

    private static string BuildAudioCodecArgument(VideoQualitySetting setting, AudioStreamInfo? audioInfo = null)
    {
        var codec = setting.AudioCodec.ToLowerInvariant();
        var bitrate = setting.AudioBitrateKbps <= 0
            ? MediaProbe.ResolveAudioBitrate(audioInfo, 192, 320)
            : setting.AudioBitrateKbps;

        return codec switch
        {
            "copy" => "-c:a copy",
            "opus" => $"-c:a libopus -b:a {bitrate}k",
            "mp3" => $"-c:a libmp3lame -b:a {bitrate}k",
            "flac" => "-c:a flac",
            _ => $"-c:a aac -b:a {bitrate}k"
        };
    }
}

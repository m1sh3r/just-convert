using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using JustConvert.Core.Converters.Tools;

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
        "mp4", "mp4-h264", "mp4-h265", "mp4-hevc", "h264", "h265", "hevc", "webm-vp9", "vp9", "webm", "webm-av1", "av1", "mp4-av1", "mov-prores422", "mov-prores4444", "remux-mp4", "remux-mkv", "frames", "gif",
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

        if (tgt == "reencode")
        {
            return VideoFormats.Contains(src);
        }

        if (src == tgt && tgt is not "frames") return false;

        if (!VideoFormats.Contains(src)) return false;
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

        var list = new List<string> { "mp4-h264" };
        if (HardwareAccelerationDetector.HasNvencH264) list.Add("mp4-h264-nvenc");
        if (HardwareAccelerationDetector.HasQsvH264) list.Add("mp4-h264-qsv");
        if (HardwareAccelerationDetector.HasAmfH264) list.Add("mp4-h264-amf");

        list.Add("mp4-h265");
        if (HardwareAccelerationDetector.HasNvencHevc) list.Add("mp4-h265-nvenc");
        if (HardwareAccelerationDetector.HasQsvHevc) list.Add("mp4-h265-qsv");
        if (HardwareAccelerationDetector.HasAmfHevc) list.Add("mp4-h265-amf");

        list.Add("webm-vp9");
        if (HardwareAccelerationDetector.HasQsvVp9) list.Add("webm-vp9-qsv");

        list.Add("webm-av1");
        if (HardwareAccelerationDetector.HasNvencAv1) list.Add("webm-av1-nvenc");
        if (HardwareAccelerationDetector.HasQsvAv1) list.Add("webm-av1-qsv");
        if (HardwareAccelerationDetector.HasAmfAv1) list.Add("webm-av1-amf");

        list.Add("mov-prores422");
        list.Add("mov-prores4444");
        list.Add("remux-mp4");
        list.Add("remux-mkv");
        list.Add("frames");
        list.Add("mp3");
        list.Add("wav");
        list.Add("flac");
        list.Add("aac");
        list.Add("m4a");
        list.Add("opus");
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
        var targetExt = isReencode ? sourceExt : rawTargetExt;

        var isExtractFrames = targetExt is "frames" or "frames-png" or "frames-jpg";
        var isCompress = targetExt is "compress" or "compressed";

        string outputExt;
        if (targetExt is "remux-mp4" or "mp4-remux" or "mp4-copy") outputExt = ".mp4";
        else if (targetExt is "remux-mkv" or "mkv-remux" or "mkv-copy") outputExt = ".mkv";
        else if (targetExt.StartsWith("mp4", StringComparison.OrdinalIgnoreCase) && targetExt != "mp4-av1") outputExt = ".mp4";
        else if (targetExt.StartsWith("mov", StringComparison.OrdinalIgnoreCase)) outputExt = ".mov";
        else if (targetExt.StartsWith("webm", StringComparison.OrdinalIgnoreCase) || targetExt is "vp9" or "av1") outputExt = ".webm";
        else if (targetExt.StartsWith("mkv", StringComparison.OrdinalIgnoreCase)) outputExt = ".mkv";
        else if (targetExt is "h264" or "h265" or "hevc" or "mp4-av1" or "compress" or "compressed") outputExt = ".mp4";
        else outputExt = $".{targetExt}";

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
                ? OutputFileNameHelper.BuildAudioSuffix(targetExt, mediaInfo?.Audio)
                : OutputFileNameHelper.BuildVideoSuffix(targetExt);

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
            var arguments = BuildVideoArguments(inputPath, outputPath, targetExt, mediaInfo?.Audio, isReencode, mediaInfo?.Video);

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

            ct.ThrowIfCancellationRequested();

            if (proc.ExitCode == 0)
            {
                progress?.Report(new ConversionProgress(100, I18n.T("StatusDone")));
                return new ConversionResult(true, outputPath, null, logs, sw.Elapsed);
            }

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

            return new ConversionResult(false, null, MediaProbe.ExtractDiagnosticMessage(logs, proc.ExitCode), logs, sw.Elapsed);
        }
        catch (OperationCanceledException)
        {
            sw.Stop();
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
            return new ConversionResult(false, null, I18n.T("StatusCancelled"), null, sw.Elapsed);
        }
        catch (Exception ex)
        {
            sw.Stop();
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
            return new ConversionResult(false, null, ex.Message, ex.ToString(), sw.Elapsed);
        }
    }

    private static string BuildVideoArguments(string input, string output, string targetExt, AudioStreamInfo? audioInfo, bool isReencode, VideoStreamInfo? videoInfo)
    {
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
            return $"-y -i \"{input}\" -vf \"fps=1\" \"{pattern}\"";
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
}

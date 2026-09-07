using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;

namespace JustConvert.Core.Converters;

public class MediaConverter : IFormatConverter
{
    public string Name => "Media Converter (FFmpeg)";

    private static readonly HashSet<string> VideoFormats = new(StringComparer.OrdinalIgnoreCase)
    {
        "mp4", "mkv", "avi", "mov", "webm", "wmv", "flv", "m4v"
    };

    private static readonly HashSet<string> AudioFormats = new(StringComparer.OrdinalIgnoreCase)
    {
        "mp3", "wav", "flac", "aac", "ogg", "m4a", "wma", "opus"
    };

    private static readonly HashSet<string> VideoTargetFormats = new(StringComparer.OrdinalIgnoreCase)
    {
        "mp4", "mp4-h264", "mp4-h265", "mp4-hevc", "h264", "h265", "hevc", "mov-prores422", "mov-prores4444", "frames"
    };

    public static string? FindFfmpegPath()
    {
        var localExe = Path.Combine(AppContext.BaseDirectory, "ffmpeg.exe");
        if (File.Exists(localExe)) return localExe;

        string[] appLocations =
        [
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "m1sh3r", "Just Convert", "ffmpeg.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "m1sh3r", "Just Convert", "ffmpeg.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "m1sh3r", "JustConvert", "ffmpeg.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "m1sh3r", "JustConvert", "ffmpeg.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "Just Convert", "ffmpeg.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "JustConvert", "ffmpeg.exe")
        ];

        foreach (var loc in appLocations)
        {
            if (File.Exists(loc)) return loc;
        }

        var customPath = Environment.GetEnvironmentVariable("FFMPEG_PATH");
        if (!string.IsNullOrEmpty(customPath))
        {
            if (File.Exists(customPath)) return customPath;
            var binPath = Path.Combine(customPath, "ffmpeg.exe");
            if (File.Exists(binPath)) return binPath;
            binPath = Path.Combine(customPath, "bin", "ffmpeg.exe");
            if (File.Exists(binPath)) return binPath;
        }

        var ffmpegHome = Environment.GetEnvironmentVariable("FFMPEG_HOME");
        if (!string.IsNullOrEmpty(ffmpegHome))
        {
            var binPath = Path.Combine(ffmpegHome, "bin", "ffmpeg.exe");
            if (File.Exists(binPath)) return binPath;
            binPath = Path.Combine(ffmpegHome, "ffmpeg.exe");
            if (File.Exists(binPath)) return binPath;
        }

        var paths = (Environment.GetEnvironmentVariable("PATH") ?? "")
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries);

        foreach (var p in paths)
        {
            try
            {
                var target = Path.Combine(p.Trim('\"'), "ffmpeg.exe");
                if (File.Exists(target)) return target;
            }
            catch { }
        }

        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        string[] knownLocations =
        [
            Path.Combine(localAppData, @"Microsoft\WinGet\Links\ffmpeg.exe"),
            Path.Combine(userProfile, @"scoop\shims\ffmpeg.exe"),
            Path.Combine(userProfile, @"scoop\apps\ffmpeg\current\bin\ffmpeg.exe"),
            @"C:\ProgramData\chocolatey\bin\ffmpeg.exe",
            @"C:\ffmpeg\bin\ffmpeg.exe",
            @"C:\Program Files\ffmpeg\bin\ffmpeg.exe",
            @"C:\Program Files (x86)\ffmpeg\bin\ffmpeg.exe"
        ];

        foreach (var loc in knownLocations)
        {
            if (File.Exists(loc)) return loc;
        }

        var wingetPackagesDir = Path.Combine(localAppData, @"Microsoft\WinGet\Packages");
        if (Directory.Exists(wingetPackagesDir))
        {
            try
            {
                var matches = Directory.GetFiles(wingetPackagesDir, "ffmpeg.exe", SearchOption.AllDirectories);
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
            return VideoFormats.Contains(src) || AudioFormats.Contains(src);
        }

        if (src == tgt && tgt is not "frames") return false;

        if (VideoFormats.Contains(src))
        {
            return VideoTargetFormats.Contains(tgt);
        }

        if (AudioFormats.Contains(src))
        {
            return AudioFormats.Contains(tgt);
        }

        return false;
    }

    public IReadOnlyList<string> GetSupportedTargetFormats(string sourceExtension)
    {
        var src = sourceExtension.TrimStart('.').ToLowerInvariant();

        if (VideoFormats.Contains(src))
        {
            return ["mp4-h264", "mp4-h265", "mov-prores422", "mov-prores4444", "frames", "reencode"];
        }

        if (AudioFormats.Contains(src))
        {
            List<string> list = ["mp3", "aac", "m4a", "wav", "flac", "ogg"];
            list.Remove(src);
            list.Add("reencode");
            return list;
        }

        return [];
    }

    public async Task<ConversionResult> ConvertAsync(
        string inputPath,
        string targetExtension,
        string? outputPath = null,
        IProgress<ConversionProgress>? progress = null,
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        var ffmpeg = FindFfmpegPath();

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
        var isReencode = targetExt == "reencode";
        if (isReencode)
        {
            targetExt = sourceExt;
        }

        var isExtractFrames = targetExt is "frames" or "frames-png" or "frames-jpg";
        var isCompress = targetExt is "compress" or "compressed";

        string outputExt;
        if (targetExt.StartsWith("mp4", StringComparison.OrdinalIgnoreCase)) outputExt = ".mp4";
        else if (targetExt.StartsWith("mov", StringComparison.OrdinalIgnoreCase)) outputExt = ".mov";
        else if (targetExt.StartsWith("webm", StringComparison.OrdinalIgnoreCase)) outputExt = ".webm";
        else if (targetExt.StartsWith("mkv", StringComparison.OrdinalIgnoreCase)) outputExt = ".mkv";
        else if (targetExt is "h264" or "h265" or "hevc") outputExt = ".mp4";
        else outputExt = $".{targetExt}";

        if (string.IsNullOrWhiteSpace(outputPath))
        {
            var dir = Path.GetDirectoryName(inputPath) ?? "";
            var fileNameWithoutExt = Path.GetFileNameWithoutExtension(inputPath);

            if (isReencode)
            {
                outputPath = Path.Combine(dir, $"{fileNameWithoutExt}{outputExt}");
            }
            else if (isExtractFrames)
            {
                outputPath = Path.Combine(dir, $"{fileNameWithoutExt}_frames");
                Directory.CreateDirectory(outputPath);
            }
            else if (isCompress)
            {
                outputPath = Path.Combine(dir, $"{fileNameWithoutExt}_compressed.mp4");
            }
            else if (targetExt is "mp4-hevc" or "hevc" or "h265" or "mp4-h265")
            {
                outputPath = Path.Combine(dir, $"{fileNameWithoutExt}_h265.mp4");
            }
            else if (targetExt is "mp4-h264" or "h264" or "mp4")
            {
                var isInputMp4 = string.Equals(Path.GetExtension(inputPath), ".mp4", StringComparison.OrdinalIgnoreCase);
                outputPath = isInputMp4
                    ? Path.Combine(dir, $"{fileNameWithoutExt}_h264.mp4")
                    : Path.Combine(dir, $"{fileNameWithoutExt}.mp4");
            }
            else if (targetExt is "mov-prores422" or "prores422" or "prores")
            {
                outputPath = Path.Combine(dir, $"{fileNameWithoutExt}_prores422.mov");
            }
            else if (targetExt is "mov-prores4444" or "prores4444")
            {
                outputPath = Path.Combine(dir, $"{fileNameWithoutExt}_prores4444.mov");
            }
            else
            {
                outputPath = Path.Combine(dir, $"{fileNameWithoutExt}{outputExt}");
            }

            if (!isExtractFrames)
            {
                var baseWithoutExt = Path.GetFileNameWithoutExtension(outputPath);
                var ext = Path.GetExtension(outputPath);
                int counter = 1;
                while (File.Exists(outputPath))
                {
                    outputPath = Path.Combine(dir, $"{baseWithoutExt}_{counter}{ext}");
                    counter++;
                }
            }
        }

        try
        {
            AudioStreamInfo? audioInfo = null;
            if (AudioFormats.Contains(targetExt))
            {
                audioInfo = await ProbeAudioInfoAsync(ffmpeg, inputPath, ct);
            }

            var arguments = BuildArguments(inputPath, outputPath, targetExt, audioInfo, isReencode);
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

            return new ConversionResult(false, null, I18n.T("FfmpegExitError", proc.ExitCode), logs, sw.Elapsed);
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

    private static string BuildArguments(string input, string output, string targetExt, AudioStreamInfo? audioInfo = null, bool isReencode = false)
    {
        if (targetExt is "frames" or "frames-png")
        {
            var pattern = Path.Combine(output, "frame_%04d.png");
            return $"-y -i \"{input}\" -vf \"fps=1\" \"{pattern}\"";
        }

        if (targetExt is "compress" or "compressed")
        {
            return $"-y -i \"{input}\" -c:v libx264 -crf 28 -preset medium -c:a aac -b:a 128k -movflags +faststart -map_metadata 0 \"{output}\"";
        }

        if (targetExt is "mp4" or "mp4-h264" or "h264")
        {
            return $"-y -i \"{input}\" -c:v libx264 -crf 23 -preset medium -c:a aac -b:a 192k -movflags +faststart -map_metadata 0 \"{output}\"";
        }

        if (targetExt is "mp4-hevc" or "mp4-h265" or "h265" or "hevc")
        {
            return $"-y -i \"{input}\" -c:v libx265 -crf 23 -preset medium -tag:v hvc1 -c:a aac -b:a 192k -movflags +faststart -map_metadata 0 \"{output}\"";
        }

        if (targetExt is "mov-prores422" or "prores422" or "prores")
        {
            return $"-y -i \"{input}\" -c:v prores_ks -profile:v 2 -c:a pcm_s16le -map_metadata 0 \"{output}\"";
        }

        if (targetExt is "mov-prores4444" or "prores4444")
        {
            return $"-y -i \"{input}\" -c:v prores_ks -profile:v 4 -pix_fmt yuva444p10le -c:a pcm_s16le -map_metadata 0 \"{output}\"";
        }

        if (targetExt is "mov" or "mov-h264")
        {
            return $"-y -i \"{input}\" -c:v libx264 -crf 23 -preset medium -c:a aac -b:a 192k -map_metadata 0 \"{output}\"";
        }

        if (targetExt == "webm")
        {
            return $"-y -i \"{input}\" -c:v libvpx-vp9 -crf 30 -b:v 0 -c:a libopus -map_metadata 0 \"{output}\"";
        }

        if (targetExt == "mkv")
        {
            return $"-y -i \"{input}\" -c:v libx264 -crf 23 -preset medium -c:a aac -b:a 192k -map_metadata 0 \"{output}\"";
        }

        if (targetExt == "avi")
        {
            return $"-y -i \"{input}\" -c:v mpeg4 -qscale:v 3 -c:a mp3 -b:a 192k -map_metadata 0 \"{output}\"";
        }

        if (targetExt == "wmv")
        {
            return $"-y -i \"{input}\" -c:v wmv2 -b:v 2M -c:a wmav2 -b:a 192k -map_metadata 0 \"{output}\"";
        }

        if (targetExt == "flv")
        {
            return $"-y -i \"{input}\" -c:v flv1 -qscale:v 3 -c:a mp3 -b:a 128k -map_metadata 0 \"{output}\"";
        }

        if (targetExt == "m4v")
        {
            return $"-y -i \"{input}\" -c:v libx264 -crf 23 -preset medium -c:a aac -b:a 192k -movflags +faststart -map_metadata 0 \"{output}\"";
        }

        if (targetExt == "gif")
        {
            return $"-y -i \"{input}\" -vf \"fps=15,scale=480:-1:flags=lanczos,split[s0][s1];[s0]palettegen[p];[s1][p]paletteuse\" \"{output}\"";
        }

        if (targetExt == "mp3")
        {
            var bitrate = ResolveAudioBitrate(audioInfo, 320, 320);
            var hasAttachedPic = audioInfo?.HasAttachedPic ?? true;
            if (hasAttachedPic)
            {
                return $"-y -i \"{input}\" -map 0:a:0 -map 0:v? -c:a libmp3lame -b:a {bitrate}k -c:v copy -disposition:v:0 attached_pic -id3v2_version 3 -metadata:s:v title=\"Album cover\" -metadata:s:v comment=\"Cover (front)\" -map_metadata 0 \"{output}\"";
            }

            return $"-y -i \"{input}\" -map 0:a:0 -c:a libmp3lame -b:a {bitrate}k -id3v2_version 3 -map_metadata 0 \"{output}\"";
        }

        if (targetExt == "wav")
        {
            return $"-y -i \"{input}\" -map 0:a:0 -c:a pcm_s16le -map_metadata 0 \"{output}\"";
        }

        if (targetExt == "flac")
        {
            var hasAttachedPic = audioInfo?.HasAttachedPic ?? true;
            if (hasAttachedPic)
            {
                return $"-y -i \"{input}\" -map 0:a:0 -map 0:v? -c:a flac -c:v copy -disposition:v:0 attached_pic -map_metadata 0 \"{output}\"";
            }

            return $"-y -i \"{input}\" -map 0:a:0 -c:a flac -map_metadata 0 \"{output}\"";
        }

        if (targetExt == "m4a")
        {
            var bitrate = ResolveAudioBitrate(audioInfo, 320, 320);
            var aacCodec = (!isReencode && string.Equals(audioInfo?.Codec, "aac", StringComparison.OrdinalIgnoreCase)) ? "-c:a copy" : $"-c:a aac -b:a {bitrate}k";
            var hasAttachedPic = audioInfo?.HasAttachedPic ?? true;
            if (hasAttachedPic)
            {
                return $"-y -i \"{input}\" -map 0:a:0 -map 0:v? {aacCodec} -c:v copy -disposition:v:0 attached_pic -map_metadata 0 \"{output}\"";
            }

            return $"-y -i \"{input}\" -map 0:a:0 {aacCodec} -map_metadata 0 \"{output}\"";
        }

        if (targetExt == "aac")
        {
            var bitrate = ResolveAudioBitrate(audioInfo, 320, 320);
            var aacCodec = (!isReencode && string.Equals(audioInfo?.Codec, "aac", StringComparison.OrdinalIgnoreCase)) ? "-c:a copy" : $"-c:a aac -b:a {bitrate}k";
            return $"-y -i \"{input}\" -map 0:a:0 {aacCodec} -map_metadata 0 \"{output}\"";
        }

        if (targetExt == "ogg")
        {
            var vorbisQuality = ResolveVorbisQuality(audioInfo);
            return $"-y -i \"{input}\" -map 0:a:0 -c:a libvorbis -q:a {vorbisQuality} -map_metadata 0 \"{output}\"";
        }

        if (targetExt == "opus")
        {
            var bitrate = ResolveAudioBitrate(audioInfo, 320, 320);
            return $"-y -i \"{input}\" -map 0:a:0 -c:a libopus -b:a {bitrate}k -map_metadata 0 \"{output}\"";
        }

        if (targetExt == "wma")
        {
            var bitrate = ResolveAudioBitrate(audioInfo, 192, 320);
            return $"-y -i \"{input}\" -map 0:a:0 -c:a wmav2 -b:a {bitrate}k -map_metadata 0 \"{output}\"";
        }

        return $"-y -i \"{input}\" \"{output}\"";
    }

    private static async Task<AudioStreamInfo?> ProbeAudioInfoAsync(string ffmpegPath, string inputPath, CancellationToken ct)
    {
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(3));

            var psi = new ProcessStartInfo
            {
                FileName = ffmpegPath,
                Arguments = $"-hide_banner -i \"{inputPath}\"",
                UseShellExecute = false,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            };

            using var proc = new Process { StartInfo = psi };
            var stderr = new System.Text.StringBuilder();

            proc.ErrorDataReceived += (_, e) =>
            {
                if (e.Data != null)
                {
                    lock (stderr)
                    {
                        stderr.AppendLine(e.Data);
                    }
                }
            };

            proc.Start();
            proc.BeginErrorReadLine();

            using var reg = cts.Token.Register(() =>
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

            return ParseAudioInfo(stderr.ToString());
        }
        catch
        {
            return null;
        }
    }

    private static AudioStreamInfo ParseAudioInfo(string output)
    {
        string? codec = null;
        int? bitrate = null;

        var streamMatch = Regex.Match(output, @"Stream #\d+:\d+.*?: Audio:\s*([a-zA-Z0-9_\-]+)(?:.*?,\s*(\d+)\s*kb/s)?", RegexOptions.IgnoreCase);
        if (streamMatch.Success)
        {
            codec = streamMatch.Groups[1].Value.ToLowerInvariant();
            if (streamMatch.Groups[2].Success && int.TryParse(streamMatch.Groups[2].Value, CultureInfo.InvariantCulture, out var parsedStreamBitrate) && parsedStreamBitrate > 0)
            {
                bitrate = parsedStreamBitrate;
            }
        }

        if (bitrate == null)
        {
            var durationMatch = Regex.Match(output, @"Duration:.*?, bitrate:\s*(\d+)\s*kb/s", RegexOptions.IgnoreCase);
            if (durationMatch.Success && int.TryParse(durationMatch.Groups[1].Value, CultureInfo.InvariantCulture, out var parsedDurationBitrate) && parsedDurationBitrate > 0)
            {
                bitrate = parsedDurationBitrate;
            }
        }

        var hasAttachedPic = Regex.IsMatch(output, @"Stream #\d+:\d+.*?: Video:", RegexOptions.IgnoreCase);

        var isLossless = (codec != null && (codec.StartsWith("pcm", StringComparison.OrdinalIgnoreCase) ||
                                            codec is "flac" or "alac" or "wavpack" or "ape" or "truehd"))
                         || (bitrate.HasValue && bitrate.Value > 320);

        return new AudioStreamInfo(bitrate, hasAttachedPic, codec, isLossless);
    }

    private static int ResolveAudioBitrate(AudioStreamInfo? info, int defaultKbps, int maxKbps)
    {
        if (info?.BitrateKbps is { } kbps && kbps > 0)
        {
            if (info.IsLossless || kbps >= maxKbps)
            {
                return maxKbps;
            }

            int[] standardBitrates = [64, 96, 128, 160, 192, 224, 256, 320];
            var closest = standardBitrates[0];
            var minDiff = Math.Abs(kbps - closest);

            for (int i = 1; i < standardBitrates.Length; i++)
            {
                var diff = Math.Abs(kbps - standardBitrates[i]);
                if (diff < minDiff)
                {
                    minDiff = diff;
                    closest = standardBitrates[i];
                }
            }

            return Math.Min(closest, maxKbps);
        }

        return defaultKbps;
    }

    private static int ResolveVorbisQuality(AudioStreamInfo? info)
    {
        if (info == null || info.IsLossless || info.BitrateKbps == null || info.BitrateKbps >= 320)
        {
            return 9;
        }

        return info.BitrateKbps switch
        {
            >= 256 => 8,
            >= 192 => 6,
            >= 160 => 5,
            >= 128 => 4,
            >= 96 => 3,
            _ => 2
        };
    }

    private sealed record AudioStreamInfo(int? BitrateKbps, bool HasAttachedPic, string? Codec, bool IsLossless);
}

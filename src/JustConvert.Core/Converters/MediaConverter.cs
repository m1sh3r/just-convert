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
            return ["mp4-h264", "mp4-h265", "mov-prores422", "mov-prores4444", "frames"];
        }

        if (AudioFormats.Contains(src))
        {
            List<string> list = ["mp3", "wav", "flac", "aac", "ogg", "m4a"];
            list.Remove(src);
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

        var targetExt = targetExtension.TrimStart('.').ToLowerInvariant();
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

            if (isExtractFrames)
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
                        progress?.Report(new ConversionProgress(pct, status, $"{currentTime:mm\\:ss} / {totalDuration:mm\\:ss}"));
                    }
                    else
                    {
                        var timeLabel = I18n.T("TimeLabel");
                        progress?.Report(new ConversionProgress(50, I18n.T("FfmpegProcessing"), $"{timeLabel}{currentTime:mm\\:ss}"));
                    }
                }
            };

            proc.Start();
            proc.BeginErrorReadLine();

            await proc.WaitForExitAsync(ct);
            sw.Stop();

            var logs = fullLog.ToString();

            if (proc.ExitCode == 0)
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
        if (targetExt is "frames" or "frames-png")
        {
            var pattern = Path.Combine(output, "frame_%04d.png");
            return $"-y -i \"{input}\" -vf \"fps=1\" \"{pattern}\"";
        }

        if (targetExt is "compress" or "compressed")
        {
            return $"-y -i \"{input}\" -c:v libx264 -crf 28 -preset medium -c:a aac -b:a 128k -movflags +faststart \"{output}\"";
        }

        if (targetExt is "mp4" or "mp4-h264" or "h264")
        {
            return $"-y -i \"{input}\" -c:v libx264 -crf 23 -preset medium -c:a aac -b:a 192k -movflags +faststart \"{output}\"";
        }

        if (targetExt is "mp4-hevc" or "mp4-h265" or "h265" or "hevc")
        {
            return $"-y -i \"{input}\" -c:v libx265 -crf 23 -preset medium -tag:v hvc1 -c:a aac -b:a 192k -movflags +faststart \"{output}\"";
        }

        if (targetExt is "mov-prores422" or "prores422" or "prores")
        {
            return $"-y -i \"{input}\" -c:v prores_ks -profile:v 2 -c:a pcm_s16le \"{output}\"";
        }

        if (targetExt is "mov-prores4444" or "prores4444")
        {
            return $"-y -i \"{input}\" -c:v prores_ks -profile:v 4 -pix_fmt yuva444p10le -c:a pcm_s16le \"{output}\"";
        }

        if (targetExt is "mov" or "mov-h264")
        {
            return $"-y -i \"{input}\" -c:v libx264 -crf 23 -preset medium -c:a aac -b:a 192k \"{output}\"";
        }

        if (targetExt == "webm")
        {
            return $"-y -i \"{input}\" -c:v libvpx-vp9 -crf 30 -b:v 0 -c:a libopus \"{output}\"";
        }

        if (targetExt == "mkv")
        {
            return $"-y -i \"{input}\" -c:v libx264 -crf 23 -preset medium -c:a aac -b:a 192k \"{output}\"";
        }

        if (targetExt == "gif")
        {
            return $"-y -i \"{input}\" -vf \"fps=15,scale=480:-1:flags=lanczos,split[s0][s1];[s0]palettegen[p];[s1][p]paletteuse\" \"{output}\"";
        }

        if (targetExt == "mp3")
        {
            return $"-y -i \"{input}\" -vn -ar 44100 -ac 2 -b:a 192k \"{output}\"";
        }

        if (targetExt == "wav")
        {
            return $"-y -i \"{input}\" -vn -c:a pcm_s16le \"{output}\"";
        }

        if (targetExt == "flac")
        {
            return $"-y -i \"{input}\" -vn -c:a flac \"{output}\"";
        }

        if (targetExt is "aac" or "m4a")
        {
            return $"-y -i \"{input}\" -vn -c:a aac -b:a 192k \"{output}\"";
        }

        if (targetExt == "ogg")
        {
            return $"-y -i \"{input}\" -vn -c:a libvorbis -q:a 5 \"{output}\"";
        }

        return $"-y -i \"{input}\" \"{output}\"";
    }
}

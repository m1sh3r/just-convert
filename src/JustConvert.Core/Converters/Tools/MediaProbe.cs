using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace JustConvert.Core.Converters.Tools;

public sealed record AudioStreamInfo(
    int? BitrateKbps,
    bool HasAttachedPic,
    string? Codec,
    bool IsLossless,
    int? BitsPerSample,
    int? SampleRate,
    int? Channels);

public sealed record VideoStreamInfo(
    bool IsHdrOrWideGamut,
    string? Codec,
    string? PixelFormat,
    int? Width,
    int? Height);

public sealed record MediaStreamInfo(
    AudioStreamInfo? Audio,
    VideoStreamInfo? Video);

public static class MediaProbe
{
    public static async Task<MediaStreamInfo?> ProbeAsync(string ffmpegPath, string inputPath, CancellationToken ct = default)
    {
        var probePath = ToolLocator.FindFfprobePath();
        if (probePath != null)
        {
            var probeResult = await ProbeWithFfprobeAsync(probePath, inputPath, ct);
            if (probeResult != null)
            {
                return probeResult;
            }
        }

        return await ProbeWithFfmpegAsync(ffmpegPath, inputPath, ct);
    }

    private static async Task<MediaStreamInfo?> ProbeWithFfprobeAsync(string ffprobePath, string inputPath, CancellationToken ct)
    {
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(3));

            var psi = new ProcessStartInfo
            {
                FileName = ffprobePath,
                Arguments = $"-v quiet -print_format json -show_format -show_streams \"{inputPath}\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var proc = new Process { StartInfo = psi };
            proc.Start();

            using var reg = cts.Token.Register(() =>
            {
                try
                {
                    if (!proc.HasExited) proc.Kill();
                }
                catch { }
            });

            var jsonOutput = await proc.StandardOutput.ReadToEndAsync(cts.Token);
            await proc.WaitForExitAsync(CancellationToken.None);

            if (proc.ExitCode != 0 || string.IsNullOrWhiteSpace(jsonOutput))
            {
                return null;
            }

            using var doc = JsonDocument.Parse(jsonOutput);
            var root = doc.RootElement;

            AudioStreamInfo? audio = null;
            VideoStreamInfo? video = null;

            int? formatBitrate = null;
            if (root.TryGetProperty("format", out var formatElem) &&
                formatElem.TryGetProperty("bit_rate", out var fbrElem) &&
                int.TryParse(fbrElem.GetString(), CultureInfo.InvariantCulture, out var fbrVal) && fbrVal > 0)
            {
                formatBitrate = fbrVal / 1000;
            }

            if (root.TryGetProperty("streams", out var streamsElem) && streamsElem.ValueKind == JsonValueKind.Array)
            {
                foreach (var s in streamsElem.EnumerateArray())
                {
                    var codecType = s.TryGetProperty("codec_type", out var ctProp) ? ctProp.GetString() : null;

                    if (codecType == "audio" && audio == null)
                    {
                        var codec = s.TryGetProperty("codec_name", out var cn) ? cn.GetString()?.ToLowerInvariant() : null;

                        int? bitrate = null;
                        if (s.TryGetProperty("bit_rate", out var brProp) &&
                            int.TryParse(brProp.GetString(), CultureInfo.InvariantCulture, out var brVal) && brVal > 0)
                        {
                            bitrate = brVal / 1000;
                        }
                        bitrate ??= formatBitrate;

                        int? bitsPerSample = null;
                        if (s.TryGetProperty("bits_per_raw_sample", out var bprs) &&
                            int.TryParse(bprs.GetString(), CultureInfo.InvariantCulture, out var bpsVal) && bpsVal > 0)
                        {
                            bitsPerSample = bpsVal;
                        }
                        else if (s.TryGetProperty("bits_per_sample", out var bps) &&
                                 int.TryParse(bps.GetString(), CultureInfo.InvariantCulture, out var bps2Val) && bps2Val > 0)
                        {
                            bitsPerSample = bps2Val;
                        }

                        int? sampleRate = null;
                        if (s.TryGetProperty("sample_rate", out var srProp) &&
                            int.TryParse(srProp.GetString(), CultureInfo.InvariantCulture, out var srVal) && srVal > 0)
                        {
                            sampleRate = srVal;
                        }

                        int? channels = null;
                        if (s.TryGetProperty("channels", out var chProp) && chProp.TryGetInt32(out var chVal) && chVal > 0)
                        {
                            channels = chVal;
                        }

                        var isLossless = (codec != null && (codec.StartsWith("pcm", StringComparison.OrdinalIgnoreCase) ||
                                                            codec is "flac" or "alac" or "wavpack" or "ape" or "truehd" or "aiff" or "dsd_lsbf" or "dsd_msbf"))
                                         || (bitsPerSample.HasValue && bitsPerSample.Value >= 24)
                                         || (bitrate.HasValue && bitrate.Value > 320);

                        audio = new AudioStreamInfo(bitrate, false, codec, isLossless, bitsPerSample, sampleRate, channels);
                    }
                    else if (codecType == "video")
                    {
                        var dispositionPic = false;
                        if (s.TryGetProperty("disposition", out var dispElem) &&
                            dispElem.TryGetProperty("attached_pic", out var apElem) &&
                            apElem.GetInt32() == 1)
                        {
                            dispositionPic = true;
                        }

                        if (dispositionPic)
                        {
                            if (audio != null)
                            {
                                audio = audio with { HasAttachedPic = true };
                            }
                        }
                        else if (video == null)
                        {
                            var codec = s.TryGetProperty("codec_name", out var cn) ? cn.GetString()?.ToLowerInvariant() : null;
                            var pixFmt = s.TryGetProperty("pix_fmt", out var pf) ? pf.GetString()?.ToLowerInvariant() : null;
                            var colorSpace = s.TryGetProperty("color_space", out var cs) ? cs.GetString()?.ToLowerInvariant() : null;
                            var colorTransfer = s.TryGetProperty("color_transfer", out var ctr) ? ctr.GetString()?.ToLowerInvariant() : null;
                            var colorPrimaries = s.TryGetProperty("color_primaries", out var cp) ? cp.GetString()?.ToLowerInvariant() : null;

                            int? width = s.TryGetProperty("width", out var wProp) && wProp.TryGetInt32(out var wVal) ? wVal : null;
                            int? height = s.TryGetProperty("height", out var hProp) && hProp.TryGetInt32(out var hVal) ? hVal : null;

                            var isHdr = (colorSpace?.Contains("bt2020") == true) ||
                                        (colorTransfer is "smpte2084" or "arib-std-b67") ||
                                        (colorPrimaries?.Contains("bt2020") == true) ||
                                        (pixFmt != null && (pixFmt.Contains("10le") || pixFmt.Contains("12le") || pixFmt.Contains("p10")));

                            video = new VideoStreamInfo(isHdr, codec, pixFmt, width, height);
                        }
                    }
                }
            }

            return new MediaStreamInfo(audio, video);
        }
        catch
        {
            return null;
        }
    }

    private static async Task<MediaStreamInfo?> ProbeWithFfmpegAsync(string ffmpegPath, string inputPath, CancellationToken ct)
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
                    if (!proc.HasExited) proc.Kill();
                }
                catch { }
            });

            await proc.WaitForExitAsync(CancellationToken.None);
            var output = stderr.ToString();

            return new MediaStreamInfo(ParseAudioInfo(output), ParseVideoInfo(output));
        }
        catch
        {
            return null;
        }
    }

    private static AudioStreamInfo? ParseAudioInfo(string output)
    {
        string? codec = null;
        int? bitrate = null;
        int? bitsPerSample = null;
        int? sampleRate = null;
        int? channels = null;

        var streamMatch = Regex.Match(output, @"Stream #\d+:\d+.*?: Audio:\s*([a-zA-Z0-9_\-]+)(?:.*?,\s*(\d+)\s*Hz)?(?:.*?,\s*([a-zA-Z0-9_]+))?(?:.*?,\s*(\d+)\s*kb/s)?", RegexOptions.IgnoreCase);
        if (streamMatch.Success)
        {
            codec = streamMatch.Groups[1].Value.ToLowerInvariant();
            if (streamMatch.Groups[2].Success && int.TryParse(streamMatch.Groups[2].Value, CultureInfo.InvariantCulture, out var parsedHz))
            {
                sampleRate = parsedHz;
            }
            if (streamMatch.Groups[3].Success)
            {
                var chStr = streamMatch.Groups[3].Value.ToLowerInvariant();
                channels = chStr switch
                {
                    "mono" => 1,
                    "stereo" => 2,
                    "5.1" => 6,
                    "7.1" => 8,
                    _ => null
                };
            }
            if (streamMatch.Groups[4].Success && int.TryParse(streamMatch.Groups[4].Value, CultureInfo.InvariantCulture, out var parsedStreamBitrate) && parsedStreamBitrate > 0)
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

        if (Regex.IsMatch(output, @"24-bit|s24|24 bit|flac\s*\(24", RegexOptions.IgnoreCase))
        {
            bitsPerSample = 24;
        }
        else if (Regex.IsMatch(output, @"32-bit|s32|32 bit|float", RegexOptions.IgnoreCase))
        {
            bitsPerSample = 32;
        }
        else if (Regex.IsMatch(output, @"16-bit|s16|16 bit", RegexOptions.IgnoreCase))
        {
            bitsPerSample = 16;
        }

        var hasAttachedPic = Regex.IsMatch(output, @"attached_pic|attached pic|disposition.*?attached_pic|title=Album cover", RegexOptions.IgnoreCase);

        var isLossless = (codec != null && (codec.StartsWith("pcm", StringComparison.OrdinalIgnoreCase) ||
                                            codec is "flac" or "alac" or "wavpack" or "ape" or "truehd" or "aiff"))
                         || (bitsPerSample.HasValue && bitsPerSample.Value >= 24)
                         || (bitrate.HasValue && bitrate.Value > 320);

        return new AudioStreamInfo(bitrate, hasAttachedPic, codec, isLossless, bitsPerSample, sampleRate, channels);
    }

    private static VideoStreamInfo? ParseVideoInfo(string output)
    {
        var streamMatch = Regex.Match(output, @"Stream #\d+:\d+.*?: Video:\s*([^\r\n]+)", RegexOptions.IgnoreCase);
        if (!streamMatch.Success) return null;

        var line = streamMatch.Groups[1].Value.ToLowerInvariant();
        var isHdrOrWideGamut = line.Contains("bt2020") ||
                               line.Contains("arib-std-b67") ||
                               line.Contains("smpte2084") ||
                               line.Contains("10le") ||
                               line.Contains("12le") ||
                               line.Contains("10be") ||
                               line.Contains("12be") ||
                               line.Contains("p10") ||
                               line.Contains("10-bit") ||
                               line.Contains("main 10") ||
                               line.Contains("profile 2") ||
                               line.Contains("profile 3") ||
                               line.Contains("dci-p3") ||
                               line.Contains("display-p3") ||
                               line.Contains("apple-log") ||
                               line.Contains("canon-log") ||
                               line.Contains("s-log") ||
                               line.Contains("v-log");

        var codecMatch = Regex.Match(line, @"^([a-zA-Z0-9_\-]+)");
        var codec = codecMatch.Success ? codecMatch.Groups[1].Value : null;

        var pixFmtMatch = Regex.Match(line, @",\s*([a-zA-Z0-9_]+)(?:\([^\)]*\))?,");
        var pixFmt = pixFmtMatch.Success ? pixFmtMatch.Groups[1].Value : null;

        return new VideoStreamInfo(isHdrOrWideGamut, codec, pixFmt, null, null);
    }

    public static int ResolveAudioBitrate(AudioStreamInfo? info, int defaultKbps, int maxKbps)
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

    public static int ResolveVorbisQuality(AudioStreamInfo? info)
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

    public static string ExtractDiagnosticMessage(string? logs, int exitCode)
    {
        if (string.IsNullOrWhiteSpace(logs))
        {
            return I18n.T("FfmpegExitError", exitCode);
        }

        var lines = logs.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        for (int i = lines.Length - 1; i >= 0; i--)
        {
            var line = lines[i];
            if (line.StartsWith("frame=", StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith("size=", StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith("video:", StringComparison.OrdinalIgnoreCase) ||
                line.Equals("Conversion failed!", StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith("Press [q]", StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith("configuration:", StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith("libav", StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith("built with", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (line.Contains("error", StringComparison.OrdinalIgnoreCase) ||
                line.Contains("invalid", StringComparison.OrdinalIgnoreCase) ||
                line.Contains("could not", StringComparison.OrdinalIgnoreCase) ||
                line.Contains("cannot", StringComparison.OrdinalIgnoreCase) ||
                line.Contains("unable to", StringComparison.OrdinalIgnoreCase) ||
                line.Contains("no such", StringComparison.OrdinalIgnoreCase) ||
                line.Contains("unknown", StringComparison.OrdinalIgnoreCase) ||
                line.Contains("failed", StringComparison.OrdinalIgnoreCase) ||
                line.Contains("not supported", StringComparison.OrdinalIgnoreCase))
            {
                return line;
            }
        }

        for (int i = lines.Length - 1; i >= 0; i--)
        {
            var line = lines[i];
            if (!line.StartsWith("frame=", StringComparison.OrdinalIgnoreCase) &&
                !line.StartsWith("size=", StringComparison.OrdinalIgnoreCase) &&
                !line.StartsWith("video:", StringComparison.OrdinalIgnoreCase) &&
                !line.StartsWith("Press [q]", StringComparison.OrdinalIgnoreCase))
            {
                return line;
            }
        }

        return I18n.T("FfmpegExitError", exitCode);
    }
}

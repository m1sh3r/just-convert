namespace JustConvert.Core.Converters.Tools;

public static class QualityEstimator
{
    public static int EstimateVideoBitrateKbps(int width, int height, double fps, int cq, string codec)
    {
        var w = width > 0 ? width : 1920;
        var h = height > 0 ? height : 1080;
        var f = fps > 0 ? fps : 30.0;
        var clampedCq = Math.Clamp(cq, 10, 45);

        var c = codec.ToLowerInvariant();
        var baseBpp = c switch
        {
            "h265" or "hevc" or "x265" or "libx265" => 0.060,
            "av1" or "svtav1" or "libsvtav1" => 0.055,
            "vp9" or "libvpx-vp9" => 0.065,
            _ => 0.090
        };

        var bpp = baseBpp * Math.Pow(2, (23.0 - clampedCq) / 6.0);
        var bitrateKbps = (int)((w * h * f * bpp) / 1000.0);

        return Math.Clamp(bitrateKbps, 250, 150000);
    }

    public static long EstimateVideoFileSize(
        int width,
        int height,
        double fps,
        double durationSeconds,
        int cq,
        string videoCodec,
        int audioBitrateKbps = 192)
    {
        var dur = durationSeconds > 0 ? durationSeconds : 60.0;
        var videoKbps = EstimateVideoBitrateKbps(width, height, fps, cq, videoCodec);
        var totalBitrateKbps = videoKbps + Math.Clamp(audioBitrateKbps, 32, 640);

        return (long)((totalBitrateKbps * 1000.0 / 8.0) * dur);
    }

    public static long EstimateVideoFileSize(
        string videoCodec,
        int cq,
        int audioBitrateKbps,
        MediaStreamInfo? mediaInfo)
    {
        var w = mediaInfo?.Video?.Width ?? 1920;
        var h = mediaInfo?.Video?.Height ?? 1080;
        var fps = mediaInfo?.Video?.FrameRateFps ?? 30.0;
        var dur = mediaInfo?.DurationSeconds ?? 60.0;
        return EstimateVideoFileSize(w, h, fps, dur, cq, videoCodec, audioBitrateKbps);
    }

    public static string GetVideoQualityDescriptionKey(int cq, int width = 1920, int height = 1080, double fps = 30.0)
    {
        var pixels = (long)width * height;
        int adjustedCq = cq;
        if (pixels >= 3840 * 2160) adjustedCq -= 2;
        else if (pixels <= 1280 * 720) adjustedCq += 2;

        if (fps >= 50) adjustedCq -= 1;

        return adjustedCq switch
        {
            <= 18 => "QualityDescriptionMaximum",
            <= 23 => "QualityDescriptionHigh",
            <= 28 => "QualityDescriptionBalanced",
            _ => "QualityDescriptionLow"
        };
    }

    public static long EstimateAudioFileSize(
        string targetFormat,
        int bitrateKbps,
        MediaStreamInfo? mediaInfo)
    {
        var dur = mediaInfo?.DurationSeconds ?? 60.0;
        var isLossless = targetFormat is "flac" or "wav" or "aiff" or "alac";
        var sr = mediaInfo?.Audio?.SampleRate ?? 44100;
        var ch = mediaInfo?.Audio?.Channels ?? 2;
        var bps = mediaInfo?.Audio?.BitsPerSample ?? 16;
        return EstimateAudioFileSize(dur, bitrateKbps, isLossless, sr, ch, bps);
    }

    public static long EstimateAudioFileSize(
        double durationSeconds,
        int bitrateKbps,
        bool isLossless = false,
        int sampleRate = 44100,
        int channels = 2,
        int bitsPerSample = 16)
    {
        var dur = durationSeconds > 0 ? durationSeconds : 60.0;

        if (isLossless)
        {
            var sr = sampleRate > 0 ? sampleRate : 44100;
            var ch = channels > 0 ? channels : 2;
            var bps = bitsPerSample > 0 ? bitsPerSample : 16;
            var rawPcmBytes = (long)(sr * ch * (bps / 8.0) * dur);
            return (long)(rawPcmBytes * 0.60);
        }

        var kbps = Math.Clamp(bitrateKbps, 32, 512);
        return (long)((kbps * 1000.0 / 8.0) * dur);
    }

    public static string GetAudioQualityDescriptionKey(int bitrateKbps, bool isLossless = false)
    {
        if (isLossless) return "QualityDescriptionLossless";

        return bitrateKbps switch
        {
            >= 320 => "QualityDescriptionMaximum",
            >= 192 => "QualityDescriptionHigh",
            >= 128 => "QualityDescriptionBalanced",
            _ => "QualityDescriptionLow"
        };
    }

    public static long EstimateImageFileSize(int width, int height, long sourceSizeBytes, int quality, string targetFormat)
    {
        var fmt = targetFormat.TrimStart('.').ToLowerInvariant();
        var q = Math.Clamp(quality, 1, 100);

        if (sourceSizeBytes > 0)
        {
            var factor = q / 100.0;
            var estimated = fmt switch
            {
                "jpg" or "jpeg" => sourceSizeBytes * factor * 0.85,
                "webp" => sourceSizeBytes * factor * 0.65,
                "avif" => sourceSizeBytes * factor * 0.50,
                "jp2" => sourceSizeBytes * factor * 0.80,
                "png" => sourceSizeBytes * 1.05,
                "bmp" => Math.Max(sourceSizeBytes, (width > 0 ? width : 1920) * (height > 0 ? height : 1080) * 4L + 54L),
                _ => sourceSizeBytes * factor
            };

            return Math.Max(1024L, (long)estimated);
        }

        var w = width > 0 ? width : 1920;
        var h = height > 0 ? height : 1080;
        var pixels = (long)w * h;

        var bpp = q switch
        {
            <= 50 => 0.45,
            <= 75 => 0.85,
            <= 90 => 1.50,
            _ => 2.40
        };

        return Math.Max(1024L, (long)(pixels * bpp / 8.0));
    }

    public static long EstimateImageFileSize(string targetFormat, int quality, MediaStreamInfo? mediaInfo)
    {
        var w = mediaInfo?.Video?.Width ?? 1920;
        var h = mediaInfo?.Video?.Height ?? 1080;
        var srcSize = mediaInfo?.FileSizeBytes ?? 0L;
        return EstimateImageFileSize(w, h, srcSize, quality, targetFormat);
    }

    public static string GetImageQualityDescriptionKey(int quality) => quality switch
    {
        <= 55 => "QualityDescriptionLow",
        <= 80 => "QualityDescriptionBalanced",
        <= 95 => "QualityDescriptionHigh",
        _ => "QualityDescriptionMaximum"
    };

    public static string FormatFileSize(long? bytes)
    {
        if (!bytes.HasValue || bytes.Value <= 0) return "—";
        var b = bytes.Value;
        if (b < 1024)
        {
            return $"< 1 {I18n.T("UnitKB")}";
        }

        if (b < 1024 * 1024)
        {
            var kb = b / 1024.0;
            return $"{kb:F0} {I18n.T("UnitKB")}";
        }

        if (b < 1024L * 1024 * 1024)
        {
            var mb = b / (1024.0 * 1024.0);
            return $"{mb:F1} {I18n.T("UnitMB")}";
        }

        var gb = b / (1024.0 * 1024.0 * 1024.0);
        return $"{gb:F2} {I18n.T("UnitGB")}";
    }

    public static string FormatCodecName(string? codec)
    {
        if (string.IsNullOrWhiteSpace(codec)) return "";
        var c = codec.Trim().ToLowerInvariant();
        return c switch
        {
            "h264" or "avc1" => "H.264",
            "hevc" or "h265" or "hvc1" or "hev1" => "H.265/HEVC",
            "av1" or "av01" => "AV1",
            "vp9" => "VP9",
            "vp8" => "VP8",
            "prores" or "prores_ks" => "ProRes",
            "mpeg4" => "MPEG-4",
            "aac" or "mp4a" => "AAC",
            "mp3" => "MP3",
            "flac" => "FLAC",
            "opus" => "Opus",
            "vorbis" => "Vorbis",
            "ac3" => "AC-3",
            "eac3" => "E-AC-3",
            "pcm_s16le" or "pcm_s24le" or "pcm_s32le" => "PCM",
            _ => codec.ToUpperInvariant()
        };
    }
}

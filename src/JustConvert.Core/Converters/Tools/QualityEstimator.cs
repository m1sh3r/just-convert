namespace JustConvert.Core.Converters.Tools;

public static class QualityEstimator
{
    public static int EstimateVideoBitrateKbps(
        int width,
        int height,
        double fps,
        int cq,
        string codec,
        string encoder = "auto",
        int? sourceBitrateKbps = null)
    {
        var w = width > 0 ? width : 1920;
        var h = height > 0 ? height : 1080;
        var f = fps > 0 ? fps : 30.0;
        var c = codec.ToLowerInvariant();
        var enc = encoder.ToLowerInvariant();

        if (c is "copy")
        {
            return sourceBitrateKbps ?? 5000;
        }

        if (c is "prores422" or "prores")
        {
            var pBps = 147_000_000.0 * ((w * (double)h) / (1920.0 * 1080.0)) * (f / 30.0);
            return (int)(pBps / 1000.0);
        }

        if (c is "prores4444")
        {
            var pBps = 330_000_000.0 * ((w * (double)h) / (1920.0 * 1080.0)) * (f / 30.0);
            return (int)(pBps / 1000.0);
        }

        if (c is "proreshq")
        {
            var pBps = 220_000_000.0 * ((w * (double)h) / (1920.0 * 1080.0)) * (f / 30.0);
            return (int)(pBps / 1000.0);
        }

        if (c is "proreslt")
        {
            var pBps = 102_000_000.0 * ((w * (double)h) / (1920.0 * 1080.0)) * (f / 30.0);
            return (int)(pBps / 1000.0);
        }

        if (c is "proresproxy")
        {
            var pBps = 45_000_000.0 * ((w * (double)h) / (1920.0 * 1080.0)) * (f / 30.0);
            return (int)(pBps / 1000.0);
        }

        var clampedCq = Math.Clamp(cq, 10, 45);
        var baseKbps1080p30 = c switch
        {
            "h265" or "hevc" or "x265" or "libx265" => 3200.0,
            "av1" or "svtav1" or "libsvtav1" => 2500.0,
            "vp9" or "libvpx-vp9" => 3300.0,
            "mpeg4" or "xvid" => 6500.0,
            _ => 4500.0
        };

        var cqScale = Math.Pow(2.0, (23.0 - clampedCq) / 6.0);
        var encMultiplier = enc switch
        {
            "nvenc" or "qsv" or "amf" => 1.30,
            _ => 1.0
        };

        var normFactor = Math.Pow((w * (double)h) / (1920.0 * 1080.0), 0.75) * Math.Pow(f / 30.0, 0.65);
        var estimatedKbps = (int)(baseKbps1080p30 * cqScale * encMultiplier * normFactor);

        if (sourceBitrateKbps.HasValue && sourceBitrateKbps.Value > 0)
        {
            var maxAllowed = (int)(sourceBitrateKbps.Value * 1.15);
            if (clampedCq >= 20 && estimatedKbps > maxAllowed)
            {
                estimatedKbps = maxAllowed;
            }
        }

        return Math.Clamp(estimatedKbps, 200, 500_000);
    }

    public static long EstimateVideoFileSize(
        int width,
        int height,
        double fps,
        double durationSeconds,
        int cq,
        string videoCodec,
        int audioBitrateKbps = 192,
        string encoder = "auto",
        int? sourceBitrateKbps = null,
        long? sourceFileSizeBytes = null,
        string? rateControl = "cq",
        int videoBitrateKbps = 15000)
    {
        var dur = durationSeconds > 0 ? durationSeconds : 60.0;
        var c = videoCodec.ToLowerInvariant();

        if (c is "copy")
        {
            if (sourceFileSizeBytes.HasValue && sourceFileSizeBytes.Value > 0)
            {
                return (long)(sourceFileSizeBytes.Value * 1.01);
            }
        }

        var rc = (rateControl ?? "cq").ToLowerInvariant();
        int videoKbps;
        if (rc is "vbr" or "cbr" && videoBitrateKbps > 0)
        {
            videoKbps = videoBitrateKbps;
        }
        else
        {
            videoKbps = EstimateVideoBitrateKbps(width, height, fps, cq, videoCodec, encoder, sourceBitrateKbps);
        }

        var totalBitrateKbps = videoKbps + Math.Clamp(audioBitrateKbps, 32, 640);

        return (long)((totalBitrateKbps * 1000.0 / 8.0) * dur * 1.015);
    }

    public static long EstimateVideoFileSize(
        string videoCodec,
        int cq,
        int audioBitrateKbps,
        MediaStreamInfo? mediaInfo,
        string encoder = "auto",
        string? rateControl = "cq",
        int videoBitrateKbps = 15000)
    {
        var w = mediaInfo?.Video?.Width ?? 1920;
        var h = mediaInfo?.Video?.Height ?? 1080;
        var fps = mediaInfo?.Video?.FrameRateFps ?? 30.0;
        var dur = mediaInfo?.DurationSeconds ?? 60.0;
        int? srcBitrate = null;
        if (mediaInfo?.FileSizeBytes.HasValue == true && dur > 0)
        {
            srcBitrate = (int)((mediaInfo.FileSizeBytes.Value * 8.0) / (dur * 1000.0));
        }

        return EstimateVideoFileSize(w, h, fps, dur, cq, videoCodec, audioBitrateKbps, encoder, srcBitrate, mediaInfo?.FileSizeBytes, rateControl, videoBitrateKbps);
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
        var fmt = targetFormat.TrimStart('.').ToLowerInvariant();
        var isLossless = fmt is "flac" or "wav" or "aiff" or "alac";
        var sr = mediaInfo?.Audio?.SampleRate ?? 44100;
        var ch = mediaInfo?.Audio?.Channels ?? 2;
        var bps = mediaInfo?.Audio?.BitsPerSample ?? 16;
        return EstimateAudioFileSize(dur, bitrateKbps, fmt, isLossless, sr, ch, bps);
    }

    public static long EstimateAudioFileSize(
        double durationSeconds,
        int bitrateKbps,
        string format = "mp3",
        bool isLossless = false,
        int sampleRate = 44100,
        int channels = 2,
        int bitsPerSample = 16)
    {
        var dur = durationSeconds > 0 ? durationSeconds : 60.0;
        var fmt = format.TrimStart('.').ToLowerInvariant();

        if (fmt is "wav" or "aiff")
        {
            var sr = sampleRate > 0 ? sampleRate : 44100;
            var ch = channels > 0 ? channels : 2;
            var bps = bitsPerSample > 0 ? bitsPerSample : 16;
            return (long)(sr * ch * (bps / 8.0) * dur) + 44L;
        }

        if (isLossless || fmt is "flac" or "alac")
        {
            var sr = sampleRate > 0 ? sampleRate : 44100;
            var ch = channels > 0 ? channels : 2;
            var bps = bitsPerSample > 0 ? bitsPerSample : 16;
            var rawPcmBytes = sr * ch * (bps / 8.0) * dur;
            var ratio = bps > 16 ? 0.68 : 0.58;
            return (long)(rawPcmBytes * ratio) + 1024L;
        }

        var kbps = Math.Clamp(bitrateKbps, 32, 512);
        return (long)((kbps * 1000.0 / 8.0) * dur * 1.02);
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
        var w = width > 0 ? width : 1920;
        var h = height > 0 ? height : 1080;
        var pixels = (long)w * h;

        if (fmt is "bmp")
        {
            return pixels * 3L + 54L;
        }

        if (fmt is "tiff" or "tif")
        {
            return (long)(pixels * 3.0 * 0.65) + 1024L;
        }

        if (fmt is "png")
        {
            return (long)(pixels * 1.5) + 1024L;
        }

        var factor = q / 100.0;
        var bpp = 0.05 + 0.22 * Math.Pow(factor, 3) + 0.15 * factor;
        var jpegBytes = (long)(pixels * bpp) + 2048L;

        if (sourceSizeBytes > 0 && pixels > 0)
        {
            var srcBpp = (double)sourceSizeBytes / pixels;
            if (srcBpp is >= 0.05 and <= 1.2)
            {
                var complexityMod = Math.Clamp(srcBpp / 0.20, 0.6, 1.8);
                jpegBytes = (long)(jpegBytes * complexityMod);
            }
        }

        return fmt switch
        {
            "webp" => Math.Max(1024L, (long)(jpegBytes * 0.72)),
            "avif" => Math.Max(1024L, (long)(jpegBytes * 0.55)),
            _ => Math.Max(1024L, jpegBytes)
        };
    }

    public static long EstimateImageFileSize(string targetFormat, int quality, MediaStreamInfo? mediaInfo)
    {
        var w = mediaInfo?.Video?.Width ?? 1920;
        var h = mediaInfo?.Video?.Height ?? 1080;
        var srcSize = mediaInfo?.FileSizeBytes ?? 0L;
        return EstimateImageFileSize(w, h, srcSize, quality, targetFormat);
    }

    public static long EstimateGifFileSize(int width, int height, double durationSeconds)
    {
        var w = width > 0 ? width : 1920;
        var h = height > 0 ? height : 1080;
        var dur = durationSeconds > 0 ? durationSeconds : 5.0;

        var gifWidth = 480;
        var gifHeight = Math.Max(180, (int)(480.0 * h / w));
        var frameCount = (long)(dur * 15.0);
        var bytesPerFrame = (long)(gifWidth * gifHeight * 0.25);

        return Math.Max(1024L, frameCount * bytesPerFrame);
    }

    public static long EstimateFramesFolderSize(int width, int height, double fps, double durationSeconds, string imageFormat)
    {
        var f = fps > 0 ? fps : 30.0;
        var dur = durationSeconds > 0 ? durationSeconds : 10.0;
        var totalFrames = (long)(f * dur);
        var perFrame = EstimateImageFileSize(width, height, 0, 90, imageFormat);
        return Math.Max(1024L, totalFrames * perFrame);
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

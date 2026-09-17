using System.IO;
using JustConvert.Core.Converters.Tools;

namespace JustConvert.Core.Converters;

public static class OutputFileNameHelper
{
    public static string? BuildVideoSuffix(string targetFormat, VideoQualitySetting? setting = null, bool appendSuffix = true)
    {
        if (!appendSuffix) return null;

        var fmt = targetFormat.ToLowerInvariant();
        if (fmt == "reencode") return "Reencode";
        if (fmt is "remux-mp4" or "remux-mkv" or "remux") return "Remux";
        if (fmt is "frames" or "frames-png" or "frames-jpg" or "frames-webp" or "frames-bmp" or "frames-tiff") return "Frames";
        if (fmt is "compress" or "compress-video") return "H.264 - CRF 28";
        if (fmt == "gif") return null;

        if (setting != null)
        {
            var codecName = setting.VideoCodec.ToLowerInvariant() switch
            {
                "h264" => setting.Encoder.ToLowerInvariant() switch
                {
                    "nvenc" => "H.264 NVENC",
                    "qsv" => "H.264 QSV",
                    "amf" => "H.264 AMF",
                    _ => "H.264"
                },
                "h265" or "hevc" => setting.Encoder.ToLowerInvariant() switch
                {
                    "nvenc" => "H.265 NVENC",
                    "qsv" => "H.265 QSV",
                    "amf" => "H.265 AMF",
                    _ => "H.265"
                },
                "av1" => setting.Encoder.ToLowerInvariant() switch
                {
                    "nvenc" => "AV1 NVENC",
                    "qsv" => "AV1 QSV",
                    "amf" => "AV1 AMF",
                    _ => "AV1"
                },
                "vp9" => setting.Encoder.ToLowerInvariant() switch
                {
                    "qsv" => "VP9 QSV",
                    _ => "VP9"
                },
                "prores422" => "ProRes 422",
                "prores4444" => "ProRes 4444",
                "copy" => "Copy",
                _ => setting.VideoCodec.ToUpperInvariant()
            };

            if (setting.VideoCodec is "prores422" or "prores4444" or "copy")
            {
                return codecName;
            }

            return $"{codecName} - CQ {setting.VideoQualityCq}";
        }

        return fmt switch
        {
            "mp4-h264" or "h264" or "mp4" => "H.264 - CQ 23",
            "mp4-h264-nvenc" or "mp4-nvenc-h264" => "H.264 NVENC - CQ 23",
            "mp4-h264-qsv" or "mp4-qsv-h264" => "H.264 QSV - CQ 23",
            "mp4-h264-amf" or "mp4-amf-h264" => "H.264 AMF - CQ 23",

            "mp4-h265" or "h265" or "hevc" or "mp4-hevc" => "H.265 - CQ 23",
            "mp4-h265-nvenc" or "mp4-hevc-nvenc" or "mp4-nvenc-h265" or "mp4-nvenc-hevc" => "H.265 NVENC - CQ 23",
            "mp4-h265-qsv" or "mp4-hevc-qsv" or "mp4-qsv-h265" or "mp4-qsv-hevc" => "H.265 QSV - CQ 23",
            "mp4-h265-amf" or "mp4-hevc-amf" or "mp4-amf-h265" or "mp4-amf-hevc" => "H.265 AMF - CQ 23",

            "webm-vp9" or "vp9" or "webm" => "VP9 - CQ 23",
            "webm-vp9-qsv" or "webm-qsv-vp9" => "VP9 QSV - CQ 23",

            "webm-av1" or "av1" => "AV1 - CQ 23",
            "webm-av1-nvenc" or "webm-nvenc-av1" => "AV1 NVENC - CQ 23",
            "webm-av1-qsv" or "webm-qsv-av1" => "AV1 QSV - CQ 23",
            "webm-av1-amf" or "webm-amf-av1" => "AV1 AMF - CQ 23",

            "mov-prores422" or "prores422" => "ProRes 422",
            "mov-prores4444" or "prores4444" => "ProRes 4444",

            "remux-mp4" or "remux-mkv" or "remux" => "Remux",
            "frames" or "frames-png" or "frames-jpg" or "frames-webp" or "frames-bmp" or "frames-tiff" => "Frames",
            "compress" or "compress-video" => "H.264 - CRF 28",
            "gif" => null,
            "reencode" => "Reencode",
            _ => null
        };
    }

    public static string? BuildAudioSuffix(string targetFormat, AudioStreamInfo? audioInfo = null, int? bitrateKbps = null, bool appendSuffix = true)
    {
        if (!appendSuffix) return null;

        var fmt = targetFormat.ToLowerInvariant();
        var is24Bit = audioInfo?.BitsPerSample >= 24;

        if (bitrateKbps.HasValue && fmt is "mp3" or "aac" or "ogg" or "vorbis" or "opus")
        {
            if (fmt is "ogg" or "vorbis") return $"Vorbis - {bitrateKbps.Value}k";
            return $"{bitrateKbps.Value}k";
        }

        return fmt switch
        {
            "mp3" => "320k",
            "aac" => "320k",
            "m4a" => audioInfo?.IsLossless == true ? "ALAC - Lossless" : (bitrateKbps.HasValue ? $"AAC - {bitrateKbps.Value}k" : "AAC - 320k"),
            "flac" => is24Bit ? "24-bit" : "Lossless",
            "wav" => is24Bit ? "PCM 24-bit" : "PCM 16-bit",
            "opus" => (audioInfo?.Channels == 1) ? "192k" : "256k",
            "ogg" or "vorbis" => "Vorbis - 320k",
            "aiff" or "aif" => is24Bit ? "PCM 24-bit" : "PCM 16-bit",
            "reencode" => "Reencode",
            _ => null
        };
    }

    public static string? BuildImageSuffix(string targetFormat, int? quality = null, bool appendQualitySuffix = true)
    {
        var fmt = targetFormat.ToLowerInvariant();
        if (fmt == "reencode") return "Reencode";

        if (AppSettings.SupportsQuality(fmt))
        {
            if (!appendQualitySuffix) return null;
            var q = quality ?? AppSettings.GetDefaultQuality(fmt);
            return $"Q{q}";
        }

        return fmt switch
        {
            "ico" => "Multi-layer",
            "tiff" or "tif" => "LZW",
            _ => null
        };
    }

    public static string GetUniquePath(string directory, string baseFileName, string? suffix, string extension, bool isDirectory = false)
    {
        var hasSuffix = !string.IsNullOrWhiteSpace(suffix);
        var baseName = hasSuffix ? $"{baseFileName} [{suffix}]" : baseFileName;
        var candidate = Path.Combine(directory, isDirectory ? baseName : $"{baseName}{extension}");

        if (!File.Exists(candidate) && !Directory.Exists(candidate))
        {
            return candidate;
        }

        int counter = 1;
        while (true)
        {
            var candidateWithIndex = isDirectory
                ? Path.Combine(directory, $"{baseName} ({counter})")
                : Path.Combine(directory, $"{baseName} ({counter}){extension}");

            if (!File.Exists(candidateWithIndex) && !Directory.Exists(candidateWithIndex))
            {
                return candidateWithIndex;
            }
            counter++;
        }
    }
}

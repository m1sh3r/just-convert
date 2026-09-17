using JustConvert.Core.Converters;
using JustConvert.Core.Logging;

namespace JustConvert.Core;

public class ConverterRegistry
{
    private readonly List<IFormatConverter> _converters =
    [
        new ImageConverter(),
        new AudioConverter(),
        new VideoConverter()
    ];

    public ConverterRegistry() { }

    public void RegisterConverter(IFormatConverter converter)
    {
        _converters.Add(converter);
    }

    public IReadOnlyList<IFormatConverter> Converters => _converters;

    public IReadOnlyList<string> GetAvailableTargetFormats(string sourceExtension)
    {
        var targets = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var converter in _converters)
        {
            var formats = converter.GetSupportedTargetFormats(sourceExtension);
            foreach (var f in formats)
            {
                var lower = f.ToLowerInvariant();
                if (seen.Add(lower))
                {
                    targets.Add(lower);
                }
            }
        }
        return targets;
    }

    public IFormatConverter? FindConverter(string sourceExtension, string targetExtension)
    {
        return _converters.FirstOrDefault(c => c.CanConvert(sourceExtension, targetExtension));
    }

    public bool CanConvert(string sourceExtension, string targetExtension)
    {
        return FindConverter(sourceExtension, targetExtension) != null;
    }

    private static readonly HashSet<string> VideoExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        "mp4", "mkv", "avi", "mov", "webm", "wmv", "flv", "m4v"
    };

    public async Task<ConversionResult> ConvertFileAsync(
        string inputPath,
        string targetExtension,
        string? outputPath = null,
        IProgress<ConversionProgress>? progress = null,
        CancellationToken ct = default,
        bool isBatch = false,
        IConversionController? controller = null)
    {
        if (!File.Exists(inputPath))
        {
            AppLogger.Warn($"[Registry] File not found: \"{inputPath}\"");
            return new ConversionResult(false, null, I18n.T("FileNotFound", inputPath));
        }

        var sourceExt = Path.GetExtension(inputPath).TrimStart('.').ToLowerInvariant();
        var targetExt = targetExtension.TrimStart('.').ToLowerInvariant();

        var isVideoSameFormat = VideoExtensions.Contains(sourceExt) && VideoExtensions.Contains(targetExt);

        if (!isVideoSameFormat && targetExt != "reencode" && targetExt != "remux" && IsSameFormat(sourceExt, targetExt) && !targetExt.StartsWith("frames"))
        {
            var msg = I18n.T("StatusSkippedAlreadyTarget");
            AppLogger.Info($"[Registry] Skipped (already target format): \"{inputPath}\" ({sourceExt})");
            progress?.Report(new ConversionProgress(100, msg));
            return new ConversionResult(true, inputPath, msg, null, TimeSpan.Zero, Skipped: true);
        }

        var converter = FindConverter(sourceExt, targetExt);
        if (converter == null)
        {
            if (isBatch)
            {
                var msg = I18n.T("StatusSkippedUnsupported");
                AppLogger.Warn($"[Registry] Skipped (unsupported in batch): \"{inputPath}\" ({sourceExt} -> {targetExt})");
                progress?.Report(new ConversionProgress(100, msg));
                return new ConversionResult(true, inputPath, msg, null, TimeSpan.Zero, Skipped: true);
            }

            AppLogger.Error($"[Registry] No converter found: \"{inputPath}\" ({sourceExt} -> {targetExt})");
            return new ConversionResult(false, null, I18n.T("NoConverterFound", sourceExt, targetExt));
        }

        AppLogger.Info($"[Registry] Dispatching to {converter.Name}: \"{inputPath}\" -> {targetExt}");
        var result = await converter.ConvertAsync(inputPath, targetExt, outputPath, progress, ct, controller);
        AppLogger.Info($"[Registry] Completed: \"{inputPath}\" (Success={result.Success}, Skipped={result.Skipped}, Duration={result.Duration.TotalSeconds:F2}s)");
        return result;
    }

    private static bool IsSameFormat(string src, string tgt)
    {
        if (src == tgt) return true;
        if (src is "jpg" or "jpeg" && tgt is "jpg" or "jpeg") return true;
        if (src is "tiff" or "tif" && tgt is "tiff" or "tif") return true;
        if (src is "jp2" or "jpeg2000" && tgt is "jp2" or "jpeg2000") return true;
        if (src is "aiff" or "aif" && tgt is "aiff" or "aif") return true;
        return false;
    }
}

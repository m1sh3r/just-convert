using JustConvert.Core.Converters;

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
            return new ConversionResult(false, null, I18n.T("FileNotFound", inputPath));
        }

        var sourceExt = Path.GetExtension(inputPath).TrimStart('.').ToLowerInvariant();
        var targetExt = targetExtension.TrimStart('.').ToLowerInvariant();

        var isVideoSameFormat = VideoExtensions.Contains(sourceExt) && VideoExtensions.Contains(targetExt);

        if (!isVideoSameFormat && targetExt != "reencode" && targetExt != "remux" && IsSameFormat(sourceExt, targetExt) && targetExt is not "frames" and not "frames-png" and not "frames-jpg")
        {
            var msg = I18n.T("StatusSkippedAlreadyTarget");
            progress?.Report(new ConversionProgress(100, msg));
            return new ConversionResult(true, inputPath, msg, null, TimeSpan.Zero, Skipped: true);
        }

        var converter = FindConverter(sourceExt, targetExt);
        if (converter == null)
        {
            if (isBatch)
            {
                var msg = I18n.T("StatusSkippedUnsupported");
                progress?.Report(new ConversionProgress(100, msg));
                return new ConversionResult(true, inputPath, msg, null, TimeSpan.Zero, Skipped: true);
            }

            return new ConversionResult(false, null, I18n.T("NoConverterFound", sourceExt, targetExt));
        }

        return await converter.ConvertAsync(inputPath, targetExt, outputPath, progress, ct, controller);
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

using JustConvert.Core.Converters;

namespace JustConvert.Core;

public class ConverterRegistry
{
    private readonly List<IFormatConverter> _converters =
    [
        new ImageConverter(),
        new MediaConverter()
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

        if (targetExt != "reencode" && sourceExt.Equals(targetExt, StringComparison.OrdinalIgnoreCase) && targetExt is not "frames" and not "frames-png" and not "frames-jpg")
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
}

namespace JustConvert.Core;

public record ConversionResult(
    bool Success,
    string? OutputPath = null,
    string? ErrorMessage = null,
    string? FullLog = null,
    TimeSpan Duration = default,
    bool Skipped = false
);

public record ConversionProgress(
    double Percentage,
    string StatusMessage,
    string? Detail = null
);

public interface IFormatConverter
{
    string Name { get; }
    bool CanConvert(string sourceExtension, string targetExtension);
    IReadOnlyList<string> GetSupportedTargetFormats(string sourceExtension);
    Task<ConversionResult> ConvertAsync(
        string inputPath,
        string targetExtension,
        string? outputPath = null,
        IProgress<ConversionProgress>? progress = null,
        CancellationToken ct = default
    );
}

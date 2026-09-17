namespace JustConvert.Core;

public record ConversionResult(
    bool Success,
    string? OutputPath = null,
    string? ErrorMessage = null,
    string? FullLog = null,
    TimeSpan Duration = default,
    bool Skipped = false,
    bool CpuFallback = false
);

public record ConversionProgress(
    double Percentage,
    string StatusMessage,
    string? Detail = null
);

public interface IConversionController
{
    void OnProcessStarted(System.Diagnostics.Process process);
}

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
        CancellationToken ct = default,
        IConversionController? controller = null
    );
}

using System.Diagnostics;
using System.IO;

namespace JustConvert.Core.Converters.Tools;

public record PresetValidationResult(bool IsValid, string Message);

public static class CustomPresetValidator
{
    public static async Task<PresetValidationResult> ValidateFfmpegArgumentsAsync(string customArguments, string category, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(customArguments))
        {
            return new PresetValidationResult(false, I18n.T("PresetValidationEmptyArgs"));
        }

        var ffmpeg = ToolLocator.FindFfmpegPath();
        if (ffmpeg == null)
        {
            return new PresetValidationResult(false, I18n.T("FfmpegNotFound"));
        }

        var cleanCat = category?.ToLowerInvariant() ?? "video";
        var dummyInput = cleanCat == "audio"
            ? "-f lavfi -i anullsrc=r=44100:cl=stereo -t 0.1"
            : "-f lavfi -i nullsrc=s=64x64:d=0.1 -t 0.1";

        var psi = new ProcessStartInfo
        {
            FileName = ffmpeg,
            Arguments = $"-v error {dummyInput} {customArguments} -f null -",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardError = true,
            StandardErrorEncoding = System.Text.Encoding.UTF8
        };

        try
        {
            using var proc = new Process { StartInfo = psi };
            proc.Start();
            var errorOutput = await proc.StandardError.ReadToEndAsync(ct);
            await proc.WaitForExitAsync(ct);

            if (proc.ExitCode == 0)
            {
                return new PresetValidationResult(true, I18n.T("PresetValidationSuccess"));
            }

            var cleanErr = string.IsNullOrWhiteSpace(errorOutput)
                ? string.Format(I18n.T("PresetValidationErrorGeneric"), proc.ExitCode)
                : errorOutput.Trim();

            return new PresetValidationResult(false, cleanErr);
        }
        catch (Exception ex)
        {
            return new PresetValidationResult(false, ex.Message);
        }
    }
}

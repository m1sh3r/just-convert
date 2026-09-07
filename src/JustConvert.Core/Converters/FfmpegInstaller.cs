using System.Diagnostics;
using System.IO.Compression;

namespace JustConvert.Core.Converters;

public static class FfmpegInstaller
{
    public static async Task<bool> DownloadToDirectoryAsync(
        string targetDir,
        IProgress<(double? Percent, string Status)>? progress = null,
        CancellationToken ct = default)
    {
        try
        {
            Directory.CreateDirectory(targetDir);
            var ffmpegDest = Path.Combine(targetDir, "ffmpeg.exe");

            if (File.Exists(ffmpegDest))
            {
                return true;
            }

            var tempZip = Path.Combine(Path.GetTempPath(), $"ffmpeg_{Guid.NewGuid():N}.zip");
            var tempExtract = Path.Combine(Path.GetTempPath(), $"ffmpeg_{Guid.NewGuid():N}");

            progress?.Report((null, I18n.T("SetupDownloadingFfmpeg")));

            using (var httpClient = new HttpClient())
            {
                var version = typeof(FfmpegInstaller).Assembly.GetName().Version?.ToString(3);
                var userAgent = string.IsNullOrEmpty(version) ? "m1sh3r-JustConvert-Installer" : $"m1sh3r-JustConvert-Installer/{version}";
                httpClient.DefaultRequestHeaders.Add("User-Agent", userAgent);
                using var response = await httpClient.GetAsync("https://www.gyan.dev/ffmpeg/builds/ffmpeg-release-essentials.zip", HttpCompletionOption.ResponseHeadersRead, ct);
                response.EnsureSuccessStatusCode();

                var totalBytes = response.Content.Headers.ContentLength;
                await using var contentStream = await response.Content.ReadAsStreamAsync(ct);
                await using var fileStream = new FileStream(tempZip, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true);

                var buffer = new byte[81920];
                long totalRead = 0;
                int bytesRead;

                while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length, ct)) > 0)
                {
                    await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), ct);
                    totalRead += bytesRead;

                    if (totalBytes.HasValue && totalBytes.Value > 0)
                    {
                        var percent = Math.Clamp((double)totalRead / totalBytes.Value * 100.0, 0, 100);
                        var mbRead = totalRead / (1024.0 * 1024.0);
                        var mbTotal = totalBytes.Value / (1024.0 * 1024.0);
                        progress?.Report((percent, I18n.T("SetupDownloadingFfmpegProgress", mbRead, mbTotal, percent)));
                    }
                    else
                    {
                        var mbRead = totalRead / (1024.0 * 1024.0);
                        progress?.Report((null, $"{I18n.T("SetupDownloadingFfmpeg")} ({mbRead:F1} MB)"));
                    }
                }
            }

            progress?.Report((null, I18n.T("SetupExtractingFfmpeg")));
            ZipFile.ExtractToDirectory(tempZip, tempExtract);

            var foundFile = Directory.GetFiles(tempExtract, "ffmpeg.exe", SearchOption.AllDirectories).FirstOrDefault();
            if (foundFile != null)
            {
                File.Copy(foundFile, ffmpegDest, true);

                try
                {
                    File.Delete(tempZip);
                    Directory.Delete(tempExtract, true);
                }
                catch { }

                return true;
            }
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine(ex.Message);
            Console.ResetColor();
        }

        return false;
    }
}

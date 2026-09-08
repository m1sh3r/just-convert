using System.Diagnostics;
using System.Text.Json;

namespace JustConvert.Core.Converters;

public static class ImageMagickInstaller
{
    private const string FallbackUrl = "https://github.com/ImageMagick/ImageMagick/releases/download/7.1.2-31/ImageMagick-7.1.2-31-portable-Q16-x64.7z";

    public static async Task<bool> DownloadToDirectoryAsync(
        string targetDir,
        IProgress<(double? Percent, string Status)>? progress = null,
        CancellationToken ct = default)
    {
        try
        {
            Directory.CreateDirectory(targetDir);
            var magickDest = Path.Combine(targetDir, "magick.exe");

            if (File.Exists(magickDest))
            {
                return true;
            }

            var downloadUrl = await ResolveDownloadUrlAsync(ct);
            var tempArchive = Path.Combine(Path.GetTempPath(), $"imagemagick_{Guid.NewGuid():N}.7z");
            var tempExtract = Path.Combine(Path.GetTempPath(), $"imagemagick_{Guid.NewGuid():N}");

            progress?.Report((null, I18n.T("SetupDownloadingMagick")));

            using (var httpClient = new HttpClient())
            {
                var version = typeof(ImageMagickInstaller).Assembly.GetName().Version?.ToString(3);
                var userAgent = string.IsNullOrEmpty(version) ? "m1sh3r-JustConvert-Installer" : $"m1sh3r-JustConvert-Installer/{version}";
                httpClient.DefaultRequestHeaders.Add("User-Agent", userAgent);

                using var response = await httpClient.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead, ct);
                response.EnsureSuccessStatusCode();

                var totalBytes = response.Content.Headers.ContentLength;
                await using var contentStream = await response.Content.ReadAsStreamAsync(ct);
                await using var fileStream = new FileStream(tempArchive, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true);

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
                        progress?.Report((percent, I18n.T("SetupDownloadingMagickProgress", mbRead, mbTotal, percent)));
                    }
                    else
                    {
                        var mbRead = totalRead / (1024.0 * 1024.0);
                        progress?.Report((null, $"{I18n.T("SetupDownloadingMagick")} ({mbRead:F1} MB)"));
                    }
                }
            }

            progress?.Report((null, I18n.T("SetupExtractingMagick")));
            Directory.CreateDirectory(tempExtract);

            var tarPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "tar.exe");
            var tarExe = File.Exists(tarPath) ? tarPath : "tar.exe";

            var psi = new ProcessStartInfo
            {
                FileName = tarExe,
                Arguments = $"-xf \"{tempArchive}\" -C \"{tempExtract}\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true,
                RedirectStandardOutput = true
            };

            using (var proc = Process.Start(psi))
            {
                if (proc != null)
                {
                    await proc.WaitForExitAsync(ct);
                }
            }

            var foundMagick = Directory.GetFiles(tempExtract, "magick.exe", SearchOption.AllDirectories).FirstOrDefault();
            if (foundMagick != null)
            {
                var fullTargetDir = Path.GetFullPath(targetDir);
                var destPrefix = fullTargetDir.EndsWith(Path.DirectorySeparatorChar) ? fullTargetDir : fullTargetDir + Path.DirectorySeparatorChar;

                var sourceFolder = Path.GetDirectoryName(foundMagick) ?? tempExtract;
                foreach (var file in Directory.GetFiles(sourceFolder))
                {
                    var destFile = Path.GetFullPath(Path.Combine(fullTargetDir, Path.GetFileName(file)));
                    if (destFile.StartsWith(destPrefix, StringComparison.OrdinalIgnoreCase))
                    {
                        try
                        {
                            File.Copy(file, destFile, true);
                        }
                        catch (IOException)
                        {
                            try
                            {
                                var oldFile = destFile + "." + Guid.NewGuid().ToString("N")[..8] + ".old";
                                if (File.Exists(destFile))
                                {
                                    File.Move(destFile, oldFile, true);
                                }
                                File.Copy(file, destFile, true);
                                try { File.Delete(oldFile); } catch { }
                            }
                            catch { }
                        }
                        catch { }
                    }
                }

                try
                {
                    File.Delete(tempArchive);
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

    private static async Task<string> ResolveDownloadUrlAsync(CancellationToken ct)
    {
        try
        {
            using var client = new HttpClient();
            client.DefaultRequestHeaders.Add("User-Agent", "m1sh3r-JustConvert-Installer");
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(5));

            var json = await client.GetStringAsync("https://api.github.com/repos/ImageMagick/ImageMagick/releases/latest", cts.Token);
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("assets", out var assets) && assets.ValueKind == JsonValueKind.Array)
            {
                foreach (var asset in assets.EnumerateArray())
                {
                    if (asset.TryGetProperty("name", out var nameProp) && asset.TryGetProperty("browser_download_url", out var urlProp))
                    {
                        var name = nameProp.GetString() ?? "";
                        if (name.StartsWith("ImageMagick-", StringComparison.OrdinalIgnoreCase) &&
                            name.Contains("-portable-Q16-x64.7z", StringComparison.OrdinalIgnoreCase))
                        {
                            var url = urlProp.GetString();
                            if (!string.IsNullOrEmpty(url))
                            {
                                return url;
                            }
                        }
                    }
                }
            }
        }
        catch { }

        return FallbackUrl;
    }
}

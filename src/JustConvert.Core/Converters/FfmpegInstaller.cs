using System.Diagnostics;
using System.IO.Compression;

namespace JustConvert.Core.Converters;

public static class FfmpegInstaller
{
    public static async Task<bool> DownloadToDirectoryAsync(string targetDir)
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

            using (var httpClient = new HttpClient())
            {
                httpClient.DefaultRequestHeaders.Add("User-Agent", "m1sh3r-JustConvert-Installer/0.0.1");
                var bytes = await httpClient.GetByteArrayAsync("https://www.gyan.dev/ffmpeg/builds/ffmpeg-release-essentials.zip");
                await File.WriteAllBytesAsync(tempZip, bytes);
            }

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

using System.IO;
using JustConvert.Core.Logging;

namespace JustConvert.Tests;

public class AppLoggerTests
{
    [Fact]
    public void AppLogger_InitializesAndLogsMessages()
    {
        AppLogger.ResetForTesting();
        AppLogger.Initialize("just-convert-test");

        var path = AppLogger.CurrentLogFilePath;
        Assert.NotNull(path);
        Assert.True(File.Exists(path));

        var tempDir = Path.GetTempPath().TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var dir = Path.GetDirectoryName(path)?.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        Assert.Equal(tempDir, dir);

        var fileName = Path.GetFileName(path);
        Assert.StartsWith("m1sh3r-just-convert-test-", fileName, StringComparison.OrdinalIgnoreCase);

        var uniqueToken = Guid.NewGuid().ToString("N");
        AppLogger.Info($"Test info: {uniqueToken}");
        AppLogger.Warn($"Test warn: {uniqueToken}");
        AppLogger.Error($"Test error: {uniqueToken}", new InvalidOperationException("Test exception"));
        AppLogger.LogProcess("ffmpeg", "-i input.mp4 output.mkv", 0, TimeSpan.FromSeconds(1.23));

        string content;
        using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
        using (var reader = new StreamReader(stream))
        {
            content = reader.ReadToEnd();
        }

        Assert.Contains(uniqueToken, content);
        Assert.Contains("[INFO]", content);
        Assert.Contains("[WARN]", content);
        Assert.Contains("[ERROR]", content);
        Assert.Contains("Test exception", content);
        Assert.Contains("Process completed: ffmpeg", content);
    }
}

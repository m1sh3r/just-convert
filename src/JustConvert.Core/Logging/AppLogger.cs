using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace JustConvert.Core.Logging;

public static class AppLogger
{
    private static readonly object Lock = new();
    private static StreamWriter? _writer;
    private static bool _initialized;

    public static string? CurrentLogFilePath { get; private set; }

    public static void Initialize(string appName = "just-convert")
    {
        lock (Lock)
        {
            if (_initialized) return;
            _initialized = true;

            try
            {
                var dir = Path.GetTempPath();
                var fileName = $"m1sh3r-{appName}-{DateTime.Now:yyyyMMdd-HHmmss}-{Environment.ProcessId}.log";
                CurrentLogFilePath = Path.Combine(dir, fileName);

                var stream = new FileStream(CurrentLogFilePath, FileMode.Create, FileAccess.Write, FileShare.ReadWrite);
                _writer = new StreamWriter(stream, Encoding.UTF8) { AutoFlush = true };

                AppDomain.CurrentDomain.UnhandledException += (s, e) =>
                {
                    var ex = e.ExceptionObject as Exception;
                    Error("AppDomain UnhandledException (IsTerminating=" + e.IsTerminating + ")", ex);
                };

                TaskScheduler.UnobservedTaskException += (s, e) =>
                {
                    Error("TaskScheduler UnobservedTaskException", e.Exception);
                };

                WriteHeader(appName);
            }
            catch { }
        }
    }

    internal static void ResetForTesting()
    {
        lock (Lock)
        {
            _writer?.Dispose();
            _writer = null;
            _initialized = false;
            CurrentLogFilePath = null;
        }
    }

    private static void WriteHeader(string appName)
    {
        var asm = typeof(AppLogger).Assembly;
        var version = asm.GetName().Version?.ToString() ?? "unknown";

        WriteLine($"=== {appName} session started at {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} ===");
        WriteLine($"Version: {version}");
        WriteLine($"Process: PID {Environment.ProcessId}, Path: {Environment.ProcessPath}");
        WriteLine($"Command Line: {Environment.CommandLine}");
        WriteLine($"OS: {RuntimeInformation.OSDescription} ({RuntimeInformation.OSArchitecture})");
        WriteLine($"Runtime: {RuntimeInformation.FrameworkDescription}");
        WriteLine(new string('-', 60));
    }

    public static void Info(string message) => WriteEntry("INFO", message);

    public static void Warn(string message) => WriteEntry("WARN", message);

    public static void Error(string message, Exception? ex = null)
    {
        if (ex != null)
        {
            WriteEntry("ERROR", $"{message}\n{ex.GetType().FullName}: {ex.Message}\n{ex.StackTrace}");
        }
        else
        {
            WriteEntry("ERROR", message);
        }
    }

    public static void LogProcess(string tool, string arguments, int exitCode, TimeSpan elapsed, string? fullLog = null)
    {
        var level = exitCode == 0 ? "INFO" : "ERROR";
        var sb = new StringBuilder();
        sb.AppendLine($"Process completed: {tool} (ExitCode: {exitCode}, Duration: {elapsed.TotalSeconds:F2}s)");
        sb.AppendLine($"Arguments: {arguments}");
        if (exitCode != 0 && !string.IsNullOrWhiteSpace(fullLog))
        {
            sb.AppendLine("Process Log:");
            sb.Append(fullLog.TrimEnd());
        }
        WriteEntry(level, sb.ToString().TrimEnd());
    }

    private static void WriteEntry(string level, string message)
    {
        var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
        var line = $"[{timestamp}] [{level}] {message}";
        WriteLine(line);
    }

    private static void WriteLine(string line)
    {
        lock (Lock)
        {
            try
            {
                _writer?.WriteLine(line);
            }
            catch { }
        }
    }
}

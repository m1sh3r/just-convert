using System.Diagnostics;
using System.IO;
using System.Windows;
using JustConvert.Cli.UI;
using JustConvert.Core;
using JustConvert.Core.Windows;
using File = System.IO.File;

namespace JustConvert.Cli;

public class Program
{
    private static readonly ConverterRegistry Registry = new();

    [STAThread]
    public static int Main(string[] args)
    {
        if (args.Length == 0)
        {
            return 0;
        }

        if (args.Length >= 1 && args[0].Equals("register", StringComparison.OrdinalIgnoreCase))
        {
            var exePath = Environment.ProcessPath ?? Path.Combine(AppContext.BaseDirectory, "just-convert.exe");
            new ClassicContextMenuManager(Registry).Register(exePath);
            return 0;
        }

        if (args.Length >= 1 && args[0].Equals("unregister", StringComparison.OrdinalIgnoreCase))
        {
            new ClassicContextMenuManager(Registry).Unregister();
            return 0;
        }

        List<string> inputFiles = [];
        string? targetFormat = null;
        string? outputPath = null;
        bool isSilent = false;
        bool isBatch = false;

        for (int i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            if (arg.Equals("convert", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if ((arg is "--to" or "-t" or "-f" or "/to" or "/t") && i + 1 < args.Length)
            {
                targetFormat = args[++i];
            }
            else if ((arg is "--out" or "-o" or "/out" or "/o") && i + 1 < args.Length)
            {
                outputPath = args[++i];
            }
            else if ((arg is "--file-list" or "-l" or "/file-list") && i + 1 < args.Length)
            {
                var listFile = args[++i];
                if (File.Exists(listFile))
                {
                    try
                    {
                        var lines = File.ReadAllLines(listFile);
                        foreach (var line in lines)
                        {
                            if (!string.IsNullOrWhiteSpace(line)) inputFiles.Add(line.Trim());
                        }
                        File.Delete(listFile);
                    }
                    catch { }
                }
            }
            else if (arg is "--silent" or "-s" or "/silent" or "/s" or "--no-gui")
            {
                isSilent = true;
            }
            else if (arg is "--batch" or "-b" or "/batch" or "/b")
            {
                isBatch = true;
            }
            else if (!arg.StartsWith('-') && !arg.StartsWith('/'))
            {
                if (targetFormat == null && inputFiles.Count == 1 && !File.Exists(arg) && !Directory.Exists(arg))
                {
                    targetFormat = arg;
                }
                else
                {
                    inputFiles.Add(arg);
                }
            }
        }

        if (inputFiles.Count > 1)
        {
            isBatch = true;
        }
        else if (!isBatch)
        {
            try
            {
                isBatch = Process.GetProcessesByName("just-convert").Length > 1;
            }
            catch { }
        }

        if (inputFiles.Count == 0 || string.IsNullOrWhiteSpace(targetFormat))
        {
            if (isSilent)
            {
                Console.Error.WriteLine(I18n.T("CliMissingArgs"));
                return 1;
            }
            return RunWindow(() => ConversionProgressWindow.CreateForError(inputFiles.FirstOrDefault() ?? "", targetFormat ?? "", I18n.T("CliMissingArgs"), null));
        }

        if (isSilent)
        {
            int failureCount = 0;
            foreach (var inputPath in inputFiles)
            {
                if (!File.Exists(inputPath))
                {
                    Console.Error.WriteLine(I18n.T("FileNotFound", inputPath));
                    failureCount++;
                    continue;
                }

                var result = Registry.ConvertFileAsync(inputPath, targetFormat, outputPath, null, default, isBatch).GetAwaiter().GetResult();
                if (!result.Success)
                {
                    Console.Error.WriteLine(result.ErrorMessage ?? I18n.T("ErrorDefault"));
                    failureCount++;
                }
            }
            return failureCount > 0 ? 1 : 0;
        }

        if (ConversionQueueIpc.TrySend(inputFiles, targetFormat, outputPath))
        {
            return 0;
        }

        var mutexName = @"Global\JustConvert_QueueMutex_" + Environment.UserName;
        Mutex? mutex = null;
        bool acquired = false;
        try
        {
            mutex = new Mutex(true, mutexName, out acquired);
        }
        catch { }

        if (!acquired)
        {
            for (int attempt = 0; attempt < 30; attempt++)
            {
                Thread.Sleep(100);
                if (ConversionQueueIpc.TrySend(inputFiles, targetFormat, outputPath))
                {
                    return 0;
                }
            }

            try
            {
                acquired = mutex?.WaitOne(500) ?? false;
            }
            catch { }

            if (!acquired)
            {
                if (ConversionQueueIpc.TrySend(inputFiles, targetFormat, outputPath))
                {
                    return 0;
                }
                return 0;
            }
        }

        ConversionProgressWindow? window = null;
        var pendingMessages = new System.Collections.Concurrent.ConcurrentQueue<QueueIpcMessage>();

        using var ipcServer = ConversionQueueIpc.StartServer(msg =>
        {
            if (window != null)
            {
                window.Dispatcher.Invoke(() =>
                {
                    window.EnqueueFiles(msg.Files, msg.TargetFormat, msg.OutputPath);
                    window.Activate();
                });
            }
            else
            {
                pendingMessages.Enqueue(msg);
            }
        });

        var exitCode = RunWindow(() =>
        {
            window = new ConversionProgressWindow(inputFiles, targetFormat, outputPath, isBatch);
            while (pendingMessages.TryDequeue(out var pending))
            {
                window.EnqueueFiles(pending.Files, pending.TargetFormat, pending.OutputPath);
            }
            return window;
        });

        try
        {
            if (acquired)
            {
                mutex?.ReleaseMutex();
            }
            mutex?.Dispose();
        }
        catch { }

        return exitCode;
    }

    private static int RunWindow(Func<ConversionProgressWindow> windowFactory)
    {
        var app = new Application();
        app.Resources.MergedDictionaries.Add(new Wpf.Ui.Markup.ThemesDictionary { Theme = Wpf.Ui.Appearance.ApplicationTheme.Light });
        app.Resources.MergedDictionaries.Add(new Wpf.Ui.Markup.ControlsDictionary());
        var window = windowFactory();
        app.Run(window);
        return window.ExitCode;
    }
}
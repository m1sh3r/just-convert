using System.Diagnostics;
using System.IO;
using System.Windows;
using JustConvert.Cli.UI;
using JustConvert.Core;
using JustConvert.Core.Converters.Tools;
using JustConvert.Core.Scanning;
using JustConvert.Core.Windows;
using File = System.IO.File;

namespace JustConvert.Cli;

public class Program
{
    private static readonly ConverterRegistry Registry = new();

    [STAThread]
    public static int Main(string[] args)
    {
        TouchpadScrollHelper.Initialize();

        if (args.Length == 0 || (args.Length == 1 && args[0] is "--settings" or "-s" or "settings"))
        {
            var app = new Application();
            app.Resources.MergedDictionaries.Add(new Wpf.Ui.Markup.ThemesDictionary { Theme = Wpf.Ui.Appearance.ApplicationTheme.Light });
            app.Resources.MergedDictionaries.Add(new Wpf.Ui.Markup.ControlsDictionary());
            var settingsWindow = new SettingsWindow();
            return app.Run(settingsWindow);
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

        if (inputFiles.Count == 1 && Directory.Exists(inputFiles[0]))
        {
            var folderPath = inputFiles[0];
            if (string.IsNullOrWhiteSpace(targetFormat))
            {
                if (isSilent)
                {
                    Console.Error.WriteLine(I18n.T("CliMissingArgs"));
                    return 1;
                }
                return RunWindow(() => new FolderBatchWindow(folderPath));
            }

            if (!isSilent && !PromptOptionsIfNeeded(targetFormat, null))
            {
                Application.Current?.Shutdown();
                return 0;
            }

            var scan = FolderScanner.Scan(folderPath, recursive: false);
            var categoryPlans = new Dictionary<MediaCategory, BatchCategoryPlan>();
            var fmt = targetFormat.TrimStart('.').ToLowerInvariant();

            if (Registry.FindConverter("mp4", fmt) != null || fmt is "remux" or "remux-mp4" or "remux-mkv" or "frames" or "gif")
            {
                categoryPlans[MediaCategory.Video] = new BatchCategoryPlan(MediaCategory.Video, true, fmt);
            }
            if (Registry.FindConverter("mp3", fmt) != null)
            {
                categoryPlans[MediaCategory.Audio] = new BatchCategoryPlan(MediaCategory.Audio, true, fmt);
            }
            if (Registry.FindConverter("png", fmt) != null)
            {
                categoryPlans[MediaCategory.Image] = new BatchCategoryPlan(MediaCategory.Image, true, fmt);
            }

            var plannedItems = FolderBatchPlanner.Plan(scan, categoryPlans, DestinationMode.InPlace);
            if (plannedItems.Count == 0)
            {
                if (isSilent) Console.Error.WriteLine(I18n.T("NoSupportedFiles"));
                return 1;
            }

            if (isSilent)
            {
                int failureCount = 0;
                foreach (var item in plannedItems)
                {
                    var res = Registry.ConvertFileAsync(item.SourceFilePath, item.TargetFormat, item.DestinationFilePath, null, default, isBatch: true).GetAwaiter().GetResult();
                    if (!res.Success)
                    {
                        Console.Error.WriteLine(res.ErrorMessage ?? I18n.T("ErrorDefault"));
                        failureCount++;
                    }
                }
                return failureCount > 0 ? 1 : 0;
            }

            return RunWindow(() => new ConversionProgressWindow(plannedItems));
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

        var mutexName = @"Local\JustConvert_QueueMutex_" + Environment.UserName;
        Mutex? mutex = null;
        bool isPrimary = false;
        try
        {
            mutex = new Mutex(true, mutexName, out isPrimary);
        }
        catch (AbandonedMutexException)
        {
            isPrimary = true;
        }
        catch { }

        if (!isPrimary)
        {
            try
            {
                ConversionQueueIpc.TrySend(inputFiles, targetFormat, outputPath, timeoutMs: 5000);
            }
            catch { }
            return 0;
        }

        ConversionProgressWindow? window = null;
        ConversionOptionsDialog? activeDialog = null;
        var pendingMessages = new System.Collections.Concurrent.ConcurrentQueue<QueueIpcMessage>();
        var batchFiles = new List<string>(inputFiles);
        var batchLock = new object();

        using var ipcServer = ConversionQueueIpc.StartServer(msg =>
        {
            if (activeDialog != null)
            {
                try
                {
                    activeDialog.Dispatcher.Invoke(() => activeDialog.AddBatchFiles(msg.Files));
                }
                catch { }

                lock (batchLock)
                {
                    foreach (var file in msg.Files)
                    {
                        if (!batchFiles.Contains(file, StringComparer.OrdinalIgnoreCase))
                        {
                            batchFiles.Add(file);
                        }
                    }
                }
            }
            else if (window != null)
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

        var sw = Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < 80)
        {
            Thread.Sleep(15);
            while (pendingMessages.TryDequeue(out var pending))
            {
                lock (batchLock)
                {
                    foreach (var file in pending.Files)
                    {
                        if (!batchFiles.Contains(file, StringComparer.OrdinalIgnoreCase))
                        {
                            batchFiles.Add(file);
                        }
                    }
                }
            }
        }

        long totalBatchSize = 0;
        lock (batchLock)
        {
            foreach (var file in batchFiles)
            {
                try
                {
                    if (File.Exists(file)) totalBatchSize += new FileInfo(file).Length;
                }
                catch { }
            }
        }

        string effectiveTargetFormat = targetFormat;
        if (!isSilent)
        {
            int currentBatchCount;
            string? firstFile;
            lock (batchLock)
            {
                currentBatchCount = batchFiles.Count;
                firstFile = batchFiles.FirstOrDefault();
            }

            var promptResult = PromptOptionsIfNeeded(
                targetFormat,
                firstFile,
                currentBatchCount,
                totalBatchSize,
                out var chosenFormat,
                dlg => activeDialog = dlg);

            activeDialog = null;

            if (!promptResult)
            {
                Application.Current?.Shutdown();
                try
                {
                    mutex?.ReleaseMutex();
                    mutex?.Dispose();
                }
                catch { }
                return 0;
            }
            if (!string.IsNullOrEmpty(chosenFormat))
            {
                effectiveTargetFormat = chosenFormat;
            }
        }

        var exitCode = RunWindow(() =>
        {
            List<string> runFiles;
            lock (batchLock)
            {
                while (pendingMessages.TryDequeue(out var pending))
                {
                    foreach (var file in pending.Files)
                    {
                        if (!batchFiles.Contains(file, StringComparer.OrdinalIgnoreCase))
                        {
                            batchFiles.Add(file);
                        }
                    }
                }
                runFiles = [.. batchFiles];
            }

            window = new ConversionProgressWindow(runFiles, effectiveTargetFormat, outputPath, isBatch || runFiles.Count > 1);
            return window;
        });

        try
        {
            mutex?.ReleaseMutex();
            mutex?.Dispose();
        }
        catch { }

        return exitCode;
    }

    private static bool PromptOptionsIfNeeded(string targetFormat, string? firstInputFilePath)
    {
        return PromptOptionsIfNeeded(targetFormat, firstInputFilePath, 1, null, out _, null);
    }

    private static bool PromptOptionsIfNeeded(
        string targetFormat,
        string? firstInputFilePath,
        int batchCount,
        long? totalBatchSizeBytes,
        out string? chosenTargetFormat,
        Action<ConversionOptionsDialog>? onDialogCreated = null)
    {
        chosenTargetFormat = null;
        var fmt = targetFormat.TrimStart('.').ToLowerInvariant();
        if (fmt.StartsWith("preset:")) return true;
        if (fmt is "reencode" or "frames" or "frames-png" or "frames-jpg" or "remux-mp4" or "remux-mkv" or "gif") return true;

        var settings = AppSettings.Load();
        var ffmpeg = ToolLocator.FindFfmpegPath();

        var app = Application.Current;
        if (app == null)
        {
            app = new Application();
            app.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            app.Resources.MergedDictionaries.Add(new Wpf.Ui.Markup.ThemesDictionary { Theme = Wpf.Ui.Appearance.ApplicationTheme.Light });
            app.Resources.MergedDictionaries.Add(new Wpf.Ui.Markup.ControlsDictionary());
        }

        void StartBackgroundProbe(ConversionOptionsDialog dialog)
        {
            if (!string.IsNullOrEmpty(firstInputFilePath) && File.Exists(firstInputFilePath) && ffmpeg != null)
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        var info = await MediaProbe.ProbeAsync(ffmpeg, firstInputFilePath);
                        if (info != null)
                        {
                            dialog.Dispatcher.Invoke(() => dialog.UpdateMediaInfo(info));
                        }
                    }
                    catch { }
                });
            }
        }

        if (fmt is "mp4" or "webm" or "mkv" or "mov")
        {
            if (settings.TryGetSavedVideoQuality(fmt, out _) && batchCount <= 1) return true;

            var dialog = new ConversionOptionsDialog(
                fmt,
                "video",
                settings.GetEffectiveVideoQuality(fmt),
                null,
                settings.AppendQualitySuffix,
                batchCount,
                totalBatchSizeBytes
            );
            onDialogCreated?.Invoke(dialog);
            StartBackgroundProbe(dialog);

            var res = dialog.ShowDialog();
            if (res != true) return false;

            chosenTargetFormat = dialog.SelectedTargetFormat;
            var effectiveFmt = chosenTargetFormat ?? fmt;

            var selected = dialog.SelectedVideoQuality;
            selected.IsRemembered = dialog.RememberChoice;
            settings.SetVideoQuality(effectiveFmt, selected);
            settings.AppendQualitySuffix = dialog.AppendQualitySuffix;
            settings.Save();
            return true;
        }

        if (fmt == "remux")
        {
            if (settings.RemuxSetting.IsRemembered && batchCount <= 1) return true;

            var dialog = new ConversionOptionsDialog(
                "remux",
                "remux",
                settings.GetEffectiveRemuxSetting(),
                null,
                settings.AppendQualitySuffix,
                batchCount,
                totalBatchSizeBytes
            );
            onDialogCreated?.Invoke(dialog);
            StartBackgroundProbe(dialog);

            var res = dialog.ShowDialog();
            if (res != true) return false;

            chosenTargetFormat = dialog.SelectedRemuxSetting.TargetContainer;
            settings.SetRemuxSetting(dialog.SelectedRemuxSetting);
            settings.AppendQualitySuffix = dialog.AppendQualitySuffix;
            settings.Save();
            return true;
        }

        if (fmt is "mp3" or "aac" or "m4a" or "ogg" or "opus")
        {
            if (settings.TryGetSavedAudioQuality(fmt, out _) && batchCount <= 1) return true;

            var dialog = new ConversionOptionsDialog(
                fmt,
                "audio",
                settings.GetEffectiveAudioQuality(fmt),
                null,
                settings.AppendQualitySuffix,
                batchCount,
                totalBatchSizeBytes
            );
            onDialogCreated?.Invoke(dialog);
            StartBackgroundProbe(dialog);

            var res = dialog.ShowDialog();
            if (res != true) return false;

            settings.SetAudioQuality(fmt, dialog.SelectedAudioBitrate, dialog.RememberChoice);
            settings.AppendQualitySuffix = dialog.AppendQualitySuffix;
            settings.Save();
            return true;
        }

        if (AppSettings.SupportsQuality(fmt))
        {
            if (settings.TryGetSavedQuality(fmt, out _) && batchCount <= 1) return true;

            var dialog = new ConversionOptionsDialog(
                fmt,
                "image",
                settings.GetEffectiveQuality(fmt),
                null,
                settings.AppendQualitySuffix,
                batchCount,
                totalBatchSizeBytes
            );
            onDialogCreated?.Invoke(dialog);
            StartBackgroundProbe(dialog);

            var res = dialog.ShowDialog();
            if (res != true) return false;

            settings.SetQuality(fmt, dialog.SelectedImageQuality, dialog.RememberChoice);
            settings.AppendQualitySuffix = dialog.AppendQualitySuffix;
            settings.Save();
            return true;
        }

        return true;
    }

    private static int RunWindow(Func<Window> windowFactory)
    {
        TouchpadScrollHelper.Initialize();
        var app = Application.Current;
        if (app == null)
        {
            app = new Application();
            app.Resources.MergedDictionaries.Add(new Wpf.Ui.Markup.ThemesDictionary { Theme = Wpf.Ui.Appearance.ApplicationTheme.Light });
            app.Resources.MergedDictionaries.Add(new Wpf.Ui.Markup.ControlsDictionary());
        }

        app.ShutdownMode = ShutdownMode.OnMainWindowClose;
        var window = windowFactory();
        app.MainWindow = window;
        app.Run(window);
        return window is ConversionProgressWindow cpw ? cpw.ExitCode : 0;
    }
}

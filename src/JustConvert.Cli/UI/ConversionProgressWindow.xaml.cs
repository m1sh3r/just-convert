using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using JustConvert.Core;
using JustConvert.Core.Logging;
using JustConvert.Core.Scanning;
using JustConvert.Core.Windows;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace JustConvert.Cli.UI;

public partial class ConversionProgressWindow : FluentWindow
{
    private readonly ConverterRegistry _registry = new();
    private readonly bool _isBatch;
    private bool _isDirectError;
    private int _maxParallel = 1;
    private readonly object _queueLock = new();
    private CancellationTokenSource? _autoCloseCts;

    public ObservableCollection<ConversionQueueItem> Items { get; } = [];
    public int ExitCode { get; private set; }

    public ConversionProgressWindow()
    {
        InitializeComponent();
        if (DesignerProperties.GetIsInDesignMode(this))
        {
            var baseTitle = I18n.T("QueueTitle");
            Title = baseTitle;
            AppTitleBar.Title = baseTitle;
            QueueItemsList.ItemsSource = DesignData.SampleItems;
            TxtOverallCount.Text = "2 / 4";
            OverallProgressBar.Value = 50;
            TxtOverallStatus.Text = I18n.T("StatusConverting");
            BtnPauseAll.Content = I18n.T("BtnPauseAll");
            BtnCancelAll.Content = I18n.T("BtnCancelAll");
            InfoBarGpuWarning.Title = I18n.T("BannerGpuFallbackWarningTitle");
            InfoBarGpuWarning.Message = I18n.T("BannerGpuFallbackWarning");
        }
    }

    public ConversionProgressWindow(IReadOnlyList<string> initialFiles, string targetFormat, string? outputPath = null, bool isBatch = false)
    {
        InitializeComponent();
        _isBatch = isBatch;

        if (!DesignerProperties.GetIsInDesignMode(this))
        {
            ApplicationThemeManager.ApplySystemTheme();
            ApplicationAccentColorManager.ApplySystemAccent();
            ApplicationThemeManager.Apply(this);
            SystemThemeWatcher.Watch(this);
        }

        QueueItemsList.ItemsSource = Items;
        var baseTitle = I18n.T("QueueTitle");
        Title = baseTitle;
        AppTitleBar.Title = baseTitle;

        EnqueueFiles(initialFiles, targetFormat, outputPath);

        StartIconRotation();
        TxtParallelValue.Text = _maxParallel.ToString();
        BtnParallelDec.IsEnabled = _maxParallel > 1;
        BtnParallelInc.IsEnabled = _maxParallel < 16;
        Loaded += OnLoaded;
    }

    public ConversionProgressWindow(string inputPath, string targetFormat, string? outputPath = null, bool isBatch = false)
        : this([inputPath], targetFormat, outputPath, isBatch)
    {
    }

    public ConversionProgressWindow(IReadOnlyList<BatchConversionItem> batchItems)
    {
        InitializeComponent();
        _isBatch = true;

        if (!DesignerProperties.GetIsInDesignMode(this))
        {
            ApplicationThemeManager.ApplySystemTheme();
            ApplicationAccentColorManager.ApplySystemAccent();
            ApplicationThemeManager.Apply(this);
            SystemThemeWatcher.Watch(this);
        }

        QueueItemsList.ItemsSource = Items;
        var baseTitle = I18n.T("QueueTitle");
        Title = baseTitle;
        AppTitleBar.Title = baseTitle;

        EnqueueBatchItems(batchItems);

        StartIconRotation();
        TxtParallelValue.Text = _maxParallel.ToString();
        BtnParallelDec.IsEnabled = _maxParallel > 1;
        BtnParallelInc.IsEnabled = _maxParallel < 16;
        Loaded += OnLoaded;
    }

    public static ConversionProgressWindow CreateForError(string inputPath, string targetFormat, string errorMessage, string? errorLog)
    {
        var window = new ConversionProgressWindow([inputPath], targetFormat);
        window.ShowErrorDirectly(errorMessage, errorLog);
        return window;
    }

    public void EnqueueFiles(IReadOnlyList<string> files, string targetFormat, string? outputPath = null)
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.Invoke(() => EnqueueFiles(files, targetFormat, outputPath));
            return;
        }

        _autoCloseCts?.Cancel();
        _autoCloseCts = null;

        foreach (var file in files)
        {
            if (string.IsNullOrWhiteSpace(file)) continue;

            var item = new ConversionQueueItem(file, targetFormat, outputPath);
            item.OnItemStateChanged += () => Dispatcher.Invoke(UpdateOverallState);
            Items.Add(item);
        }

        UpdateOverallState();
        ProcessQueue();
    }

    public void EnqueueBatchItems(IReadOnlyList<BatchConversionItem> batchItems)
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.Invoke(() => EnqueueBatchItems(batchItems));
            return;
        }

        _autoCloseCts?.Cancel();
        _autoCloseCts = null;

        foreach (var bi in batchItems)
        {
            if (string.IsNullOrWhiteSpace(bi.SourceFilePath)) continue;

            var item = new ConversionQueueItem(bi.SourceFilePath, bi.TargetFormat, bi.DestinationFilePath);
            item.OnItemStateChanged += () => Dispatcher.Invoke(UpdateOverallState);
            Items.Add(item);
        }

        UpdateOverallState();
        ProcessQueue();
    }

    private void ShowErrorDirectly(string errorMessage, string? errorLog)
    {
        StopIconRotation();
        _isDirectError = true;
        ErrorDetailOverlay.Visibility = Visibility.Visible;
        InfoBarError.Message = errorMessage;
        TxtErrorLog.Text = !string.IsNullOrWhiteSpace(errorLog) ? errorLog : errorMessage;
        ExitCode = 1;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_isDirectError) return;
        ProcessQueue();
        UpdateOverallState();
    }

    private void ProcessQueue()
    {
        lock (_queueLock)
        {
            int runningCount = Items.Count(i => i.Status == QueueItemStatus.Converting);
            int availableSlots = _maxParallel - runningCount;

            while (availableSlots > 0)
            {
                var autoPaused = Items.FirstOrDefault(i => i.Status == QueueItemStatus.Paused && i.AutoPaused);
                if (autoPaused != null)
                {
                    autoPaused.Resume();
                    availableSlots--;
                    continue;
                }

                var next = Items.FirstOrDefault(i => i.Status == QueueItemStatus.Queued);
                if (next == null) break;

                StartItemConversion(next);
                availableSlots--;
            }
        }
    }

    private void StartItemConversion(ConversionQueueItem item)
    {
        item.Status = QueueItemStatus.Converting;
        item.StatusText = I18n.T("StatusPreparing");
        item.Cts = new CancellationTokenSource();

        var sourceExt = Path.GetExtension(item.InputPath).TrimStart('.').ToLowerInvariant();
        var targetExt = item.TargetFormat.TrimStart('.').ToLowerInvariant();

        if (targetExt != "reencode" && targetExt != "remux" && ClassicContextMenuManager.IsSameFormat(sourceExt, targetExt) && !targetExt.StartsWith("frames"))
        {
            item.Status = QueueItemStatus.Done;
            item.StatusText = I18n.T("StatusSkippedAlreadyTarget");
            item.ProgressPercentage = 100;
            item.IsIndeterminate = false;
            Dispatcher.InvokeAsync(() =>
            {
                ProcessQueue();
                UpdateOverallState();
            });
            return;
        }

        if (_registry.FindConverter(sourceExt, targetExt) == null)
        {
            item.Status = QueueItemStatus.Done;
            item.StatusText = I18n.T("StatusSkippedUnsupported");
            item.ProgressPercentage = 100;
            item.IsIndeterminate = false;
            Dispatcher.InvokeAsync(() =>
            {
                ProcessQueue();
                UpdateOverallState();
            });
            return;
        }

        var progress = new Progress<ConversionProgress>(p =>
        {
            Dispatcher.Invoke(() =>
            {
                if (item.Status != QueueItemStatus.Converting) return;

                item.StatusText = p.StatusMessage;
                if (p.Percentage >= 0 && p.Percentage <= 100)
                {
                    item.IsIndeterminate = false;
                    item.ProgressPercentage = p.Percentage;
                }
                else
                {
                    item.IsIndeterminate = true;
                }

                if (!string.IsNullOrEmpty(p.Detail))
                {
                    item.Detail = p.Percentage > 0 ? $"{p.Percentage:F0}% ({p.Detail})" : p.Detail;
                }
                else if (p.Percentage > 0)
                {
                    item.Detail = $"{p.Percentage:F0}%";
                }
                else
                {
                    item.Detail = null;
                }
            });
        });

        _ = Task.Run(async () =>
        {
            try
            {
                var result = await _registry.ConvertFileAsync(item.InputPath, item.TargetFormat, item.OutputPath, progress, item.Cts.Token, _isBatch, item);

                Dispatcher.Invoke(() =>
                {
                    if (result.Success)
                    {
                        item.CpuFallback = result.CpuFallback;
                        item.ProgressPercentage = 100;
                        item.IsIndeterminate = false;
                        item.Status = QueueItemStatus.Done;
                        item.StatusText = result.Skipped
                            ? (result.ErrorMessage ?? I18n.T("StatusSkipped"))
                            : (result.CpuFallback ? I18n.T("StatusDoneCpuFallback") : I18n.T("StatusDone"));
                        item.Detail = "100%";
                        if (result.CpuFallback)
                        {
                            InfoBarGpuWarning.IsOpen = true;
                        }
                    }
                    else if (item.Cts.IsCancellationRequested)
                    {
                        item.Status = QueueItemStatus.Cancelled;
                        item.StatusText = I18n.T("StatusItemCancelled");
                        item.Detail = null;
                    }
                    else
                    {
                        item.Status = QueueItemStatus.Error;
                        item.StatusText = result.ErrorMessage ?? I18n.T("ErrorDefault");
                        item.ErrorMessage = result.ErrorMessage ?? I18n.T("ErrorDefault");
                        item.ErrorLog = result.FullLog;
                        item.Detail = null;
                    }
                });
            }
            catch (OperationCanceledException)
            {
                Dispatcher.Invoke(() =>
                {
                    item.Status = QueueItemStatus.Cancelled;
                    item.StatusText = I18n.T("StatusItemCancelled");
                    item.Detail = null;
                });
            }
            catch (Exception ex)
            {
                AppLogger.Error($"[Queue] Unhandled exception converting \"{item.InputPath}\"", ex);
                Dispatcher.Invoke(() =>
                {
                    item.Status = QueueItemStatus.Error;
                    item.StatusText = ex.Message;
                    item.ErrorMessage = ex.Message;
                    item.ErrorLog = ex.ToString();
                    item.Detail = null;
                });
            }
            finally
            {
                Dispatcher.Invoke(() =>
                {
                    ProcessQueue();
                    UpdateOverallState();
                });
            }
        });
    }

    private void BtnParallelDec_Click(object sender, RoutedEventArgs e)
    {
        if (_maxParallel > 1)
        {
            UpdateMaxParallel(_maxParallel - 1);
        }
    }

    private void BtnParallelInc_Click(object sender, RoutedEventArgs e)
    {
        if (_maxParallel < 16)
        {
            UpdateMaxParallel(_maxParallel + 1);
        }
    }

    private void UpdateMaxParallel(int newVal)
    {
        var oldVal = _maxParallel;
        _maxParallel = newVal;
        TxtParallelValue.Text = _maxParallel.ToString();
        BtnParallelDec.IsEnabled = _maxParallel > 1;
        BtnParallelInc.IsEnabled = _maxParallel < 16;

        if (newVal > oldVal)
        {
            int extraSlots = newVal - oldVal;
            var autoPaused = Items.Where(i => i.Status == QueueItemStatus.Paused && i.AutoPaused).Take(extraSlots).ToList();
            foreach (var item in autoPaused)
            {
                item.Resume();
            }
            ProcessQueue();
        }
        else
        {
            var converting = Items.Where(i => i.Status == QueueItemStatus.Converting).Reverse().ToList();
            int excess = converting.Count - newVal;
            for (int i = 0; i < excess && i < converting.Count; i++)
            {
                converting[i].Pause(isAuto: true);
            }
        }

        UpdateOverallState();
    }

    private void UpdateOverallState()
    {
        int total = Items.Count;
        int completed = Items.Count(i => i.Status is QueueItemStatus.Done or QueueItemStatus.Error or QueueItemStatus.Cancelled);
        int converting = Items.Count(i => i.Status == QueueItemStatus.Converting);
        int paused = Items.Count(i => i.Status == QueueItemStatus.Paused);
        int errors = Items.Count(i => i.Status == QueueItemStatus.Error);

        var baseTitle = I18n.T("QueueTitle");
        Title = total > 0 && completed < total
            ? $"({completed}/{total}) {baseTitle}"
            : baseTitle;

        TxtOverallCount.Text = total > 0 ? $"{completed} / {total}" : string.Empty;
        OverallProgressBar.Value = total > 0 ? (completed * 100.0 / total) : 0;

        if (converting > 0)
        {
            TxtOverallStatus.Text = I18n.T("StatusConverting");
            StartIconRotation();
            BtnPauseAll.Visibility = Visibility.Visible;
            BtnPauseAll.IsEnabled = true;
            BtnPauseAll.Content = I18n.T("BtnPauseAll");
            BtnCancelAll.Content = I18n.T("BtnCancelAll");
            BtnCopyErrors.Visibility = Visibility.Collapsed;
        }
        else if (paused > 0 && completed < total)
        {
            TxtOverallStatus.Text = I18n.T("StatusPaused");
            StopIconRotation();
            BtnPauseAll.Visibility = Visibility.Visible;
            BtnPauseAll.IsEnabled = true;
            BtnPauseAll.Content = I18n.T("BtnResumeAll");
            BtnCancelAll.Content = I18n.T("BtnCancelAll");
            BtnCopyErrors.Visibility = Visibility.Collapsed;
        }
        else if (total > 0 && completed == total)
        {
            StopIconRotation();
            bool allSuccessful = errors == 0 && Items.All(i => i.Status == QueueItemStatus.Done);
            if (allSuccessful)
            {
                TxtOverallStatus.Text = I18n.T("StatusAllDone");
                BtnCancelAll.Content = I18n.T("BtnClose");
                BtnPauseAll.Visibility = Visibility.Collapsed;
                BtnCopyErrors.Visibility = Visibility.Collapsed;
                ExitCode = 0;

                _autoCloseCts?.Cancel();
                _autoCloseCts = new CancellationTokenSource();
                var token = _autoCloseCts.Token;

                _ = Task.Run(async () =>
                {
                    try
                    {
                        var delayMs = 600;
                        try
                        {
                            if (Process.GetProcessesByName("just-convert").Length > 1)
                            {
                                delayMs = 1500;
                            }
                        }
                        catch { }

                        await Task.Delay(delayMs, token);
                        if (!token.IsCancellationRequested)
                        {
                            Dispatcher.Invoke(Close);
                        }
                    }
                    catch (OperationCanceledException) { }
                });
            }
            else
            {
                _autoCloseCts?.Cancel();
                _autoCloseCts = null;
                TxtOverallStatus.Text = errors > 0
                    ? I18n.T("StatusCompletedWithErrors", completed - errors, errors)
                    : I18n.T("StatusAllDone");
                BtnCancelAll.Content = I18n.T("BtnClose");
                BtnPauseAll.Visibility = Visibility.Collapsed;
                BtnCopyErrors.Visibility = errors > 0 ? Visibility.Visible : Visibility.Collapsed;
            }
        }
        else
        {
            StopIconRotation();
            TxtOverallStatus.Text = I18n.T("StatusPreparing");
            BtnPauseAll.Visibility = Visibility.Visible;
            BtnPauseAll.IsEnabled = true;
            BtnPauseAll.Content = I18n.T("BtnPauseAll");
            BtnCancelAll.Content = I18n.T("BtnCancelAll");
            BtnCopyErrors.Visibility = Visibility.Collapsed;
        }
    }

    private void BtnPauseAll_Click(object sender, RoutedEventArgs e)
    {
        int converting = Items.Count(i => i.Status == QueueItemStatus.Converting);
        if (converting > 0)
        {
            foreach (var item in Items.Where(i => i.Status == QueueItemStatus.Converting))
            {
                item.Pause(isAuto: false);
            }
        }
        else
        {
            var paused = Items.Where(i => i.Status == QueueItemStatus.Paused).ToList();
            foreach (var item in paused)
            {
                item.Resume();
            }
            ProcessQueue();
        }
        UpdateOverallState();
    }

    private void BtnCancelAll_Click(object sender, RoutedEventArgs e)
    {
        int completed = Items.Count(i => i.Status is QueueItemStatus.Done or QueueItemStatus.Error or QueueItemStatus.Cancelled);
        if (Items.Count > 0 && completed == Items.Count)
        {
            Close();
            return;
        }

        foreach (var item in Items.Where(i => i.Status is QueueItemStatus.Converting or QueueItemStatus.Paused or QueueItemStatus.Queued))
        {
            item.Cancel();
        }
        UpdateOverallState();
    }

    private void BtnItemPauseResume_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: ConversionQueueItem item })
        {
            if (item.Status == QueueItemStatus.Converting)
            {
                item.Pause(isAuto: false);
                ProcessQueue();
            }
            else if (item.Status == QueueItemStatus.Paused)
            {
                if (item.RunningProcess != null && !item.RunningProcess.HasExited)
                {
                    item.Resume();
                }
                else
                {
                    StartItemConversion(item);
                }
            }
            else if (item.Status == QueueItemStatus.Queued)
            {
                StartItemConversion(item);
            }
            UpdateOverallState();
        }
    }

    private void BtnItemCancel_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: ConversionQueueItem item })
        {
            item.Cancel();
            ProcessQueue();
            UpdateOverallState();
        }
    }

    private void BtnItemError_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: ConversionQueueItem item })
        {
            InfoBarError.Message = item.ErrorMessage ?? I18n.T("ErrorDefault");
            TxtErrorLog.Text = !string.IsNullOrWhiteSpace(item.ErrorLog) ? item.ErrorLog : item.ErrorMessage;
            ErrorDetailOverlay.Visibility = Visibility.Visible;
        }
    }

    private async void BtnItemCopyError_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: ConversionQueueItem item })
        {
            try
            {
                var log = !string.IsNullOrWhiteSpace(item.ErrorLog) ? item.ErrorLog : item.ErrorMessage ?? I18n.T("ErrorDefault");
                Clipboard.SetText(log);

                if (sender is FrameworkElement elem)
                {
                    elem.ToolTip = I18n.T("BtnCopied");
                    await Task.Delay(2000);
                    elem.ToolTip = I18n.T("TooltipCopyError");
                }
            }
            catch { }
        }
    }

    private async void BtnCopyErrors_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var errorItems = Items.Where(i => i.Status == QueueItemStatus.Error).ToList();
            if (errorItems.Count == 0) return;

            var sb = new StringBuilder();
            for (int i = 0; i < errorItems.Count; i++)
            {
                var item = errorItems[i];
                if (errorItems.Count > 1)
                {
                    sb.AppendLine($"=== [{i + 1}/{errorItems.Count}] {item.FileName} ({item.TargetFormatDisplay}) ===");
                }
                var log = !string.IsNullOrWhiteSpace(item.ErrorLog) ? item.ErrorLog : item.ErrorMessage;
                sb.AppendLine(log);
                if (i < errorItems.Count - 1)
                {
                    sb.AppendLine();
                }
            }

            Clipboard.SetText(sb.ToString().TrimEnd());
            TxtBtnCopyErrors.Text = I18n.T("BtnCopied");
            await Task.Delay(2000);
            TxtBtnCopyErrors.Text = I18n.T("BtnCopyAllErrors");
        }
        catch { }
    }

    private async void BtnCopy_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Clipboard.SetText(TxtErrorLog.Text);
            TxtBtnCopy.Text = I18n.T("BtnCopied");
            await Task.Delay(2000);
            TxtBtnCopy.Text = I18n.T("BtnCopyError");
        }
        catch { }
    }

    private void BtnCloseError_Click(object sender, RoutedEventArgs e)
    {
        if (_isDirectError)
        {
            Close();
        }
        else
        {
            ErrorDetailOverlay.Visibility = Visibility.Collapsed;
        }
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        _autoCloseCts?.Cancel();
        _autoCloseCts?.Dispose();
        _autoCloseCts = null;

        foreach (var item in Items)
        {
            if (item.Status is QueueItemStatus.Converting or QueueItemStatus.Paused or QueueItemStatus.Queued)
            {
                item.Cancel();
            }
        }
        base.OnClosing(e);
    }

    private void StartIconRotation()
    {
        var animation = new DoubleAnimation
        {
            From = 0,
            To = 360,
            Duration = TimeSpan.FromSeconds(2),
            RepeatBehavior = RepeatBehavior.Forever
        };
        IconRotateTransform.BeginAnimation(RotateTransform.AngleProperty, animation);
    }

    private void StopIconRotation()
    {
        IconRotateTransform.BeginAnimation(RotateTransform.AngleProperty, null);
    }
}

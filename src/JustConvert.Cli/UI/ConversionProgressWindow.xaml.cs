using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using JustConvert.Core;
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

    public ConversionProgressWindow(IReadOnlyList<string> initialFiles, string targetFormat, string? outputPath = null, bool isBatch = false)
    {
        InitializeComponent();
        _isBatch = isBatch;

        ApplicationThemeManager.ApplySystemTheme();
        ApplicationAccentColorManager.ApplySystemAccent();
        ApplicationThemeManager.Apply(this);
        SystemThemeWatcher.Watch(this);

        QueueItemsList.ItemsSource = Items;
        AppTitleBar.Title = I18n.T("QueueTitle");

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

        if (targetExt != "reencode" && sourceExt.Equals(targetExt, StringComparison.OrdinalIgnoreCase) && targetExt is not "frames" and not "frames-png" and not "frames-jpg")
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
                        item.ProgressPercentage = 100;
                        item.IsIndeterminate = false;
                        item.Status = QueueItemStatus.Done;
                        item.StatusText = result.Skipped ? (result.ErrorMessage ?? I18n.T("StatusSkipped")) : I18n.T("StatusDone");
                        item.Detail = "100%";
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

        TxtOverallCount.Text = total > 0 ? $"{completed} / {total}" : string.Empty;
        OverallProgressBar.Value = total > 0 ? (completed * 100.0 / total) : 0;

        if (converting > 0)
        {
            TxtOverallStatus.Text = I18n.T("StatusConverting");
            StartIconRotation();
            BtnPauseAll.Content = I18n.T("BtnPauseAll");
            BtnCancelAll.Content = I18n.T("BtnCancelAll");
        }
        else if (paused > 0 && completed < total)
        {
            TxtOverallStatus.Text = I18n.T("StatusPaused");
            StopIconRotation();
            BtnPauseAll.Content = I18n.T("BtnResumeAll");
            BtnCancelAll.Content = I18n.T("BtnCancelAll");
        }
        else if (total > 0 && completed == total)
        {
            StopIconRotation();
            bool allSuccessful = errors == 0 && Items.All(i => i.Status == QueueItemStatus.Done);
            if (allSuccessful)
            {
                TxtOverallStatus.Text = I18n.T("StatusAllDone");
                BtnCancelAll.Content = I18n.T("BtnClose");
                BtnPauseAll.IsEnabled = false;
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
                BtnPauseAll.IsEnabled = false;
            }
        }
        else
        {
            StopIconRotation();
            TxtOverallStatus.Text = I18n.T("StatusPreparing");
            BtnPauseAll.Content = I18n.T("BtnPauseAll");
            BtnCancelAll.Content = I18n.T("BtnCancelAll");
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

    private void BtnCopy_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Clipboard.SetText(TxtErrorLog.Text);
            BtnCopy.Content = I18n.T("BtnCopied");
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

    private void OnScrollViewerPreviewMouseWheel(object sender, System.Windows.Input.MouseWheelEventArgs e)
    {
        if (sender is not ScrollViewer sv || sv.ScrollableHeight <= 0) return;

        var offset = sv.VerticalOffset - (e.Delta * 0.25);
        sv.ScrollToVerticalOffset(Math.Clamp(offset, 0, sv.ScrollableHeight));
        e.Handled = true;
    }
}
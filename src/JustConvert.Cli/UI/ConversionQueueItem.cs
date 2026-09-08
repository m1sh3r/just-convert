using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media;
using JustConvert.Core;
using JustConvert.Core.Windows;
using Wpf.Ui.Controls;

namespace JustConvert.Cli.UI;

public enum QueueItemStatus
{
    Queued,
    Converting,
    Paused,
    Done,
    Error,
    Cancelled
}

public class ConversionQueueItem : INotifyPropertyChanged, IConversionController
{
    private QueueItemStatus _status = QueueItemStatus.Queued;
    private string _statusText = string.Empty;
    private string? _detail;
    private double _progressPercentage;
    private bool _isIndeterminate = true;
    private bool _autoPaused;
    private Process? _runningProcess;

    public string Id { get; } = Guid.NewGuid().ToString("N");
    public string InputPath { get; }
    public string FileName { get; }
    public string TargetFormat { get; }
    public string TargetFormatDisplay { get; }
    public string? OutputPath { get; set; }
    public CancellationTokenSource? Cts { get; set; }

    public string? ErrorMessage { get; set; }
    public string? ErrorLog { get; set; }

    public event Action? OnItemStateChanged;

    public ConversionQueueItem(string inputPath, string targetFormat, string? outputPath = null)
    {
        InputPath = inputPath;
        TargetFormat = targetFormat;
        OutputPath = outputPath;
        FileName = Path.GetFileName(inputPath);
        if (string.IsNullOrEmpty(FileName)) FileName = inputPath;
        TargetFormatDisplay = $"→ {I18n.GetSubMenuTitle(targetFormat)}";
        _statusText = I18n.T("StatusQueued");
    }

    public Process? RunningProcess => _runningProcess;

    public void OnProcessStarted(Process process)
    {
        _runningProcess = process;
        if (_status == QueueItemStatus.Paused)
        {
            ProcessSuspender.Suspend(process);
        }
    }

    public QueueItemStatus Status
    {
        get => _status;
        set
        {
            if (_status != value)
            {
                _status = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(StatusSymbol));
                OnPropertyChanged(nameof(StatusBrush));
                OnPropertyChanged(nameof(StatusTextBrush));
                OnPropertyChanged(nameof(ProgressBarVisibility));
                OnPropertyChanged(nameof(PauseResumeSymbol));
                OnPropertyChanged(nameof(PauseResumeTooltip));
                OnPropertyChanged(nameof(PauseResumeVisibility));
                OnPropertyChanged(nameof(CancelVisibility));
                OnPropertyChanged(nameof(ErrorButtonVisibility));
                OnPropertyChanged(nameof(DisplayStatus));
                OnItemStateChanged?.Invoke();
            }
        }
    }

    public bool AutoPaused
    {
        get => _autoPaused;
        set
        {
            _autoPaused = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(DisplayStatus));
        }
    }

    public string StatusText
    {
        get => _statusText;
        set
        {
            if (_statusText != value)
            {
                _statusText = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(DisplayStatus));
            }
        }
    }

    public string DisplayStatus
    {
        get
        {
            if (_status == QueueItemStatus.Paused)
            {
                return _autoPaused ? I18n.T("StatusAutoPaused") : I18n.T("StatusPaused");
            }
            return _statusText;
        }
    }

    public string? Detail
    {
        get => _detail;
        set
        {
            if (_detail != value)
            {
                _detail = value;
                OnPropertyChanged();
            }
        }
    }

    public double ProgressPercentage
    {
        get => _progressPercentage;
        set
        {
            if (Math.Abs(_progressPercentage - value) > 0.01)
            {
                _progressPercentage = value;
                OnPropertyChanged();
            }
        }
    }

    public bool IsIndeterminate
    {
        get => _isIndeterminate;
        set
        {
            if (_isIndeterminate != value)
            {
                _isIndeterminate = value;
                OnPropertyChanged();
            }
        }
    }

    public SymbolRegular StatusSymbol => _status switch
    {
        QueueItemStatus.Queued => SymbolRegular.Clock16,
        QueueItemStatus.Converting => SymbolRegular.ArrowSync16,
        QueueItemStatus.Paused => SymbolRegular.Pause16,
        QueueItemStatus.Done => SymbolRegular.Checkmark16,
        QueueItemStatus.Error => SymbolRegular.ErrorCircle16,
        QueueItemStatus.Cancelled => SymbolRegular.Dismiss16,
        _ => SymbolRegular.Document16
    };

    public Brush StatusBrush => _status switch
    {
        QueueItemStatus.Converting => (Brush)Application.Current.FindResource("AccentTextFillColorPrimaryBrush"),
        QueueItemStatus.Done => (Brush)Application.Current.FindResource("AccentTextFillColorPrimaryBrush"),
        QueueItemStatus.Error => (Brush)Application.Current.FindResource("SystemFillColorCriticalBrush"),
        QueueItemStatus.Paused => (Brush)Application.Current.FindResource("TextFillColorSecondaryBrush"),
        _ => (Brush)Application.Current.FindResource("TextFillColorTertiaryBrush")
    };

    public Brush StatusTextBrush => _status switch
    {
        QueueItemStatus.Error => (Brush)Application.Current.FindResource("SystemFillColorCriticalBrush"),
        QueueItemStatus.Converting => (Brush)Application.Current.FindResource("TextFillColorPrimaryBrush"),
        _ => (Brush)Application.Current.FindResource("TextFillColorSecondaryBrush")
    };

    public Visibility ProgressBarVisibility => _status switch
    {
        QueueItemStatus.Converting or QueueItemStatus.Paused or QueueItemStatus.Queued => Visibility.Visible,
        _ => Visibility.Collapsed
    };

    public SymbolRegular PauseResumeSymbol => _status == QueueItemStatus.Converting ? SymbolRegular.Pause16 : SymbolRegular.Play16;

    public string PauseResumeTooltip => _status == QueueItemStatus.Converting ? I18n.T("TooltipPause") : I18n.T("TooltipResume");

    public Visibility PauseResumeVisibility => _status switch
    {
        QueueItemStatus.Converting or QueueItemStatus.Paused or QueueItemStatus.Queued => Visibility.Visible,
        _ => Visibility.Collapsed
    };

    public Visibility CancelVisibility => _status switch
    {
        QueueItemStatus.Done or QueueItemStatus.Error or QueueItemStatus.Cancelled => Visibility.Collapsed,
        _ => Visibility.Visible
    };

    public Visibility ErrorButtonVisibility => _status == QueueItemStatus.Error ? Visibility.Visible : Visibility.Collapsed;

    public void Pause(bool isAuto = false)
    {
        if (_status is QueueItemStatus.Done or QueueItemStatus.Error or QueueItemStatus.Cancelled) return;

        AutoPaused = isAuto;
        Status = QueueItemStatus.Paused;

        if (_runningProcess != null)
        {
            ProcessSuspender.Suspend(_runningProcess);
        }
    }

    public void Resume()
    {
        if (_status != QueueItemStatus.Paused) return;

        AutoPaused = false;
        if (_runningProcess != null && !_runningProcess.HasExited)
        {
            ProcessSuspender.Resume(_runningProcess);
            Status = QueueItemStatus.Converting;
            StatusText = I18n.T("StatusConverting");
        }
        else
        {
            Status = QueueItemStatus.Queued;
            StatusText = I18n.T("StatusQueued");
        }
    }

    public void Cancel()
    {
        if (_status is QueueItemStatus.Done or QueueItemStatus.Error or QueueItemStatus.Cancelled) return;

        Status = QueueItemStatus.Cancelled;
        StatusText = I18n.T("StatusItemCancelled");
        ProgressPercentage = 0;
        IsIndeterminate = false;

        if (_runningProcess != null)
        {
            ProcessSuspender.Resume(_runningProcess);
        }
        try
        {
            Cts?.Cancel();
        }
        catch { }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
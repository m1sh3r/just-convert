using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using JustConvert.Core;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace JustConvert.Cli.UI;

public partial class QueueProcessingWindow : FluentWindow
{
    private readonly CancellationTokenSource _cts = new();
    private readonly List<string> _incomingFiles = [];
    private readonly object _lock = new();

    public CancellationToken CancellationToken => _cts.Token;
    public bool IsCancelled => _cts.IsCancellationRequested;

    public QueueProcessingWindow()
    {
        InitializeComponent();

        if (DesignerProperties.GetIsInDesignMode(this))
        {
            var baseTitle = I18n.T("QueueProcessingTitle");
            Title = baseTitle;
            AppTitleBar.Title = baseTitle;
            TxtStatus.Text = I18n.T("QueueProcessingPreparing");
            TxtCount.Text = I18n.T("QueueProcessingCollecting");
            BtnCancel.Content = I18n.T("BtnCancel");
            return;
        }

        FluentThemeService.Watch(this);

        Title = I18n.T("QueueProcessingTitle");
        AppTitleBar.Title = I18n.T("QueueProcessingTitle");
        StartIconRotation();
    }

    public void UpdateProgress(int current, int total, string? status = null)
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.BeginInvoke(() => UpdateProgress(current, total, status));
            return;
        }

        if (total > 0)
        {
            ProgressBar.IsIndeterminate = false;
            ProgressBar.Minimum = 0;
            ProgressBar.Maximum = total;
            ProgressBar.Value = Math.Clamp(current, 0, total);
            TxtCount.Text = string.Format(I18n.T("QueueProcessingFilesProgress"), current, total);
        }
        else
        {
            ProgressBar.IsIndeterminate = true;
            TxtCount.Text = I18n.T("QueueProcessingCollecting");
        }

        if (!string.IsNullOrEmpty(status))
        {
            TxtStatus.Text = status;
        }
    }

    public void SetIndeterminate(string? status = null)
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.BeginInvoke(() => SetIndeterminate(status));
            return;
        }

        ProgressBar.IsIndeterminate = true;
        TxtCount.Text = I18n.T("QueueProcessingCollecting");
        if (!string.IsNullOrEmpty(status))
        {
            TxtStatus.Text = status;
        }
    }

    public void AddBatchFiles(IEnumerable<string> files)
    {
        lock (_lock)
        {
            foreach (var f in files)
            {
                if (!string.IsNullOrWhiteSpace(f) && !_incomingFiles.Contains(f, StringComparer.OrdinalIgnoreCase))
                {
                    _incomingFiles.Add(f);
                }
            }
        }
    }

    public IReadOnlyList<string> DrainIncomingFiles()
    {
        lock (_lock)
        {
            var result = _incomingFiles.ToList();
            _incomingFiles.Clear();
            return result;
        }
    }

    public void Cancel()
    {
        _cts.Cancel();
        try
        {
            DialogResult = false;
        }
        catch (InvalidOperationException)
        {
            Close();
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (!e.Handled && e.Key == Key.Escape)
        {
            Cancel();
            e.Handled = true;
        }
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        Cancel();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!_cts.IsCancellationRequested)
        {
            _cts.Cancel();
        }
        StopIconRotation();
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

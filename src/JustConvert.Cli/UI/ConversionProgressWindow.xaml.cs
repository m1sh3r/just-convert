using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using JustConvert.Core;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace JustConvert.Cli.UI;

public partial class ConversionProgressWindow : FluentWindow
{
    private readonly string _inputPath;
    private readonly string _targetFormat;
    private readonly string? _outputPath;
    private readonly ConverterRegistry _registry = new();
    private readonly CancellationTokenSource _cts = new();

    private bool _isRunning;
    private bool _isDirectError;

    public int ExitCode { get; private set; }

    public ConversionProgressWindow(string inputPath, string targetFormat, string? outputPath = null)
    {
        InitializeComponent();
        _inputPath = inputPath;
        _targetFormat = targetFormat;
        _outputPath = outputPath;

        ApplicationThemeManager.ApplySystemTheme();
        ApplicationAccentColorManager.ApplySystemAccent();
        ApplicationThemeManager.Apply(this);
        SystemThemeWatcher.Watch(this);

        var fileName = Path.GetFileName(inputPath);
        TxtFileName.Text = !string.IsNullOrEmpty(fileName) ? fileName : inputPath;
        TxtTargetFormat.Text = $"→ {I18n.GetSubMenuTitle(targetFormat)}";
        AppTitleBar.Title = string.Empty;

        StartIconRotation();
        Loaded += OnLoaded;
    }

    public static ConversionProgressWindow CreateForError(string inputPath, string targetFormat, string errorMessage, string? errorLog)
    {
        var window = new ConversionProgressWindow(inputPath, targetFormat);
        window.ShowErrorDirectly(errorMessage, errorLog);
        return window;
    }

    private void ShowErrorDirectly(string errorMessage, string? errorLog)
    {
        StopIconRotation();
        _isDirectError = true;
        _isRunning = false;
        MinHeight = 360;
        MaxHeight = 360;
        Height = 360;
        ProgressPanel.Visibility = Visibility.Collapsed;
        ErrorPanel.Visibility = Visibility.Visible;

        AppTitleBar.Title = string.Empty;
        InfoBarError.Message = errorMessage;
        TxtErrorLog.Text = !string.IsNullOrWhiteSpace(errorLog) ? errorLog : errorMessage;
        ExitCode = 1;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_isDirectError) return;

        _isRunning = true;
        TxtStatus.Text = I18n.T("StatusPreparing");

        var progress = new Progress<ConversionProgress>(p =>
        {
            Dispatcher.Invoke(() =>
            {
                TxtStatus.Text = p.StatusMessage;

                if (p.Percentage >= 0 && p.Percentage <= 100)
                {
                    ProgressBar.IsIndeterminate = false;
                    ProgressBar.Value = p.Percentage;
                }
                else
                {
                    ProgressBar.IsIndeterminate = true;
                }

                if (!string.IsNullOrEmpty(p.Detail))
                {
                    TxtProgressDetails.Text = p.Percentage > 0
                        ? $"{p.Percentage:F0}% ({p.Detail})"
                        : p.Detail;
                }
                else if (p.Percentage > 0)
                {
                    TxtProgressDetails.Text = $"{p.Percentage:F0}%";
                }
                else
                {
                    TxtProgressDetails.Text = string.Empty;
                }
            });
        });

        try
        {
            var result = await Task.Run(async () =>
            {
                return await _registry.ConvertFileAsync(_inputPath, _targetFormat, _outputPath, progress, _cts.Token);
            });

            _isRunning = false;

            if (result.Success)
            {
                StopIconRotation();
                ProgressBar.IsIndeterminate = false;
                ProgressBar.Value = 100;
                TxtStatus.Text = I18n.T("StatusDone");
                TxtProgressDetails.Text = "100%";
                BtnCancel.IsEnabled = false;
                ExitCode = 0;
                await Task.Delay(500);
                Close();
            }
            else if (_cts.IsCancellationRequested)
            {
                StopIconRotation();
                ProgressBar.IsIndeterminate = false;
                ProgressBar.Value = 0;
                TxtStatus.Text = I18n.T("StatusCancelled");
                TxtProgressDetails.Text = string.Empty;
                BtnCancel.IsEnabled = false;
                ExitCode = 2;
                await Task.Delay(500);
                Close();
            }
            else
            {
                ShowErrorDirectly(result.ErrorMessage ?? I18n.T("ErrorDefault"), result.FullLog);
            }
        }
        catch (OperationCanceledException)
        {
            StopIconRotation();
            _isRunning = false;
            ProgressBar.IsIndeterminate = false;
            ProgressBar.Value = 0;
            TxtStatus.Text = I18n.T("StatusCancelled");
            TxtProgressDetails.Text = string.Empty;
            BtnCancel.IsEnabled = false;
            ExitCode = 2;
            await Task.Delay(500);
            Close();
        }
        catch (Exception ex)
        {
            _isRunning = false;
            ShowErrorDirectly(ex.Message, ex.ToString());
        }
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        if (_isRunning && !_cts.IsCancellationRequested)
        {
            _cts.Cancel();
            BtnCancel.IsEnabled = false;
            TxtStatus.Text = I18n.T("StatusCancelling");
            ProgressBar.IsIndeterminate = true;
        }
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (_isRunning && !_cts.IsCancellationRequested)
        {
            _cts.Cancel();
        }
        base.OnClosing(e);
    }

    private void BtnCopy_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var textToCopy = $"File: {_inputPath}\nTarget: {_targetFormat}\nError: {InfoBarError.Message}\n\nLog:\n{TxtErrorLog.Text}";
            Clipboard.SetText(textToCopy);
            BtnCopy.Content = I18n.T("BtnCopied");
        }
        catch { }
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e)
    {
        Close();
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

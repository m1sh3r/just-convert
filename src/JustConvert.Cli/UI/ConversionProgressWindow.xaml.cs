using System.IO;
using System.Windows;
using JustConvert.Core;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace JustConvert.Cli.UI;

public partial class ErrorWindow : FluentWindow
{
    private readonly string _inputPath;
    private readonly string _targetFormat;
    private readonly string _errorMessage;
    private readonly string? _errorLog;

    public ErrorWindow(string inputPath, string targetFormat, string errorMessage, string? errorLog)
    {
        InitializeComponent();
        _inputPath = inputPath;
        _targetFormat = targetFormat;
        _errorMessage = errorMessage;
        _errorLog = errorLog;

        ApplicationThemeManager.ApplySystemTheme();
        ApplicationThemeManager.Apply(this);
        SystemThemeWatcher.Watch(this);

        var fileName = Path.GetFileName(inputPath);
        var subTitle = !string.IsNullOrEmpty(fileName) ? $"{fileName} -> {targetFormat.ToUpperInvariant()}" : targetFormat.ToUpperInvariant();
        AppTitleBar.Title = $"{I18n.T("TitleError")} - {subTitle}";

        InfoBarError.Message = errorMessage;
        TxtErrorLog.Text = !string.IsNullOrWhiteSpace(errorLog) ? errorLog : errorMessage;
    }

    private void BtnCopy_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var textToCopy = $"File: {_inputPath}\nTarget: {_targetFormat}\nError: {_errorMessage}\n\nLog:\n{_errorLog ?? _errorMessage}";
            Clipboard.SetText(textToCopy);
            BtnCopy.Content = I18n.T("BtnCopied");
        }
        catch { }
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}

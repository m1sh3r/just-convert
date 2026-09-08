using System.Windows;
using JustConvert.Core;
using JustConvert.Core.Converters;
using JustConvert.Core.Windows;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace JustConvert.Installer.UI;

public partial class InstallerWindow : FluentWindow
{
    private readonly bool _startWithUninstall;
    private bool _isWorking;
    private CancellationTokenSource? _cts;

    public InstallerWindow(InstallScope initialScope = InstallScope.CurrentUser, bool startWithUninstall = false)
    {
        InitializeComponent();
        _startWithUninstall = startWithUninstall;

        var title = string.IsNullOrWhiteSpace(Program.AppVersion)
            ? I18n.T("SetupTitle")
            : I18n.T("SetupTitleWithVersion", Program.AppVersion.TrimStart('v'));
        Title = title;
        AppTitleBar.Title = title;

        ApplicationThemeManager.ApplySystemTheme();
        ApplicationAccentColorManager.ApplySystemAccent();
        ApplicationThemeManager.Apply(this);
        SystemThemeWatcher.Watch(this);

        if (initialScope == InstallScope.AllUsers)
        {
            RadioAllUsers.IsChecked = true;
        }
        else
        {
            RadioCurrentUser.IsChecked = true;
        }

        if (_startWithUninstall)
        {
            PerformUninstall();
        }
    }

    private async void BtnInstall_Click(object sender, RoutedEventArgs e)
    {
        var scope = RadioAllUsers.IsChecked == true ? InstallScope.AllUsers : InstallScope.CurrentUser;
        var installDir = Program.GetInstallDirectory(scope);

        if (scope == InstallScope.AllUsers && !Program.IsAdministrator())
        {
            Program.ElevateProcess(["/allusers"]);
            Close();
            return;
        }

        _cts = new CancellationTokenSource();
        _isWorking = true;

        ConfigPanel.Visibility = Visibility.Collapsed;
        ProgressPanel.Visibility = Visibility.Visible;
        BtnInstall.Visibility = Visibility.Collapsed;
        BtnUninstall.Visibility = Visibility.Collapsed;
        BtnClose.Content = I18n.T("BtnCancel");

        var progress = new Progress<(double? Percent, string Status)>(update =>
        {
            Dispatcher.Invoke(() =>
            {
                TxtStatus.Text = update.Status;
                if (update.Percent.HasValue && update.Percent.Value >= 0)
                {
                    ProgressBar.IsIndeterminate = false;
                    ProgressBar.Value = update.Percent.Value;
                }
                else
                {
                    ProgressBar.IsIndeterminate = true;
                }
            });
        });

        try
        {
            await Task.Run(async () =>
            {
                await Program.InstallCoreAsync(installDir, scope, true, progress, _cts.Token);
            }, _cts.Token);

            _isWorking = false;
            ProgressPanel.Visibility = Visibility.Collapsed;
            SuccessPanel.Visibility = Visibility.Visible;
            InfoBarSuccess.Title = I18n.T("SetupSuccessHeader");
            TxtSuccessText.Text = I18n.T("SetupSuccessText");
            BtnClose.Content = I18n.T("BtnClose");
        }
        catch (OperationCanceledException)
        {
            _isWorking = false;
            try
            {
                Program.UninstallCore(installDir, scope);
            }
            catch { }
            Close();
        }
        catch (Exception ex)
        {
            _isWorking = false;
            ProgressPanel.Visibility = Visibility.Collapsed;
            ErrorPanel.Visibility = Visibility.Visible;
            InfoBarError.Title = I18n.T("SetupErrorHeader");
            TxtErrorLog.Text = ex.Message + "\n" + ex.StackTrace;
            BtnClose.Content = I18n.T("BtnClose");
        }
    }

    private void BtnUninstall_Click(object sender, RoutedEventArgs e)
    {
        PerformUninstall();
    }

    private async void PerformUninstall()
    {
        var scope = RadioAllUsers.IsChecked == true ? InstallScope.AllUsers : InstallScope.CurrentUser;
        var installDir = Program.GetInstallDirectory(scope);

        if (scope == InstallScope.AllUsers && !Program.IsAdministrator())
        {
            Program.ElevateProcess(["/uninstall", "/allusers"]);
            Close();
            return;
        }

        ConfigPanel.Visibility = Visibility.Collapsed;
        ProgressPanel.Visibility = Visibility.Visible;
        BtnInstall.Visibility = Visibility.Collapsed;
        BtnUninstall.Visibility = Visibility.Collapsed;

        var progress = new Progress<(double? Percent, string Status)>(update =>
        {
            Dispatcher.Invoke(() =>
            {
                TxtStatus.Text = update.Status;
                ProgressBar.IsIndeterminate = true;
            });
        });

        try
        {
            await Task.Run(() =>
            {
                Program.UninstallCore(installDir, scope, progress);
            });

            ProgressPanel.Visibility = Visibility.Collapsed;
            SuccessPanel.Visibility = Visibility.Visible;
            InfoBarSuccess.Title = I18n.T("SetupUninstallSuccessHeader");
            TxtSuccessText.Text = I18n.T("SetupUninstallSuccessText");
            BtnClose.Content = I18n.T("BtnClose");
        }
        catch (Exception ex)
        {
            ProgressPanel.Visibility = Visibility.Collapsed;
            ErrorPanel.Visibility = Visibility.Visible;
            InfoBarError.Title = I18n.T("SetupErrorHeader");
            TxtErrorLog.Text = ex.Message + "\n" + ex.StackTrace;
            BtnClose.Content = I18n.T("BtnClose");
        }
    }

    private async void BtnClose_Click(object sender, RoutedEventArgs e)
    {
        if (_isWorking)
        {
            if (await ConfirmCancelAsync())
            {
                _cts?.Cancel();
            }
            return;
        }

        Close();
    }

    protected override async void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        if (_isWorking)
        {
            e.Cancel = true;
            if (await ConfirmCancelAsync())
            {
                _cts?.Cancel();
            }
            return;
        }

        base.OnClosing(e);
    }

    private async Task<bool> ConfirmCancelAsync()
    {
        var msgBox = new Wpf.Ui.Controls.MessageBox
        {
            Title = I18n.T("SetupCancelConfirmTitle"),
            Content = I18n.T("SetupCancelConfirmText"),
            PrimaryButtonText = I18n.T("BtnYes"),
            CloseButtonText = I18n.T("BtnNo"),
            Owner = this
        };

        var result = await msgBox.ShowDialogAsync();
        return result == Wpf.Ui.Controls.MessageBoxResult.Primary;
    }
}

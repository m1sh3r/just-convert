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

    public InstallerWindow(InstallScope initialScope = InstallScope.CurrentUser, bool startWithUninstall = false)
    {
        InitializeComponent();
        _startWithUninstall = startWithUninstall;

        ApplicationThemeManager.ApplySystemTheme();
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
        var downloadFfmpeg = MediaConverter.FindFfmpegPath() == null;

        if (scope == InstallScope.AllUsers && !Program.IsAdministrator())
        {
            Program.ElevateProcess(["/allusers"]);
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
                await Program.InstallCoreAsync(installDir, scope, downloadFfmpeg, progress);
            });

            ProgressPanel.Visibility = Visibility.Collapsed;
            SuccessPanel.Visibility = Visibility.Visible;
            InfoBarSuccess.Title = I18n.T("SetupSuccessHeader");
            TxtSuccessText.Text = I18n.T("SetupSuccessText");
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

    private void BtnClose_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
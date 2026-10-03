using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using JustConvert.Core;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace JustConvert.Cli.UI;

public enum MessageDialogType
{
    Information,
    Question,
    Warning,
    Error
}

public enum MessageDialogButtons
{
    Ok,
    YesNo
}

public partial class MessageDialog : FluentWindow
{
    public MessageDialog() : this(string.Empty, I18n.T("SettingsTitle"), MessageDialogType.Information, MessageDialogButtons.Ok)
    {
    }

    public MessageDialog(string message, string title, MessageDialogType type, MessageDialogButtons buttons)
    {
        InitializeComponent();

        if (!DesignerProperties.GetIsInDesignMode(this))
        {
            FluentThemeService.Watch(this);
        }

        Title = title;
        AppTitleBar.Title = title;
        TxtMessage.Text = message;

        ConfigureType(type);
        ConfigureButtons(buttons);

        if (DesignerProperties.GetIsInDesignMode(this))
        {
            TxtMessage.Text = "Текст сообщения диалогового окна";
            BtnPrimary.Content = I18n.T("BtnYes");
            BtnSecondary.Content = I18n.T("BtnNo");
        }
    }

    private void ConfigureType(MessageDialogType type)
    {
        IconMessage.Symbol = type switch
        {
            MessageDialogType.Information => SymbolRegular.Info24,
            MessageDialogType.Question => SymbolRegular.QuestionCircle24,
            MessageDialogType.Warning => SymbolRegular.Warning24,
            MessageDialogType.Error => SymbolRegular.DismissCircle24,
            _ => SymbolRegular.Info24
        };

        IconMessage.Foreground = ApplicationAccentColorManager.PrimaryAccentBrush ?? TryFindResource("AccentTextFillColorPrimaryBrush") as Brush ?? Brushes.DodgerBlue;
    }

    private void ConfigureButtons(MessageDialogButtons buttons)
    {
        if (buttons == MessageDialogButtons.Ok)
        {
            BtnPrimary.Content = I18n.T("BtnOk");
            BtnPrimary.IsDefault = true;
            BtnPrimary.IsCancel = true;
            BtnSecondary.Visibility = Visibility.Collapsed;
        }
        else
        {
            BtnPrimary.Content = I18n.T("BtnYes");
            BtnPrimary.IsDefault = true;
            BtnPrimary.IsCancel = false;
            BtnSecondary.Content = I18n.T("BtnNo");
            BtnSecondary.IsCancel = true;
            BtnSecondary.Visibility = Visibility.Visible;
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (!e.Handled && e.Key == Key.Escape)
        {
            if (BtnSecondary.Visibility == Visibility.Visible)
            {
                OnSecondaryClick(this, new RoutedEventArgs());
            }
            else
            {
                OnPrimaryClick(this, new RoutedEventArgs());
            }
            e.Handled = true;
        }
    }

    public static bool ShowConfirm(Window? owner, string message, string title)
    {
        var dlg = new MessageDialog(message, title, MessageDialogType.Question, MessageDialogButtons.YesNo);
        if (owner != null && owner.IsLoaded)
        {
            dlg.Owner = owner;
        }
        return dlg.ShowDialog() == true;
    }

    public static void ShowInfo(Window? owner, string message, string title)
    {
        var dlg = new MessageDialog(message, title, MessageDialogType.Information, MessageDialogButtons.Ok);
        if (owner != null && owner.IsLoaded)
        {
            dlg.Owner = owner;
        }
        dlg.ShowDialog();
    }

    public static void ShowWarning(Window? owner, string message, string title)
    {
        var dlg = new MessageDialog(message, title, MessageDialogType.Warning, MessageDialogButtons.Ok);
        if (owner != null && owner.IsLoaded)
        {
            dlg.Owner = owner;
        }
        dlg.ShowDialog();
    }

    public static void ShowError(Window? owner, string message, string title)
    {
        var dlg = new MessageDialog(message, title, MessageDialogType.Error, MessageDialogButtons.Ok);
        if (owner != null && owner.IsLoaded)
        {
            dlg.Owner = owner;
        }
        dlg.ShowDialog();
    }

    private void OnPrimaryClick(object sender, RoutedEventArgs e)
    {
        try { DialogResult = true; } catch (InvalidOperationException) { }
        Close();
    }

    private void OnSecondaryClick(object sender, RoutedEventArgs e)
    {
        try { DialogResult = false; } catch (InvalidOperationException) { }
        Close();
    }
}

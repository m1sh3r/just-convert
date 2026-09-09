using System.Windows;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace JustConvert.Cli.UI;

public partial class InputDialog : FluentWindow
{
    public string InputText => TxtInput.Text.Trim();

    public InputDialog(string prompt, string title, string defaultValue = "")
    {
        InitializeComponent();

        ApplicationThemeManager.ApplySystemTheme();
        ApplicationAccentColorManager.ApplySystemAccent();
        ApplicationThemeManager.Apply(this);
        SystemThemeWatcher.Watch(this);

        AppTitleBar.Title = title;
        TxtPrompt.Text = prompt;
        TxtInput.Text = defaultValue;
        TxtInput.SelectAll();
        Loaded += (_, _) => TxtInput.Focus();
    }

    public static string? Show(Window owner, string prompt, string title, string defaultValue = "")
    {
        var dlg = new InputDialog(prompt, title, defaultValue)
        {
            Owner = owner
        };

        return dlg.ShowDialog() == true ? dlg.InputText : null;
    }

    private void OnOkClick(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }

    private void OnCancelClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}

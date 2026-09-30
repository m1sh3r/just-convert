using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using JustConvert.Cli.UI;
using Xunit;

namespace JustConvert.Tests;

public class UiInputDialogTests
{
    [Fact]
    public void InputDialog_Construct_InitializesPromptAndDefaultValue()
    {
        StaTestRunner.Run(() =>
        {
            var dialog = new InputDialog("Enter profile name:", "Rename Profile", "Standard");

            Assert.Equal("Enter profile name:", dialog.TxtPrompt.Text);
            Assert.Equal("Standard", dialog.TxtInput.Text);
            Assert.Equal("Standard", dialog.InputText);
            Assert.Equal("Rename Profile", dialog.Title);
            Assert.Equal("Rename Profile", dialog.AppTitleBar.Title);
        });
    }

    [Fact]
    public void InputDialog_InputText_TrimsWhitespace()
    {
        StaTestRunner.Run(() =>
        {
            var dialog = new InputDialog("Prompt", "Title");
            dialog.TxtInput.Text = "   Custom Profile Name   ";

            Assert.Equal("Custom Profile Name", dialog.InputText);
        });
    }

    [Fact]
    public void InputDialog_ClickOkAndCancel_ClosesWindow()
    {
        StaTestRunner.Run(() =>
        {
            var dialog = new InputDialog("Prompt", "Title", "Initial");
            dialog.BtnOk.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));

            var cancelDialog = new InputDialog("Prompt", "Title", "Initial");
            cancelDialog.BtnCancel.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
        });
    }

    [Fact]
    public void InputDialog_BtnCancel_HasIsCancelSet()
    {
        StaTestRunner.Run(() =>
        {
            var dialog = new InputDialog("Prompt", "Title");
            Assert.True(dialog.BtnCancel.IsCancel);
        });
    }

    [Fact]
    public void InputDialog_PressEscape_HandlesEvent()
    {
        StaTestRunner.Run(() =>
        {
            var dialog = new InputDialog("Prompt", "Title");
            var keyArgs = new KeyEventArgs(
                Keyboard.PrimaryDevice,
                new System.Windows.Interop.HwndSource(0, 0, 0, 0, 0, "", IntPtr.Zero),
                0,
                Key.Escape)
            {
                RoutedEvent = Keyboard.KeyDownEvent
            };
            dialog.RaiseEvent(keyArgs);
            Assert.True(keyArgs.Handled);
        });
    }
}

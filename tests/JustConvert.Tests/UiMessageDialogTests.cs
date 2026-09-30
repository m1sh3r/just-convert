using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using JustConvert.Cli.UI;
using Wpf.Ui.Controls;
using Xunit;

namespace JustConvert.Tests;

public class UiMessageDialogTests
{
    [Fact]
    public void MessageDialog_Construct_WithOkButtons_ConfiguresSingleButton()
    {
        StaTestRunner.Run(() =>
        {
            var dialog = new MessageDialog("Operation completed successfully", "Info", MessageDialogType.Information, MessageDialogButtons.Ok);

            Assert.Equal("Operation completed successfully", dialog.TxtMessage.Text);
            Assert.Equal("Info", dialog.Title);
            Assert.Equal("Info", dialog.AppTitleBar.Title);
            Assert.Equal(SymbolRegular.Info24, dialog.IconMessage.Symbol);
            var expectedColor = (Wpf.Ui.Appearance.ApplicationAccentColorManager.PrimaryAccentBrush as System.Windows.Media.SolidColorBrush)?.Color;
            var actualColor = (dialog.IconMessage.Foreground as System.Windows.Media.SolidColorBrush)?.Color;
            Assert.Equal(expectedColor, actualColor);
            Assert.Equal(Visibility.Visible, dialog.BtnPrimary.Visibility);
            Assert.Equal(Visibility.Collapsed, dialog.BtnSecondary.Visibility);
        });
    }


    [Fact]
    public void MessageDialog_Construct_WithYesNoButtons_ConfiguresTwoButtons()
    {
        StaTestRunner.Run(() =>
        {
            var dialog = new MessageDialog("Are you sure?", "Confirm", MessageDialogType.Question, MessageDialogButtons.YesNo);

            Assert.Equal("Are you sure?", dialog.TxtMessage.Text);
            Assert.Equal("Confirm", dialog.Title);
            Assert.Equal(SymbolRegular.QuestionCircle24, dialog.IconMessage.Symbol);
            Assert.Equal(Visibility.Visible, dialog.BtnPrimary.Visibility);
            Assert.Equal(Visibility.Visible, dialog.BtnSecondary.Visibility);
        });
    }

    [Theory]
    [InlineData(MessageDialogType.Information, SymbolRegular.Info24)]
    [InlineData(MessageDialogType.Question, SymbolRegular.QuestionCircle24)]
    [InlineData(MessageDialogType.Warning, SymbolRegular.Warning24)]
    [InlineData(MessageDialogType.Error, SymbolRegular.DismissCircle24)]
    public void MessageDialog_Construct_SetsExpectedIcons(MessageDialogType type, SymbolRegular expectedSymbol)
    {
        StaTestRunner.Run(() =>
        {
            var dialog = new MessageDialog("Test message", "Test Title", type, MessageDialogButtons.Ok);
            Assert.Equal(expectedSymbol, dialog.IconMessage.Symbol);
        });
    }

    [Fact]
    public void MessageDialog_ClickPrimaryAndSecondary_ClosesDialog()
    {
        StaTestRunner.Run(() =>
        {
            var dialogYes = new MessageDialog("Message", "Title", MessageDialogType.Question, MessageDialogButtons.YesNo);
            dialogYes.BtnPrimary.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));

            var dialogNo = new MessageDialog("Message", "Title", MessageDialogType.Question, MessageDialogButtons.YesNo);
            dialogNo.BtnSecondary.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
        });
    }

    [Fact]
    public void MessageDialog_Buttons_HaveExpectedIsCancel()
    {
        StaTestRunner.Run(() =>
        {
            var dialogOk = new MessageDialog("Info", "Title", MessageDialogType.Information, MessageDialogButtons.Ok);
            Assert.True(dialogOk.BtnPrimary.IsCancel);

            var dialogYesNo = new MessageDialog("Confirm", "Title", MessageDialogType.Question, MessageDialogButtons.YesNo);
            Assert.False(dialogYesNo.BtnPrimary.IsCancel);
            Assert.True(dialogYesNo.BtnSecondary.IsCancel);
        });
    }

    [Fact]
    public void MessageDialog_PressEscape_HandlesEvent()
    {
        StaTestRunner.Run(() =>
        {
            var dialogOk = new MessageDialog("Info", "Title", MessageDialogType.Information, MessageDialogButtons.Ok);
            var keyArgsOk = new KeyEventArgs(
                Keyboard.PrimaryDevice,
                new System.Windows.Interop.HwndSource(0, 0, 0, 0, 0, "", IntPtr.Zero),
                0,
                Key.Escape)
            {
                RoutedEvent = Keyboard.KeyDownEvent
            };
            dialogOk.RaiseEvent(keyArgsOk);
            Assert.True(keyArgsOk.Handled);

            var dialogYesNo = new MessageDialog("Confirm", "Title", MessageDialogType.Question, MessageDialogButtons.YesNo);
            var keyArgsYesNo = new KeyEventArgs(
                Keyboard.PrimaryDevice,
                new System.Windows.Interop.HwndSource(0, 0, 0, 0, 0, "", IntPtr.Zero),
                0,
                Key.Escape)
            {
                RoutedEvent = Keyboard.KeyDownEvent
            };
            dialogYesNo.RaiseEvent(keyArgsYesNo);
            Assert.True(keyArgsYesNo.Handled);
        });
    }
}

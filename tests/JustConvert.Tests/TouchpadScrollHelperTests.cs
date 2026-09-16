using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using JustConvert.Cli.UI;

namespace JustConvert.Tests;

public class TouchpadScrollHelperTests
{
    [Fact]
    public void Initialize_CanBeCalledMultipleTimesSafely()
    {
        TouchpadScrollHelper.Initialize();
        TouchpadScrollHelper.Initialize();
    }

    [Fact]
    public void HandlePreviewMouseWheel_WhenNoScrollableHeight_DoesNotHandle()
    {
        StaTestRunner.Run(() =>
        {
            var sv = new ScrollViewer();
            var mouseDevice = Mouse.PrimaryDevice;
            var eventArgs = new MouseWheelEventArgs(mouseDevice, 0, 120)
            {
                RoutedEvent = UIElement.PreviewMouseWheelEvent
            };

            TouchpadScrollHelper.HandlePreviewMouseWheel(sv, eventArgs);
            Assert.False(eventArgs.Handled);
        });
    }
}

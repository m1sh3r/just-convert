using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace JustConvert.Cli.UI;

public static class TouchpadScrollHelper
{
    private static long _lastEventTime;
    private static bool _isInitialized;

    public static void Initialize()
    {
        if (_isInitialized) return;
        _isInitialized = true;

        EventManager.RegisterClassHandler(
            typeof(ScrollViewer),
            UIElement.PreviewMouseWheelEvent,
            new MouseWheelEventHandler(OnScrollViewerPreviewMouseWheel),
            handledEventsToo: false);
    }

    public static void HandlePreviewMouseWheel(ScrollViewer sv, MouseWheelEventArgs e)
    {
        if (sv.ScrollableHeight <= 0) return;

        var now = Environment.TickCount64;
        var elapsed = now - _lastEventTime;
        _lastEventTime = now;

        var isHighFrequency = elapsed < 45 || Math.Abs(e.Delta) % 120 != 0;
        var multiplier = isHighFrequency ? 0.06 : 0.25;

        var offset = sv.VerticalOffset - (e.Delta * multiplier);
        sv.ScrollToVerticalOffset(Math.Clamp(offset, 0, sv.ScrollableHeight));
        e.Handled = true;
    }

    private static void OnScrollViewerPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (sender is ScrollViewer sv)
        {
            HandlePreviewMouseWheel(sv, e);
        }
    }
}

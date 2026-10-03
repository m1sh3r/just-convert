using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using Microsoft.Win32;
using Wpf.Ui.Appearance;

namespace JustConvert.Cli.UI;

public static class FluentThemeService
{
    private static bool _eventsHooked;

    public static void Initialize()
    {
        if (_eventsHooked) return;
        _eventsHooked = true;

        SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
        Apply();
    }

    public static void Watch(Window window)
    {
        Initialize();

        try
        {
            ApplicationThemeManager.Apply(window);
        }
        catch { }

        if (window.IsLoaded)
        {
            HookWindow(window);
        }
        else
        {
            window.Loaded += (_, _) => HookWindow(window);
        }

        window.Closed += (_, _) =>
        {
            UnhookWindow(window);
        };
    }

    private static void HookWindow(Window window)
    {
        var helper = new WindowInteropHelper(window);
        if (helper.Handle == IntPtr.Zero) return;

        var source = HwndSource.FromHwnd(helper.Handle);
        source?.RemoveHook(WndProc);
        source?.AddHook(WndProc);
    }

    private static void UnhookWindow(Window window)
    {
        try
        {
            var helper = new WindowInteropHelper(window);
            if (helper.Handle != IntPtr.Zero)
            {
                var source = HwndSource.FromHwnd(helper.Handle);
                source?.RemoveHook(WndProc);
            }
        }
        catch { }
    }

    private static IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        const int WM_SETTINGCHANGE = 0x001A;
        const int WM_THEMECHANGED = 0x031A;
        const int WM_DWMCOLORIZATIONCOLORCHANGED = 0x0320;

        if (msg is WM_SETTINGCHANGE or WM_THEMECHANGED or WM_DWMCOLORIZATIONCOLORCHANGED)
        {
            Application.Current?.Dispatcher.BeginInvoke(new Action(Apply));
        }

        return IntPtr.Zero;
    }

    private static void OnUserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
    {
        if (e.Category is UserPreferenceCategory.Color or UserPreferenceCategory.General or UserPreferenceCategory.VisualStyle)
        {
            Application.Current?.Dispatcher.BeginInvoke(new Action(Apply));
        }
    }

    public static void Apply()
    {
        ApplicationThemeManager.ApplySystemTheme();
        var theme = ApplicationThemeManager.GetAppTheme();

        var accentColor = GetSystemAccentColor();
        ApplyFluentAccent(accentColor, theme);

        if (Application.Current != null)
        {
            foreach (Window window in Application.Current.Windows)
            {
                try
                {
                    ApplicationThemeManager.Apply(window);
                }
                catch { }
            }
        }
    }

    public static Color GetSystemAccentColor()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\DWM");
            if (key?.GetValue("AccentColor") is int abgr)
            {
                var a = (byte)((abgr >> 24) & 0xFF);
                var b = (byte)((abgr >> 16) & 0xFF);
                var g = (byte)((abgr >> 8) & 0xFF);
                var r = (byte)(abgr & 0xFF);
                return Color.FromArgb(a != 0 ? a : (byte)255, r, g, b);
            }

            if (key?.GetValue("ColorizationColor") is int argb)
            {
                var a = (byte)((argb >> 24) & 0xFF);
                var r = (byte)((argb >> 16) & 0xFF);
                var g = (byte)((argb >> 8) & 0xFF);
                var b = (byte)(argb & 0xFF);
                return Color.FromArgb(a != 0 ? a : (byte)255, r, g, b);
            }
        }
        catch { }

        return Color.FromRgb(0x00, 0x78, 0xD4);
    }

    public static void ApplyFluentAccent(Color baseAccent, ApplicationTheme theme)
    {
        RgbToHsl(baseAccent, out var h, out var s, out var l);

        bool isDark = theme == ApplicationTheme.Dark;

        var defaultColor = isDark
            ? HslToRgb(h, s, Math.Clamp(l, 0.45, 0.65))
            : HslToRgb(h, s, Math.Clamp(l, 0.35, 0.55));

        var secondaryColor = isDark
            ? HslToRgb(h, s, Math.Clamp(l + 0.10, 0.0, 1.0))
            : HslToRgb(h, s, Math.Clamp(l * 0.90, 0.0, 1.0));

        var tertiaryColor = isDark
            ? HslToRgb(h, s, Math.Clamp(l + 0.20, 0.0, 1.0))
            : HslToRgb(h, s, Math.Clamp(l * 0.80, 0.0, 1.0));

        var textPrimaryColor = isDark
            ? HslToRgb(h, s, Math.Clamp(Math.Max(l, 0.65), 0.0, 1.0))
            : HslToRgb(h, s, Math.Clamp(Math.Min(l, 0.40), 0.0, 1.0));

        var textSecondaryColor = isDark
            ? HslToRgb(h, s, Math.Clamp(Math.Max(l, 0.75), 0.0, 1.0))
            : HslToRgb(h, s, Math.Clamp(Math.Min(l, 0.30), 0.0, 1.0));

        try
        {
            ApplicationAccentColorManager.Apply(baseAccent, defaultColor, secondaryColor, tertiaryColor);
        }
        catch { }

        var app = Application.Current;
        if (app?.Resources == null) return;

        SetResource(app, "SystemAccentColor", baseAccent);
        SetResource(app, "SystemAccentColorPrimary", defaultColor);
        SetResource(app, "SystemAccentColorSecondary", secondaryColor);
        SetResource(app, "SystemAccentColorTertiary", tertiaryColor);

        SetBrushResource(app, "SystemAccentBrush", baseAccent);
        SetBrushResource(app, "SystemAccentColorPrimaryBrush", defaultColor);
        SetBrushResource(app, "SystemAccentColorSecondaryBrush", secondaryColor);
        SetBrushResource(app, "SystemAccentColorTertiaryBrush", tertiaryColor);

        SetBrushResource(app, "AccentFillColorDefaultBrush", defaultColor);
        SetBrushResource(app, "AccentFillColorSecondaryBrush", secondaryColor);
        SetBrushResource(app, "AccentFillColorTertiaryBrush", tertiaryColor);

        SetBrushResource(app, "AccentTextFillColorPrimaryBrush", textPrimaryColor);
        SetBrushResource(app, "AccentTextFillColorSecondaryBrush", textSecondaryColor);
        SetBrushResource(app, "AccentTextFillColorTertiaryBrush", secondaryColor);
    }

    private static void SetResource(Application app, string key, object value)
    {
        app.Resources[key] = value;
    }

    private static void SetBrushResource(Application app, string key, Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        app.Resources[key] = brush;
    }

    private static void RgbToHsl(Color rgb, out double h, out double s, out double l)
    {
        double r = rgb.R / 255.0;
        double g = rgb.G / 255.0;
        double b = rgb.B / 255.0;

        double max = Math.Max(r, Math.Max(g, b));
        double min = Math.Min(r, Math.Min(g, b));
        double delta = max - min;

        l = (max + min) / 2.0;

        if (delta == 0.0)
        {
            h = 0.0;
            s = 0.0;
        }
        else
        {
            s = l <= 0.5 ? delta / (max + min) : delta / (2.0 - max - min);

            if (r == max)
                h = (g - b) / delta + (g < b ? 6.0 : 0.0);
            else if (g == max)
                h = (b - r) / delta + 2.0;
            else
                h = (r - g) / delta + 4.0;

            h /= 6.0;
        }
    }

    private static Color HslToRgb(double h, double s, double l)
    {
        double r, g, b;

        if (s == 0.0)
        {
            r = g = b = l;
        }
        else
        {
            double q = l < 0.5 ? l * (1.0 + s) : l + s - l * s;
            double p = 2.0 * l - q;

            r = HueToRgb(p, q, h + 1.0 / 3.0);
            g = HueToRgb(p, q, h);
            b = HueToRgb(p, q, h - 1.0 / 3.0);
        }

        return Color.FromRgb(
            (byte)Math.Clamp(Math.Round(r * 255.0), 0, 255),
            (byte)Math.Clamp(Math.Round(g * 255.0), 0, 255),
            (byte)Math.Clamp(Math.Round(b * 255.0), 0, 255)
        );
    }

    private static double HueToRgb(double p, double q, double t)
    {
        if (t < 0.0) t += 1.0;
        if (t > 1.0) t -= 1.0;
        if (t < 1.0 / 6.0) return p + (q - p) * 6.0 * t;
        if (t < 1.0 / 2.0) return q;
        if (t < 2.0 / 3.0) return p + (q - p) * (2.0 / 3.0 - t) * 6.0;
        return p;
    }
}

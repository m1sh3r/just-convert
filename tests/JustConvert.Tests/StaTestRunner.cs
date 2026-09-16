using System.Windows;
using System.Windows.Threading;

namespace JustConvert.Tests;

public static class StaTestRunner
{
    private static readonly object SyncLock = new();
    private static readonly Dispatcher UiDispatcher;

    static StaTestRunner()
    {
        var readyTcs = new TaskCompletionSource();

        var thread = new Thread(() =>
        {
            if (Application.Current == null)
            {
                var app = new Application
                {
                    ShutdownMode = ShutdownMode.OnExplicitShutdown
                };
                app.Resources.MergedDictionaries.Add(new Wpf.Ui.Markup.ThemesDictionary { Theme = Wpf.Ui.Appearance.ApplicationTheme.Light });
                app.Resources.MergedDictionaries.Add(new Wpf.Ui.Markup.ControlsDictionary());
            }

            _ = Dispatcher.CurrentDispatcher;
            readyTcs.SetResult();
            Dispatcher.Run();
        })
        {
            IsBackground = true,
            Name = "JustConvert_UiTestThread"
        };

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        readyTcs.Task.GetAwaiter().GetResult();

        UiDispatcher = Dispatcher.FromThread(thread)!;
    }

    public static void Run(Action action)
    {
        lock (SyncLock)
        {
            Exception? error = null;

            UiDispatcher.Invoke(() =>
            {
                try
                {
                    action();
                }
                catch (Exception ex)
                {
                    error = ex;
                }
                finally
                {
                    if (Application.Current != null)
                    {
                        var openWindows = Application.Current.Windows.Cast<Window>().ToList();
                        foreach (var win in openWindows)
                        {
                            try
                            {
                                win.Close();
                            }
                            catch { }
                        }
                    }
                }
            });

            if (error != null)
            {
                System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(error).Throw();
            }
        }
    }
}

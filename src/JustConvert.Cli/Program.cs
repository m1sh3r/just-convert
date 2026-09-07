using System.IO;
using System.Windows;
using JustConvert.Cli.UI;
using JustConvert.Core;
using File = System.IO.File;

namespace JustConvert.Cli;

public class Program
{
    private static readonly ConverterRegistry Registry = new();

    [STAThread]
    public static int Main(string[] args)
    {
        if (args.Length == 0)
        {
            return 0;
        }

        string? inputPath = null;
        string? targetFormat = null;
        string? outputPath = null;
        bool isSilent = false;

        for (int i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            if (arg.Equals("convert", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if ((arg is "--to" or "-t" or "-f" or "/to" or "/t") && i + 1 < args.Length)
            {
                targetFormat = args[++i];
            }
            else if ((arg is "--out" or "-o" or "/out" or "/o") && i + 1 < args.Length)
            {
                outputPath = args[++i];
            }
            else if (arg is "--silent" or "-s" or "/silent" or "/s" or "--no-gui")
            {
                isSilent = true;
            }
            else if (!arg.StartsWith('-') && !arg.StartsWith('/'))
            {
                if (inputPath == null)
                {
                    inputPath = arg;
                }
                else if (targetFormat == null)
                {
                    targetFormat = arg;
                }
            }
        }

        if (string.IsNullOrWhiteSpace(inputPath) || string.IsNullOrWhiteSpace(targetFormat))
        {
            if (isSilent)
            {
                Console.Error.WriteLine(I18n.T("CliMissingArgs"));
                return 1;
            }
            return RunWindow(() => ConversionProgressWindow.CreateForError(inputPath ?? "", targetFormat ?? "", I18n.T("CliMissingArgs"), null));
        }

        if (!File.Exists(inputPath))
        {
            if (isSilent)
            {
                Console.Error.WriteLine(I18n.T("FileNotFound", inputPath));
                return 1;
            }
            return RunWindow(() => ConversionProgressWindow.CreateForError(inputPath, targetFormat, I18n.T("FileNotFound", inputPath), null));
        }

        if (isSilent)
        {
            var result = Registry.ConvertFileAsync(inputPath, targetFormat, outputPath).GetAwaiter().GetResult();
            if (!result.Success)
            {
                Console.Error.WriteLine(result.ErrorMessage ?? I18n.T("ErrorDefault"));
                return 1;
            }
            return 0;
        }

        return RunWindow(() => new ConversionProgressWindow(inputPath, targetFormat, outputPath));
    }

    private static int RunWindow(Func<ConversionProgressWindow> windowFactory)
    {
        var app = new Application();
        app.Resources.MergedDictionaries.Add(new Wpf.Ui.Markup.ThemesDictionary { Theme = Wpf.Ui.Appearance.ApplicationTheme.Light });
        app.Resources.MergedDictionaries.Add(new Wpf.Ui.Markup.ControlsDictionary());
        var window = windowFactory();
        app.Run(window);
        return window.ExitCode;
    }
}


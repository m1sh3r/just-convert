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
            ShowErrorWindow(inputPath ?? "", targetFormat ?? "", I18n.T("CliMissingArgs"), null);
            return 1;
        }

        if (!File.Exists(inputPath))
        {
            ShowErrorWindow(inputPath, targetFormat, I18n.T("FileNotFound", inputPath), null);
            return 1;
        }

        var result = Registry.ConvertFileAsync(inputPath, targetFormat, outputPath).GetAwaiter().GetResult();
        if (!result.Success)
        {
            ShowErrorWindow(inputPath, targetFormat, result.ErrorMessage ?? I18n.T("ErrorDefault"), result.FullLog);
            return 1;
        }

        return 0;
    }

    private static void ShowErrorWindow(string inputPath, string targetFormat, string errorMessage, string? errorLog)
    {
        var app = new Application();
        var window = new ErrorWindow(inputPath, targetFormat, errorMessage, errorLog);
        app.Run(window);
    }
}


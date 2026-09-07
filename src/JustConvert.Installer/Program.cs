using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Windows;
using JustConvert.Core;
using JustConvert.Core.Converters;
using JustConvert.Core.Windows;
using JustConvert.Installer.UI;
using Microsoft.Win32;
using Wpf.Ui.Appearance;
using Wpf.Ui.Markup;

namespace JustConvert.Installer;

public class Program
{
    public static readonly string AppVersion = FileVersionInfo.GetVersionInfo(Environment.ProcessPath ?? typeof(Program).Assembly.Location).ProductVersion?.Split('+')[0] ?? string.Empty;
    public const string AppPublisher = "m1sh3r";
    public const string AppName = "Just Convert";

    private static readonly ConverterRegistry ConverterReg = new();
    private static readonly ClassicContextMenuManager ClassicManager = new(ConverterReg);

    [STAThread]
    public static int Main(string[] args)
    {
        var isUninstall = args.Contains("/uninstall", StringComparer.OrdinalIgnoreCase) ||
                          args.Contains("-u", StringComparer.OrdinalIgnoreCase) ||
                          args.Contains("--uninstall", StringComparer.OrdinalIgnoreCase);

        var isSilent = args.Contains("/silent", StringComparer.OrdinalIgnoreCase) ||
                       args.Contains("/s", StringComparer.OrdinalIgnoreCase) ||
                       args.Contains("-s", StringComparer.OrdinalIgnoreCase) ||
                       args.Contains("--silent", StringComparer.OrdinalIgnoreCase);

        var isAllUsers = args.Contains("/allusers", StringComparer.OrdinalIgnoreCase) ||
                         args.Contains("/all", StringComparer.OrdinalIgnoreCase);

        var chosenScope = isAllUsers ? InstallScope.AllUsers : InstallScope.CurrentUser;

        if (isSilent)
        {
            AttachConsole(ATTACH_PARENT_PROCESS);

            try
            {
                if (chosenScope == InstallScope.AllUsers && !IsAdministrator())
                {
                    return ElevateProcess(args);
                }

                var installDir = GetInstallDirectory(chosenScope);
                if (isUninstall)
                {
                    Console.WriteLine("Uninstalling Just Convert...");
                    var uninstallProgress = new Progress<(double? Percent, string Status)>(report =>
                    {
                        Console.WriteLine(report.Status);
                    });
                    UninstallCore(installDir, chosenScope, uninstallProgress);
                    Console.WriteLine("Uninstall complete.");
                    return 0;
                }

                Console.WriteLine("Installing Just Convert...");
                var silentProgress = new Progress<(double? Percent, string Status)>(report =>
                {
                    if (report.Percent.HasValue)
                    {
                        Console.WriteLine($"{report.Status} ({report.Percent.Value:F0}%)");
                    }
                    else
                    {
                        Console.WriteLine(report.Status);
                    }
                });

                InstallCoreAsync(installDir, chosenScope, true, silentProgress).GetAwaiter().GetResult();
                Console.WriteLine("Installation complete.");
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Setup failed: {ex.Message}");
                return 1;
            }
        }

        var app = new Application();
        app.Resources.MergedDictionaries.Add(new ThemesDictionary { Theme = ApplicationTheme.Light });
        app.Resources.MergedDictionaries.Add(new ControlsDictionary());
        var window = new InstallerWindow(chosenScope, isUninstall);
        return app.Run(window);
    }

    public static string GetInstallDirectory(InstallScope scope)
    {
        return scope == InstallScope.AllUsers
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "m1sh3r", "Just Convert")
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "m1sh3r", "Just Convert");
    }

    public static async Task InstallCoreAsync(string installDir, InstallScope scope, bool downloadFfmpeg, IProgress<(double? Percent, string Status)>? progress = null, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        progress?.Report((null, I18n.T("SetupCopying")));

        KillRunningProcesses(installDir);

        if (!Directory.Exists(installDir))
        {
            Directory.CreateDirectory(installDir);
        }

        var payloadExtracted = TryExtractEmbeddedPayload(installDir);
        if (!payloadExtracted)
        {
            var sourceDir = AppContext.BaseDirectory;
            CopyDirectoryFiles(sourceDir, installDir);
        }

        var currentExe = Environment.ProcessPath;
        if (!string.IsNullOrEmpty(currentExe) && File.Exists(currentExe))
        {
            var destSetup = Path.Combine(installDir, "JustConvert-Setup.exe");
            try { File.Copy(currentExe, destSetup, true); } catch { }
        }

        var installedExe = Path.Combine(installDir, "just-convert.exe");

        if (!File.Exists(installedExe))
        {
            string[] fallbackDirs =
            [
                Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "JustConvert.Cli", "bin", "Release", "net10.0-windows"),
                Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "JustConvert.Cli", "bin", "Debug", "net10.0-windows"),
                Path.Combine(AppContext.BaseDirectory, "..", "JustConvert.Cli", "bin", "Release", "net10.0-windows"),
                Path.Combine(AppContext.BaseDirectory, "..", "JustConvert.Cli", "bin", "Debug", "net10.0-windows")
            ];

            foreach (var fbDir in fallbackDirs)
            {
                if (Directory.Exists(fbDir) && File.Exists(Path.Combine(fbDir, "just-convert.exe")))
                {
                    CopyDirectoryFiles(fbDir, installDir);
                    break;
                }
            }
        }

        if (!File.Exists(installedExe))
        {
            throw new FileNotFoundException("just-convert executable was not found in setup package.");
        }

        ct.ThrowIfCancellationRequested();

        if (downloadFfmpeg && MediaConverter.FindFfmpegPath() == null)
        {
            await FfmpegInstaller.DownloadToDirectoryAsync(installDir, progress, ct);
        }

        ct.ThrowIfCancellationRequested();

        progress?.Report((null, I18n.T("SetupRegistering")));
        ClassicManager.Register(installedExe, scope);
        RegisterModernMenu(installDir, scope);
        RegisterUninstallEntry(installDir, scope);
        NotifyShell();
    }

    public static void UninstallCore(string installDir, InstallScope scope, IProgress<(double? Percent, string Status)>? progress = null)
    {
        progress?.Report((null, I18n.T("SetupUninstalling")));

        KillRunningProcesses(installDir);
        ClassicManager.Unregister(scope);
        UnregisterModernMenu();
        RemoveUninstallEntry(scope);
        NotifyShell();

        if (Directory.Exists(installDir))
        {
            try
            {
                foreach (var file in Directory.GetFiles(installDir, "*.*", SearchOption.AllDirectories))
                {
                    try { File.Delete(file); } catch { }
                }

                Directory.Delete(installDir, true);
            }
            catch
            {
                try
                {
                    var currentPid = Environment.ProcessId;
                    var script = $"Wait-Process -Id {currentPid} -ErrorAction SilentlyContinue; Start-Sleep -Milliseconds 500; Remove-Item -LiteralPath '{installDir}' -Recurse -Force -ErrorAction SilentlyContinue";
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "powershell.exe",
                        Arguments = $"-NoProfile -ExecutionPolicy Bypass -Command \"{script}\"",
                        CreateNoWindow = true,
                        UseShellExecute = false
                    });
                }
                catch { }
            }
        }
    }

    public static bool IsAdministrator()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return false;
        using var identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }

    public static int ElevateProcess(string[] args)
    {
        var processPath = Environment.ProcessPath;
        if (string.IsNullOrEmpty(processPath)) return 1;

        var startInfo = new ProcessStartInfo
        {
            FileName = processPath,
            Arguments = string.Join(" ", args),
            UseShellExecute = true,
            Verb = "runas"
        };

        try
        {
            using var proc = Process.Start(startInfo);
            proc?.WaitForExit();
            return proc?.ExitCode ?? 0;
        }
        catch
        {
            return 1;
        }
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool AttachConsole(int dwProcessId);
    private const int ATTACH_PARENT_PROCESS = -1;

    [DllImport("shell32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern void SHChangeNotify(int wEventId, uint uFlags, IntPtr dwItem1, IntPtr dwItem2);

    public static void NotifyShell()
    {
        try
        {
            SHChangeNotify(0x08000000, 0x0000, IntPtr.Zero, IntPtr.Zero);
        }
        catch { }
    }

    private static void KillRunningProcesses(string? installDir = null)
    {
        try
        {
            foreach (var name in new[] { "just-convert", "ffmpeg" })
            {
                foreach (var proc in Process.GetProcessesByName(name))
                {
                    try
                    {
                        if (installDir == null || (proc.MainModule?.FileName?.StartsWith(installDir, StringComparison.OrdinalIgnoreCase) ?? true))
                        {
                            proc.Kill();
                            proc.WaitForExit(2000);
                        }
                    }
                    catch { }
                }
            }
        }
        catch { }
    }

    private static bool TryExtractEmbeddedPayload(string destinationDir)
    {
        try
        {
            using var stream = typeof(Program).Assembly.GetManifestResourceStream("Payload.zip");
            if (stream == null) return false;

            using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
            foreach (var entry in archive.Entries)
            {
                if (string.IsNullOrEmpty(entry.Name)) continue;
                var destPath = Path.Combine(destinationDir, entry.FullName);
                var destSubDir = Path.GetDirectoryName(destPath);
                if (!string.IsNullOrEmpty(destSubDir) && !Directory.Exists(destSubDir))
                {
                    Directory.CreateDirectory(destSubDir);
                }
                try
                {
                    entry.ExtractToFile(destPath, true);
                }
                catch { }
            }
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static void CopyDirectoryFiles(string source, string destination)
    {
        var filesToCopy = Directory.GetFiles(source, "*.*", SearchOption.TopDirectoryOnly);
        foreach (var file in filesToCopy)
        {
            var fileName = Path.GetFileName(file);
            var destFile = Path.Combine(destination, fileName);
            try
            {
                File.Copy(file, destFile, true);
            }
            catch { }
        }

        var manifestsDir = Path.Combine(source, "manifests");
        if (Directory.Exists(manifestsDir))
        {
            var destManifests = Path.Combine(destination, "manifests");
            Directory.CreateDirectory(destManifests);
            foreach (var f in Directory.GetFiles(manifestsDir))
            {
                try
                {
                    File.Copy(f, Path.Combine(destManifests, Path.GetFileName(f)), true);
                    File.Copy(f, Path.Combine(destination, Path.GetFileName(f)), true);
                }
                catch { }
            }
        }
    }

    private static void RegisterModernMenu(string installDir, InstallScope scope)
    {
        try
        {
            var manifest = Path.Combine(installDir, "AppxManifest.xml");
            if (File.Exists(manifest))
            {
                var script = $"Add-AppxPackage -Register \"{manifest}\" -AllowExternalContent -ErrorAction SilentlyContinue";
                var psi = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = $"-NoProfile -ExecutionPolicy Bypass -Command \"{script}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using var proc = Process.Start(psi);
                proc?.WaitForExit(5000);
            }
        }
        catch { }
    }

    private static void UnregisterModernMenu()
    {
        try
        {
            var script = "Remove-AppxPackage -Package (Get-AppxPackage -Name JustConvert).PackageFullName -ErrorAction SilentlyContinue";
            var psi = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -ExecutionPolicy Bypass -Command \"{script}\"",
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var proc = Process.Start(psi);
            proc?.WaitForExit(5000);
        }
        catch { }
    }

    private static void RegisterUninstallEntry(string installDir, InstallScope scope)
    {
        try
        {
            var setupExe = Path.Combine(installDir, "JustConvert-Setup.exe");
            var iconExe = Path.Combine(installDir, "just-convert.exe");
            var root = scope == InstallScope.AllUsers ? Registry.LocalMachine : Registry.CurrentUser;
            var uninstallKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\Just Convert";
            using var key = root.CreateSubKey(uninstallKeyPath, true);
            if (key == null) return;

            var uninstallCmd = $"\"{setupExe}\" /uninstall" + (scope == InstallScope.AllUsers ? " /allusers" : "");
            var quietUninstallCmd = uninstallCmd + " /silent";

            key.SetValue("DisplayName", AppName);
            key.SetValue("DisplayVersion", AppVersion);
            key.SetValue("Publisher", AppPublisher);
            key.SetValue("InstallLocation", installDir);
            key.SetValue("DisplayIcon", $"\"{iconExe}\",0");
            key.SetValue("UninstallString", uninstallCmd);
            key.SetValue("QuietUninstallString", quietUninstallCmd);
            key.SetValue("NoModify", 1, RegistryValueKind.DWord);
            key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
            key.SetValue("EstimatedSize", 25000, RegistryValueKind.DWord);
        }
        catch { }
    }

    private static void RemoveUninstallEntry(InstallScope scope)
    {
        try
        {
            var root = scope == InstallScope.AllUsers ? Registry.LocalMachine : Registry.CurrentUser;
            root.DeleteSubKeyTree(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\Just Convert", false);
        }
        catch { }
    }
}

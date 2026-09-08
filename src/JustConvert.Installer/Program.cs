using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Reflection;
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
    public static readonly string AppVersion = typeof(Program).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0] ?? string.Empty;
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

    public static async Task InstallCoreAsync(string installDir, InstallScope scope, bool ensureDependencies = true, IProgress<(double? Percent, string Status)>? progress = null, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        progress?.Report((null, I18n.T("SetupCopying")));

        KillRunningProcesses(installDir);
        CleanupOldFiles(installDir);

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
            SafeCopyFile(currentExe, destSetup);
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

        if (ensureDependencies)
        {
            var hasFfmpeg = File.Exists(Path.Combine(installDir, "ffmpeg.exe")) || MediaConverter.FindFfmpegPath() != null;
            if (!hasFfmpeg)
            {
                await FfmpegInstaller.DownloadToDirectoryAsync(installDir, progress, ct);
            }

            ct.ThrowIfCancellationRequested();

            var hasMagick = File.Exists(Path.Combine(installDir, "magick.exe")) || ImageConverter.FindMagickPath() != null;
            if (!hasMagick)
            {
                await ImageMagickInstaller.DownloadToDirectoryAsync(installDir, progress, ct);
            }
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
            foreach (var name in new[] { "just-convert", "ffmpeg", "magick" })
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

    private static void CleanupOldFiles(string installDir)
    {
        try
        {
            if (!Directory.Exists(installDir)) return;
            foreach (var old in Directory.GetFiles(installDir, "*.old", SearchOption.AllDirectories))
            {
                try { File.Delete(old); } catch { }
            }
        }
        catch { }
    }

    private static void SafeCopyFile(string source, string destination)
    {
        try
        {
            File.Copy(source, destination, true);
        }
        catch (IOException)
        {
            try
            {
                var oldFile = destination + "." + Guid.NewGuid().ToString("N")[..8] + ".old";
                if (File.Exists(destination))
                {
                    File.Move(destination, oldFile, true);
                }
                File.Copy(source, destination, true);
                try { File.Delete(oldFile); } catch { }
            }
            catch { }
        }
        catch { }
    }

    private static void SafeExtractEntry(ZipArchiveEntry entry, string destination)
    {
        try
        {
            entry.ExtractToFile(destination, true);
        }
        catch (IOException)
        {
            try
            {
                var oldFile = destination + "." + Guid.NewGuid().ToString("N")[..8] + ".old";
                if (File.Exists(destination))
                {
                    File.Move(destination, oldFile, true);
                }
                entry.ExtractToFile(destination, true);
                try { File.Delete(oldFile); } catch { }
            }
            catch { }
        }
        catch { }
    }

    private static bool TryExtractEmbeddedPayload(string destinationDir)
    {
        try
        {
            using var stream = typeof(Program).Assembly.GetManifestResourceStream("Payload.zip");
            if (stream == null) return false;

            var fullDestDirPath = Path.GetFullPath(destinationDir + Path.DirectorySeparatorChar);

            using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
            foreach (var entry in archive.Entries)
            {
                if (string.IsNullOrEmpty(entry.Name)) continue;
                var destFileName = Path.GetFullPath(Path.Combine(destinationDir, entry.FullName));
                if (!destFileName.StartsWith(fullDestDirPath, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var destSubDir = Path.GetDirectoryName(destFileName);
                if (!string.IsNullOrEmpty(destSubDir) && !Directory.Exists(destSubDir))
                {
                    Directory.CreateDirectory(destSubDir);
                }
                SafeExtractEntry(entry, destFileName);
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
        var fullDest = Path.GetFullPath(destination);
        var destPrefix = fullDest.EndsWith(Path.DirectorySeparatorChar) ? fullDest : fullDest + Path.DirectorySeparatorChar;

        var filesToCopy = Directory.GetFiles(source, "*.*", SearchOption.TopDirectoryOnly);
        foreach (var file in filesToCopy)
        {
            var fileName = Path.GetFileName(file);
            var destFile = Path.GetFullPath(Path.Combine(fullDest, fileName));
            if (!destFile.StartsWith(destPrefix, StringComparison.OrdinalIgnoreCase)) continue;
            SafeCopyFile(file, destFile);
        }

        var manifestsDir = Path.Combine(source, "manifests");
        if (Directory.Exists(manifestsDir))
        {
            var destManifests = Path.GetFullPath(Path.Combine(fullDest, "manifests"));
            if (destManifests.StartsWith(destPrefix, StringComparison.OrdinalIgnoreCase))
            {
                Directory.CreateDirectory(destManifests);
                var manifestDestPrefix = destManifests.EndsWith(Path.DirectorySeparatorChar) ? destManifests : destManifests + Path.DirectorySeparatorChar;
                foreach (var f in Directory.GetFiles(manifestsDir))
                {
                    var fName = Path.GetFileName(f);
                    var d1 = Path.GetFullPath(Path.Combine(destManifests, fName));
                    var d2 = Path.GetFullPath(Path.Combine(fullDest, fName));
                    if (d1.StartsWith(manifestDestPrefix, StringComparison.OrdinalIgnoreCase))
                    {
                        SafeCopyFile(f, d1);
                    }
                    if (d2.StartsWith(destPrefix, StringComparison.OrdinalIgnoreCase))
                    {
                        SafeCopyFile(f, d2);
                    }
                }
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

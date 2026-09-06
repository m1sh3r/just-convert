using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Windows;
using JustConvert.Core;
using JustConvert.Core.Converters;
using JustConvert.Core.Windows;
using JustConvert.Installer.UI;
using Microsoft.Win32;

namespace JustConvert.Installer;

public class Program
{
    public const string AppVersion = "0.0.1";
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
            var installDir = GetInstallDirectory(chosenScope);
            if (isUninstall)
            {
                UninstallCore(installDir, chosenScope);
                return 0;
            }

            InstallCoreAsync(installDir, chosenScope, false).GetAwaiter().GetResult();
            return 0;
        }

        var app = new Application();
        var window = new InstallerWindow(chosenScope, isUninstall);
        return app.Run(window);
    }

    public static string GetInstallDirectory(InstallScope scope)
    {
        return scope == InstallScope.AllUsers
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "m1sh3r", "Just Convert")
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "m1sh3r", "Just Convert");
    }

    public static async Task InstallCoreAsync(string installDir, InstallScope scope, bool downloadFfmpeg, IProgress<string>? progress = null)
    {
        progress?.Report(I18n.T("SetupCopying"));

        if (!Directory.Exists(installDir))
        {
            Directory.CreateDirectory(installDir);
        }

        var sourceDir = AppContext.BaseDirectory;
        CopyDirectoryFiles(sourceDir, installDir);

        var installedExe = Path.Combine(installDir, "just-convert.exe");
        var installedDll = Path.Combine(installDir, "just-convert.dll");

        if (!File.Exists(installedExe) || !File.Exists(installedDll))
        {
            string[] fallbackDirs =
            [
                Path.Combine(sourceDir, "..", "..", "..", "..", "JustConvert.Cli", "bin", "Release", "net10.0-windows"),
                Path.Combine(sourceDir, "..", "..", "..", "..", "JustConvert.Cli", "bin", "Debug", "net10.0-windows"),
                Path.Combine(sourceDir, "..", "JustConvert.Cli", "bin", "Release", "net10.0-windows"),
                Path.Combine(sourceDir, "..", "JustConvert.Cli", "bin", "Debug", "net10.0-windows")
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

        if (!File.Exists(installedExe) || !File.Exists(installedDll))
        {
            throw new FileNotFoundException("just-convert executable or runtime files were not found in setup package.");
        }

        if (downloadFfmpeg && MediaConverter.FindFfmpegPath() == null)
        {
            progress?.Report(I18n.T("SetupDownloadingFfmpeg"));
            await FfmpegInstaller.DownloadToDirectoryAsync(installDir);
        }

        progress?.Report(I18n.T("SetupRegistering"));
        ClassicManager.Register(installedExe, scope);
        RegisterModernMenu(installDir, scope);
        RegisterUninstallEntry(installDir, scope);
    }

    public static void UninstallCore(string installDir, InstallScope scope, IProgress<string>? progress = null)
    {
        progress?.Report(I18n.T("SetupUninstalling"));

        ClassicManager.Unregister(scope);
        UnregisterModernMenu();
        RemoveUninstallEntry(scope);

        if (Directory.Exists(installDir))
        {
            try
            {
                Directory.Delete(installDir, true);
            }
            catch { }
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

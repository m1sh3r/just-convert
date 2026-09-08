using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace JustConvert.Core.Windows;

[SupportedOSPlatform("windows")]
public static class ProcessSuspender
{
    [DllImport("ntdll.dll", EntryPoint = "NtSuspendProcess")]
    private static extern int NtSuspendProcess(IntPtr processHandle);

    [DllImport("ntdll.dll", EntryPoint = "NtResumeProcess")]
    private static extern int NtResumeProcess(IntPtr processHandle);

    public static bool Suspend(Process? process)
    {
        if (process == null) return false;
        try
        {
            if (!process.HasExited)
            {
                return NtSuspendProcess(process.Handle) == 0;
            }
        }
        catch { }
        return false;
    }

    public static bool Resume(Process? process)
    {
        if (process == null) return false;
        try
        {
            if (!process.HasExited)
            {
                return NtResumeProcess(process.Handle) == 0;
            }
        }
        catch { }
        return false;
    }
}
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace JustConvert.Core;

public readonly record struct GpuCapabilities(
    bool HasNvencH264,
    bool HasNvencHevc,
    bool HasNvencAv1,
    bool HasQsvH264,
    bool HasQsvHevc,
    bool HasQsvVp9,
    bool HasQsvAv1,
    bool HasAmfH264,
    bool HasAmfHevc,
    bool HasAmfAv1
)
{
    public bool HasNvenc => HasNvencH264 || HasNvencHevc || HasNvencAv1;
    public bool HasQsv => HasQsvH264 || HasQsvHevc || HasQsvVp9 || HasQsvAv1;
    public bool HasAmf => HasAmfH264 || HasAmfHevc || HasAmfAv1;
    public bool HasAny => HasNvenc || HasQsv || HasAmf;

    public static GpuCapabilities Empty => default;

    public static GpuCapabilities Merge(GpuCapabilities a, GpuCapabilities b) => new(
        a.HasNvencH264 || b.HasNvencH264,
        a.HasNvencHevc || b.HasNvencHevc,
        a.HasNvencAv1 || b.HasNvencAv1,
        a.HasQsvH264 || b.HasQsvH264,
        a.HasQsvHevc || b.HasQsvHevc,
        a.HasQsvVp9 || b.HasQsvVp9,
        a.HasQsvAv1 || b.HasQsvAv1,
        a.HasAmfH264 || b.HasAmfH264,
        a.HasAmfHevc || b.HasAmfHevc,
        a.HasAmfAv1 || b.HasAmfAv1
    );
}

public static class HardwareAccelerationDetector
{
    private static readonly Lazy<GpuCapabilities> DetectedCapabilities = new(DetectCapabilities);

    public static GpuCapabilities Capabilities => DetectedCapabilities.Value;

    public static bool HasNvenc => Capabilities.HasNvenc;
    public static bool HasNvencH264 => Capabilities.HasNvencH264;
    public static bool HasNvencHevc => Capabilities.HasNvencHevc;
    public static bool HasNvencAv1 => Capabilities.HasNvencAv1;

    public static bool HasQsv => Capabilities.HasQsv;
    public static bool HasQsvH264 => Capabilities.HasQsvH264;
    public static bool HasQsvHevc => Capabilities.HasQsvHevc;
    public static bool HasQsvVp9 => Capabilities.HasQsvVp9;
    public static bool HasQsvAv1 => Capabilities.HasQsvAv1;

    public static bool HasAmf => Capabilities.HasAmf;
    public static bool HasAmfH264 => Capabilities.HasAmfH264;
    public static bool HasAmfHevc => Capabilities.HasAmfHevc;
    public static bool HasAmfAv1 => Capabilities.HasAmfAv1;

    public static bool HasAny => Capabilities.HasAny;

    private static GpuCapabilities DetectCapabilities()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return GpuCapabilities.Empty;
        }

        var caps1 = DetectViaRegistry();
        var caps2 = DetectViaDisplayDevices();

        return GpuCapabilities.Merge(caps1, caps2);
    }

    private static GpuCapabilities DetectViaRegistry()
    {
        var result = GpuCapabilities.Empty;

        try
        {
            const string displayClassKey = @"SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}";
            using var classKey = Registry.LocalMachine.OpenSubKey(displayClassKey);
            if (classKey == null) return result;

            string[] subKeyNames = [];
            try
            {
                subKeyNames = classKey.GetSubKeyNames();
            }
            catch { }

            foreach (var subKeyName in subKeyNames)
            {
                try
                {
                    using var adapterKey = classKey.OpenSubKey(subKeyName);
                    if (adapterKey == null) continue;

                    var matchingDeviceId = adapterKey.GetValue("MatchingDeviceId")?.ToString() ?? string.Empty;
                    var driverDesc = adapterKey.GetValue("DriverDesc")?.ToString() ?? string.Empty;
                    var provider = adapterKey.GetValue("ProviderName")?.ToString() ?? string.Empty;

                    var combined = $"{matchingDeviceId} {driverDesc} {provider}".ToUpperInvariant();
                    var adapterCaps = EvaluateAdapter(combined);
                    result = GpuCapabilities.Merge(result, adapterCaps);
                }
                catch { }
            }
        }
        catch { }

        return result;
    }

    private static GpuCapabilities DetectViaDisplayDevices()
    {
        var result = GpuCapabilities.Empty;

        try
        {
            var device = new DISPLAY_DEVICE { cb = (uint)Marshal.SizeOf<DISPLAY_DEVICE>() };
            uint id = 0;
            while (EnumDisplayDevices(null, id, ref device, 0))
            {
                var combined = $"{device.DeviceString} {device.DeviceID}".ToUpperInvariant();
                var adapterCaps = EvaluateAdapter(combined);
                result = GpuCapabilities.Merge(result, adapterCaps);
                id++;
                device.cb = (uint)Marshal.SizeOf<DISPLAY_DEVICE>();
            }
        }
        catch { }

        return result;
    }

    private static GpuCapabilities EvaluateAdapter(string combined)
    {
        var nvencH264 = false;
        var nvencHevc = false;
        var nvencAv1 = false;

        var qsvH264 = false;
        var qsvHevc = false;
        var qsvVp9 = false;
        var qsvAv1 = false;

        var amfH264 = false;
        var amfHevc = false;
        var amfAv1 = false;

        if (combined.Contains("VEN_10DE") || combined.Contains("NVIDIA") || combined.Contains("GEFORCE"))
        {
            var isExcluded = combined.Contains("GT 1030") ||
                             combined.Contains("MX110") || combined.Contains("MX130") || combined.Contains("MX150") ||
                             combined.Contains("MX230") || combined.Contains("MX250") ||
                             combined.Contains("MX330") || combined.Contains("MX350");

            if (!isExcluded)
            {
                nvencH264 = true;
                nvencHevc = true;
                if (combined.Contains("RTX 40") || combined.Contains("RTX 50") ||
                    combined.Contains("ADA") || combined.Contains("BLACKWELL") ||
                    combined.Contains("L40") || combined.Contains("L4"))
                {
                    nvencAv1 = true;
                }
            }
        }

        if (combined.Contains("VEN_8086") || combined.Contains("VEN_8087") || combined.Contains("VEN_163C") || combined.Contains("INTEL"))
        {
            qsvH264 = true;
            qsvHevc = true;
            qsvVp9 = true;
            if (combined.Contains("ARC") || combined.Contains("A310") || combined.Contains("A380") ||
                combined.Contains("A580") || combined.Contains("A750") || combined.Contains("A770") ||
                combined.Contains("B570") || combined.Contains("B580") || combined.Contains("BATTLEMAGE") ||
                combined.Contains("CORE(TM) ULTRA") || combined.Contains("CORE ULTRA") ||
                combined.Contains("METEOR") || combined.Contains("LUNAR") || combined.Contains("ARROW") ||
                combined.Contains("XE2"))
            {
                qsvAv1 = true;
            }
        }

        if (combined.Contains("VEN_1002") || combined.Contains("VEN_1022") || combined.Contains("AMD") || combined.Contains("RADEON"))
        {
            var isExcluded = combined.Contains("RX 6400") || combined.Contains("RX 6500");

            if (!isExcluded)
            {
                amfH264 = true;
                amfHevc = true;
                if (combined.Contains("RX 7") || combined.Contains("RX 8") ||
                    combined.Contains("RDNA 3") || combined.Contains("RDNA3") ||
                    combined.Contains("RDNA 4") || combined.Contains("RDNA4") ||
                    combined.Contains("780M") || combined.Contains("880M") || combined.Contains("890M") ||
                    combined.Contains("RADEON 7") || combined.Contains("RADEON 8"))
                {
                    amfAv1 = true;
                }
            }
        }

        return new GpuCapabilities(
            nvencH264, nvencHevc, nvencAv1,
            qsvH264, qsvHevc, qsvVp9, qsvAv1,
            amfH264, amfHevc, amfAv1
        );
    }

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern bool EnumDisplayDevices(string? lpDevice, uint iDevNum, ref DISPLAY_DEVICE lpDisplayDevice, uint dwFlags);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct DISPLAY_DEVICE
    {
        public uint cb;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string DeviceName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string DeviceString;
        public uint StateFlags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string DeviceID;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string DeviceKey;
    }
}

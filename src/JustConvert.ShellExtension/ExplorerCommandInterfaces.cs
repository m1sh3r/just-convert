using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;

namespace JustConvert.ShellExtension;

public enum EXPCMDFLAGS
{
    ECF_DEFAULT = 0x000,
    ECF_HASSUBCOMMANDS = 0x001,
    ECF_HASSPLITBUTTON = 0x002,
    ECF_HIDELABEL = 0x004,
    ECF_ISSEPARATOR = 0x008,
    ECF_HASLUASHIELD = 0x010,
    ECF_SEPARATORBEFORE = 0x020,
    ECF_SEPARATORAFTER = 0x040,
    ECF_ISDROPDOWN = 0x080,
    ECF_TOGGLEABLE = 0x100,
    ECF_AUTOMENUICONS = 0x200
}

public enum EXPCMDSTATE
{
    ECS_ENABLED = 0x00,
    ECS_DISABLED = 0x01,
    ECS_HIDDEN = 0x02,
    ECS_CHECKBOX = 0x04,
    ECS_CHECKED = 0x08,
    ECS_RADIOCHECK = 0x10
}

[ComImport]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
[Guid("a08ce4d0-fa25-44ab-b57c-c7b1c323e0db")]
public interface IExplorerCommand
{
    [PreserveSig]
    int GetTitle(IntPtr psiItemArray, [MarshalAs(UnmanagedType.LPWStr)] out string ppszName);

    [PreserveSig]
    int GetIcon(IntPtr psiItemArray, [MarshalAs(UnmanagedType.LPWStr)] out string ppszIcon);

    [PreserveSig]
    int GetToolTip(IntPtr psiItemArray, [MarshalAs(UnmanagedType.LPWStr)] out string ppszInfotip);

    [PreserveSig]
    int GetCanonicalName(out Guid pguidCommandName);

    [PreserveSig]
    int GetState(IntPtr psiItemArray, [MarshalAs(UnmanagedType.Bool)] bool fOkToBeSlow, out EXPCMDSTATE pCmdState);

    [PreserveSig]
    int Invoke(IntPtr psiItemArray, IntPtr pbc);

    [PreserveSig]
    int GetFlags(out EXPCMDFLAGS pFlags);

    [PreserveSig]
    int EnumSubCommands(out IEnumExplorerCommand ppEnum);
}

[ComImport]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
[Guid("a88826f8-186f-4987-aade-ea0cef8fbfe8")]
public interface IEnumExplorerCommand
{
    [PreserveSig]
    int Next(uint celt, [MarshalAs(UnmanagedType.LPArray, ArraySubType = UnmanagedType.Interface, SizeParamIndex = 0)] out IExplorerCommand[] apUICommand, out uint pceltFetched);

    [PreserveSig]
    int Skip(uint celt);

    [PreserveSig]
    int Reset();

    [PreserveSig]
    int Clone(out IEnumExplorerCommand ppenum);
}

[ComImport]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
[Guid("b63ea76d-1f85-456f-a19c-48159efa858b")]
public interface IShellItemArray
{
    [PreserveSig]
    int BindToHandler(IntPtr pbc, ref Guid bhid, ref Guid riid, out IntPtr ppvOut);

    [PreserveSig]
    int GetPropertyStore(int flags, ref Guid riid, out IntPtr ppv);

    [PreserveSig]
    int GetPropertyDescriptionList(ref Guid keyType, ref Guid riid, out IntPtr ppv);

    [PreserveSig]
    int GetAttributes(int AttribFlags, uint sfgaoMask, out uint psfgaoAttribs);

    [PreserveSig]
    int GetCount(out uint pdwNumItems);

    [PreserveSig]
    int GetItemAt(uint dwIndex, out IShellItem ppsi);

    [PreserveSig]
    int EnumItems(out IntPtr ppenumShellItems);
}

[ComImport]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
[Guid("43826d1e-e718-42ee-bc55-a1e261c37bfe")]
public interface IShellItem
{
    [PreserveSig]
    int BindToHandler(IntPtr pbc, ref Guid bhid, ref Guid riid, out IntPtr ppv);

    [PreserveSig]
    int GetParent(out IShellItem ppsi);

    [PreserveSig]
    int GetDisplayName(uint sigdnName, [MarshalAs(UnmanagedType.LPWStr)] out string ppszName);

    [PreserveSig]
    int GetAttributes(uint sfgaoMask, out uint psfgaoAttribs);

    [PreserveSig]
    int Compare(IShellItem psi, uint hint, out int piOrder);
}

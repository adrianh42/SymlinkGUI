using System.Runtime.InteropServices;

namespace SymlinkGUI.Core;

internal static partial class NativeMethods
{
    public const uint SYMBOLIC_LINK_FLAG_FILE = 0x0;
    public const uint SYMBOLIC_LINK_FLAG_DIRECTORY = 0x1;
    public const uint SYMBOLIC_LINK_FLAG_ALLOW_UNPRIVILEGED_CREATE = 0x2;

    public const int ERROR_SUCCESS = 0;
    public const int ERROR_INVALID_FUNCTION = 1;
    public const int ERROR_FILE_NOT_FOUND = 2;
    public const int ERROR_PATH_NOT_FOUND = 3;
    public const int ERROR_ACCESS_DENIED = 5;
    public const int ERROR_NOT_SUPPORTED = 50;
    public const int ERROR_INVALID_PARAMETER = 87;
    public const int ERROR_INVALID_NAME = 123;
    public const int ERROR_ALREADY_EXISTS = 183;
    public const int ERROR_FILE_EXISTS = 80;
    public const int ERROR_PRIVILEGE_NOT_HELD = 1314;
    public const int ERROR_CANCELLED = 1223;

    [LibraryImport("kernel32.dll", EntryPoint = "CreateSymbolicLinkW", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    [return: MarshalAs(UnmanagedType.U1)]
    public static partial bool CreateSymbolicLink(string lpSymlinkFileName, string lpTargetFileName, uint dwFlags);

    // Shell change notification so Explorer refreshes immediately.
    public const int SHCNE_CREATE = 0x00000002;
    public const int SHCNE_MKDIR = 0x00000008;
    public const int SHCNE_UPDATEDIR = 0x00001000;
    public const int SHCNE_ASSOCCHANGED = 0x08000000;
    public const uint SHCNF_IDLIST = 0x0000;
    public const uint SHCNF_PATHW = 0x0005;
    public const uint SHCNF_FLUSH = 0x1000;

    [LibraryImport("shell32.dll", StringMarshalling = StringMarshalling.Utf16)]
    public static partial void SHChangeNotify(int wEventId, uint uFlags, string? dwItem1, string? dwItem2);

    [LibraryImport("shell32.dll")]
    public static partial void SHChangeNotify(int wEventId, uint uFlags, nint dwItem1, nint dwItem2);

    // Simple modal message box for the headless (context menu) path.
    public const uint MB_OK = 0x0;
    public const uint MB_ICONERROR = 0x10;
    public const uint MB_ICONWARNING = 0x30;
    public const uint MB_ICONINFORMATION = 0x40;
    public const uint MB_SETFOREGROUND = 0x10000;

    [LibraryImport("user32.dll", EntryPoint = "MessageBoxW", StringMarshalling = StringMarshalling.Utf16)]
    public static partial int MessageBox(nint hWnd, string text, string caption, uint type);
}

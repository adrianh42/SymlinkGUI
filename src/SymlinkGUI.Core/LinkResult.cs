namespace SymlinkGUI.Core;

public enum LinkError
{
    None,
    SourceNotFound,
    DestinationNotFound,
    AlreadyExists,
    InvalidName,
    PrivilegeRequired,
    AccessDenied,
    NotSupportedByFileSystem,
    NotSupportedForSource,
    Cancelled,
    Unknown,
}

/// <summary>Outcome of a single link creation attempt.</summary>
public sealed record LinkResult(string SourcePath, string LinkPath, LinkError Error, int Win32Code = 0, string? Detail = null)
{
    public bool Success => Error == LinkError.None;

    public static LinkResult Ok(string source, string link) => new(source, link, LinkError.None);

    public static LinkResult Fail(string source, string link, LinkError error, string? detail = null)
        => new(source, link, error, 0, detail);

    public static LinkResult FromWin32(string source, string link, int code) => new(source, link, MapWin32(code), code);

    public static LinkError MapWin32(int code) => code switch
    {
        NativeMethods.ERROR_SUCCESS => LinkError.None,
        NativeMethods.ERROR_PRIVILEGE_NOT_HELD => LinkError.PrivilegeRequired,
        NativeMethods.ERROR_ALREADY_EXISTS or NativeMethods.ERROR_FILE_EXISTS => LinkError.AlreadyExists,
        NativeMethods.ERROR_PATH_NOT_FOUND => LinkError.DestinationNotFound,
        NativeMethods.ERROR_FILE_NOT_FOUND => LinkError.SourceNotFound,
        NativeMethods.ERROR_ACCESS_DENIED => LinkError.AccessDenied,
        NativeMethods.ERROR_INVALID_NAME => LinkError.InvalidName,
        NativeMethods.ERROR_INVALID_FUNCTION or NativeMethods.ERROR_NOT_SUPPORTED => LinkError.NotSupportedByFileSystem,
        NativeMethods.ERROR_CANCELLED => LinkError.Cancelled,
        _ => LinkError.Unknown,
    };

    /// <summary>Human readable explanation suitable for UI.</summary>
    public string Message => Detail ?? Error switch
    {
        LinkError.None => "Link created.",
        LinkError.SourceNotFound => "The source file or folder no longer exists.",
        LinkError.DestinationNotFound => "The destination folder does not exist.",
        LinkError.AlreadyExists => "A file or folder with that name already exists at the destination.",
        LinkError.InvalidName => "The link name contains invalid characters.",
        LinkError.PrivilegeRequired => "Creating symbolic links requires administrator rights.",
        LinkError.AccessDenied => "Access denied. You may not have permission to write to the destination folder.",
        LinkError.NotSupportedByFileSystem => "The destination drive's file system does not support symbolic links (e.g. FAT32/exFAT).",
        LinkError.NotSupportedForSource => "This link type is not supported for the selected source.",
        LinkError.Cancelled => "The operation was cancelled.",
        _ => Win32Code != 0
            ? $"Windows error {Win32Code}: {new System.ComponentModel.Win32Exception(Win32Code).Message}"
            : "An unknown error occurred.",
    };
}

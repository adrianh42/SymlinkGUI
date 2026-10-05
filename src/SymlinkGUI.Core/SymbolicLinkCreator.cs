using System.Runtime.InteropServices;

namespace SymlinkGUI.Core;

/// <summary>Creates NTFS symbolic links via <c>CreateSymbolicLinkW</c>.</summary>
public sealed class SymbolicLinkCreator : ILinkCreator
{
    public LinkType Type => LinkType.SymbolicLink;

    public bool Supports(string sourcePath) => File.Exists(sourcePath) || Directory.Exists(sourcePath);

    public bool RequiresElevation => !Elevation.CanCreateSymlinksUnprivileged;

    public LinkResult Create(string sourcePath, string linkPath)
    {
        bool isDirectory = Directory.Exists(sourcePath);
        if (!isDirectory && !File.Exists(sourcePath))
            return LinkResult.Fail(sourcePath, linkPath, LinkError.SourceNotFound);

        uint flags = (isDirectory ? NativeMethods.SYMBOLIC_LINK_FLAG_DIRECTORY : NativeMethods.SYMBOLIC_LINK_FLAG_FILE)
                     | NativeMethods.SYMBOLIC_LINK_FLAG_ALLOW_UNPRIVILEGED_CREATE;

        if (NativeMethods.CreateSymbolicLink(linkPath, sourcePath, flags))
            return LinkResult.Ok(sourcePath, linkPath);

        int error = Marshal.GetLastPInvokeError();

        // Very old Windows 10 builds reject the unprivileged flag with ERROR_INVALID_PARAMETER; retry without it.
        if (error == NativeMethods.ERROR_INVALID_PARAMETER)
        {
            flags &= ~NativeMethods.SYMBOLIC_LINK_FLAG_ALLOW_UNPRIVILEGED_CREATE;
            if (NativeMethods.CreateSymbolicLink(linkPath, sourcePath, flags))
                return LinkResult.Ok(sourcePath, linkPath);
            error = Marshal.GetLastPInvokeError();
        }

        return LinkResult.FromWin32(sourcePath, linkPath, error);
    }
}

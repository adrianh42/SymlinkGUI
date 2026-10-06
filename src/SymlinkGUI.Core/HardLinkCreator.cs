using System.Runtime.InteropServices;

namespace SymlinkGUI.Core;

/// <summary>Creates NTFS hard links via <c>CreateHardLinkW</c>.</summary>
public sealed class HardLinkCreator : ILinkCreator
{
    public LinkType Type => LinkType.HardLink;

    public bool Supports(string sourcePath) => File.Exists(sourcePath);

    public bool RequiresElevation => false;

    public LinkResult Create(string sourcePath, string linkPath)
    {
        if (Directory.Exists(sourcePath))
            return LinkResult.Fail(sourcePath, linkPath, LinkError.NotSupportedForSource, "Hard links can only be created for files.");

        if (!File.Exists(sourcePath))
            return LinkResult.Fail(sourcePath, linkPath, LinkError.SourceNotFound);

        string? sourceRoot = Path.GetPathRoot(Path.GetFullPath(sourcePath));
        string? linkRoot = Path.GetPathRoot(Path.GetFullPath(linkPath));
        if (!string.Equals(sourceRoot, linkRoot, StringComparison.OrdinalIgnoreCase))
            return LinkResult.Fail(sourcePath, linkPath, LinkError.NotSameDrive);

        if (NativeMethods.CreateHardLink(linkPath, sourcePath, nint.Zero))
            return LinkResult.Ok(sourcePath, linkPath);

        return LinkResult.FromWin32(sourcePath, linkPath, Marshal.GetLastPInvokeError());
    }
}

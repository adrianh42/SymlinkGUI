using System.Runtime.InteropServices;
using System.Text;

namespace SymlinkGUI.Core;

/// <summary>Creates NTFS directory junctions via <c>FSCTL_SET_REPARSE_POINT</c>.</summary>
public sealed class JunctionCreator : ILinkCreator
{
    public LinkType Type => LinkType.Junction;

    public bool Supports(string sourcePath) => Directory.Exists(sourcePath);

    public bool RequiresElevation => false;

    public LinkResult Create(string sourcePath, string linkPath)
    {
        if (File.Exists(sourcePath))
            return LinkResult.Fail(sourcePath, linkPath, LinkError.NotSupportedForSource, "Junctions can only be created for folders.");

        if (!Directory.Exists(sourcePath))
            return LinkResult.Fail(sourcePath, linkPath, LinkError.SourceNotFound);

        string fullSource = Path.GetFullPath(sourcePath);
        if (fullSource.StartsWith(@"\\") && !fullSource.StartsWith(@"\\?\") && !fullSource.StartsWith(@"\\.\"))
            return LinkResult.Fail(sourcePath, linkPath, LinkError.NotSupportedForSource, "Junctions cannot point to network shares.");

        try
        {
            Directory.CreateDirectory(linkPath);
        }
        catch (Exception ex)
        {
            return LinkResult.Fail(sourcePath, linkPath, LinkError.AccessDenied, ex.Message);
        }

        using var handle = NativeMethods.CreateFile(
            linkPath,
            NativeMethods.GENERIC_WRITE,
            NativeMethods.FILE_SHARE_READ | NativeMethods.FILE_SHARE_WRITE | NativeMethods.FILE_SHARE_DELETE,
            nint.Zero,
            NativeMethods.OPEN_EXISTING,
            NativeMethods.FILE_FLAG_BACKUP_SEMANTICS | NativeMethods.FILE_FLAG_OPEN_REPARSE_POINT,
            nint.Zero);

        if (handle.IsInvalid)
        {
            int error = Marshal.GetLastPInvokeError();
            TryDeleteDirectory(linkPath);
            return LinkResult.FromWin32(sourcePath, linkPath, error);
        }

        string target = Path.TrimEndingDirectorySeparator(fullSource) + @"\";
        string substituteName = @"\??\" + target;
        string printName = target;

        byte[] substituteBytes = Encoding.Unicode.GetBytes(substituteName);
        byte[] printBytes = Encoding.Unicode.GetBytes(printName);

        ushort substituteOffset = 0;
        ushort substituteLength = (ushort)substituteBytes.Length;
        ushort printOffset = (ushort)(substituteLength + 2);
        ushort printLength = (ushort)printBytes.Length;
        int pathBufferLength = printOffset + printLength + 2;
        ushort reparseDataLength = (ushort)(8 + pathBufferLength);

        byte[] buffer = new byte[8 + reparseDataLength];
        using (var ms = new MemoryStream(buffer))
        using (var writer = new BinaryWriter(ms))
        {
            writer.Write(NativeMethods.IO_REPARSE_TAG_MOUNT_POINT); // ReparseTag (4 bytes)
            writer.Write(reparseDataLength);                       // ReparseDataLength (2 bytes)
            writer.Write((ushort)0);                               // Reserved (2 bytes)
            writer.Write(substituteOffset);                        // SubstituteNameOffset (2 bytes)
            writer.Write(substituteLength);                        // SubstituteNameLength (2 bytes)
            writer.Write(printOffset);                             // PrintNameOffset (2 bytes)
            writer.Write(printLength);                             // PrintNameLength (2 bytes)
            writer.Write(substituteBytes);                         // PathBuffer (substitute)
            writer.Write((short)0);                                // Null terminator (2 bytes)
            writer.Write(printBytes);                              // PathBuffer (print)
            writer.Write((short)0);                                // Null terminator (2 bytes)
        }

        unsafe
        {
            fixed (byte* pBuffer = buffer)
            {
                bool success = NativeMethods.DeviceIoControl(
                    handle,
                    NativeMethods.FSCTL_SET_REPARSE_POINT,
                    (nint)pBuffer,
                    (uint)buffer.Length,
                    nint.Zero,
                    0,
                    out _,
                    nint.Zero);

                if (!success)
                {
                    int error = Marshal.GetLastPInvokeError();
                    handle.Dispose();
                    TryDeleteDirectory(linkPath);
                    return LinkResult.FromWin32(sourcePath, linkPath, error);
                }
            }
        }

        return LinkResult.Ok(sourcePath, linkPath);
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            Directory.Delete(path);
        }
        catch
        {
            // Best effort cleanup.
        }
    }
}

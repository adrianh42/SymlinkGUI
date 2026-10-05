namespace SymlinkGUI.Core;

/// <summary>
/// Entry point for creating links: validates input, resolves name collisions,
/// dispatches to the right <see cref="ILinkCreator"/>, and notifies Explorer.
/// </summary>
public sealed class LinkService
{
    private readonly Dictionary<LinkType, ILinkCreator> _creators;

    /// <summary>The link types enabled in this build. Add new creators here.</summary>
    public static LinkService Default { get; } = new([new SymbolicLinkCreator()]);

    public LinkService(IEnumerable<ILinkCreator> creators)
    {
        _creators = creators.ToDictionary(c => c.Type);
    }

    public IReadOnlyList<LinkType> SupportedTypes => [.. _creators.Keys.Order()];

    public bool TryGetCreator(LinkType type, out ILinkCreator creator) => _creators.TryGetValue(type, out creator!);

    public bool RequiresElevation(LinkType type) => TryGetCreator(type, out var c) && c.RequiresElevation;

    /// <summary>True if anything (file, folder, or even a broken link) exists at the path.</summary>
    public static bool PathExists(string path)
    {
        try
        {
            // GetAttributes does not follow links, so dangling symlinks are detected too.
            File.GetAttributes(path);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>Returns an error message if <paramref name="name"/> is not a valid file name, otherwise null.</summary>
    public static string? ValidateName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return "Enter a name for the link.";
        if (name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            return "The name contains characters that aren't allowed in file names ( \\ / : * ? \" < > | ).";
        if (name.EndsWith('.') || name.EndsWith(' '))
            return "The name can't end with a space or a period.";
        if (name is "." or "..")
            return "That name is reserved.";

        string stem = Path.GetFileNameWithoutExtension(name).ToUpperInvariant();
        string[] reserved = ["CON", "PRN", "AUX", "NUL", "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
                             "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"];
        if (reserved.Contains(stem))
            return $"\"{name}\" is a reserved name in Windows.";
        return null;
    }

    /// <summary>
    /// Finds a free path for <paramref name="name"/> inside <paramref name="folder"/>, appending " (2)", " (3)"...
    /// For files the counter goes before the extension; folders keep their full name (e.g. "v1.2 (2)").
    /// </summary>
    public static string GetUniquePath(string folder, string name, bool isDirectory)
    {
        string candidate = Path.Combine(folder, name);
        if (!PathExists(candidate)) return candidate;

        string stem = isDirectory ? name : Path.GetFileNameWithoutExtension(name);
        string ext = isDirectory ? "" : Path.GetExtension(name);
        for (int i = 2; ; i++)
        {
            candidate = Path.Combine(folder, $"{stem} ({i}){ext}");
            if (!PathExists(candidate)) return candidate;
        }
    }

    /// <summary>Validates and creates a single link at an exact path.</summary>
    public LinkResult CreateLink(LinkType type, string sourcePath, string linkPath)
    {
        sourcePath = Path.GetFullPath(sourcePath);
        linkPath = Path.GetFullPath(linkPath);

        if (!TryGetCreator(type, out var creator))
            return LinkResult.Fail(sourcePath, linkPath, LinkError.NotSupportedForSource, $"{type.DisplayName()} is not supported yet.");
        if (!PathExists(sourcePath))
            return LinkResult.Fail(sourcePath, linkPath, LinkError.SourceNotFound);
        if (!creator.Supports(sourcePath))
            return LinkResult.Fail(sourcePath, linkPath, LinkError.NotSupportedForSource);

        string? parent = Path.GetDirectoryName(linkPath);
        if (parent is null || !Directory.Exists(parent))
            return LinkResult.Fail(sourcePath, linkPath, LinkError.DestinationNotFound);
        if (ValidateName(Path.GetFileName(linkPath)) is { } nameError)
            return LinkResult.Fail(sourcePath, linkPath, LinkError.InvalidName, nameError);
        if (PathExists(linkPath))
            return LinkResult.Fail(sourcePath, linkPath, LinkError.AlreadyExists);

        var result = creator.Create(sourcePath, linkPath);
        if (result.Success)
            NotifyCreated(linkPath, Directory.Exists(sourcePath));
        return result;
    }

    /// <summary>Creates one link per source inside <paramref name="destinationFolder"/>, auto-renaming on collisions.</summary>
    public IReadOnlyList<LinkResult> DropLinks(LinkType type, IEnumerable<string> sources, string destinationFolder)
    {
        var results = new List<LinkResult>();
        foreach (var raw in sources)
        {
            string source = Path.GetFullPath(raw);
            string name = Path.GetFileName(Path.TrimEndingDirectorySeparator(source));
            if (string.IsNullOrEmpty(name))
            {
                // A drive root such as "D:\" has no file name; use "D" instead.
                name = source.TrimEnd('\\', ':');
            }

            if (!PathExists(source))
            {
                results.Add(LinkResult.Fail(source, Path.Combine(destinationFolder, name), LinkError.SourceNotFound));
                continue;
            }

            string linkPath = GetUniquePath(destinationFolder, name, Directory.Exists(source));
            results.Add(CreateLink(type, source, linkPath));
        }
        return results;
    }

    private static void NotifyCreated(string linkPath, bool isDirectory)
    {
        try
        {
            NativeMethods.SHChangeNotify(isDirectory ? NativeMethods.SHCNE_MKDIR : NativeMethods.SHCNE_CREATE,
                NativeMethods.SHCNF_PATHW, linkPath, null);
        }
        catch
        {
            // Best effort only.
        }
    }
}

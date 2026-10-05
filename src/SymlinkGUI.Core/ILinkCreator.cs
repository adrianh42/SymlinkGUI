namespace SymlinkGUI.Core;

/// <summary>
/// Strategy for creating one kind of link. Add a new implementation (e.g. JunctionCreator,
/// HardLinkCreator) and register it in <see cref="LinkService"/> to support more link types;
/// the context menu and command line pick them up automatically.
/// </summary>
public interface ILinkCreator
{
    LinkType Type { get; }

    /// <summary>Whether this link type can point at the given source (e.g. junctions are folder-only).</summary>
    bool Supports(string sourcePath);

    /// <summary>Whether creating this link type in the current process would need elevation.</summary>
    bool RequiresElevation { get; }

    /// <summary>Creates a link at <paramref name="linkPath"/> pointing to <paramref name="sourcePath"/>. Both must be full paths.</summary>
    LinkResult Create(string sourcePath, string linkPath);
}

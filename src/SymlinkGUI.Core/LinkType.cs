namespace SymlinkGUI.Core;

/// <summary>
/// Kinds of file system links the app can create.
/// Only <see cref="SymbolicLink"/> is implemented today; the others are reserved
/// so new <see cref="ILinkCreator"/> implementations can be added without API changes.
/// </summary>
public enum LinkType
{
    SymbolicLink,
    Junction,
    HardLink,
}

public static class LinkTypeExtensions
{
    /// <summary>Stable token used on the command line and in registry verbs.</summary>
    public static string ToToken(this LinkType type) => type switch
    {
        LinkType.SymbolicLink => "symlink",
        LinkType.Junction => "junction",
        LinkType.HardLink => "hardlink",
        _ => throw new ArgumentOutOfRangeException(nameof(type)),
    };

    public static bool TryParseToken(string? token, out LinkType type)
    {
        switch (token?.Trim().ToLowerInvariant())
        {
            case "symlink": type = LinkType.SymbolicLink; return true;
            case "junction": type = LinkType.Junction; return true;
            case "hardlink": type = LinkType.HardLink; return true;
            default: type = default; return false;
        }
    }

    public static string DisplayName(this LinkType type) => type switch
    {
        LinkType.SymbolicLink => "Symbolic Link",
        LinkType.Junction => "Junction",
        LinkType.HardLink => "Hard Link",
        _ => type.ToString(),
    };
}

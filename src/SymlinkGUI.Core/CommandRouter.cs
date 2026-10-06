namespace SymlinkGUI.Core;

/// <summary>
/// Handles the command-line verbs used by the Explorer context menu without starting the WinUI/XAML runtime,
/// so right-click actions stay fast.
/// </summary>
/// <remarks>
/// Verbs:
/// <list type="bullet">
/// <item><c>--pick &lt;path&gt;</c>: add a path to the picked sources (one process per selected item).</item>
/// <item><c>--drop &lt;type&gt; &lt;folder&gt;</c>: create links to the picked sources in a folder.</item>
/// <item><c>--create-links &lt;type&gt; &lt;folder&gt; &lt;source&gt;...</c>: internal; used for the elevated relaunch of a drop.</item>
/// <item><c>--create &lt;type&gt; &lt;source&gt; &lt;linkPath&gt;</c>: internal; used by the GUI for an elevated create. Exit code is encoded with <see cref="EncodeExitCode"/>.</item>
/// <item><c>--install</c> / <c>--uninstall</c>: register or remove the context menu.</item>
/// </list>
/// Anything else (no args, <c>--open</c>, <c>--gui</c>) is left to the GUI.
/// </remarks>
public static class CommandRouter
{
    private const string Caption = "Symlink GUI";

    public static bool TryRunHeadless(string[] args, out int exitCode)
    {
        exitCode = 0;
        if (args.Length == 0) return false;

        switch (args[0].ToLowerInvariant())
        {
            case "--pick":
                exitCode = Pick(args[1..]);
                return true;
            case "--drop" when args.Length >= 3:
                exitCode = Drop(args[1], CommandLine.NormalizePathArgument(args[2]));
                return true;
            case "--create-links" when args.Length >= 4:
                exitCode = CreateLinks(args[1], CommandLine.NormalizePathArgument(args[2]), args[3..]);
                return true;
            case "--create" when args.Length >= 4:
                exitCode = CreateSingle(args[1], args[2], args[3]);
                return true;
            case "--install":
                ContextMenuRegistrar.CreateDefault().Install();
                return true;
            case "--uninstall":
                ContextMenuRegistrar.CreateDefault().Uninstall();
                return true;
            default:
                return false;
        }
    }

    /// <summary>Encodes a result as a process exit code: 0 = success, &gt;0 = Win32 error, &lt;0 = negated <see cref="LinkError"/>.</summary>
    public static int EncodeExitCode(LinkResult result) =>
        result.Success ? 0 : result.Win32Code != 0 ? result.Win32Code : -(int)result.Error;

    public static LinkResult DecodeExitCode(int code, string source, string link) => code switch
    {
        0 => LinkResult.Ok(source, link),
        < 0 => LinkResult.Fail(source, link, (LinkError)(-code)),
        _ => LinkResult.FromWin32(source, link, code),
    };

    private static int Pick(string[] paths)
    {
        var normalized = paths.Select(CommandLine.NormalizePathArgument).Where(LinkService.PathExists).ToList();
        PickStore.Default.Pick(normalized);
        return 0;
    }

    private static int Drop(string typeToken, string folder)
    {
        if (!TryResolveType(typeToken, out var type)) return 1;

        var picked = PickStore.Default.GetPicked();
        if (picked.Count == 0)
        {
            Show("Nothing has been picked yet.\n\nRight-click a file or folder and choose \"Pick as Link Source\" first.",
                NativeMethods.MB_ICONINFORMATION);
            return 1;
        }

        if (LinkService.Default.RequiresElevation(type))
        {
            // Hand the whole batch to an elevated copy of ourselves; it reports its own results.
            int code = Elevation.RunElevatedAndWait(["--create-links", type.ToToken(), folder, .. picked]);
            return code == NativeMethods.ERROR_CANCELLED ? 0 : code;
        }

        return CreateLinks(type, folder, picked);
    }

    private static int CreateLinks(string typeToken, string folder, string[] sources)
    {
        if (!TryResolveType(typeToken, out var type)) return 1;
        return CreateLinks(type, folder, sources);
    }

    private static int CreateLinks(LinkType type, string folder, IReadOnlyList<string> sources)
    {
        if (!Directory.Exists(folder))
        {
            Show($"The destination folder doesn't exist:\n{folder}", NativeMethods.MB_ICONERROR);
            return 1;
        }

        var results = LinkService.Default.DropLinks(type, sources, folder);
        var failures = results.Where(r => !r.Success).ToList();
        if (failures.Count == 0) return 0; // Success is silent: the new links simply appear in Explorer.

        int ok = results.Count - failures.Count;
        var lines = failures.Take(10).Select(f => $"• {Path.GetFileName(f.SourcePath)}: {f.Message}");
        string more = failures.Count > 10 ? $"\n…and {failures.Count - 10} more." : "";
        string header = ok > 0
            ? $"{ok} link(s) created, {failures.Count} failed:"
            : failures.Count == 1 ? "The link could not be created:" : $"{failures.Count} links could not be created:";

        Show($"{header}\n\n{string.Join("\n", lines)}{more}", NativeMethods.MB_ICONWARNING);
        return 1;
    }

    private static int CreateSingle(string typeToken, string source, string linkPath)
    {
        if (!LinkTypeExtensions.TryParseToken(typeToken, out var type))
            return -(int)LinkError.NotSupportedForSource;
        var result = LinkService.Default.CreateLink(type, source, linkPath);
        return EncodeExitCode(result);
    }

    private static bool TryResolveType(string token, out LinkType type)
    {
        if (LinkTypeExtensions.TryParseToken(token, out type) && LinkService.Default.TryGetCreator(type, out _))
            return true;
        Show($"Unsupported link type: {token}", NativeMethods.MB_ICONERROR);
        return false;
    }

    private static void Show(string message, uint icon) =>
        NativeMethods.MessageBox(0, message, Caption, NativeMethods.MB_OK | NativeMethods.MB_SETFOREGROUND | icon);
}

using Microsoft.Win32;

namespace SymlinkGUI.Core;

/// <summary>
/// Installs/uninstalls the classic Explorer context menu verbs under HKCU (no admin needed).
/// On Windows 11 these appear under "Show more options".
/// </summary>
public sealed class ContextMenuRegistrar
{
    public const string VerbPrefix = "SymlinkGUI.";
    private const string PickVerb = VerbPrefix + "Pick";
    private const string OpenVerb = VerbPrefix + "Open";
    private const string DropVerb = VerbPrefix + "Drop";

    // Class keys we touch. Kept in one place so uninstall removes everything install created.
    private static readonly string[] ItemClasses = [@"*", @"Directory"];
    private static readonly string[] DropTargetClasses = [@"Directory", @"Drive"];
    private static readonly string[] BackgroundClasses = [@"Directory\Background", @"DesktopBackground"];

    private readonly string _exePath;
    private readonly IReadOnlyList<LinkType> _types;
    private readonly RegistryKey _root;
    private readonly string _classesPath;

    public ContextMenuRegistrar(string exePath, IReadOnlyList<LinkType> types, RegistryKey? root = null, string classesPath = @"Software\Classes")
    {
        if (types.Count == 0) throw new ArgumentException("At least one link type is required.", nameof(types));
        _exePath = exePath;
        _types = types;
        _root = root ?? Registry.CurrentUser;
        _classesPath = classesPath;
    }

    public static ContextMenuRegistrar CreateDefault() =>
        new(Elevation.ExecutablePath, LinkService.Default.SupportedTypes);

    public void Install()
    {
        Uninstall(notify: false); // Start clean so removed link types don't leave stale submenu entries.

        string exe = $"\"{_exePath}\"";

        foreach (var cls in ItemClasses)
        {
            WriteVerb(cls, PickVerb, "Pick as Link Source", $"{exe} --pick \"%1\"", multiSelect: "Player");
            WriteVerb(cls, OpenVerb, "Create Link To...", $"{exe} --open \"%1\"", multiSelect: "Single");
        }

        foreach (var cls in DropTargetClasses)
            WriteDropVerb(cls, exe, "%1", isBackground: false);
        foreach (var cls in BackgroundClasses)
            WriteDropVerb(cls, exe, "%V", isBackground: true);

        NotifyShell();
    }

    public void Uninstall() => Uninstall(notify: true);

    private void Uninstall(bool notify)
    {
        foreach (var cls in ItemClasses.Concat(DropTargetClasses).Concat(BackgroundClasses).Distinct())
        {
            using var shell = _root.OpenSubKey($@"{_classesPath}\{cls}\shell", writable: true);
            if (shell is null) continue;
            foreach (var name in shell.GetSubKeyNames().Where(n => n.StartsWith(VerbPrefix, StringComparison.OrdinalIgnoreCase)))
                shell.DeleteSubKeyTree(name, throwOnMissingSubKey: false);
        }
        if (notify) NotifyShell();
    }

    public bool IsInstalled
    {
        get
        {
            using var key = _root.OpenSubKey($@"{_classesPath}\Directory\Background\shell\{DropVerb}");
            return key is not null;
        }
    }

    /// <summary>The executable path the installed verbs point to, or null if not installed.</summary>
    public string? RegisteredExecutablePath
    {
        get
        {
            using var key = _root.OpenSubKey($@"{_classesPath}\*\shell\{PickVerb}\command");
            if (key?.GetValue(null) is not string command || !command.StartsWith('"')) return null;
            int end = command.IndexOf('"', 1);
            return end > 1 ? command[1..end] : null;
        }
    }

    /// <summary>True if installed but pointing at a different executable (e.g. the app was moved).</summary>
    public bool IsStale => IsInstalled &&
        !string.Equals(RegisteredExecutablePath, _exePath, StringComparison.OrdinalIgnoreCase);

    private void WriteDropVerb(string cls, string exe, string placeholder, bool isBackground)
    {
        if (_types.Count == 1)
        {
            var type = _types[0];
            WriteVerb(cls, DropVerb, $"Drop {type.DisplayName()} Here", $"{exe} --drop {type.ToToken()} \"{placeholder}\"", multiSelect: isBackground ? null : "Single");
            return;
        }

        // Multiple link types: cascading "Drop Link Here ▸" submenu.
        using var verb = _root.CreateSubKey($@"{_classesPath}\{cls}\shell\{DropVerb}");
        verb.SetValue("MUIVerb", "Drop Link Here");
        verb.SetValue("SubCommands", "");
        if (!isBackground)
            verb.SetValue("MultiSelectModel", "Single");
        SetIcon(verb);
        for (int i = 0; i < _types.Count; i++)
        {
            var type = _types[i];
            using var sub = verb.CreateSubKey($@"shell\{i + 1:00}_{type.ToToken()}");
            sub.SetValue("MUIVerb", type.DisplayName());
            using var cmd = sub.CreateSubKey("command");
            cmd.SetValue(null, $"{exe} --drop {type.ToToken()} \"{placeholder}\"");
        }
    }

    private void WriteVerb(string cls, string verbName, string text, string command, string? multiSelect)
    {
        using var verb = _root.CreateSubKey($@"{_classesPath}\{cls}\shell\{verbName}");
        verb.SetValue("MUIVerb", text);
        if (multiSelect is not null)
            verb.SetValue("MultiSelectModel", multiSelect);
        SetIcon(verb);
        using var cmd = verb.CreateSubKey("command");
        cmd.SetValue(null, command);
    }

    private void SetIcon(RegistryKey verb) => verb.SetValue("Icon", $"\"{_exePath}\",0");

    private static void NotifyShell()
    {
        try
        {
            NativeMethods.SHChangeNotify(NativeMethods.SHCNE_ASSOCCHANGED, NativeMethods.SHCNF_IDLIST, 0, 0);
        }
        catch
        {
            // Best effort.
        }
    }
}

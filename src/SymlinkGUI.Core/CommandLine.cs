using System.Text;

namespace SymlinkGUI.Core;

/// <summary>Command line quoting / path-argument helpers.</summary>
public static class CommandLine
{
    /// <summary>Joins arguments using the MSVCRT quoting rules so they round-trip through <c>CommandLineToArgvW</c>.</summary>
    public static string Join(IEnumerable<string> arguments)
    {
        var sb = new StringBuilder();
        foreach (var arg in arguments)
        {
            if (sb.Length > 0) sb.Append(' ');
            AppendQuoted(sb, arg);
        }
        return sb.ToString();
    }

    private static void AppendQuoted(StringBuilder sb, string arg)
    {
        if (arg.Length > 0 && arg.IndexOfAny([' ', '\t', '\n', '\v', '"']) < 0)
        {
            sb.Append(arg);
            return;
        }

        sb.Append('"');
        int backslashes = 0;
        foreach (char c in arg)
        {
            if (c == '\\')
            {
                backslashes++;
                continue;
            }

            if (c == '"')
            {
                sb.Append('\\', backslashes * 2 + 1);
            }
            else
            {
                sb.Append('\\', backslashes);
            }
            backslashes = 0;
            sb.Append(c);
        }
        sb.Append('\\', backslashes * 2);
        sb.Append('"');
    }

    /// <summary>
    /// Cleans a path received from Explorer. Explorer passes drive roots as <c>"C:\"</c>, which the C runtime
    /// parses as <c>C:"</c> (the backslash escapes the closing quote). Paths can't contain quotes, so it's safe
    /// to strip them and restore the trailing separator.
    /// </summary>
    public static string NormalizePathArgument(string raw)
    {
        string path = raw.Trim().Trim('"');
        if (path.Length == 2 && path[1] == ':')
            path += Path.DirectorySeparatorChar;

        try
        {
            path = Path.GetFullPath(path);
        }
        catch
        {
            // Leave as-is; callers validate existence.
        }

        // Keep "C:\" but remove trailing separators from everything else.
        if (path.Length > 3)
            path = Path.TrimEndingDirectorySeparator(path);
        return path;
    }
}

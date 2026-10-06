using System;
using System.Windows.Forms;
using SymlinkGUI.Core;

namespace SymlinkGUI;

public static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        // Context-menu verbs (--pick, --drop, ...) run headless and exit before any GUI is initialized.
        if (CommandRouter.TryRunHeadless(args, out int exitCode))
            return exitCode;

        ApplicationConfiguration.Initialize();
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);

        // Check if "--open <path>" was passed by Explorer's "Create Symlink To..." context menu.
        string? openSource = null;
        int i = Array.FindIndex(args, a => a.Equals("--open", StringComparison.OrdinalIgnoreCase));
        if (i >= 0 && i + 1 < args.Length)
        {
            openSource = CommandLine.NormalizePathArgument(args[i + 1]);
        }

        Application.Run(new MainForm(openSource));
        return 0;
    }
}

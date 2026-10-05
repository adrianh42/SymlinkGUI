using Microsoft.UI.Xaml;

namespace SymlinkGUI;

public partial class App : Application
{
    private MainWindow? _window;

    public App()
    {
        InitializeComponent();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        _window = new MainWindow();

        // "Create Symlink To..." from Explorer launches us with: --open "<path>"
        var cmd = Environment.GetCommandLineArgs();
        int i = Array.FindIndex(cmd, a => a.Equals("--open", StringComparison.OrdinalIgnoreCase));
        if (i >= 0 && i + 1 < cmd.Length)
            _window.SetSource(Core.CommandLine.NormalizePathArgument(cmd[i + 1]));

        _window.Activate();
    }
}

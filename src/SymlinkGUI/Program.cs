using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using SymlinkGUI.Core;

namespace SymlinkGUI;

public static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        // Context-menu verbs (--pick, --drop, ...) run headless and exit before any XAML is initialized.
        if (CommandRouter.TryRunHeadless(args, out int exitCode))
            return exitCode;

        WinRT.ComWrappersSupport.InitializeComWrappers();
        Application.Start(p =>
        {
            var context = new DispatcherQueueSynchronizationContext(DispatcherQueue.GetForCurrentThread());
            SynchronizationContext.SetSynchronizationContext(context);
            _ = new App();
        });
        return 0;
    }
}

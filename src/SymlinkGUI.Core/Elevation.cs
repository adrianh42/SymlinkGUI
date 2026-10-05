using System.ComponentModel;
using System.Diagnostics;
using System.Security.Principal;
using Microsoft.Win32;

namespace SymlinkGUI.Core;

/// <summary>Helpers for detecting privileges and relaunching elevated.</summary>
public static class Elevation
{
    public static bool IsAdministrator
    {
        get
        {
            using var identity = WindowsIdentity.GetCurrent();
            return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
        }
    }

    /// <summary>True when Windows Developer Mode is enabled (allows unprivileged symlink creation).</summary>
    public static bool IsDeveloperModeEnabled
    {
        get
        {
            try
            {
                using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\AppModelUnlock");
                return key?.GetValue("AllowDevelopmentWithoutDevLicense") is int value && value != 0;
            }
            catch
            {
                return false;
            }
        }
    }

    public static bool CanCreateSymlinksUnprivileged => IsAdministrator || IsDeveloperModeEnabled;

    /// <summary>Full path of the running executable.</summary>
    public static string ExecutablePath =>
        Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule!.FileName;

    /// <summary>
    /// Relaunches this executable elevated with the given arguments and waits for it to exit.
    /// Returns the child's exit code, or <see cref="NativeMethods.ERROR_CANCELLED"/> if the user declined UAC.
    /// </summary>
    public static int RunElevatedAndWait(IEnumerable<string> arguments)
    {
        var psi = new ProcessStartInfo(ExecutablePath)
        {
            UseShellExecute = true,
            Verb = "runas",
            Arguments = CommandLine.Join(arguments),
            WorkingDirectory = Path.GetDirectoryName(ExecutablePath) ?? Path.GetTempPath(),
        };

        try
        {
            using var process = Process.Start(psi);
            if (process is null) return NativeMethods.ERROR_CANCELLED;
            process.WaitForExit();
            return process.ExitCode;
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == NativeMethods.ERROR_CANCELLED)
        {
            return NativeMethods.ERROR_CANCELLED;
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == NativeMethods.ERROR_NOT_SUPPORTED)
        {
            // Non-interactive session, SSH, or environment where UAC elevation prompt cannot be displayed
            return NativeMethods.ERROR_PRIVILEGE_NOT_HELD;
        }
    }

    public static Task<int> RunElevatedAndWaitAsync(IEnumerable<string> arguments)
    {
        var args = arguments.ToArray();
        return Task.Run(() => RunElevatedAndWait(args));
    }
}

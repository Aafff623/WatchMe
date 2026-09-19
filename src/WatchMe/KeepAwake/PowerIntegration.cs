using System.Diagnostics;
using System.IO;
using WatchMe.Interop;

namespace WatchMe.KeepAwake;

/// <summary>Real SetThreadExecutionState adapter.</summary>
public sealed class ExecutionStateApi : IExecutionStateApi
{
    public void SetKeepAwake(bool on) =>
        _ = NativeMethods.SetThreadExecutionState(NativeMethods.ES_CONTINUOUS
            | (on ? NativeMethods.ES_SYSTEM_REQUIRED | NativeMethods.ES_DISPLAY_REQUIRED : 0));
}

/// <summary>Runs powercfg via UAC elevation (verb runas) so lid-close policy can be changed.</summary>
public sealed class PowerProcessRunner : IPowerProcessRunner
{
    public int? RunElevated(string arguments)
    {
        try
        {
            var info = new ProcessStartInfo("powercfg", arguments)
            {
                Verb = "runas",
                UseShellExecute = true,
                CreateNoWindow = true,
            };
            using var process = Process.Start(info);
            if (process is null)
                return null;

            if (!process.WaitForExit(20_000))
            {
                try
                {
                    process.Kill();
                }
                catch (Exception killEx) when (killEx is InvalidOperationException or System.ComponentModel.Win32Exception)
                {
                    // Killing an elevated child from a normal process can be denied; treat as failure.
                }

                return null;
            }

            return process.ExitCode;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            // User dismissed the UAC prompt.
            return null;
        }
        catch (IOException)
        {
            return null;
        }
    }
}

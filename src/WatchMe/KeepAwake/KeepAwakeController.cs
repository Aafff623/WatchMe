using System.IO;

namespace WatchMe.KeepAwake;

/// <summary>Wraps SetThreadExecutionState so tests can observe calls.</summary>
public interface IExecutionStateApi
{
    void SetKeepAwake(bool on);
}

/// <summary>Runs powercfg commands; elevated runs prompt UAC, queries run in-process.</summary>
public interface IPowerProcessRunner
{
    /// <summary>Returns exit code, or null when the user cancelled the UAC prompt.</summary>
    int? RunElevated(string arguments);
}

/// <summary>
/// Keep-awake controller, ported from the macOS SystemSleepGuard/AppSettingsStore pair:
/// coffee mode = execution-state keep-awake; lid-close immunity = powercfg LIDACTION 0
/// via an elevated helper, restored on clean exit and on crash recovery at next startup.
/// </summary>
public sealed class KeepAwakeController
{
    private const string LidActionGroup = "SUB_BUTTONS";
    private const string LidActionSetting = "LIDACTION";
    private const int LidDoNothing = 0;
    private const int LidSleep = 1;

    private readonly IExecutionStateApi _executionState;
    private readonly IPowerProcessRunner _power;
    private readonly string _lidStateFile;

    public KeepAwakeController(IExecutionStateApi executionState, IPowerProcessRunner power, string stateDir)
    {
        _executionState = executionState;
        _power = power;
        _lidStateFile = Path.Combine(stateDir, "lid-never-sleep.active");
    }

    public static KeepAwakeController Default(IExecutionStateApi executionState, IPowerProcessRunner power)
        => new(executionState, power, AppPaths.KeepAwakeStateDir);

    public bool CoffeeActive { get; private set; }

    public bool LidNeverSleepActive => File.Exists(_lidStateFile);

    public void ToggleCoffee() => SetCoffee(!CoffeeActive);

    public void SetCoffee(bool on)
    {
        if (CoffeeActive == on)
            return;

        CoffeeActive = on;
        _executionState.SetKeepAwake(on);
    }

    /// <summary>Enables/disables "close the lid without sleeping". Returns false when elevation was refused.</summary>
    public bool SetLidNeverSleep(bool enable)
    {
        var target = enable ? LidDoNothing : LidSleep;
        var ac = _power.RunElevated($"/setacvalueindex SCHEME_CURRENT {LidActionGroup} {LidActionSetting} {target}");
        var dc = ac is null ? null : _power.RunElevated($"/setdcvalueindex SCHEME_CURRENT {LidActionGroup} {LidActionSetting} {target}");
        var apply = dc is null ? null : _power.RunElevated("/setactive SCHEME_CURRENT");
        if (ac != 0 || dc != 0 || apply != 0)
            return false;

        if (enable)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_lidStateFile)!);
            File.WriteAllText(_lidStateFile, DateTimeOffset.Now.ToString("O"));
        }
        else
        {
            if (File.Exists(_lidStateFile))
                File.Delete(_lidStateFile);
        }

        return true;
    }

    /// <summary>Call at startup: if a previous session crashed while lid immunity was on, restore default sleep.</summary>
    public void RecoverOnStartup()
    {
        if (!LidNeverSleepActive)
            return;

        try
        {
            _ = SetLidNeverSleep(false);
        }
        catch (IOException)
        {
            // Best effort — never block startup on recovery.
        }
    }

    /// <summary>Call on clean exit so system sleep behavior never stays disabled by accident.</summary>
    public void Shutdown()
    {
        SetCoffee(false);
        if (LidNeverSleepActive)
            RecoverOnStartup();
    }
}

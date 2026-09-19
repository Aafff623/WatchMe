using System.IO;
using WatchMe.KeepAwake;

namespace WatchMe.Tests;

public sealed class FakeExecutionState : IExecutionStateApi
{
    public bool KeepAwake { get; private set; }

    public void SetKeepAwake(bool on) => KeepAwake = on;
}

public sealed class FakePowerRunner : IPowerProcessRunner
{
    public List<string> Calls { get; } = [];

    public int ExitCode { get; set; } = 0;

    /// <summary>When set, RunElevated returns null once (simulating a cancelled UAC prompt).</summary>
    public bool CancelUacNext { get; set; }

    public int? RunElevated(string arguments)
    {
        if (CancelUacNext)
        {
            CancelUacNext = false;
            return null;
        }

        Calls.Add(arguments);
        return ExitCode;
    }
}

public class KeepAwakeControllerTests : IDisposable
{
    private readonly string _stateDir = Path.Combine(Path.GetTempPath(), $"watchme-ka-{Guid.NewGuid():N}");

    private (KeepAwakeController Controller, FakeExecutionState Exec, FakePowerRunner Power) Make()
    {
        var exec = new FakeExecutionState();
        var power = new FakePowerRunner();
        return (new KeepAwakeController(exec, power, _stateDir), exec, power);
    }

    [Fact]
    public void Coffee_TogglesExecutionState()
    {
        var (controller, exec, _) = Make();
        Assert.False(controller.CoffeeActive);

        controller.ToggleCoffee();
        Assert.True(controller.CoffeeActive);
        Assert.True(exec.KeepAwake);

        controller.ToggleCoffee();
        Assert.False(controller.CoffeeActive);
        Assert.False(exec.KeepAwake);
    }

    [Fact]
    public void SetLidNeverSleep_True_RunsPowercfg_AndWritesStateFile()
    {
        var (controller, _, power) = Make();
        Assert.True(controller.SetLidNeverSleep(true));

        Assert.Equal(3, power.Calls.Count);
        Assert.Contains(power.Calls, c => c.Contains("LIDACTION 0") && c.StartsWith("/setacvalueindex"));
        Assert.Contains(power.Calls, c => c.Contains("LIDACTION 0") && c.StartsWith("/setdcvalueindex"));
        Assert.Contains(power.Calls, c => c == "/setactive SCHEME_CURRENT");
        Assert.True(controller.LidNeverSleepActive);
    }

    [Fact]
    public void SetLidNeverSleep_False_RestoresLidSleep_AndRemovesStateFile()
    {
        var (controller, _, power) = Make();
        Assert.True(controller.SetLidNeverSleep(true));
        Assert.True(controller.SetLidNeverSleep(false));

        Assert.Contains(power.Calls, c => c.Contains("LIDACTION 1"));
        Assert.False(controller.LidNeverSleepActive);
    }

    [Fact]
    public void SetLidNeverSleep_UacCancelled_ReturnsFalse_WithoutStateFile()
    {
        var (controller, _, power) = Make();
        power.CancelUacNext = true;

        Assert.False(controller.SetLidNeverSleep(true));
        Assert.False(controller.LidNeverSleepActive);
    }

    [Fact]
    public void SetLidNeverSleep_PowercfgFailure_ReturnsFalse()
    {
        var (controller, _, power) = Make();
        power.ExitCode = -1;

        Assert.False(controller.SetLidNeverSleep(true));
        Assert.False(controller.LidNeverSleepActive);
    }

    [Fact]
    public void RecoverOnStartup_RestoresLidSleep_AfterCrash()
    {
        // simulate a crash: state file exists, but Shutdown() never ran
        Directory.CreateDirectory(_stateDir);
        File.WriteAllText(Path.Combine(_stateDir, "lid-never-sleep.active"), "stale");

        var (controller, _, power) = Make();
        Assert.True(controller.LidNeverSleepActive);

        controller.RecoverOnStartup();
        Assert.False(controller.LidNeverSleepActive);
        Assert.Contains(power.Calls, c => c.Contains("LIDACTION 1"));
    }

    [Fact]
    public void Shutdown_RestoresEverything()
    {
        var (controller, exec, power) = Make();
        controller.SetCoffee(true);
        Assert.True(controller.SetLidNeverSleep(true));

        controller.Shutdown();
        Assert.False(exec.KeepAwake);
        Assert.False(controller.LidNeverSleepActive);
        Assert.Contains(power.Calls, c => c.Contains("LIDACTION 1"));
    }

    public void Dispose()
    {
        if (Directory.Exists(_stateDir))
            Directory.Delete(_stateDir, recursive: true);
    }
}

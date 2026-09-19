using System.IO;
using System.Threading;
using System.Windows;
using WatchMe.KeepAwake;
using WatchMe.Notch;
using WatchMe.Settings;
using WatchMe.Themes;

namespace WatchMe;

public partial class App : Application
{
    private static Mutex? _singleInstance;
    private NotchController? _controller;

    protected override void OnStartup(StartupEventArgs e)
    {
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            LogCrash("AppDomain", args.ExceptionObject as Exception);
        DispatcherUnhandledException += (_, args) =>
        {
            LogCrash("Dispatcher", args.Exception);
            MessageBox.Show($"发生未处理的错误：\n{args.Exception.Message}", "WatchMe",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            args.Handled = true;
        };

        try
        {
            StartupCore(e);
        }
        catch (Exception ex)
        {
            LogCrash("Startup", ex);
            throw;
        }
    }

    private void StartupCore(StartupEventArgs e)
    {
        _singleInstance = new Mutex(initiallyOwned: true, $"{AppPaths.BundleId}.SingleInstance", out var createdNew);
        if (!createdNew)
        {
            MessageBox.Show("WatchMe 已在运行（可在系统托盘中找到它）。", "WatchMe",
                MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown();
            return;
        }

        base.OnStartup(e);

        var settings = SettingsStore.Default().Load();
        ThemeManager.Apply(settings.Theme);

        _controller = new NotchController(settings,
            KeepAwakeController.Default(new ExecutionStateApi(), new PowerProcessRunner()));
        Exit += (_, _) => _controller.Shutdown();
    }

    private static void LogCrash(string source, Exception? ex)
    {
        try
        {
            Directory.CreateDirectory(AppPaths.DataDir);
            File.AppendAllText(Path.Combine(AppPaths.DataDir, "crash.log"),
                $"[{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss}] {source}: {ex}\n\n");
        }
        catch
        {
            // Logging must never throw.
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _controller?.Dispose();
        _singleInstance?.ReleaseMutex();
        base.OnExit(e);
    }
}

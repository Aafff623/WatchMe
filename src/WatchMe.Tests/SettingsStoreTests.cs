using System.IO;
using WatchMe.Core;
using WatchMe.Settings;

namespace WatchMe.Tests;

public class SettingsStoreTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"watchme-settings-{Guid.NewGuid():N}.json");

    [Fact]
    public void Load_MissingFile_ReturnsDefaults()
    {
        var settings = new SettingsStore(_path).Load();
        Assert.Equal(TriggerMode.Hover, settings.Trigger);
        Assert.Equal(ThemeMode.Dark, settings.Theme);
        Assert.True(settings.ClipboardHistoryEnabled);
        Assert.Equal("Ctrl+Alt+W", settings.HotkeyDisplay);
    }

    [Fact]
    public void Save_And_Reload_RoundTrips()
    {
        var store = new SettingsStore(_path);
        var settings = new AppSettings
        {
            Trigger = TriggerMode.Click,
            Theme = ThemeMode.FollowSystem,
            HotkeyDisplay = "Ctrl+Shift+Space",
            PreferredScreenDeviceName = @"\\.\DISPLAY2",
            StartWithSystem = true,
            ClipboardHistoryMaxEntries = 500,
        };
        store.Save(settings);

        var loaded = new SettingsStore(_path).Load();
        Assert.Equal(TriggerMode.Click, loaded.Trigger);
        Assert.Equal(ThemeMode.FollowSystem, loaded.Theme);
        Assert.Equal("Ctrl+Shift+Space", loaded.HotkeyDisplay);
        Assert.Equal(@"\\.\DISPLAY2", loaded.PreferredScreenDeviceName);
        Assert.True(loaded.StartWithSystem);
        Assert.Equal(500, loaded.ClipboardHistoryMaxEntries);
    }

    [Fact]
    public void Load_GuardsCorruptedCap()
    {
        var store = new SettingsStore(_path);
        store.Save(new AppSettings { ClipboardHistoryMaxEntries = 99 });
        var loaded = store.Load();
        Assert.Equal(99, loaded.ClipboardHistoryMaxEntries);

        var hostile = new AppSettings { ClipboardHistoryMaxEntries = 999999 };
        store.Save(hostile);
        Assert.Equal(200, new SettingsStore(_path).Load().ClipboardHistoryMaxEntries);
    }

    [Theory]
    [InlineData("Ctrl+Alt+W", true)]
    [InlineData("Ctrl+Shift+Space", true)]
    [InlineData("Ctrl+Alt+Shift+Win+F9", true)]
    [InlineData("Ctrl+Alt+1", true)]
    [InlineData("", false)]
    [InlineData("Ctrl", false)]
    [InlineData("Ctrl++", false)]
    [InlineData("A+B", false)]
    [InlineData("Ctrl+Alt+X+Y", false)]
    public void HotkeyPattern_ParseValidity(string input, bool expected)
    {
        Assert.Equal(expected, HotkeyPattern.TryParse(input, out var modifiers, out var vk));
        if (expected)
        {
            Assert.NotEqual(HotkeyModifiers.None, modifiers);
            Assert.NotEqual(0u, vk);
        }
    }

    [Fact]
    public void HotkeyPattern_ProducesWin32ModifierBits()
    {
        Assert.True(HotkeyPattern.TryParse("Ctrl+Alt+W", out var modifiers, out var vk));
        Assert.Equal(HotkeyModifiers.Control | HotkeyModifiers.Alt, modifiers);
        Assert.Equal('W', vk);
    }

    public void Dispose()
    {
        if (File.Exists(_path))
            File.Delete(_path);
    }
}

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
        };
        store.Save(settings);

        var loaded = new SettingsStore(_path).Load();
        Assert.Equal(TriggerMode.Click, loaded.Trigger);
        Assert.Equal(ThemeMode.FollowSystem, loaded.Theme);
        Assert.Equal("Ctrl+Shift+Space", loaded.HotkeyDisplay);
        Assert.Equal(@"\\.\DISPLAY2", loaded.PreferredScreenDeviceName);
        Assert.True(loaded.StartWithSystem);
    }

    [Fact]
    public void Load_ToleratesLegacyJson_WithRemovedFields()
    {
        const string legacyJson = """
            {
              "trigger": 1,
              "theme": 2,
              "hotkeyDisplay": "Ctrl+Alt+J",
              "clipboardHistoryEnabled": true,
              "clipboardHistoryMaxEntries": 300
            }
            """;
        File.WriteAllText(_path, legacyJson);

        var loaded = new SettingsStore(_path).Load();
        Assert.Equal(TriggerMode.Click, loaded.Trigger);
        Assert.Equal(ThemeMode.Dark, loaded.Theme);
        Assert.Equal("Ctrl+Alt+J", loaded.HotkeyDisplay);
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

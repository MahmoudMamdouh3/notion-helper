using System.IO;
using System.Text.Json;
using NotionHelper.Models;
using NotionHelper.Services;

namespace NotionHelper.Tests;

public sealed class AppSettingsTests
{
    [Fact]
    public void SettingsStore_UsesTheDocumentedDefaultWhenNoFileExists()
    {
        var path = CreateTemporarySettingsPath();

        var settings = new AppSettingsStore(path).Load();

        Assert.Equal("qwen2.5:7b", settings.Model);
        Assert.Equal(ShortcutPreset.ControlShiftSpace, settings.Shortcut);
        Assert.False(File.Exists(path));
    }

    [Fact]
    public void SettingsStore_RoundTripsModelAndShortcutInLocalJson()
    {
        var path = CreateTemporarySettingsPath();
        var store = new AppSettingsStore(path);
        var settings = new AppSettings
        {
            Model = "qwen2.5:7b",
            Shortcut = ShortcutPreset.ControlAltSpace
        };

        try
        {
            store.Save(settings);

            Assert.Equal(settings, store.Load());
            Assert.Contains("\"ControlAltSpace\"", File.ReadAllText(path), StringComparison.Ordinal);
        }
        finally
        {
            DeleteTemporarySettings(path);
        }
    }

    [Theory]
    [InlineData("")]
    [InlineData("model with spaces")]
    [InlineData("https://example.com/api")]
    [InlineData("../remote-model")]
    public void SettingsStore_RejectsUnsafeOrInvalidModelNames(string model)
    {
        var settings = new AppSettings { Model = model };

        Assert.Throws<InvalidDataException>(() => AppSettingsStore.Validate(settings));
    }

    [Fact]
    public void SettingsStore_RejectsUndefinedShortcutPreset()
    {
        var settings = new AppSettings { Shortcut = (ShortcutPreset)99 };

        Assert.Throws<InvalidDataException>(() => AppSettingsStore.Validate(settings));
    }

    [Fact]
    public void SettingsStore_RejectsUnknownSettingsEnumInsteadOfSilentlyDefaulting()
    {
        var path = CreateTemporarySettingsPath();
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, """{"model":"qwen2.5:3b","shortcut":"Unknown"}""");

            Assert.Throws<InvalidDataException>(() => new AppSettingsStore(path).Load());
        }
        finally
        {
            DeleteTemporarySettings(path);
        }
    }

    [Fact]
    public void SettingsStore_LoadsCaseInsensitivePropertyNames()
    {
        var path = CreateTemporarySettingsPath();
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, """{"model":"qwen2.5:3b","shortcut":"ControlAltSpace"}""");

            var settings = new AppSettingsStore(path).Load();

            Assert.Equal("qwen2.5:3b", settings.Model);
            Assert.Equal(ShortcutPreset.ControlAltSpace, settings.Shortcut);
        }
        finally
        {
            DeleteTemporarySettings(path);
        }
    }

    [Theory]
    [InlineData(ShortcutPreset.ControlShiftSpace, 0x0006u, 0x20u, "Ctrl+Shift+Space")]
    [InlineData(ShortcutPreset.ControlAltSpace, 0x0003u, 0x20u, "Ctrl+Alt+Space")]
    [InlineData(ShortcutPreset.ControlShiftN, 0x0006u, 0x4Eu, "Ctrl+Shift+N")]
    public void ShortcutPresets_ReturnExpectedWindowsBindings(
        ShortcutPreset preset,
        uint modifiers,
        uint virtualKey,
        string displayName)
    {
        var binding = ShortcutPresets.GetBinding(preset);

        Assert.Equal(modifiers, binding.Modifiers);
        Assert.Equal(virtualKey, binding.VirtualKey);
        Assert.Equal(displayName, binding.DisplayName);
    }

    private static string CreateTemporarySettingsPath() =>
        Path.Combine(Path.GetTempPath(), $"notion-helper-{Guid.NewGuid():N}", "settings.json");

    private static void DeleteTemporarySettings(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }

        var directory = Path.GetDirectoryName(path);
        if (directory is not null && Directory.Exists(directory))
        {
            Directory.Delete(directory);
        }
    }
}

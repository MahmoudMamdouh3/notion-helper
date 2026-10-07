namespace NotionHelper.Models;

public enum ShortcutPreset
{
    ControlShiftSpace,
    ControlAltSpace,
    ControlShiftN
}

public sealed record AppSettings
{
    public const string DefaultModel = "qwen2.5:7b";

    public string Model { get; init; } = DefaultModel;
    public ShortcutPreset Shortcut { get; init; } = ShortcutPreset.ControlShiftSpace;
}

public readonly record struct ShortcutBinding(uint Modifiers, uint VirtualKey, string DisplayName);

public static class ShortcutPresets
{
    public static ShortcutBinding GetBinding(ShortcutPreset preset) => preset switch
    {
        ShortcutPreset.ControlShiftSpace => new(0x0002 | 0x0004, 0x20, "Ctrl+Shift+Space"),
        ShortcutPreset.ControlAltSpace => new(0x0002 | 0x0001, 0x20, "Ctrl+Alt+Space"),
        ShortcutPreset.ControlShiftN => new(0x0002 | 0x0004, 0x4E, "Ctrl+Shift+N"),
        _ => throw new ArgumentOutOfRangeException(nameof(preset), preset, "Unsupported shortcut preset.")
    };
}

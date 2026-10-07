using System.Runtime.InteropServices;

namespace NotionHelper.Interop;

internal static class KeyboardInput
{
    private const uint InputKeyboard = 1;
    private const uint KeyeventfKeyup = 0x0002;
    private const ushort VkControl = 0x11;
    private const ushort VkC = 0x43;
    private const ushort VkV = 0x56;

    internal static void SendCopy() => SendChord(VkC);

    internal static void SendPaste() => SendChord(VkV);

    private static void SendChord(ushort key)
    {
        var inputs = new[]
        {
            CreateKey(VkControl, false),
            CreateKey(key, false),
            CreateKey(key, true),
            CreateKey(VkControl, true)
        };

        var sent = NativeMethods.SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<Input>());
        if (sent != inputs.Length)
        {
            throw new InvalidOperationException("Windows could not send the copy/paste keyboard shortcut.");
        }
    }

    private static Input CreateKey(ushort key, bool keyUp) => new()
    {
        Type = InputKeyboard,
        Data = new InputUnion
        {
            Keyboard = new KeyboardInputData
            {
                VirtualKey = key,
                Flags = keyUp ? KeyeventfKeyup : 0
            }
        }
    };
}

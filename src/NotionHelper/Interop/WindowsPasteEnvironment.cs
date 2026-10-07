using NotionHelper.Models;
using NotionHelper.Services;
using WpfClipboard = System.Windows.Clipboard;
using WpfDataFormats = System.Windows.DataFormats;
using WpfDataObject = System.Windows.DataObject;

namespace NotionHelper.Interop;

internal sealed class WindowsPasteEnvironment : IPasteEnvironment, ISelectionCaptureEnvironment
{
    public nint ForegroundWindow => NativeMethods.GetForegroundWindow();
    public uint ClipboardSequenceNumber => NativeMethods.GetClipboardSequenceNumber();

    public bool IsTargetCurrent(WindowTargetSnapshot target, out string error)
    {
        if (!target.IsValid || !NativeMethods.IsWindow(target.Handle))
        {
            error = "The captured window has closed.";
            return false;
        }

        if (NativeMethods.GetWindowThreadProcessId(target.Handle, out var processId) == 0 ||
            !target.Matches(target.Handle, processId))
        {
            error = "The captured window handle no longer belongs to the original application.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    public void SetClipboard(ImprovementResult result)
    {
        var data = new WpfDataObject();
        data.SetData(WpfDataFormats.Html, HtmlClipboardFormatter.ToClipboardHtml(result));
        data.SetData(WpfDataFormats.UnicodeText, result.ToPlainText());
        WpfClipboard.SetDataObject(data, true);
    }

    public bool TryFocus(nint targetWindow) => NativeMethods.SetForegroundWindow(targetWindow);

    public async Task WaitBeforePasteAsync() => await Task.Delay(180);

    public async Task WaitBeforeCopyAsync() => await Task.Delay(180);

    public async Task WaitForClipboardAsync() => await Task.Delay(250);

    public void SendCopy() => KeyboardInput.SendCopy();

    public string ReadUnicodeText() =>
        WpfClipboard.ContainsText()
            ? WpfClipboard.GetText(System.Windows.TextDataFormat.UnicodeText)
            : string.Empty;

    public void SendPaste() => KeyboardInput.SendPaste();
}

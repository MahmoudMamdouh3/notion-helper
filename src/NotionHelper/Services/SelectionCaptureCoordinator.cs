using NotionHelper.Models;

namespace NotionHelper.Services;

internal interface ISelectionCaptureEnvironment
{
    bool IsTargetCurrent(WindowTargetSnapshot target, out string error);
    nint ForegroundWindow { get; }
    uint ClipboardSequenceNumber { get; }
    Task WaitBeforeCopyAsync();
    void SendCopy();
    Task WaitForClipboardAsync();
    string ReadUnicodeText();
}

internal enum SelectionCaptureDisposition
{
    Captured,
    TargetChanged,
    FocusChanged,
    ClipboardUnchanged,
    ClipboardChanged,
    EmptyText
}

internal sealed record SelectionCaptureOutcome(
    SelectionCaptureDisposition Disposition,
    string? Text = null,
    string? Error = null);

internal sealed class SelectionCaptureCoordinator(ISelectionCaptureEnvironment environment)
{
    internal async Task<SelectionCaptureOutcome> CaptureAsync(
        WindowTargetSnapshot target,
        uint initialClipboardSequence)
    {
        await environment.WaitBeforeCopyAsync();
        if (!environment.IsTargetCurrent(target, out var error))
        {
            return new SelectionCaptureOutcome(SelectionCaptureDisposition.TargetChanged, Error: error);
        }

        if (environment.ForegroundWindow != target.Handle)
        {
            return new SelectionCaptureOutcome(SelectionCaptureDisposition.FocusChanged);
        }

        environment.SendCopy();
        await environment.WaitForClipboardAsync();

        if (!environment.IsTargetCurrent(target, out error))
        {
            return new SelectionCaptureOutcome(SelectionCaptureDisposition.TargetChanged, Error: error);
        }

        if (environment.ForegroundWindow != target.Handle)
        {
            return new SelectionCaptureOutcome(SelectionCaptureDisposition.FocusChanged);
        }

        var copiedClipboardSequence = environment.ClipboardSequenceNumber;
        if (copiedClipboardSequence == initialClipboardSequence)
        {
            return new SelectionCaptureOutcome(SelectionCaptureDisposition.ClipboardUnchanged);
        }

        var text = environment.ReadUnicodeText();
        if (environment.ClipboardSequenceNumber != copiedClipboardSequence)
        {
            return new SelectionCaptureOutcome(SelectionCaptureDisposition.ClipboardChanged);
        }

        return string.IsNullOrWhiteSpace(text)
            ? new SelectionCaptureOutcome(SelectionCaptureDisposition.EmptyText)
            : new SelectionCaptureOutcome(SelectionCaptureDisposition.Captured, Text: text);
    }
}

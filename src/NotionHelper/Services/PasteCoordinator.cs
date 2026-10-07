using NotionHelper.Models;

namespace NotionHelper.Services;

internal interface IPasteEnvironment
{
    bool IsTargetCurrent(WindowTargetSnapshot target, out string error);
    void SetClipboard(ImprovementResult result);
    bool TryFocus(nint targetWindow);
    nint ForegroundWindow { get; }
    Task WaitBeforePasteAsync();
    void SendPaste();
}

internal enum PasteDisposition
{
    Pasted,
    TargetUnavailable,
    FocusFailed,
    FocusChanged
}

internal sealed record PasteOutcome(PasteDisposition Disposition, string Message);

internal sealed class PasteCoordinator(IPasteEnvironment environment)
{
    internal async Task<PasteOutcome> ApplyAsync(
        ImprovementResult result,
        WindowTargetSnapshot target)
    {
        if (!environment.IsTargetCurrent(target, out var error))
        {
            return new PasteOutcome(
                PasteDisposition.TargetUnavailable,
                $"Apply is blocked because the original target is no longer available: {error} Capture the selection again.");
        }

        environment.SetClipboard(result);
        if (!environment.TryFocus(target.Handle))
        {
            return new PasteOutcome(
                PasteDisposition.FocusFailed,
                $"Could not return focus to {target.ProcessName}. The preview is on the clipboard; paste it manually.");
        }

        await environment.WaitBeforePasteAsync();
        if (environment.ForegroundWindow != target.Handle)
        {
            return new PasteOutcome(
                PasteDisposition.FocusChanged,
                "Focus returned to a different window. The preview remains on the clipboard; paste it manually.");
        }

        if (!environment.IsTargetCurrent(target, out error))
        {
            return new PasteOutcome(
                PasteDisposition.FocusChanged,
                $"The original window changed. The preview remains on the clipboard; paste it manually. {error}");
        }

        environment.SendPaste();
        return new PasteOutcome(
            PasteDisposition.Pasted,
            $"Paste sent to {target.ProcessName}. Verify the result in the target app.");
    }
}

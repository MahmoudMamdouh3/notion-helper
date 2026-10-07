namespace NotionHelper.Models;

public readonly record struct WindowTargetSnapshot(nint Handle, uint ProcessId, string ProcessName)
{
    public bool IsValid => Handle != nint.Zero && ProcessId != 0 && !string.IsNullOrWhiteSpace(ProcessName);

    public bool Matches(nint handle, uint processId) =>
        IsValid && handle == Handle && processId == ProcessId;
}

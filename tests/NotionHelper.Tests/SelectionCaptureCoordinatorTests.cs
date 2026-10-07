using NotionHelper.Models;
using NotionHelper.Services;

namespace NotionHelper.Tests;

public sealed class SelectionCaptureCoordinatorTests
{
    private static readonly WindowTargetSnapshot Target = new((nint)1234, 42, "TestEditor");

    [Fact]
    public async Task CaptureAsync_ReadsSelectionOnlyAfterTargetChecksAndStableClipboard()
    {
        var environment = new FakeCaptureEnvironment
        {
            TargetChecks = [true, true],
            ForegroundWindows = [Target.Handle, Target.Handle],
            ClipboardSequences = [8, 8],
            ReadText = "Captured text."
        };

        var outcome = await new SelectionCaptureCoordinator(environment).CaptureAsync(Target, 7);

        Assert.Equal(SelectionCaptureDisposition.Captured, outcome.Disposition);
        Assert.Equal("Captured text.", outcome.Text);
        Assert.Equal(
            ["delay-before-copy", "validate", "foreground", "copy", "delay-for-clipboard",
                "validate", "foreground", "clipboard-sequence", "read", "clipboard-sequence"],
            environment.Calls);
    }

    [Fact]
    public async Task CaptureAsync_DoesNotCopyWhenTargetChangesBeforeCopy()
    {
        var environment = new FakeCaptureEnvironment { TargetChecks = [false] };

        var outcome = await new SelectionCaptureCoordinator(environment).CaptureAsync(Target, 7);

        Assert.Equal(SelectionCaptureDisposition.TargetChanged, outcome.Disposition);
        Assert.DoesNotContain("copy", environment.Calls);
        Assert.DoesNotContain("read", environment.Calls);
    }

    [Fact]
    public async Task CaptureAsync_DoesNotCopyWhenForegroundChangesBeforeCopy()
    {
        var environment = new FakeCaptureEnvironment
        {
            TargetChecks = [true],
            ForegroundWindows = [(nint)5678]
        };

        var outcome = await new SelectionCaptureCoordinator(environment).CaptureAsync(Target, 7);

        Assert.Equal(SelectionCaptureDisposition.FocusChanged, outcome.Disposition);
        Assert.DoesNotContain("copy", environment.Calls);
        Assert.DoesNotContain("read", environment.Calls);
    }

    [Fact]
    public async Task CaptureAsync_DoesNotReadWhenTargetChangesAfterCopy()
    {
        var environment = new FakeCaptureEnvironment
        {
            TargetChecks = [true, false],
            ForegroundWindows = [Target.Handle]
        };

        var outcome = await new SelectionCaptureCoordinator(environment).CaptureAsync(Target, 7);

        Assert.Equal(SelectionCaptureDisposition.TargetChanged, outcome.Disposition);
        Assert.Contains("copy", environment.Calls);
        Assert.DoesNotContain("read", environment.Calls);
    }

    [Fact]
    public async Task CaptureAsync_DoesNotReadWhenForegroundChangesAfterCopy()
    {
        var environment = new FakeCaptureEnvironment
        {
            TargetChecks = [true, true],
            ForegroundWindows = [Target.Handle, (nint)5678]
        };

        var outcome = await new SelectionCaptureCoordinator(environment).CaptureAsync(Target, 7);

        Assert.Equal(SelectionCaptureDisposition.FocusChanged, outcome.Disposition);
        Assert.Contains("copy", environment.Calls);
        Assert.DoesNotContain("read", environment.Calls);
    }

    [Fact]
    public async Task CaptureAsync_DoesNotReadWhenClipboardSequenceDidNotChange()
    {
        var environment = new FakeCaptureEnvironment
        {
            TargetChecks = [true, true],
            ForegroundWindows = [Target.Handle, Target.Handle],
            ClipboardSequences = [7]
        };

        var outcome = await new SelectionCaptureCoordinator(environment).CaptureAsync(Target, 7);

        Assert.Equal(SelectionCaptureDisposition.ClipboardUnchanged, outcome.Disposition);
        Assert.DoesNotContain("read", environment.Calls);
    }

    [Fact]
    public async Task CaptureAsync_RejectsClipboardChangeWhileReading()
    {
        var environment = new FakeCaptureEnvironment
        {
            TargetChecks = [true, true],
            ForegroundWindows = [Target.Handle, Target.Handle],
            ClipboardSequences = [8, 9],
            ReadText = "Possibly unrelated text."
        };

        var outcome = await new SelectionCaptureCoordinator(environment).CaptureAsync(Target, 7);

        Assert.Equal(SelectionCaptureDisposition.ClipboardChanged, outcome.Disposition);
        Assert.Null(outcome.Text);
    }

    private sealed class FakeCaptureEnvironment : ISelectionCaptureEnvironment
    {
        private int _targetCheckIndex;
        private int _foregroundIndex;
        private int _clipboardSequenceIndex;

        public List<string> Calls { get; } = [];
        public bool[] TargetChecks { get; init; } = [];
        public nint[] ForegroundWindows { get; init; } = [];
        public uint[] ClipboardSequences { get; init; } = [];
        public string ReadText { get; init; } = string.Empty;

        public nint ForegroundWindow
        {
            get
            {
                Calls.Add("foreground");
                return ForegroundWindows[Math.Min(_foregroundIndex++, ForegroundWindows.Length - 1)];
            }
        }

        public uint ClipboardSequenceNumber
        {
            get
            {
                Calls.Add("clipboard-sequence");
                return ClipboardSequences[Math.Min(_clipboardSequenceIndex++, ClipboardSequences.Length - 1)];
            }
        }

        public bool IsTargetCurrent(WindowTargetSnapshot target, out string error)
        {
            Calls.Add("validate");
            var isValid = TargetChecks[_targetCheckIndex++];
            error = isValid ? string.Empty : "test target changed";
            return isValid;
        }

        public Task WaitBeforeCopyAsync()
        {
            Calls.Add("delay-before-copy");
            return Task.CompletedTask;
        }

        public void SendCopy() => Calls.Add("copy");

        public Task WaitForClipboardAsync()
        {
            Calls.Add("delay-for-clipboard");
            return Task.CompletedTask;
        }

        public string ReadUnicodeText()
        {
            Calls.Add("read");
            return ReadText;
        }
    }
}

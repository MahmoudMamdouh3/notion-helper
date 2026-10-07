using NotionHelper.Models;
using NotionHelper.Services;

namespace NotionHelper.Tests;

public sealed class PasteCoordinatorTests
{
    private static readonly WindowTargetSnapshot Target = new((nint)1234, 42, "TestEditor");
    private static readonly ImprovementResult Result = new()
    {
        Blocks = [new ContentBlock { Type = "paragraph", Text = "Reviewed output." }]
    };

    [Fact]
    public async Task ApplyAsync_BlocksClipboardWriteWhenTargetIsInvalid()
    {
        var environment = new FakePasteEnvironment { TargetChecks = [false] };

        var outcome = await new PasteCoordinator(environment).ApplyAsync(Result, Target);

        Assert.Equal(PasteDisposition.TargetUnavailable, outcome.Disposition);
        Assert.DoesNotContain("clipboard", environment.Calls);
        Assert.DoesNotContain("focus", environment.Calls);
        Assert.DoesNotContain("paste", environment.Calls);
    }

    [Fact]
    public async Task ApplyAsync_LeavesPreviewForManualPasteWhenFocusFails()
    {
        var environment = new FakePasteEnvironment
        {
            TargetChecks = [true],
            FocusSucceeds = false
        };

        var outcome = await new PasteCoordinator(environment).ApplyAsync(Result, Target);

        Assert.Equal(PasteDisposition.FocusFailed, outcome.Disposition);
        Assert.Equal(["validate", "clipboard", "focus"], environment.Calls);
        Assert.Contains("paste it manually", outcome.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ApplyAsync_DoesNotSendPasteIfForegroundChangesDuringHandoff()
    {
        var environment = new FakePasteEnvironment
        {
            TargetChecks = [true],
            ForegroundWindowValue = (nint)5678
        };

        var outcome = await new PasteCoordinator(environment).ApplyAsync(Result, Target);

        Assert.Equal(PasteDisposition.FocusChanged, outcome.Disposition);
        Assert.Contains("clipboard", environment.Calls);
        Assert.DoesNotContain("paste", environment.Calls);
    }

    [Fact]
    public async Task ApplyAsync_DoesNotSendPasteIfWindowIdentityChangesDuringHandoff()
    {
        var environment = new FakePasteEnvironment { TargetChecks = [true, false] };

        var outcome = await new PasteCoordinator(environment).ApplyAsync(Result, Target);

        Assert.Equal(PasteDisposition.FocusChanged, outcome.Disposition);
        Assert.Equal(2, environment.TargetCheckCount);
        Assert.DoesNotContain("paste", environment.Calls);
    }

    [Fact]
    public async Task ApplyAsync_SendsPasteOnlyAfterBothIdentityChecksAndFocusHandoff()
    {
        var environment = new FakePasteEnvironment { TargetChecks = [true, true] };

        var outcome = await new PasteCoordinator(environment).ApplyAsync(Result, Target);

        Assert.Equal(PasteDisposition.Pasted, outcome.Disposition);
        Assert.Equal(
            ["validate", "clipboard", "focus", "delay", "foreground", "validate", "paste"],
            environment.Calls);
    }

    private sealed class FakePasteEnvironment : IPasteEnvironment
    {
        private int _targetCheckIndex;

        public List<string> Calls { get; } = [];
        public bool[] TargetChecks { get; init; } = [];
        public bool FocusSucceeds { get; init; } = true;
        public nint ForegroundWindowValue { get; init; } = Target.Handle;
        public int TargetCheckCount => _targetCheckIndex;

        public nint ForegroundWindow
        {
            get
            {
                Calls.Add("foreground");
                return ForegroundWindowValue;
            }
        }

        public bool IsTargetCurrent(WindowTargetSnapshot target, out string error)
        {
            Calls.Add("validate");
            var isValid = _targetCheckIndex < TargetChecks.Length && TargetChecks[_targetCheckIndex++];
            error = isValid ? string.Empty : "test target changed";
            return isValid;
        }

        public void SetClipboard(ImprovementResult result) => Calls.Add("clipboard");

        public bool TryFocus(nint targetWindow)
        {
            Calls.Add("focus");
            return FocusSucceeds;
        }

        public Task WaitBeforePasteAsync()
        {
            Calls.Add("delay");
            return Task.CompletedTask;
        }

        public void SendPaste() => Calls.Add("paste");
    }
}

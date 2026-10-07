using NotionHelper.Models;

namespace NotionHelper.Tests;

public sealed class WindowTargetSnapshotTests
{
    [Fact]
    public void Matches_AcceptsOnlyTheCapturedWindowAndProcess()
    {
        var target = new WindowTargetSnapshot((nint)1234, 42, "Notion");

        Assert.True(target.IsValid);
        Assert.True(target.Matches((nint)1234, 42));
        Assert.False(target.Matches((nint)1235, 42));
        Assert.False(target.Matches((nint)1234, 43));
    }

    [Fact]
    public void DefaultSnapshotIsInvalid()
    {
        Assert.False(default(WindowTargetSnapshot).IsValid);
        Assert.False(default(WindowTargetSnapshot).Matches((nint)1234, 42));
    }
}

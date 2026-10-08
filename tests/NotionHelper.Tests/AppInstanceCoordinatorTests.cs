using NotionHelper.Services;

namespace NotionHelper.Tests;

public sealed class AppInstanceCoordinatorTests
{
    [Fact]
    public void SecondInstanceSignalsPrimaryForActivation()
    {
        var instanceName = Guid.NewGuid().ToString("N");
        using var primary = new AppInstanceCoordinator(instanceName);
        Assert.True(primary.IsPrimary);

        using var activated = new ManualResetEventSlim();
        primary.StartListening(() => activated.Set());
        var isSecondaryPrimary = RunOnSeparateThread(() =>
        {
            using var secondary = new AppInstanceCoordinator(instanceName);
            return secondary.IsPrimary;
        });

        Assert.False(isSecondaryPrimary);
        Assert.True(activated.Wait(TimeSpan.FromSeconds(2)));
    }

    [Fact]
    public void DisposingPrimaryReleasesInstanceOwnership()
    {
        var instanceName = Guid.NewGuid().ToString("N");
        var first = new AppInstanceCoordinator(instanceName);
        Assert.True(first.IsPrimary);

        first.Dispose();

        using var replacement = new AppInstanceCoordinator(instanceName);
        Assert.True(replacement.IsPrimary);
    }

    [Fact]
    public void SecondaryCannotStartActivationListener()
    {
        var instanceName = Guid.NewGuid().ToString("N");
        using var primary = new AppInstanceCoordinator(instanceName);

        var exception = RunOnSeparateThread(() =>
        {
            using var secondary = new AppInstanceCoordinator(instanceName);
            return Record.Exception(() => secondary.StartListening(() => { }));
        });

        var invalidOperation = Assert.IsType<InvalidOperationException>(exception);
        Assert.Contains("Only the primary", invalidOperation.Message, StringComparison.Ordinal);
    }

    private static T RunOnSeparateThread<T>(Func<T> action)
    {
        T? result = default;
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                result = action();
            }
            catch (Exception exception)
            {
                failure = exception;
            }
        });
        thread.Start();
        thread.Join();

        if (failure is not null)
        {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
        }

        return result!;
    }
}

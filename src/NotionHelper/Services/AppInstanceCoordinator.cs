using System.IO;
using System.Threading;

namespace NotionHelper.Services;

internal sealed class AppInstanceCoordinator : IDisposable
{
    private static readonly TimeSpan ActivationSignalTimeout = TimeSpan.FromSeconds(2);
    private readonly Mutex _instanceMutex;
    private readonly EventWaitHandle? _activationEvent;
    private readonly CancellationTokenSource _stop = new();
    private Task? _listener;
    private bool _ownsMutex;
    private bool _disposed;

    internal AppInstanceCoordinator(string instanceName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(instanceName);

        var objectPrefix = $@"Local\NotionHelper-{instanceName}";
        _instanceMutex = new Mutex(initiallyOwned: false, $"{objectPrefix}-Mutex");
        try
        {
            try
            {
                _ownsMutex = _instanceMutex.WaitOne(0);
            }
            catch (AbandonedMutexException)
            {
                _ownsMutex = true;
            }

            IsPrimary = _ownsMutex;
            if (IsPrimary)
            {
                _activationEvent = new EventWaitHandle(
                    initialState: false,
                    EventResetMode.AutoReset,
                    $"{objectPrefix}-Activate");
            }
            else
            {
                SignalPrimary($"{objectPrefix}-Activate");
            }
        }
        catch
        {
            if (_ownsMutex)
            {
                _instanceMutex.ReleaseMutex();
                _ownsMutex = false;
            }

            _instanceMutex.Dispose();
            throw;
        }
    }

    internal bool IsPrimary { get; }

    internal void StartListening(Action onActivation)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(onActivation);
        if (!IsPrimary)
        {
            throw new InvalidOperationException("Only the primary application instance can listen for activation.");
        }

        if (_listener is not null)
        {
            throw new InvalidOperationException("The primary application instance is already listening.");
        }

        var activationEvent = _activationEvent
            ?? throw new InvalidOperationException("The primary activation event is unavailable.");
        _listener = Task.Run(() =>
        {
            while (!_stop.IsCancellationRequested)
            {
                activationEvent.WaitOne();
                if (!_stop.IsCancellationRequested)
                {
                    onActivation();
                }
            }
        });
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _stop.Cancel();
        _activationEvent?.Set();
        _listener?.GetAwaiter().GetResult();
        _activationEvent?.Dispose();
        _stop.Dispose();
        if (_ownsMutex)
        {
            _instanceMutex.ReleaseMutex();
            _ownsMutex = false;
        }

        _instanceMutex.Dispose();
    }

    private static void SignalPrimary(string eventName)
    {
        var deadline = DateTime.UtcNow + ActivationSignalTimeout;
        while (DateTime.UtcNow < deadline)
        {
            try
            {
                using var activationEvent = EventWaitHandle.OpenExisting(eventName);
                activationEvent.Set();
                return;
            }
            catch (WaitHandleCannotBeOpenedException)
            {
                Thread.Sleep(25);
            }
        }

        throw new IOException("The existing Notion Helper instance did not become ready to receive activation.");
    }
}

using System.IO;
using System.Security;
using NotionHelper.Services;
using Application = System.Windows.Application;
using MessageBox = System.Windows.MessageBox;
using MessageBoxButton = System.Windows.MessageBoxButton;
using MessageBoxImage = System.Windows.MessageBoxImage;
using ExitEventArgs = System.Windows.ExitEventArgs;
using StartupEventArgs = System.Windows.StartupEventArgs;

namespace NotionHelper;

public partial class App : Application
{
    private const string InstanceName = "8d41b4ef-5ac4-4980-9a6d-1200d3849b23";
    private AppInstanceCoordinator? _instanceCoordinator;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ShutdownMode = System.Windows.ShutdownMode.OnExplicitShutdown;

        try
        {
            _instanceCoordinator = new AppInstanceCoordinator(InstanceName);
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or SecurityException
                or PlatformNotSupportedException)
        {
            MessageBox.Show(
                $"Notion Helper could not establish its single-instance startup coordination: {exception.Message}",
                "Notion Helper startup error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown(1);
            return;
        }

        if (!_instanceCoordinator.IsPrimary)
        {
            Shutdown();
            return;
        }

        var window = new MainWindow();
        MainWindow = window;
        window.Show();
        window.Hide();
        _instanceCoordinator.StartListening(() =>
            Dispatcher.BeginInvoke(window.ActivateFromExternalLaunch));
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _instanceCoordinator?.Dispose();
        base.OnExit(e);
    }
}

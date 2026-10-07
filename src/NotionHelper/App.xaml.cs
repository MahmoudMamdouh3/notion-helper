using Application = System.Windows.Application;
using StartupEventArgs = System.Windows.StartupEventArgs;

namespace NotionHelper;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ShutdownMode = System.Windows.ShutdownMode.OnExplicitShutdown;

        var window = new MainWindow();
        MainWindow = window;
        window.Show();
        window.Hide();
    }
}

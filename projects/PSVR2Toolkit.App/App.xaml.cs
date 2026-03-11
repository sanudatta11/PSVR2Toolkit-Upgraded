using System.Windows;
using PSVR2Toolkit.CAPI;

namespace PSVR2Toolkit.App;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Initialize IPC client
        var ipcClient = IpcClient.Instance();
        ipcClient.Start();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        // Clean up IPC client
        var ipcClient = IpcClient.Instance();
        ipcClient.Stop();

        base.OnExit(e);
    }
}

using System;
using System.Linq;
using System.Threading;
using System.Windows;

namespace QBLauncherPro;

public partial class App : Application
{
    public static bool StartInBackground { get; private set; }

    private const string WakeEventName = "Local\\QBLauncherPro_CSharp_Wakeup";
    private EventWaitHandle? _wakeEvent;

    protected override void OnStartup(StartupEventArgs e)
    {
        DispatcherUnhandledException += (_, args) =>
        {
            args.Handled = true;
        };

        StartInBackground = e.Args.Any(a => string.Equals(a, "--background", StringComparison.OrdinalIgnoreCase));
        _wakeEvent = new EventWaitHandle(false, EventResetMode.AutoReset, WakeEventName, out bool createdNew);
        if (!createdNew)
        {
            _wakeEvent.Set();
            Shutdown();
            return;
        }

        ThreadPool.RegisterWaitForSingleObject(_wakeEvent, (_, _) =>
        {
            Dispatcher.BeginInvoke(() =>
            {
                if (MainWindow is MainWindow mw)
                {
                    mw.WakeFromBackground();
                }
            });
        }, null, -1, false);

        base.OnStartup(e);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _wakeEvent?.Dispose();
        base.OnExit(e);
    }
}

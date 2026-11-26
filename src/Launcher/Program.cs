using System.Security.Principal;
using System.Windows;

namespace CodexBootAnimation;

internal static class Program
{
    private const string Title = "Codex Boot Animation";
    private const string Usage = "Options: --quick (default), --full, --direct, --preview, --diagnostics, --help.\n" +
        "Use --preview with --quick or --full to play only the animation.";

    [STAThread]
    private static int Main(string[] args)
    {
        if (!LaunchOptions.TryParse(args, out var options))
        {
            MessageBox.Show(Usage, Title, MessageBoxButton.OK, MessageBoxImage.Warning);
            return 2;
        }
        if (options.Help)
        {
            MessageBox.Show(Usage, Title);
            return 0;
        }

        using var log = new LaunchLog(options.Diagnostics);
        using var user = WindowsIdentity.GetCurrent();
        using var mutex = new Mutex(true, $@"Local\CodexBootAnimation-{user.User!.Value}", out bool first);
        if (!first)
        {
            log.Record("duplicate-suppressed");
            return 0;
        }

        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        app.DispatcherUnhandledException += (_, error) =>
        {
            log.Record($"dispatcher-error-0x{error.Exception.HResult:X8}");
            error.Handled = true;
            app.Shutdown(1);
        };
        app.Startup += async (_, _) =>
        {
            int exit = 1;
            try { exit = await RunAsync(options, log.Record); }
            catch (Exception ex) { log.Record($"launcher-error-0x{ex.HResult:X8}"); }
            finally { app.Shutdown(exit); }
        };
        try { return app.Run(); }
        finally { mutex.ReleaseMutex(); }
    }

    private static async Task<int> RunAsync(LaunchOptions options, Action<string> record)
    {
        record($"launch-{options.Mode}{(options.Preview ? "-preview" : "")}");
        using var focus = new ForegroundHandoff(record);
        var animation = options.Mode == PlaybackMode.Direct ? null : new AnimationHost(focus, record);
        var client = options.Preview ? null : new ClientActivation(focus, record);
        var coordinator = new LaunchCoordinator(TimeSpan.FromSeconds(18), record);
        var result = await coordinator.RunAsync(options, animation, client);
        record($"result-{result.Animation?.ToString() ?? "none"}-{result.Client?.ToString() ?? "none"}");

        if (result.Client == ClientStatus.Missing)
        {
            Notify(focus, "The supported ChatGPT desktop app is not registered for this Windows user. " +
                "Check the ChatGPT entry in Start.");
            return 3;
        }
        if (result.Client == ClientStatus.ActivationFailed)
        {
            Notify(focus, "Windows could not open ChatGPT. Try the ChatGPT entry in Start.");
            return 3;
        }
        if (result.Client is ClientStatus.TimedOut or ClientStatus.Ambiguous)
            return 4;
        if (options.Preview && result.Animation is AnimationEnd.Failed or AnimationEnd.TimedOut)
        {
            Notify(focus, "The animation could not play. Check the Evergreen WebView2 Runtime and the theme files.");
            return 5;
        }
        return 0;
    }

    private static void Notify(ForegroundHandoff focus, string text)
    {
        if (focus.CanNotify)
            MessageBox.Show(text, Title, MessageBoxButton.OK, MessageBoxImage.Information);
    }
}

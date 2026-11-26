using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using System.Windows;
using System.Windows.Media;

namespace CodexBootAnimation;

internal static class Program
{
    private const string Title = "Neural Overdrive Launcher";

    [STAThread]
    private static int Main(string[] args)
    {
        var store = new SettingsStore(LaunchLog.DataDirectory);
        var (preferences, state) = store.Load();
        bool valid = LaunchOptions.TryParse(args, preferences, out var options);
        var words = new SettingsText(options.Language);
        if (!valid)
        {
            MessageBox.Show(words["Usage"], Title, MessageBoxButton.OK, MessageBoxImage.Warning);
            return 2;
        }
        if (options.Help)
        {
            MessageBox.Show(words["Usage"], Title);
            return 0;
        }

        using var log = new LaunchLog(options.Diagnostics);
        using var user = WindowsIdentity.GetCurrent();
        string mutexName = $@"Local\CodexBootAnimation-{user.User!.Value}";
        if (options.Settings)
            mutexName += "-Settings-" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
                AppContext.BaseDirectory.ToUpperInvariant())));
        using var mutex = new Mutex(true, mutexName, out bool first);
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
            if (options.Settings)
            {
                ShowSettings(app, store, preferences, state, options, log.Record);
                return;
            }
            int exit = 1;
            try { exit = await RunAsync(options, log.Record); }
            catch (Exception ex) { log.Record($"launcher-error-0x{ex.HResult:X8}"); }
            finally { app.Shutdown(exit); }
        };
        try { return app.Run(); }
        finally { mutex.ReleaseMutex(); }
    }

    private static void ShowSettings(Application app, SettingsStore store, LauncherPreferences preferences,
        SettingsState state, LaunchOptions options, Action<string> record)
    {
        try
        {
            record("settings-opening");
            var window = new SettingsWindow(store, preferences, state, options);
            app.MainWindow = window;
            window.ContentRendered += (_, _) =>
            {
                var dpi = VisualTreeHelper.GetDpi(window);
                record($"settings-window-rendered-dpi-{dpi.PixelsPerInchX:0}");
            };
            window.Closed += (_, _) =>
            {
                record("settings-window-closed");
                app.Shutdown(0);
            };
            window.Show();
        }
        catch (Exception ex)
        {
            record($"settings-error-0x{ex.HResult:X8}");
            app.Shutdown(1);
        }
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
        var words = new SettingsText(options.Language);

        if (result.Client == ClientStatus.Missing)
        {
            Notify(focus, words["ClientMissing"]);
            return 3;
        }
        if (result.Client == ClientStatus.ActivationFailed)
        {
            Notify(focus, words["ClientError"]);
            return 3;
        }
        if (result.Client is ClientStatus.TimedOut or ClientStatus.Ambiguous)
            return 4;
        if (options.Preview && result.Animation is AnimationEnd.Failed or AnimationEnd.TimedOut)
        {
            Notify(focus, words["PreviewError"]);
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

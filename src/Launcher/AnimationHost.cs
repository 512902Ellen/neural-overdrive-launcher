using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using static CodexBootAnimation.NativeWindows;
using Color = System.Drawing.Color;

namespace CodexBootAnimation;

internal sealed class AnimationHost(ForegroundHandoff focus, Action<string> record) : IAnimation
{
    private static readonly TimeSpan StartupDeadline = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan PlaybackGrace = TimeSpan.FromSeconds(2);
    private const string Headers = "Content-Type: text/html; charset=utf-8\r\n" +
        "Cache-Control: no-store\r\nX-Content-Type-Options: nosniff\r\n" +
        "Content-Security-Policy: default-src 'none'; script-src 'unsafe-inline'; style-src 'unsafe-inline'; " +
        "base-uri 'none'; form-action 'none'; frame-ancestors 'none'\r\n";
    private readonly BridgeSession bridge = new();
    private readonly TaskCompletionSource firstFrame = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource<AnimationEnd> finished = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private Window? window;
    private WebView2? view;
    private bool closed;

    public async Task<AnimationEnd> PlayAsync(LaunchOptions options)
    {
        try
        {
            OpenWindow();
            _ = InitializeAsync(options);
            var result = await AnimationDeadline.WaitAsync(firstFrame.Task, finished.Task, StartupDeadline,
                options.AnimationDuration + PlaybackGrace);
            if (result == AnimationEnd.TimedOut)
                End(result, firstFrame.Task.IsCompleted ? "animation-deadline" : "first-frame-deadline");
        }
        catch (Exception ex)
        {
            End(AnimationEnd.Failed, $"host-error-0x{ex.HResult:X8}");
        }
        return await finished.Task;
    }

    private void OpenWindow()
    {
        window = new Window
        {
            Title = "Neural Overdrive Launcher",
            WindowStyle = WindowStyle.None,
            ResizeMode = ResizeMode.NoResize,
            Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(3, 6, 17)),
            Width = 1024,
            Height = 576
        };
        window.PreviewKeyDown += (_, key) =>
        {
            if (key.Key != Key.Escape) return;
            key.Handled = true;
            End(AnimationEnd.Skipped, "native-escape");
        };
        window.Deactivated += (_, _) => focus.AnimationDeactivated();
        window.Closed += (_, _) =>
        {
            closed = true;
            End(AnimationEnd.Closed, "window-closed");
            try { view?.Dispose(); }
            catch (Exception ex) { record($"webview-dispose-0x{ex.HResult:X8}"); }
        };

        var handle = new WindowInteropHelper(window).EnsureHandle();
        focus.TrackAnimation(handle);
        GetCursorPos(out var cursor);
        var monitor = MonitorFromPoint(cursor, 2); // MONITOR_DEFAULTTONEAREST
        var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
        if (GetMonitorInfo(monitor, ref info))
        {
            var bounds = info.Work;
            SetWindowPos(handle, 0, bounds.Left, bounds.Top, bounds.Right - bounds.Left,
                bounds.Bottom - bounds.Top, 0x10 | 0x4); // SWP_NOACTIVATE | SWP_NOZORDER
        }
        window.Show();
        record("animation-window-shown");
        if (GetWindowRect(handle, out var viewport))
            record($"animation-window-pixels-{viewport.Right - viewport.Left}x{viewport.Bottom - viewport.Top}");
    }

    private async Task InitializeAsync(LaunchOptions options)
    {
        try
        {
            await Dispatcher.Yield(DispatcherPriority.Background);
            if (closed) return;
            byte[] theme = ThemeCatalog.LoadRuntime(options.Mode);
            string profile = Path.Combine(LaunchLog.DataDirectory, "webview2");
            if (Directory.CreateDirectory(LaunchLog.DataDirectory).Attributes.HasFlag(FileAttributes.ReparsePoint) ||
                Directory.CreateDirectory(profile).Attributes.HasFlag(FileAttributes.ReparsePoint))
                throw new IOException("WebView2 requires a launcher-owned data directory.");
            var environment = await CoreWebView2Environment.CreateAsync(userDataFolder: profile,
                options: new CoreWebView2EnvironmentOptions { ExclusiveUserDataFolderAccess = true });
            if (closed) return;
            if (!string.Equals(Path.GetFullPath(environment.UserDataFolder), profile, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("WebView2 profile location was overridden.");

            view = new WebView2 { DefaultBackgroundColor = Color.FromArgb(3, 6, 17), AllowExternalDrop = false };
            window!.Content = view;
            await view.EnsureCoreWebView2Async(environment);
            if (closed) return;
            record($"webview-initialized-{environment.BrowserVersionString}");
            Configure(view.CoreWebView2, environment, theme, options);
            view.CoreWebView2.Navigate(ThemeCatalog.BuiltIn.Entry);
        }
        catch (WebView2RuntimeNotFoundException)
        {
            End(AnimationEnd.Failed, "webview-runtime-missing");
        }
        catch (InvalidDataException)
        {
            End(AnimationEnd.Failed, "theme-integrity-failure");
        }
        catch (IOException)
        {
            End(AnimationEnd.Failed, "theme-or-profile-io-failure");
        }
        catch (Exception ex)
        {
            End(AnimationEnd.Failed, $"webview-error-0x{ex.HResult:X8}");
        }
    }

    private void Configure(CoreWebView2 core, CoreWebView2Environment environment, byte[] theme, LaunchOptions options)
    {
        var settings = core.Settings;
        settings.AreDevToolsEnabled = false;
        settings.AreDefaultContextMenusEnabled = false;
        settings.AreDefaultScriptDialogsEnabled = false;
        settings.AreBrowserAcceleratorKeysEnabled = false;
        settings.IsBuiltInErrorPageEnabled = false;
        settings.IsZoomControlEnabled = false;
        settings.IsGeneralAutofillEnabled = false;
        settings.IsPasswordAutosaveEnabled = false;
        settings.AreHostObjectsAllowed = false;
        settings.IsStatusBarEnabled = false;

        bool navigating = false;
        core.NavigationStarting += (_, args) =>
        {
            args.Cancel = navigating || args.IsRedirected || args.Uri != ThemeCatalog.BuiltIn.Entry;
            if (!args.Cancel) navigating = true;
        };
        core.FrameNavigationStarting += (_, args) => args.Cancel = true;
        core.NewWindowRequested += (_, args) => args.Handled = true;
        core.DownloadStarting += (_, args) => args.Cancel = true;
        core.LaunchingExternalUriScheme += (_, args) => args.Cancel = true;
        core.PermissionRequested += (_, args) =>
        {
            args.SavesInProfile = false;
            args.State = CoreWebView2PermissionState.Deny;
        };
        core.AddWebResourceRequestedFilter("*", CoreWebView2WebResourceContext.All,
            CoreWebView2WebResourceRequestSourceKinds.All);
        core.WebResourceRequested += (_, args) =>
        {
            bool entry = args.Request.Uri == ThemeCatalog.BuiltIn.Entry && args.Request.Method == "GET" &&
                args.ResourceContext == CoreWebView2WebResourceContext.Document;
            args.Response = entry
                ? environment.CreateWebResourceResponse(new MemoryStream(theme, writable: false), 200, "OK", Headers)
                : environment.CreateWebResourceResponse(null, 403, "Forbidden", "Cache-Control: no-store");
        };
        core.NavigationCompleted += (_, args) =>
        {
            if (!args.IsSuccess) End(AnimationEnd.Failed, $"theme-navigation-{args.WebErrorStatus}");
        };
        core.ProcessFailed += (_, args) => End(AnimationEnd.Failed, $"webview-process-{args.ProcessFailedKind}");
        core.WebMessageReceived += (_, args) =>
        {
            if (closed || finished.Task.IsCompleted) return;
            try { ReceiveMessage(core, args, options); }
            catch (Exception ex) { End(AnimationEnd.Failed, $"bridge-error-0x{ex.HResult:X8}"); }
        };
    }

    private void ReceiveMessage(CoreWebView2 core, CoreWebView2WebMessageReceivedEventArgs args, LaunchOptions options)
    {
        switch (bridge.Receive(args.Source, args.WebMessageAsJson))
        {
            case BridgeEvent.Ready:
                bool reduced = options.RespectReducedMotion && !SystemParameters.ClientAreaAnimation;
                record(reduced ? "theme-ready-reduced-motion" : "theme-ready");
                core.PostWebMessageAsJson(bridge.StartMessage(options, reduced));
                if (window!.IsActive) view!.Focus();
                break;
            case BridgeEvent.Started:
                record("theme-first-frame-rendered");
                firstFrame.TrySetResult();
                break;
            case BridgeEvent.Completed:
                End(AnimationEnd.Completed, "theme-completed");
                break;
            case BridgeEvent.SkipRequested:
                End(AnimationEnd.Skipped, "theme-escape");
                break;
            case BridgeEvent.Error:
                End(AnimationEnd.Failed, "theme-render-error");
                break;
            default:
                // The deadlines still apply if a rejected message leaves the page stalled.
                record("bridge-message-rejected");
                break;
        }
    }

    private void End(AnimationEnd end, string stage)
    {
        if (finished.TrySetResult(end)) record(stage);
    }

    public void Dismiss()
    {
        if (closed || window is null) return;
        focus.BeforeAnimationDismiss();
        End(AnimationEnd.Closed, "animation-dismissed");
        window.Close();
        record("animation-window-removed");
    }
}

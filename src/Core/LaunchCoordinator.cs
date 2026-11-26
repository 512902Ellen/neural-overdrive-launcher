namespace CodexBootAnimation;

internal enum PlaybackMode { Full, Quick, Direct }
internal enum AnimationEnd { Completed, Skipped, Closed, Failed, TimedOut }
internal enum ClientStatus { WindowFound, Missing, ActivationFailed, TimedOut, Ambiguous }

internal sealed record LaunchOptions(PlaybackMode Mode, bool Preview, bool Diagnostics, bool Help)
{
    public static bool TryParse(string[] args, out LaunchOptions options)
    {
        var mode = PlaybackMode.Quick;
        var modeSet = false;
        var preview = false;
        var diagnostics = false;
        var help = false;
        foreach (var arg in args)
        {
            switch (arg)
            {
                case "--preview": preview = true; break;
                case "--diagnostics": diagnostics = true; break;
                case "--help": help = true; break;
                case "--full" or "--quick" or "--direct" when !modeSet:
                    modeSet = true;
                    mode = arg switch
                    {
                        "--full" => PlaybackMode.Full,
                        "--direct" => PlaybackMode.Direct,
                        _ => PlaybackMode.Quick
                    };
                    break;
                default:
                    options = new(mode, preview, diagnostics, help);
                    return false;
            }
        }

        options = new(mode, preview, diagnostics, help);
        return !preview || mode != PlaybackMode.Direct;
    }

    public TimeSpan AnimationDuration => TimeSpan.FromSeconds(Mode == PlaybackMode.Full ? 9 : 5);
}

internal readonly record struct ClientWindow(nint Handle, uint ProcessId);
internal readonly record struct ClientResult(ClientStatus Status, ClientWindow Window = default);
internal readonly record struct LaunchResult(AnimationEnd? Animation, ClientStatus? Client);

internal interface IAnimation
{
    Task<AnimationEnd> PlayAsync(LaunchOptions options);
    void Dismiss();
}

internal interface IClientLaunch
{
    Task<ClientResult> FindOrActivateAsync(CancellationToken cancellationToken);
    void Handoff(ClientWindow window, bool allowForeground);
}

internal sealed class LaunchCoordinator(TimeSpan clientDeadline, Action<string> record)
{
    public async Task<LaunchResult> RunAsync(LaunchOptions options, IAnimation? animation, IClientLaunch? client)
    {
        using var cancellation = new CancellationTokenSource();
        var playback = options.Mode == PlaybackMode.Direct ? null : animation!.PlayAsync(options);
        try
        {
            if (options.Preview)
                return new(await playback!, null);

            var clientTask = ObserveClientAsync(client!, cancellation.Token);
            AnimationEnd? animationEnd = null;
            var allowForeground = playback is null;
            if (playback is not null)
            {
                var first = await Task.WhenAny(playback, clientTask);
                if (first == clientTask && await clientTask is { Status: ClientStatus.Missing or ClientStatus.ActivationFailed } failed)
                    return new(null, failed.Status);

                animationEnd = await playback;
                if (animationEnd == AnimationEnd.Closed)
                    return new(animationEnd, null);

                allowForeground = clientTask.IsCompletedSuccessfully;
                animation!.Dismiss();
            }

            var result = await clientTask;
            if (result.Status == ClientStatus.WindowFound)
                client!.Handoff(result.Window, allowForeground);
            return new(animationEnd, result.Status);
        }
        finally
        {
            cancellation.Cancel();
            animation?.Dismiss();
        }
    }

    private async Task<ClientResult> ObserveClientAsync(IClientLaunch client, CancellationToken cancellation)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        deadline.CancelAfter(clientDeadline);
        var operation = client.FindOrActivateAsync(deadline.Token);
        try
        {
            return await operation.WaitAsync(deadline.Token);
        }
        catch (OperationCanceledException) when (deadline.IsCancellationRequested)
        {
            // Observe a late fault even if an in-flight system activation outlives this launch.
            _ = operation.ContinueWith(task => _ = task.Exception, TaskContinuationOptions.OnlyOnFaulted);
            record("client-deadline");
            return new(ClientStatus.TimedOut);
        }
        catch (Exception ex)
        {
            record($"client-error-0x{ex.HResult:X8}");
            return new(ClientStatus.ActivationFailed);
        }
    }
}

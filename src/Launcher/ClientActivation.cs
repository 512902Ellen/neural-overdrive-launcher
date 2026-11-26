using System.ComponentModel;
using System.Diagnostics;
using static CodexBootAnimation.NativeWindows;

namespace CodexBootAnimation;

internal sealed class ClientActivation(ForegroundHandoff focus, Action<string> record) : IClientLaunch
{
    private static readonly TimeSpan SampleInterval = TimeSpan.FromMilliseconds(125);
    private readonly ClientWindowTracker tracker = new();

    public Task<ClientResult> FindOrActivateAsync(CancellationToken cancellationToken) =>
        Task.Run(() => FindOrActivateCoreAsync(cancellationToken), cancellationToken);

    private async Task<ClientResult> FindOrActivateCoreAsync(CancellationToken cancellation)
    {
        var time = Stopwatch.StartNew();
        uint count = 0, length = 0;
        int registration = GetPackagesByPackageFamily(SystemAppActivation.PackageFamily, ref count, 0, ref length, 0);
        if (registration != 0 && registration != InsufficientBuffer)
            throw new Win32Exception(registration);
        if (count == 0)
        {
            record("client-missing");
            return new(ClientStatus.Missing);
        }

        var sample = tracker.Scan();
        record(sample.Inaccessible > 0 ? "client-initial-scan-had-inaccessible-windows" : "client-initial-scan");
        if (sample.Count == 0)
        {
            // Submit the registered app's activation at most once. Windows owns the client from this point.
            SystemAppActivation.Submit(cancellation);
            record("activation-accepted");
        }

        while (time.Elapsed < TimeSpan.FromSeconds(17))
        {
            await Task.Delay(SampleInterval, cancellation).ConfigureAwait(false);
            var next = tracker.Scan();
            if (next.Selected is { } window && window == sample.Selected)
            {
                record("client-window-stable");
                return new(ClientStatus.WindowFound, window);
            }
            sample = next;
        }

        bool ambiguous = sample.Count > 1 && sample.Selected is null;
        record(ambiguous ? "client-window-ambiguous" : "client-window-deadline");
        return new(ambiguous ? ClientStatus.Ambiguous : ClientStatus.TimedOut);
    }

    public void Handoff(ClientWindow window, bool allowForeground)
    {
        if (tracker.StillMatches(window))
            focus.Complete(window, allowForeground);
        else
            record("handoff-window-no-longer-matches");
    }
}

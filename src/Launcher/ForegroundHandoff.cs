using static CodexBootAnimation.NativeWindows;

namespace CodexBootAnimation;

internal sealed class ForegroundHandoff : IDisposable
{
    private readonly nint initial = GetForegroundWindow();
    private readonly WinEventCallback onChanged;
    private readonly Action<string> record;
    private readonly nint hook;
    private nint animation;
    private bool sawAnimation;
    private bool yielded;
    private bool ownedAtDismiss;
    private bool dismissing;

    public ForegroundHandoff(Action<string> record)
    {
        this.record = record;
        onChanged = ForegroundChanged;
        const uint foreground = 3;
        hook = SetWinEventHook(foreground, foreground, 0, onChanged, 0, 0, 0);
    }

    public void TrackAnimation(nint window) => animation = window;

    public bool CanNotify => !yielded && (GetForegroundWindow() == initial ||
        ownedAtDismiss && GetForegroundWindow() == 0);

    public void AnimationDeactivated()
    {
        if (!dismissing) yielded = true;
    }

    public void BeforeAnimationDismiss()
    {
        ownedAtDismiss = animation != 0 && GetForegroundWindow() == animation && !yielded;
        dismissing = true;
    }

    public void Complete(ClientWindow target, bool allowed)
    {
        var current = GetForegroundWindow();
        if (current == target.Handle)
        {
            record("handoff-already-foreground");
            return;
        }

        bool stillInLaunch = animation == 0
            ? initial != 0 && current == initial && !yielded
            : ownedAtDismiss && (current == 0 || current == initial || current == animation);
        if (!allowed || hook == 0 || !stillInLaunch)
        {
            record("handoff-left-to-windows");
            return;
        }

        if (IsIconic(target.Handle))
            ShowWindowAsync(target.Handle, 9); // SW_RESTORE
        if (GetForegroundWindow() == current)
            SetForegroundWindow(target.Handle);
        record(GetForegroundWindow() == target.Handle ? "handoff-foreground" : "handoff-left-to-windows");
    }

    private void ForegroundChanged(nint _, uint type, nint window, int objectId,
        int childId, uint eventThread, uint eventTime)
    {
        if (dismissing) return;
        if (window == animation && animation != 0)
            sawAnimation = true;
        else if (window != 0 && (sawAnimation || window != initial))
            yielded = true;
    }

    public void Dispose()
    {
        if (hook != 0)
            UnhookWinEvent(hook);
        GC.KeepAlive(onChanged);
    }
}

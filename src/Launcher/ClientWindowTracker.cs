using static CodexBootAnimation.NativeWindows;

namespace CodexBootAnimation;

internal enum WindowIdentity { Match, Other, Inaccessible }

internal sealed class ClientWindowTracker
{
    // Selection uses the foreground if it is an eligible member of this app, or the sole eligible window.
    public (ClientWindow? Selected, int Count, int Inaccessible) Scan()
    {
        var candidates = new List<ClientWindow>();
        var identities = new Dictionary<uint, WindowIdentity>();
        int inaccessible = 0;
        EnumWindows((window, _) =>
        {
            if (!IsCandidate(window, out uint process))
                return true;
            if (!identities.TryGetValue(process, out var identity))
                identities[process] = identity = Identify(process);
            if (identity == WindowIdentity.Match)
                candidates.Add(new(window, process));
            if (identity == WindowIdentity.Inaccessible)
                inaccessible++;
            return true;
        }, 0);

        if (candidates.Count == 1)
            return (candidates[0], 1, inaccessible);
        var foreground = GetForegroundWindow();
        foreach (var candidate in candidates)
            if (candidate.Handle == foreground)
                return (candidate, candidates.Count, inaccessible);
        return (null, candidates.Count, inaccessible);
    }

    public bool StillMatches(ClientWindow window) =>
        IsCandidate(window.Handle, out uint process) && window.ProcessId == process &&
        Identify(process) == WindowIdentity.Match;

    private static bool IsCandidate(nint window, out uint process)
    {
        process = 0;
        const uint owner = 4;
        const int extendedStyle = -20;
        const int toolOrNoActivate = 0x80 | 0x08000000;
        const uint dwmCloaked = 14;
        if (!IsWindowVisible(window) || GetWindow(window, owner) != 0 ||
            (GetWindowLongPtr(window, extendedStyle).ToInt64() & toolOrNoActivate) != 0 ||
            DwmGetWindowAttribute(window, dwmCloaked, out int cloaked, sizeof(int)) != 0 || cloaked != 0 ||
            GetWindowThreadProcessId(window, out process) == 0 || process == Environment.ProcessId)
            return false;
        return IsIconic(window) || (GetWindowRect(window, out var bounds) &&
            bounds.Right - bounds.Left >= 300 && bounds.Bottom - bounds.Top >= 180);
    }

    private static WindowIdentity Identify(uint id)
    {
        const uint queryLimitedInformation = 0x1000;
        using var process = OpenProcess(queryLimitedInformation, false, id);
        if (process.IsInvalid)
            return WindowIdentity.Inaccessible;

        var family = new char[128];
        uint familySize = (uint)family.Length;
        if (GetPackageFamilyName(process, ref familySize, family) != 0 ||
            !family.AsSpan(0, (int)familySize - 1).Equals(SystemAppActivation.PackageFamily, StringComparison.OrdinalIgnoreCase))
            return WindowIdentity.Other;

        var appId = new char[256];
        uint appIdSize = (uint)appId.Length;
        int result = GetApplicationUserModelId(process, ref appIdSize, appId);
        return result == NoApplication || (result == 0 &&
            appId.AsSpan(0, (int)appIdSize - 1).Equals(SystemAppActivation.Aumid, StringComparison.OrdinalIgnoreCase))
            ? WindowIdentity.Match : WindowIdentity.Other;
    }
}

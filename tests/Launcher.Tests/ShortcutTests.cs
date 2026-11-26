using System.Runtime.Versioning;
using CodexBootAnimation;

internal static class ShortcutTests
{
    public static Task Run()
    {
        if (!OperatingSystem.IsWindows())
        {
            Console.WriteLine("SKIP Windows Shell Link COM checks on this platform.");
            return Task.CompletedTask;
        }
        var complete = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            try
            {
                if (OperatingSystem.IsWindows()) RunOnWindows();
                complete.SetResult();
            }
            catch (Exception ex) { complete.SetException(ex); }
        }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        return complete.Task;
    }

    [SupportedOSPlatform("windows")]
    private static void RunOnWindows()
    {
        using var root = new TestDirectory();
        string desktop = Directory.CreateDirectory(Path.Combine(root.Path, "桌面 测试")).FullName;
        string portable = Directory.CreateDirectory(Path.Combine(root.Path, "便携 程序")).FullName;
        string executable = Path.Combine(portable, "CodexBootAnimation.exe");
        File.WriteAllText(executable, "fixture");
        var shortcuts = new DesktopShortcuts(desktop, executable);
        string launch = Path.Combine(desktop, "Codex Boot Animation - Launch Codex.lnk");
        string official = Path.Combine(desktop, "ChatGPT.lnk");
        File.WriteAllText(official, "fixture for official shortcut filename");
        File.WriteAllText(launch, "another application uses this filename");
        Equal(ShortcutResult.Conflict, shortcuts.Create(ShortcutKind.Launch));
        Equal(ShortcutRemoval.Conflict, shortcuts.Remove(ShortcutKind.Launch));
        Equal("another application uses this filename", File.ReadAllText(launch));
        File.Delete(launch);

        foreach (var kind in Enum.GetValues<ShortcutKind>())
        {
            Equal(ShortcutResult.Created, shortcuts.Create(kind));
            Equal(ShortcutResult.AlreadyExists, shortcuts.Create(kind));
        }

        string moved = Path.Combine(root.Path, "Moved copy");
        Directory.Move(portable, moved);
        shortcuts = new(desktop, Path.Combine(moved, Path.GetFileName(executable)));
        foreach (var kind in Enum.GetValues<ShortcutKind>())
        {
            Equal(ShortcutResult.OldLocation, shortcuts.Create(kind));
            Equal(ShortcutRemoval.Removed, shortcuts.Remove(kind));
            Equal(ShortcutRemoval.Missing, shortcuts.Remove(kind));
        }
        Equal("fixture for official shortcut filename", File.ReadAllText(official));
        Equal(1, Directory.GetFiles(desktop).Length);
    }

    private static void Equal<T>(T expected, T actual)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new InvalidOperationException($"Expected {expected}. Received {actual}.");
    }
}

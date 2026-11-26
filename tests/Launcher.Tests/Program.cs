using System.Text.Json;
using CodexBootAnimation;

var tests = new (string Name, Func<Task> Run)[]
{
    ("mode parsing and default", Modes),
    ("launch entry options override stored preferences", PreferencesAndOptions),
    ("portable settings round-trip, replacement, move and defaults", SettingsRoundTrip),
    ("invalid, incomplete, oversized and future settings recover", SettingsRecovery),
    ("bridge source, size, shape, token and order", Bridge),
    ("no bridge startup and stalled playback deadlines", AnimationDeadlines),
    ("preview bypasses client", Preview),
    ("direct bypasses renderer", Direct),
    ("healthy animation and early client handoff", Complete),
    ("render failure keeps client activation active", Recovery),
    ("escape removes animation while client starts", Skip),
    ("missing client ends animation early", Missing),
    ("activation failure ends animation early", ActivationFailure),
    ("closing launcher cancels client monitoring", Close),
    ("client hard deadline leaves app independent", ClientTimeout),
    ("Windows shortcut creation, conflicts and removal use an isolated folder", ShortcutTests.Run)
};
foreach (var (name, run) in tests)
{
    await run().WaitAsync(TimeSpan.FromSeconds(5));
    Console.WriteLine($"PASS {name}");
}

static void Check(bool condition)
{
    if (!condition) throw new InvalidOperationException("Assertion failed.");
}

static Task Modes()
{
    Check(LaunchOptions.TryParse([], out var quick) && quick.Mode == PlaybackMode.Quick &&
        quick.AnimationDuration == TimeSpan.FromSeconds(5));
    Check(LaunchOptions.TryParse(["--full", "--preview"], out var preview) && preview.Preview &&
        preview.AnimationDuration == TimeSpan.FromSeconds(9));
    Check(LaunchOptions.TryParse(["--direct"], out var direct) && direct.Mode == PlaybackMode.Direct);
    Check(!LaunchOptions.TryParse(["--direct", "--preview"], out _));
    Check(!LaunchOptions.TryParse(["--quick", "--full"], out _));
    Check(!LaunchOptions.TryParse(["--unknown"], out _));
    return Task.CompletedTask;
}

static Task PreferencesAndOptions()
{
    var saved = new LauncherPreferences(PlaybackMode.Full, UiLanguage.SimplifiedChinese, false);
    Check(LaunchOptions.TryParse([], saved, out var launch) && launch.Mode == PlaybackMode.Full &&
        launch.Language == UiLanguage.SimplifiedChinese && !launch.RespectReducedMotion);
    Check(LaunchOptions.TryParse(["--quick", "--language", "en", "--respect-reduced-motion"], saved, out var manual) &&
        manual.Mode == PlaybackMode.Quick && manual.Language == UiLanguage.English && manual.RespectReducedMotion);
    Check(LaunchOptions.TryParse(["--settings"], saved, out var settings) && settings.Settings && !settings.Preview);
    Check(LaunchOptions.TryParse(["--preview"], saved with { Mode = PlaybackMode.Direct }, out var preview) &&
        preview.Preview && preview.Mode == PlaybackMode.Quick);
    Check(!LaunchOptions.TryParse(["--settings", "--direct"], saved, out _));
    Check(!LaunchOptions.TryParse(["--settings", "--preview"], saved, out _));
    Check(!LaunchOptions.TryParse(["--language"], saved, out _));
    Check(!LaunchOptions.TryParse(["--language", "other"], saved, out _));
    return Task.CompletedTask;
}

static Task SettingsRoundTrip()
{
    using var root = new TestDirectory();
    string directory = Path.Combine(root.Path, "Portable 目录", ".launcher-data");
    var store = new SettingsStore(directory);
    Check(store.Load() == (LauncherPreferences.Default, SettingsState.Missing) && !Directory.Exists(directory));
    var preferences = new LauncherPreferences(PlaybackMode.Full, UiLanguage.SimplifiedChinese, false);
    store.Save(preferences);
    using (var saved = JsonDocument.Parse(File.ReadAllText(Path.Combine(directory, "settings.json"))))
    {
        Check(saved.RootElement.GetProperty("schemaVersion").GetInt32() == 1);
        Check(saved.RootElement.GetProperty("language").GetString() == "zh-CN");
    }
    Check(store.Load() == (preferences, SettingsState.Loaded));
    string moved = Path.Combine(root.Path, "Moved copy");
    Directory.Move(Path.GetDirectoryName(directory)!, moved);
    directory = Path.Combine(moved, ".launcher-data");
    store = new SettingsStore(directory);
    Check(store.Load() == (preferences, SettingsState.Loaded));
    store.Save(LauncherPreferences.Default);
    Check(store.Load() == (LauncherPreferences.Default, SettingsState.Loaded));
    Check(Directory.GetFiles(directory).Length == 1);
    return Task.CompletedTask;
}

static Task SettingsRecovery()
{
    using var root = new TestDirectory();
    var store = new SettingsStore(root.Path);
    var path = Path.Combine(root.Path, "settings.json");
    foreach (string invalid in new[]
    {
        "", "{", "null", "{}", new string(' ', 4097),
        """{"schemaVersion":2,"launchMode":"quick","language":"en","respectReducedMotion":true}""",
        """{"schemaVersion":1,"launchMode":999,"language":"en","respectReducedMotion":true}""",
        """{"schemaVersion":1,"launchMode":"quick","language":"unknown","respectReducedMotion":true}""",
        """{"schemaVersion":1,"launchMode":"quick","language":"en"}"""
    })
    {
        File.WriteAllText(path, invalid);
        Check(store.Load() == (LauncherPreferences.Default, SettingsState.Recovered));
        Check(File.ReadAllText(path) == invalid);
    }
    store.Save(LauncherPreferences.Default);
    Check(store.Load() == (LauncherPreferences.Default, SettingsState.Loaded));
    return Task.CompletedTask;
}

static Task Bridge()
{
    var bridge = new BridgeSession();
    const string ready = """{"v":1,"type":"ready"}""";
    Check(bridge.Receive("https://wrong.invalid/boot.html", ready) == BridgeEvent.Rejected);
    Check(bridge.Receive(BridgeSession.Entry, new string('x', 257)) == BridgeEvent.Rejected);
    Check(bridge.Receive(BridgeSession.Entry, "[]") == BridgeEvent.Rejected);
    Check(bridge.Receive(BridgeSession.Entry, """{"v":1,"type":"ready","extra":0}""") == BridgeEvent.Rejected);
    Check(bridge.Receive(BridgeSession.Entry, ready) == BridgeEvent.Ready);
    Check(bridge.Receive(BridgeSession.Entry, ready) == BridgeEvent.Rejected);
    using var start = JsonDocument.Parse(bridge.StartMessage(new(PlaybackMode.Quick, true, false, false), true));
    var id = start.RootElement.GetProperty("id").GetString();
    Check(start.RootElement.GetProperty("mode").GetString() == "quick");
    Check(start.RootElement.GetProperty("reducedMotion").GetBoolean());
    Check(start.RootElement.GetProperty("respectSystemReducedMotion").GetBoolean());
    BridgeEvent Message(string type, string? token) =>
        bridge.Receive(BridgeSession.Entry, JsonSerializer.Serialize(new { v = 1, type, id = token }));
    Check(Message("completed", id) == BridgeEvent.Rejected);
    Check(Message("started", "old-session") == BridgeEvent.Rejected);
    Check(Message("started", id) == BridgeEvent.Started);
    Check(Message("runCommand", id) == BridgeEvent.Rejected);
    Check(Message("completed", id) == BridgeEvent.Completed);
    Check(Message("completed", id) == BridgeEvent.Rejected);
    return Task.CompletedTask;
}

static async Task AnimationDeadlines()
{
    var noFrame = new TaskCompletionSource();
    var noFinish = new TaskCompletionSource<AnimationEnd>();
    var shortDeadline = TimeSpan.FromMilliseconds(35);
    Check(await AnimationDeadline.WaitAsync(noFrame.Task, noFinish.Task, shortDeadline, shortDeadline) ==
        AnimationEnd.TimedOut);
    Check(await AnimationDeadline.WaitAsync(Task.CompletedTask, noFinish.Task, shortDeadline, shortDeadline) ==
        AnimationEnd.TimedOut);
    Check(await AnimationDeadline.WaitAsync(noFrame.Task, Task.FromResult(AnimationEnd.Skipped), shortDeadline,
        shortDeadline) == AnimationEnd.Skipped);
}

static async Task Preview()
{
    var animation = new FakeAnimation(AnimationEnd.Completed);
    var client = new FakeClient();
    var result = await Run(animation, client, preview: true);
    Check(result == new LaunchResult(AnimationEnd.Completed, null) && animation.Dismissed && !client.Started);
}

static async Task Direct()
{
    var animation = new FakeAnimation(null);
    var client = Ready();
    var result = await Run(animation, client, mode: PlaybackMode.Direct);
    Check(result == new LaunchResult(null, ClientStatus.WindowFound) && !animation.Started && client.Foreground);
}

static async Task Complete()
{
    var animation = new FakeAnimation(null);
    var client = Ready();
    client.OnHandoff = () => Check(animation.Dismissed);
    var operation = Run(animation, client);
    Check(animation.Started && client.Started && !animation.Dismissed);
    animation.Playback.SetResult(AnimationEnd.Completed);
    Check((await operation).Client == ClientStatus.WindowFound && client.Foreground);
}

static async Task Recovery()
{
    var animation = new FakeAnimation(AnimationEnd.Failed);
    var client = new FakeClient();
    var operation = Run(animation, client);
    Check(animation.Dismissed && client.Started);
    client.Result.SetResult(new(ClientStatus.WindowFound, new(42, 1)));
    Check(await operation == new LaunchResult(AnimationEnd.Failed, ClientStatus.WindowFound) && !client.Foreground);
}

static async Task Skip()
{
    var animation = new FakeAnimation(AnimationEnd.Skipped);
    var client = new FakeClient();
    var operation = Run(animation, client);
    Check(animation.Dismissed && client.Started);
    client.Result.SetResult(new(ClientStatus.WindowFound, new(42, 1)));
    Check(await operation == new LaunchResult(AnimationEnd.Skipped, ClientStatus.WindowFound) && !client.Foreground);
}

static async Task Missing()
{
    var animation = new FakeAnimation(null);
    var client = new FakeClient();
    client.Result.SetResult(new(ClientStatus.Missing));
    Check(await Run(animation, client) == new LaunchResult(null, ClientStatus.Missing) && animation.Dismissed);
}

static async Task ActivationFailure()
{
    var animation = new FakeAnimation(null);
    var client = new FakeClient();
    client.Result.SetException(new InvalidOperationException("Simulated COM failure."));
    Check(await Run(animation, client) == new LaunchResult(null, ClientStatus.ActivationFailed) &&
        animation.Dismissed);
}

static async Task Close()
{
    var animation = new FakeAnimation(AnimationEnd.Closed);
    var client = new FakeClient();
    Check(await Run(animation, client) == new LaunchResult(AnimationEnd.Closed, null) && client.Token.IsCancellationRequested);
}

static async Task ClientTimeout()
{
    var client = new FakeClient();
    var coordinator = new LaunchCoordinator(TimeSpan.FromMilliseconds(35), _ => { });
    var result = await coordinator.RunAsync(new(PlaybackMode.Direct, false, false, false), null, client);
    Check(result.Client == ClientStatus.TimedOut && client.Token.IsCancellationRequested && !client.Foreground);
}

static FakeClient Ready()
{
    var client = new FakeClient();
    client.Result.SetResult(new(ClientStatus.WindowFound, new(42, 1)));
    return client;
}

static Task<LaunchResult> Run(FakeAnimation animation, FakeClient client, bool preview = false,
    PlaybackMode mode = PlaybackMode.Quick) =>
    new LaunchCoordinator(TimeSpan.FromSeconds(1), _ => { })
        .RunAsync(new(mode, preview, false, false), animation, client);

file sealed class FakeAnimation : IAnimation
{
    public FakeAnimation(AnimationEnd? end)
    {
        if (end.HasValue) Playback.SetResult(end.Value);
    }

    public TaskCompletionSource<AnimationEnd> Playback { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public bool Started { get; private set; }
    public bool Dismissed { get; private set; }

    public Task<AnimationEnd> PlayAsync(LaunchOptions options)
    {
        Started = true;
        return Playback.Task;
    }

    public void Dismiss() => Dismissed = true;
}

file sealed class FakeClient : IClientLaunch
{
    public TaskCompletionSource<ClientResult> Result { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public bool Started { get; private set; }
    public bool Foreground { get; private set; }
    public CancellationToken Token { get; private set; }
    public Action? OnHandoff { get; set; }

    public Task<ClientResult> FindOrActivateAsync(CancellationToken token)
    {
        Started = true;
        Token = token;
        return Result.Task;
    }

    public void Handoff(ClientWindow window, bool allowForeground)
    {
        Foreground = allowForeground;
        OnHandoff?.Invoke();
    }
}

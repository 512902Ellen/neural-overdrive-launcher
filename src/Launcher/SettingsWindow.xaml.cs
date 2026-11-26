using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace CodexBootAnimation;

internal partial class SettingsWindow : Window, INotifyPropertyChanged
{
    private readonly SettingsStore store;
    private readonly bool diagnostics;
    private LauncherPreferences saved;
    private UiLanguage language;
    private bool changingControls;
    private string? statusKey;

    public SettingsWindow(SettingsStore store, LauncherPreferences preferences, SettingsState state, LaunchOptions options)
    {
        this.store = store;
        saved = preferences;
        diagnostics = options.Diagnostics;
        language = options.Language;
        Words = new(language);
        InitializeComponent();
        var iconUri = new Uri("pack://application:,,,/Assets/launcher.ico");
        HeaderIcon.Source = new IconBitmapDecoder(iconUri, BitmapCreateOptions.None, BitmapCacheOption.OnLoad)
            .Frames.MaxBy(frame => frame.PixelWidth);
        Width = Math.Min(Width, SystemParameters.WorkArea.Width);
        Height = Math.Min(Height, SystemParameters.WorkArea.Height);
        DataContext = this;
        Apply(preferences with { Language = language });
        if (state == SettingsState.Recovered) SetStatus("Recovered");
        else SetStatus(Draft == saved ? null : "Dirty");

        try
        {
            using var image = new MemoryStream(ThemeCatalog.LoadPreview(), writable: false);
            var png = BitmapFrame.Create(image, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
            ThemeArt.Background = new ImageBrush(png) { Stretch = Stretch.UniformToFill };
            ThemePreviewUnavailable.Visibility = Visibility.Collapsed;
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException or UnauthorizedAccessException or FormatException) { }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public SettingsText Words { get; private set; }
    public string Status => statusKey is null ? "" : Words[statusKey];
    public string ThemeName => ThemeCatalog.BuiltIn.Name;
    public string ThemeVersion => $"v{ThemeCatalog.BuiltIn.Version}";
    public string LauncherVersion => $"v{typeof(SettingsWindow).Assembly.GetName().Version!.ToString(3)}";

    private LauncherPreferences Draft => new(
        FullMode.IsChecked == true ? PlaybackMode.Full : DirectMode.IsChecked == true ? PlaybackMode.Direct : PlaybackMode.Quick,
        Chinese.IsChecked == true ? UiLanguage.SimplifiedChinese : UiLanguage.English,
        RespectMotion.IsChecked == true);

    private void Apply(LauncherPreferences preferences)
    {
        changingControls = true;
        QuickMode.IsChecked = preferences.Mode == PlaybackMode.Quick;
        FullMode.IsChecked = preferences.Mode == PlaybackMode.Full;
        DirectMode.IsChecked = preferences.Mode == PlaybackMode.Direct;
        English.IsChecked = preferences.Language == UiLanguage.English;
        Chinese.IsChecked = preferences.Language == UiLanguage.SimplifiedChinese;
        RespectMotion.IsChecked = preferences.RespectReducedMotion;
        changingControls = false;
        SetLanguage(preferences.Language);
    }

    private void PreferenceChanged(object sender, RoutedEventArgs e)
    {
        if (changingControls || !IsInitialized) return;
        var draft = Draft;
        SetLanguage(draft.Language);
        SetStatus(draft == saved ? null : "Dirty");
    }

    private void SetLanguage(UiLanguage value)
    {
        Language = XmlLanguage.GetLanguage(value == UiLanguage.SimplifiedChinese ? "zh-CN" : "en");
        if (value == language) return;
        language = value;
        Words = new(language);
        PropertyChanged?.Invoke(this, new(nameof(Words)));
    }

    private void SetStatus(string? key)
    {
        statusKey = key;
        PropertyChanged?.Invoke(this, new(nameof(Status)));
        UIElementAutomationPeer.FromElement(StatusLine)?.RaiseAutomationEvent(AutomationEvents.LiveRegionChanged);
    }

    private void SaveClick(object sender, RoutedEventArgs e)
    {
        try
        {
            var draft = Draft;
            store.Save(draft);
            saved = draft;
            SetStatus("Saved");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { SetStatus("SaveError"); }
    }

    private void ResetClick(object sender, RoutedEventArgs e)
    {
        Apply(LauncherPreferences.Default);
        SetStatus("ResetDraft");
    }

    private static DesktopShortcuts Desktop()
    {
        string path = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        if (path.Length == 0 || !Directory.Exists(path)) throw new DirectoryNotFoundException();
        return new(path, Environment.ProcessPath!);
    }

    private void CreateLaunchClick(object sender, RoutedEventArgs e) => CreateShortcut(ShortcutKind.Launch);
    private void CreateSettingsClick(object sender, RoutedEventArgs e) => CreateShortcut(ShortcutKind.Settings);

    private void CreateShortcut(ShortcutKind kind)
    {
        try
        {
            SetStatus(Desktop().Create(kind) switch
            {
                ShortcutResult.Created => "LinkCreated",
                ShortcutResult.AlreadyExists => "LinkExists",
                ShortcutResult.OldLocation => "LinkOld",
                _ => "LinkConflict"
            });
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or COMException) { SetStatus("LinkError"); }
    }

    private void RemoveClick(object sender, RoutedEventArgs e)
    {
        try
        {
            var desktop = Desktop();
            var launch = desktop.Remove(ShortcutKind.Launch);
            var settings = desktop.Remove(ShortcutKind.Settings);
            bool removed = launch == ShortcutRemoval.Removed || settings == ShortcutRemoval.Removed;
            bool kept = launch == ShortcutRemoval.Conflict || settings == ShortcutRemoval.Conflict;
            SetStatus(removed ? kept ? "LinksPartial" : "LinksRemoved" : kept ? "LinkConflict" : "LinksMissing");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or COMException) { SetStatus("LinkError"); }
    }

    private async void PreviewClick(object sender, RoutedEventArgs e) => await RunLauncherAsync(preview: true);
    private async void LaunchClick(object sender, RoutedEventArgs e) => await RunLauncherAsync(preview: false);

    private async Task RunLauncherAsync(bool preview)
    {
        var draft = Draft;
        var start = new ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute = false };
        start.ArgumentList.Add(draft.Mode switch
        {
            PlaybackMode.Full => "--full",
            PlaybackMode.Direct when !preview => "--direct",
            _ => "--quick"
        });
        start.ArgumentList.Add(draft.RespectReducedMotion ? "--respect-reduced-motion" : "--ignore-reduced-motion");
        start.ArgumentList.Add("--language");
        start.ArgumentList.Add(draft.Language == UiLanguage.SimplifiedChinese ? "zh-CN" : "en");
        if (preview) start.ArgumentList.Add("--preview");
        if (diagnostics) start.ArgumentList.Add("--diagnostics");

        PreviewButton.IsEnabled = LaunchButton.IsEnabled = false;
        SetStatus(preview ? "StartingPreview" : "StartingClient");
        try
        {
            using var launcher = Process.Start(start)!;
            await launcher.WaitForExitAsync();
            SetStatus(launcher.ExitCode switch
            {
                0 => Draft == saved ? null : "Dirty",
                3 => "ClientError",
                4 => "ClientTimeout",
                5 => "PreviewError",
                _ => "LaunchError"
            });
        }
        catch (Win32Exception) { SetStatus("LaunchError"); }
        finally { PreviewButton.IsEnabled = LaunchButton.IsEnabled = true; }
    }

    private void MinimizeClick(object sender, RoutedEventArgs e) => SystemCommands.MinimizeWindow(this);
    private void CloseClick(object sender, RoutedEventArgs e) => Close();
}

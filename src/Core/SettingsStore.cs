using System.Text.Json;
using System.Text.Json.Serialization;

namespace CodexBootAnimation;

internal enum UiLanguage
{
    [JsonStringEnumMemberName("en")] English,
    [JsonStringEnumMemberName("zh-CN")] SimplifiedChinese
}

internal sealed record LauncherPreferences(PlaybackMode Mode, UiLanguage Language, bool RespectReducedMotion)
{
    public static LauncherPreferences Default { get; } = new(PlaybackMode.Quick, UiLanguage.English, true);
}

internal enum SettingsState { Loaded, Missing, Recovered }

internal sealed class SettingsStore(string directory)
{
    private const int Version = 1;
    private const int MaxBytes = 4096;
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter<PlaybackMode>(JsonNamingPolicy.CamelCase, false),
            new JsonStringEnumConverter<UiLanguage>(allowIntegerValues: false) }
    };
    private string FilePath => Path.Combine(directory, "settings.json");

    public (LauncherPreferences Preferences, SettingsState State) Load()
    {
        try
        {
            RequireLocalPath(directory);
            RequireLocalPath(FilePath);
            using var file = new FileStream(FilePath, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete);
            if (file.Length is <= 0 or > MaxBytes)
                return (LauncherPreferences.Default, SettingsState.Recovered);
            var bytes = new byte[(int)file.Length];
            file.ReadExactly(bytes);
            var document = JsonSerializer.Deserialize<SettingsDocument>(bytes, Json);
            if (document is not { SchemaVersion: Version })
                return (LauncherPreferences.Default, SettingsState.Recovered);
            return (new(document.LaunchMode, document.Language, document.RespectReducedMotion), SettingsState.Loaded);
        }
        catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
        {
            return (LauncherPreferences.Default, SettingsState.Missing);
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            return (LauncherPreferences.Default, SettingsState.Recovered);
        }
    }

    public void Save(LauncherPreferences preferences)
    {
        var profile = Directory.CreateDirectory(directory);
        RequireLocalPath(profile.FullName);
        try { RequireLocalPath(FilePath); }
        catch (FileNotFoundException) { }
        var temporary = Path.Combine(directory, $"settings-{Guid.NewGuid():N}.tmp");
        bool created = false;
        try
        {
            using (var file = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                created = true;
                JsonSerializer.Serialize(file, new SettingsDocument
                {
                    SchemaVersion = Version,
                    LaunchMode = preferences.Mode,
                    Language = preferences.Language,
                    RespectReducedMotion = preferences.RespectReducedMotion
                }, Json);
                file.Flush(flushToDisk: true);
            }
            File.Move(temporary, FilePath, overwrite: true);
            created = false;
        }
        finally
        {
            if (created) File.Delete(temporary);
        }
    }

    private static void RequireLocalPath(string path)
    {
        if (File.GetAttributes(path).HasFlag(FileAttributes.ReparsePoint))
            throw new IOException("Portable settings require a launcher-owned data directory and file.");
    }

    private sealed class SettingsDocument
    {
        public required int SchemaVersion { get; init; }
        public required PlaybackMode LaunchMode { get; init; }
        public required UiLanguage Language { get; init; }
        public required bool RespectReducedMotion { get; init; }
    }
}

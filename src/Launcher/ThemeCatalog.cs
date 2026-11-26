using System.IO;
using System.Security.Cryptography;

namespace CodexBootAnimation;

internal sealed record BuiltInTheme(string Id, string Name, string Version, IReadOnlyList<PlaybackMode> Modes,
    string Entry, string RuntimeFile, string RuntimeSha256, string PreviewFile, string PreviewSha256);

internal static class ThemeCatalog
{
    public static BuiltInTheme BuiltIn { get; } = new(
        "neural-overdrive", "NEURAL OVERDRIVE", "0.3.0",
        Array.AsReadOnly(new[] { PlaybackMode.Quick, PlaybackMode.Full }), BridgeSession.Entry,
        "boot.html", "80AB2F1EA96C2376FB584A1C64C497014F61AC26A616680AAD578E1515D1F242",
        "preview.png", "D8577CC6F70E3E4E6310412CAFD628B518EFB058C0C2FBBF73DE0C2EB5040211");

    public static byte[] LoadRuntime(PlaybackMode mode)
    {
        if (!BuiltIn.Modes.Contains(mode)) throw new InvalidDataException("Unsupported theme mode.");
        return Load(BuiltIn.RuntimeFile, BuiltIn.RuntimeSha256, 65536);
    }

    public static byte[] LoadPreview() => Load(BuiltIn.PreviewFile, BuiltIn.PreviewSha256, 1024 * 1024);

    private static byte[] Load(string fileName, string hash, int sizeLimit)
    {
        var directory = Path.Combine(AppContext.BaseDirectory, "theme");
        var path = Path.Combine(directory, fileName);
        if ((File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0 ||
            (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
            throw new InvalidDataException("The built-in theme must use local files.");
        using var file = File.OpenRead(path);
        if (file.Length <= 0 || file.Length > sizeLimit)
            throw new InvalidDataException("Unexpected built-in theme size.");
        var bytes = new byte[(int)file.Length];
        file.ReadExactly(bytes);
        if (Convert.ToHexString(SHA256.HashData(bytes)) != hash)
            throw new InvalidDataException("Built-in theme integrity check failed.");
        return bytes;
    }
}

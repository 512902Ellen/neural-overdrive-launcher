using System.IO;
using System.Security.Cryptography;

namespace CodexBootAnimation;

internal static class ThemeCatalog
{
    private const string RuntimeSha256 = "AD8629D2EF7E726A990FC9B0B6D8A1051693940C3221FF027A7E6BE45B4CCE60";

    public static byte[] Load()
    {
        var directory = Path.Combine(AppContext.BaseDirectory, "theme");
        var path = Path.Combine(directory, "boot.html");
        if ((File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0 ||
            (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
            throw new InvalidDataException("The built-in theme must use local files.");
        using var file = File.OpenRead(path);
        if (file.Length is <= 0 or > 65536)
            throw new InvalidDataException("Unexpected built-in theme size.");
        var bytes = new byte[(int)file.Length];
        file.ReadExactly(bytes);
        if (Convert.ToHexString(SHA256.HashData(bytes)) != RuntimeSha256)
            throw new InvalidDataException("Built-in theme integrity check failed.");
        return bytes;
    }
}

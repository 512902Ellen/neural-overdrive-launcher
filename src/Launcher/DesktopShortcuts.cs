using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Runtime.Versioning;
using System.Text;

namespace CodexBootAnimation;

internal enum ShortcutKind { Launch, Settings }
internal enum ShortcutResult { Created, AlreadyExists, OldLocation, Conflict }
internal enum ShortcutRemoval { Missing, Removed, Conflict }

[SupportedOSPlatform("windows")]
internal sealed class DesktopShortcuts(string desktop, string executable)
{
    private static readonly Guid ShellLinkClass = new("00021401-0000-0000-C000-000000000046");
    private const string Owner = "Codex Boot Animation portable launcher | ";

    public ShortcutResult Create(ShortcutKind kind)
    {
        string path = PathFor(kind);
        if (Exists(path)) return Existing(path, kind);
        string temporary = Path.Combine(desktop, $".codex-boot-{Guid.NewGuid():N}.lnk");
        try
        {
            UseLink(link =>
            {
                link.SetPath(executable);
                link.SetArguments(Arguments(kind));
                link.SetWorkingDirectory(Path.GetDirectoryName(executable)!);
                link.SetDescription(Owner + kind);
                link.SetIconLocation(executable, 0);
                ((IPersistFile)link).Save(temporary, true);
                return true;
            });
            try { File.Move(temporary, path); }
            catch (IOException) when (Exists(path)) { return Existing(path, kind); }
            return ShortcutResult.Created;
        }
        finally { File.Delete(temporary); }
    }

    public ShortcutRemoval Remove(ShortcutKind kind)
    {
        string path = PathFor(kind);
        if (!Exists(path)) return ShortcutRemoval.Missing;
        if (Existing(path, kind) == ShortcutResult.Conflict) return ShortcutRemoval.Conflict;
        File.Delete(path);
        return ShortcutRemoval.Removed;
    }

    private string PathFor(ShortcutKind kind) => Path.Combine(desktop, kind == ShortcutKind.Settings
        ? "Codex Boot Animation - Launcher Settings.lnk" : "Codex Boot Animation - Launch Codex.lnk");

    private static string Arguments(ShortcutKind kind) => kind == ShortcutKind.Settings ? "--settings" : "";

    private static bool Exists(string path)
    {
        try { _ = File.GetAttributes(path); return true; }
        catch (FileNotFoundException) { return false; }
    }

    private ShortcutResult Existing(string path, ShortcutKind kind)
    {
        if ((File.GetAttributes(path) & (FileAttributes.ReparsePoint | FileAttributes.Directory)) != 0)
            return ShortcutResult.Conflict;
        try
        {
            return UseLink(link =>
            {
                ((IPersistFile)link).Load(path, 0); // STGM_READ. Do not resolve or run the target.
                var description = new StringBuilder(1024);
                var arguments = new StringBuilder(1024);
                var target = new StringBuilder(32768);
                link.GetDescription(description, description.Capacity);
                link.GetArguments(arguments, arguments.Capacity);
                link.GetPath(target, target.Capacity, 0, 4); // SLGP_RAWPATH
                string targetPath = target.ToString();
                if (description.ToString() != Owner + kind || arguments.ToString() != Arguments(kind) ||
                    !Path.IsPathFullyQualified(targetPath) ||
                    !string.Equals(Path.GetFileName(targetPath), Path.GetFileName(executable), StringComparison.OrdinalIgnoreCase))
                    return ShortcutResult.Conflict;
                return string.Equals(targetPath, executable, StringComparison.OrdinalIgnoreCase)
                    ? ShortcutResult.AlreadyExists : ShortcutResult.OldLocation;
            });
        }
        catch (COMException) { return ShortcutResult.Conflict; }
    }

    private static T UseLink<T>(Func<IShellLinkW, T> action)
    {
        var link = (IShellLinkW)Activator.CreateInstance(Type.GetTypeFromCLSID(ShellLinkClass)!)!;
        try { return action(link); }
        finally { Marshal.ReleaseComObject(link); }
    }

    [ComImport, Guid("000214F9-0000-0000-C000-000000000046")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellLinkW
    {
        void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder path, int count, nint findData, uint flags);
        void GetIDList(out nint item);
        void SetIDList(nint item);
        void GetDescription([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder description, int count);
        void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string description);
        void GetWorkingDirectory([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder directory, int count);
        void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string directory);
        void GetArguments([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder arguments, int count);
        void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string arguments);
        void GetHotkey(out ushort hotkey);
        void SetHotkey(ushort hotkey);
        void GetShowCmd(out int command);
        void SetShowCmd(int command);
        void GetIconLocation([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder path, int count, out int index);
        void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string path, int index);
        void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string path, uint reserved);
        void Resolve(nint window, uint flags);
        void SetPath([MarshalAs(UnmanagedType.LPWStr)] string path);
    }
}

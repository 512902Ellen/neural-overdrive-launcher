using System.Diagnostics;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Automation;

if (args.Length != 2)
    throw new ArgumentException("Provide a fresh portable test copy executable and a directory for screenshots.");

string executable = Path.GetFullPath(args[0]);
string evidence = Directory.CreateDirectory(args[1]).FullName;
string dataPath = Path.Combine(Path.GetDirectoryName(executable)!, ".launcher-data");
if (Directory.Exists(dataPath))
    throw new InvalidOperationException("Use a fresh, disposable copy of the launcher.");

WindowProbe.SetThreadDpiAwarenessContext(-4); // DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2
using (var window = SettingsProbe.Start(executable))
{
    window.ExpectSelected("LanguageEnglish");
    window.ExpectSelected("ModeQuick");
    window.WaitForTitle("Launcher Settings");
    window.Capture(Path.Combine(evidence, "settings-en.png"));
    window.Select("LanguageChinese");
    window.WaitForTitle("启动器设置");
    window.Click("SaveSettings");
    window.ExpectStatus("设置已保存。");
}
using (var window = SettingsProbe.Start(executable))
{
    window.WaitForTitle("启动器设置");
    window.ExpectSelected("LanguageChinese");
    window.Capture(Path.Combine(evidence, "settings-zh-CN.png"));
    window.Select("ModeCinematic");
    window.Click("SaveSettings");
    window.ExpectStatus("设置已保存。");
}
using (var window = SettingsProbe.Start(executable))
{
    window.ExpectSelected("ModeCinematic");
    window.Click("ResetSettings");
    window.WaitForTitle("Launcher Settings");
    window.ExpectSelected("ModeQuick");
    window.ExpectSelected("LanguageEnglish");
    window.Click("SaveSettings");
    window.ExpectStatus("Settings saved.");
    window.CheckKeyboardAndCompactLayout();
    window.Click("PreviewAnimation");
    SettingsProbe.WaitFor(() => !window.Find("PreviewAnimation").Current.IsEnabled, TimeSpan.FromSeconds(3),
        "Preview starts a separate launcher invocation.");
    SettingsProbe.WaitFor(() => window.Find("PreviewAnimation").Current.IsEnabled, TimeSpan.FromSeconds(12),
        "Preview finished and restored its control.");
}

string[] events = Directory.GetFiles(Path.Combine(dataPath, "logs"), "launch-*.log")
    .SelectMany(File.ReadLines).ToArray();
if (!events.Any(line => line.EndsWith("result-Completed-none")) || events.Any(line =>
        line.Contains("\tactivation-") || line.Contains("\tclient-") || line.Contains("\thandoff-")))
    throw new InvalidOperationException("The settings preview must complete without any client activation or tracking.");
Console.WriteLine("PASS live language switch, save and reopen, reset, settings exit and preview with no client stages");

string settingsFile = Path.Combine(dataPath, "settings.json");
string previewFile = Path.Combine(Path.GetDirectoryName(executable)!, "theme", "preview.png");
byte[] originalPreview = File.ReadAllBytes(previewFile);
File.WriteAllText(settingsFile, "{ broken JSON");
try
{
    File.WriteAllText(previewFile, "Corrupt test asset");
    using var window = SettingsProbe.Start(executable);
    window.ExpectSelected("ModeQuick");
    window.ExpectSelected("LanguageEnglish");
    window.ExpectStatus("Defaults loaded. Save settings to replace the unreadable or unsupported file.");
    if (window.Find("ThemePreviewUnavailable").Current.IsOffscreen)
        throw new InvalidOperationException("Invalid theme preview should show a status inside the settings window.");
    window.Click("SaveSettings");
    window.ExpectStatus("Settings saved.");
}
finally { File.WriteAllBytes(previewFile, originalPreview); }

string parked = dataPath + "-test-hold";
Directory.Move(dataPath, parked);
try
{
    File.WriteAllText(dataPath, "Blocks the data directory in this temporary test copy.");
    using var window = SettingsProbe.Start(executable);
    window.ExpectSelected("ModeQuick");
    window.Click("SaveSettings");
    window.ExpectStatus("Could not save. Move the launcher to a writable folder and try again.");
}
finally
{
    File.Delete(dataPath);
    Directory.Move(parked, dataPath);
}
Console.WriteLine("PASS corrupt configuration and preview-art recovery, and visible feedback when portable storage is unavailable");

internal sealed class SettingsProbe(Process process, AutomationElement window, nint handle) : IDisposable
{
    public static SettingsProbe Start(string executable)
    {
        var watch = Stopwatch.StartNew();
        var start = new ProcessStartInfo(executable) { UseShellExecute = false, Arguments = "--settings --diagnostics" };
        start.Environment["DOTNET_ROOT"] = Path.Combine(AppContext.BaseDirectory, "unavailable-runtime");
        start.Environment["DOTNET_ROOT_X64"] = start.Environment["DOTNET_ROOT"];
        start.Environment["PATH"] = Environment.SystemDirectory;
        var process = Process.Start(start)!;
        try
        {
            WaitFor(() =>
            {
                process.Refresh();
                if (process.HasExited) throw new InvalidOperationException($"Settings exited with {process.ExitCode}.");
                return process.MainWindowHandle != 0;
            }, TimeSpan.FromSeconds(8), "Settings window opens.");
            var handle = process.MainWindowHandle;
            var probe = new SettingsProbe(process, AutomationElement.FromHandle(handle), handle);
            WaitFor(() => probe.TryFind("ModeQuick") is not null, TimeSpan.FromSeconds(4), "Settings controls become available.");
            Console.WriteLine($"Settings available in {watch.ElapsedMilliseconds} ms at {WindowProbe.GetDpiForWindow(handle)} DPI");
            return probe;
        }
        catch
        {
            process.CloseMainWindow();
            process.Dispose();
            throw;
        }
    }

    private AutomationElement? TryFind(string id) => window.FindFirst(TreeScope.Descendants,
        new PropertyCondition(AutomationElement.AutomationIdProperty, id));

    public AutomationElement Find(string id) => TryFind(id) ?? throw new InvalidOperationException($"Missing control {id}.");

    public void Click(string id) => ((InvokePattern)Find(id).GetCurrentPattern(InvokePattern.Pattern)).Invoke();

    public void Select(string id) => ((SelectionItemPattern)Find(id).GetCurrentPattern(SelectionItemPattern.Pattern)).Select();

    public void WaitForTitle(string title) => WaitFor(() => window.Current.Name == title, TimeSpan.FromSeconds(3),
        "The window title updates to the selected language.");

    public void ExpectSelected(string id) => WaitFor(() =>
        ((SelectionItemPattern)Find(id).GetCurrentPattern(SelectionItemPattern.Pattern)).Current.IsSelected,
        TimeSpan.FromSeconds(3), $"{id} is selected.");

    public void ExpectStatus(string value) => WaitFor(() => Find("SettingsStatus").Current.Name == value,
        TimeSpan.FromSeconds(4), $"Settings status matches '{value}'.");

    public void Capture(string path)
    {
        Thread.Sleep(200);
        WindowProbe.Capture(handle, path);
    }

    public void CheckKeyboardAndCompactLayout()
    {
        var scale = WindowProbe.GetDpiForWindow(handle) / 96.0;
        if (!WindowProbe.SetWindowPos(handle, 0, 0, 0, (int)(660 * scale), (int)(518 * scale), 0x16))
            throw new InvalidOperationException("Could not resize the test window.");

        var quick = Find("ModeQuick");
        quick.SetFocus();
        if (WindowProbe.GetForegroundWindow() != handle)
            throw new InvalidOperationException("Keyboard test requires the launcher test window in the foreground.");
        SendKeys.SendWait("{RIGHT} ");
        ExpectSelected("ModeCinematic");
        if (WindowProbe.GetForegroundWindow() != handle)
            throw new InvalidOperationException("The settings keyboard test lost the foreground.");
        SendKeys.SendWait("{TAB}");
        WaitFor(() => AutomationElement.FocusedElement?.Current.AutomationId.StartsWith("Language") == true,
            TimeSpan.FromSeconds(2), "Tab moves from the mode group to the language controls.");
        Select("ModeQuick");
        Console.WriteLine("PASS compact window layout, arrow/Space mode selection and Tab navigation");
    }

    public static void WaitFor(Func<bool> condition, TimeSpan timeout, string message)
    {
        var timer = Stopwatch.StartNew();
        while (!condition())
        {
            if (timer.Elapsed > timeout) throw new TimeoutException(message);
            Thread.Sleep(35);
        }
    }

    public void Dispose()
    {
        if (!process.HasExited)
        {
            Click("CloseSettings");
            if (!process.WaitForExit(3500) || process.ExitCode != 0)
                throw new InvalidOperationException("Settings did not exit cleanly.");
        }
        process.Dispose();
    }
}

internal static class WindowProbe
{
    [StructLayout(LayoutKind.Sequential)]
    private record struct Rect(int Left, int Top, int Right, int Bottom);

    [DllImport("user32.dll")]
    internal static extern nint SetThreadDpiAwarenessContext(nint context);

    [DllImport("user32.dll")]
    internal static extern uint GetDpiForWindow(nint window);

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(nint window, out Rect rect);

    [DllImport("user32.dll")]
    private static extern bool PrintWindow(nint window, nint dc, uint flags);

    [DllImport("user32.dll")]
    internal static extern bool SetWindowPos(nint window, nint after, int x, int y, int width, int height, uint flags);

    [DllImport("user32.dll")]
    internal static extern nint GetForegroundWindow();

    internal static void Capture(nint window, string path)
    {
        if (!GetWindowRect(window, out Rect rect)) throw new InvalidOperationException("Could not read window bounds.");
        using var bitmap = new Bitmap(rect.Right - rect.Left, rect.Bottom - rect.Top, PixelFormat.Format24bppRgb);
        using var graphics = Graphics.FromImage(bitmap);
        var dc = graphics.GetHdc();
        try
        {
            if (!PrintWindow(window, dc, 2)) throw new InvalidOperationException("Could not capture the settings window.");
        }
        finally { graphics.ReleaseHdc(dc); }
        bitmap.Save(path, ImageFormat.Png);
    }
}

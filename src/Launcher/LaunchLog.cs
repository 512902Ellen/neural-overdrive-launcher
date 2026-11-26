using System.Diagnostics;
using System.IO;

namespace CodexBootAnimation;

internal sealed class LaunchLog : IDisposable
{
    public static string DataDirectory => Path.Combine(AppContext.BaseDirectory, ".launcher-data");
    private readonly Stopwatch time = Stopwatch.StartNew();
    private readonly object gate = new();
    private StreamWriter? writer;

    public LaunchLog(bool enabled)
    {
        if (!enabled) return;
        try
        {
            var directory = Path.Combine(DataDirectory, "logs");
            if (Directory.CreateDirectory(DataDirectory).Attributes.HasFlag(FileAttributes.ReparsePoint) ||
                Directory.CreateDirectory(directory).Attributes.HasFlag(FileAttributes.ReparsePoint))
                return;
            var name = $"launch-{DateTime.UtcNow:yyyyMMddTHHmmssfff}-{Guid.NewGuid():N}.log";
            writer = new StreamWriter(Path.Combine(directory, name)) { AutoFlush = true };
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
    }

    public void Record(string name)
    {
        lock (gate)
        {
            try { writer?.WriteLine($"{time.ElapsedMilliseconds}\t{name}"); }
            catch (IOException) { }
        }
    }

    public void Dispose()
    {
        lock (gate)
        {
            if (writer is null) return;
            using var process = Process.GetCurrentProcess();
            Record($"launcher-cpu-ms-{(long)process.TotalProcessorTime.TotalMilliseconds}");
            Record($"launcher-peak-working-set-bytes-{process.PeakWorkingSet64}");
            try { writer.Dispose(); }
            catch (IOException) { }
            writer = null;
        }
    }
}

internal sealed class TestDirectory : IDisposable
{
    public string Path { get; }

    public TestDirectory()
    {
        string root = Environment.GetEnvironmentVariable("CODEX_BOOT_TEST_ROOT") ?? System.IO.Path.GetTempPath();
        Path = Directory.CreateDirectory(System.IO.Path.Combine(root, $"codex-boot-tests-{Guid.NewGuid():N}")).FullName;
    }

    public void Dispose() => Directory.Delete(Path, recursive: true);
}

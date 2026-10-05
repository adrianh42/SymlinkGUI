namespace SymlinkGUI.Tests;

/// <summary>Creates a unique temp directory and deletes it (without following links) on dispose.</summary>
public sealed class TempDir : IDisposable
{
    public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "SymlinkGUI.Tests", Guid.NewGuid().ToString("N"));

    public TempDir() => Directory.CreateDirectory(Path);

    public string Combine(params string[] parts) => System.IO.Path.Combine([Path, .. parts]);

    public string CreateFile(string name, string content = "hello")
    {
        string p = Combine(name);
        File.WriteAllText(p, content);
        return p;
    }

    public string CreateDir(string name) => Directory.CreateDirectory(Combine(name)).FullName;

    public void Dispose()
    {
        try
        {
            // Directory.Delete(recursive) removes symlinks themselves without touching their targets.
            Directory.Delete(Path, recursive: true);
        }
        catch
        {
        }
    }
}

public sealed class ManualTimeProvider(DateTimeOffset start) : TimeProvider
{
    public DateTimeOffset Now { get; set; } = start;
    public override DateTimeOffset GetUtcNow() => Now;
    public void Advance(TimeSpan by) => Now += by;
}

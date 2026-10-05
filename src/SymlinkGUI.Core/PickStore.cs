using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SymlinkGUI.Core;

/// <summary>
/// Persists the paths chosen with "Pick as Symlink Source" so a later "Drop" (a separate process) can use them.
/// Explorer launches one process per selected item, so picks arriving within <see cref="MergeWindow"/>
/// of each other are merged into a single selection.
/// </summary>
public sealed class PickStore
{
    private readonly string _filePath;
    private readonly TimeProvider _time;
    private readonly string _mutexName;

    public TimeSpan MergeWindow { get; }

    public static string DefaultFilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SymlinkGUI", "picked.json");

    public static PickStore Default { get; } = new(DefaultFilePath);

    public PickStore(string filePath, TimeProvider? time = null, TimeSpan? mergeWindow = null)
    {
        _filePath = Path.GetFullPath(filePath);
        _time = time ?? TimeProvider.System;
        MergeWindow = mergeWindow ?? TimeSpan.FromSeconds(2);

        // One mutex per store file so tests using temp files don't contend with the real store.
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(_filePath.ToUpperInvariant())))[..16];
        _mutexName = $@"Local\SymlinkGUI.PickStore.{hash}";
    }

    /// <summary>Adds paths to the current selection, or starts a new selection if the last pick is older than the merge window.</summary>
    public void Pick(IEnumerable<string> paths)
    {
        var incoming = paths.Select(Path.GetFullPath).ToList();
        if (incoming.Count == 0) return;

        WithLock(() =>
        {
            var now = _time.GetUtcNow();
            var data = Read();
            var list = data is not null && now - data.LastPickedAt <= MergeWindow ? data.Paths : [];

            foreach (var p in incoming)
                if (!list.Contains(p, StringComparer.OrdinalIgnoreCase))
                    list.Add(p);

            Write(new PickData(now, list));
        });
    }

    public IReadOnlyList<string> GetPicked() => WithLock(() => (IReadOnlyList<string>?)Read()?.Paths ?? []);

    public void Clear() => WithLock(() =>
    {
        if (File.Exists(_filePath)) File.Delete(_filePath);
    });

    private PickData? Read()
    {
        try
        {
            if (!File.Exists(_filePath)) return null;
            return JsonSerializer.Deserialize(File.ReadAllText(_filePath), PickStoreJsonContext.Default.PickData);
        }
        catch
        {
            return null; // Corrupt file: treat as empty.
        }
    }

    private void Write(PickData data)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);
        string tmp = _filePath + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(data, PickStoreJsonContext.Default.PickData));
        File.Move(tmp, _filePath, overwrite: true);
    }

    private void WithLock(Action action) => WithLock(() => { action(); return 0; });

    private T WithLock<T>(Func<T> func)
    {
        using var mutex = new Mutex(false, _mutexName);
        bool owned = false;
        try
        {
            try
            {
                owned = mutex.WaitOne(TimeSpan.FromSeconds(5));
            }
            catch (AbandonedMutexException)
            {
                owned = true;
            }
            return func();
        }
        finally
        {
            if (owned) mutex.ReleaseMutex();
        }
    }
}

internal sealed record PickData(DateTimeOffset LastPickedAt, List<string> Paths);

[JsonSerializable(typeof(PickData))]
[JsonSourceGenerationOptions(WriteIndented = true)]
internal sealed partial class PickStoreJsonContext : JsonSerializerContext;

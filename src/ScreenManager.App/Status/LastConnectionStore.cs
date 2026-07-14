using System.IO;
using System.Text.Json;
using System.Threading;
using OrderBuddy.ScreenManager.Core.Config;

namespace OrderBuddy.ScreenManager.App.Status;

public interface ILastConnectionStore
{
    DateTimeOffset? GetLastSuccessUtc(string screenId);
    Task SetLastSuccessUtcAsync(string screenId, DateTimeOffset utcTimestamp, CancellationToken cancellationToken = default);
}

public sealed class LastConnectionStore : ILastConnectionStore
{
    private readonly string _filePath;
    private readonly object _sync = new();
    private Dictionary<string, DateTimeOffset> _cache = new(StringComparer.OrdinalIgnoreCase);
    private bool _loaded;

    public LastConnectionStore(ConfigServiceOptions options)
    {
        var rootDirectory = Path.GetDirectoryName(options.ConfigFilePath)
            ?? throw new InvalidOperationException("Config root directory is unavailable.");

        Directory.CreateDirectory(rootDirectory);
        _filePath = Path.Combine(rootDirectory, "screen-status.json");
    }

    public DateTimeOffset? GetLastSuccessUtc(string screenId)
    {
        lock (_sync)
        {
            EnsureLoadedUnsafe();
            return _cache.TryGetValue(screenId, out var value) ? value : null;
        }
    }

    public async Task SetLastSuccessUtcAsync(string screenId, DateTimeOffset utcTimestamp, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        Dictionary<string, DateTimeOffset> snapshot;
        lock (_sync)
        {
            EnsureLoadedUnsafe();
            _cache[screenId] = utcTimestamp;
            snapshot = new Dictionary<string, DateTimeOffset>(_cache, StringComparer.OrdinalIgnoreCase);
        }

        var tempFile = _filePath + ".tmp";
        var payload = JsonSerializer.Serialize(snapshot, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(tempFile, payload, cancellationToken);

        if (File.Exists(_filePath))
        {
            File.Replace(tempFile, _filePath, null, true);
        }
        else
        {
            File.Move(tempFile, _filePath, false);
        }
    }

    private void EnsureLoadedUnsafe()
    {
        if (_loaded)
        {
            return;
        }

        _loaded = true;

        if (!File.Exists(_filePath))
        {
            return;
        }

        try
        {
            var json = File.ReadAllText(_filePath);
            var loaded = JsonSerializer.Deserialize<Dictionary<string, DateTimeOffset>>(json);
            if (loaded is not null)
            {
                _cache = new Dictionary<string, DateTimeOffset>(loaded, StringComparer.OrdinalIgnoreCase);
            }
        }
        catch
        {
            _cache = new Dictionary<string, DateTimeOffset>(StringComparer.OrdinalIgnoreCase);
        }
    }
}

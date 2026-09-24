using System.Text.Json.Serialization;

namespace WebHosterMcp.Core;

/// <summary>
/// registry.json schema v1.
/// </summary>
public class Registry
{
    public int Version { get; set; } = 1;
    public Dictionary<string, SiteEntry> Sites { get; set; } = new();
}

/// <summary>
/// Single site entry. timestamps = local server time (DateTime.Now, ISO-8601 without 'Z').
/// </summary>
public class SiteEntry
{
    public string SitePath { get; set; } = "";
    public string Type { get; set; } = "files";
    public string? Path { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime UpdatedAt { get; set; } = DateTime.Now;
    public int RetentionSeconds { get; set; } = 0;
}

/// <summary>
/// Persistence layer for registry.json.
/// - Atomic writes via tmp + File.Move(overwrite) (no half-state on disk)
/// - SemaphoreSlim mutex (one write operation at a time)
/// - FileShare.Read for concurrent reads during a write
/// - snake_case JSON via PropertyNamingPolicy.SnakeCaseLower
/// </summary>
public class SiteRegistry
{
    private readonly string _path;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private Registry _registry = new();

    public SiteRegistry(string path)
    {
        _path = path;
    }

    /// <summary>Loads registry.json from disk into memory (idempotent, empty when not present).</summary>
    public async Task LoadAsync(CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            await LoadFromDiskAsync(ct);
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>Writes the current in-memory state atomically to registry.json.</summary>
    public async Task SaveAsync(CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            await SaveToDiskAsync(ct);
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// Deploy/Update of a site (atomic: load + modify + save under lock).
    /// Creates a new entry or updates an existing one. Preserves CreatedAt on update.
    /// </summary>
    public async Task<SiteEntry> DeployAsync(SiteEntry newEntry, CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            await LoadFromDiskAsync(ct);
            if (_registry.Sites.TryGetValue(newEntry.SitePath, out var existing))
            {
                newEntry.CreatedAt = existing.CreatedAt;
            }
            else
            {
                newEntry.CreatedAt = DateTime.Now;
            }
            newEntry.UpdatedAt = DateTime.Now;
            _registry.Sites[newEntry.SitePath] = newEntry;
            await SaveToDiskAsync(ct);
            return newEntry;
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>Deletes a site (atomic: load + remove + save). Returns true if the site existed.</summary>
    public async Task<bool> DeleteAsync(string sitePath, CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            await LoadFromDiskAsync(ct);
            if (!_registry.Sites.Remove(sitePath)) return false;
            await SaveToDiskAsync(ct);
            return true;
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>Reads a site entry (null if not present). Auto-loads from disk.</summary>
    public async Task<SiteEntry?> ReadAsync(string sitePath, CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            await LoadFromDiskAsync(ct);
            return _registry.Sites.TryGetValue(sitePath, out var entry) ? entry : null;
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>Reads all site entries as a snapshot (copies the values).</summary>
    public async Task<List<SiteEntry>> ReadAllAsync(CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            await LoadFromDiskAsync(ct);
            return _registry.Sites.Values.ToList();
        }
        finally
        {
            _lock.Release();
        }
    }

    // === Private helpers — MUST be called under _lock ===

    private async Task LoadFromDiskAsync(CancellationToken ct)
    {
        if (!File.Exists(_path))
        {
            _registry = new Registry();
            return;
        }
        await using var stream = new FileStream(_path, FileMode.Open, FileAccess.Read, FileShare.Read);
        _registry = (await System.Text.Json.JsonSerializer.DeserializeAsync(
            stream,
            SiteRegistryJsonContext.Default.Registry,
            ct)) ?? new Registry();
    }

    private async Task SaveToDiskAsync(CancellationToken ct)
    {
        var tmpPath = _path + ".tmp";
        await using (var stream = new FileStream(tmpPath, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            await System.Text.Json.JsonSerializer.SerializeAsync(
                stream,
                _registry,
                SiteRegistryJsonContext.Default.Registry,
                ct);
        }
        File.Move(tmpPath, _path, overwrite: true);
    }
}

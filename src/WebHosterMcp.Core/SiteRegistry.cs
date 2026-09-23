using System.Text.Json.Serialization;

namespace WebHosterMcp.Core;

/// <summary>
/// registry.json Schema v1 (siehe specs/mvp1.md §Storage).
/// </summary>
public class Registry
{
    public int Version { get; set; } = 1;
    public Dictionary<string, SiteEntry> Sites { get; set; } = new();
}

/// <summary>
/// Einzelner Site-Eintrag. timestamps = lokale Server-Zeit (DateTime.Now, ISO-8601 ohne 'Z').
/// </summary>
public class SiteEntry
{
    public string SitePath { get; set; } = "";
    public string Type { get; set; } = "files";
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime UpdatedAt { get; set; } = DateTime.Now;
    public int RetentionSeconds { get; set; } = 0;
}

/// <summary>
/// Persistenz-Schicht für registry.json.
/// - Atomic writes via tmp + File.Move(overwrite) (kein Half-State auf Disk)
/// - SemaphoreSlim-Mutex (eine Schreib-Operation zur Zeit)
/// - FileShare.Read für parallele Lesezugriffe während eines Writes
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

    /// <summary>Lädt registry.json von Disk in den Speicher (idempotent, leer wenn nicht vorhanden).</summary>
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

    /// <summary>Schreibt den aktuellen Speicherzustand atomar nach registry.json.</summary>
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
    /// Deploy/Update einer Site (atomic: load + modify + save unter Lock).
    /// Legt neu an oder aktualisiert bestehend. Erhält CreatedAt bei Update.
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

    /// <summary>Löscht eine Site (atomic: load + remove + save). Returns true wenn Site existierte.</summary>
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

    /// <summary>Liest einen Site-Eintrag (null wenn nicht vorhanden). Auto-loadet von Disk.</summary>
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

    /// <summary>Liest alle Site-Einträge als Snapshot (kopiert die Values).</summary>
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

    // === Private Helpers — MÜSSEN unter _lock aufgerufen werden ===

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

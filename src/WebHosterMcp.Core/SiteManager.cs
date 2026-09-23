using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace WebHosterMcp.Core;

/// <summary>
/// Site-Lifecycle-Manager für MVP1 `deploy` (type=files), `list_sites`, `get_site_info`, `delete_site`.
/// - Input-Validation per MVP1 v1.3 (type/site_path/mode/type_immutable/path_traversal/path_too_long/duplicate_path/file_too_large/missing_content/invalid_file_entry)
/// - Atomic file writes (tmp + File.Move overwrite)
/// - Mode merge/replace semantics + empty-files handling
/// - result_url uses LAN-IP if Host:Ip == 0.0.0.0
/// </summary>
public class SiteManager
{
    private readonly SiteRegistry _registry;
    private readonly SitesOptions _sitesOptions;
    private readonly HostOptions _hostOptions;
    private readonly string _sitesRoot;

    private static readonly Regex SitePathRegex = new(@"^[a-z0-9-]{3,32}$", RegexOptions.Compiled);
    private static readonly HashSet<string> AllowedTypes = new(StringComparer.Ordinal)
    {
        "files", "folder", "a2ui", "json-schema-form"
    };

    private static readonly Dictionary<string, string> ContentTypeByExtension = new(StringComparer.OrdinalIgnoreCase)
    {
        [".html"] = "text/html; charset=utf-8",
        [".htm"] = "text/html; charset=utf-8",
        [".css"] = "text/css; charset=utf-8",
        [".js"] = "application/javascript; charset=utf-8",
        [".mjs"] = "application/javascript; charset=utf-8",
        [".json"] = "application/json; charset=utf-8",
        [".svg"] = "image/svg+xml",
        [".png"] = "image/png",
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg",
        [".gif"] = "image/gif",
        [".webp"] = "image/webp",
        [".ico"] = "image/x-icon",
        [".woff"] = "font/woff",
        [".woff2"] = "font/woff2",
        [".ttf"] = "font/ttf",
        [".txt"] = "text/plain; charset=utf-8"
    };

    public SiteManager(
        SiteRegistry registry,
        SitesOptions sitesOptions,
        HostOptions hostOptions)
    {
        _registry = registry;
        _sitesOptions = sitesOptions;
        _hostOptions = hostOptions;
        _sitesRoot = Path.GetFullPath(sitesOptions.SitesRoot);
        Directory.CreateDirectory(_sitesRoot);
    }

    // === Public API (called by MCP layer / tools) ===

    /// <summary>Deploy/Update einer Site. Atomar (load + modify + save) innerhalb der Registry-Locks.</summary>
    public async Task<DeployResult> DeployAsync(DeployRequest request, CancellationToken ct = default)
    {
        // --- Validation ---
        if (!AllowedTypes.Contains(request.Type))
            return ErrorResult("invalid_type");

        var sitePath = request.SitePath;
        if (string.IsNullOrEmpty(sitePath))
        {
            sitePath = GenerateRandomSitePath();
        }
        else if (!SitePathRegex.IsMatch(sitePath))
        {
            return ErrorResult("invalid_site_id");
        }

        var mode = string.IsNullOrEmpty(request.Mode) ? "merge" : request.Mode;
        if (mode != "merge" && mode != "replace")
        {
            return ErrorResult("invalid_mode", sitePath);
        }

        var existing = await _registry.ReadAsync(sitePath, ct);

        if (existing != null && existing.Type != request.Type)
        {
            return ErrorResult("type_immutable", sitePath);
        }

        // --- Process files (nur für type=files; andere Types: keine Files-Operations) ---
        List<DeployResultFile>? resultFiles = null;
        if (request.Type == "files")
        {
            var fileResult = await WriteFilesAsync(sitePath, mode, request.Files ?? Array.Empty<FileEntry>(), ct);
            if (fileResult.Error != null)
            {
                return ErrorResult(fileResult.Error, sitePath);
            }
            resultFiles = fileResult.ResultFiles;
        }

        // --- Retention ---
        var retentionSeconds = request.RetentionSeconds ?? existing?.RetentionSeconds ?? 0;

        // --- Registry Update ---
        var entry = new SiteEntry
        {
            SitePath = sitePath,
            Type = request.Type,
            CreatedAt = existing?.CreatedAt ?? DateTime.Now,
            UpdatedAt = DateTime.Now,
            RetentionSeconds = retentionSeconds
        };
        await _registry.DeployAsync(entry, ct);

        // --- Result ---
        var url = BuildSiteUrl(sitePath);
        if (resultFiles != null)
        {
            var baseUrl = url.TrimEnd('/');
            for (var i = 0; i < resultFiles.Count; i++)
            {
                resultFiles[i] = resultFiles[i] with { ResultPath = $"{baseUrl}/{resultFiles[i].Path}" };
            }
        }

        return new DeployResult(sitePath, url, resultFiles, null);
    }

    /// <summary>Listet alle Sites (MVP1 `list_sites`).</summary>
    public async Task<List<SiteEntry>> ListAsync(CancellationToken ct = default)
        => await _registry.ReadAllAsync(ct);

    /// <summary>Liest eine Site (MVP1 `get_site_info`).</summary>
    public async Task<SiteEntry?> GetAsync(string sitePath, CancellationToken ct = default)
        => await _registry.ReadAsync(sitePath, ct);

    /// <summary>Löscht eine Site (MVP1 `delete_site`). Entfernt Registry-Eintrag UND Site-Folder.</summary>
    public async Task<bool> DeleteAsync(string sitePath, CancellationToken ct = default)
    {
        await _registry.LoadAsync(ct);
        var deleted = await _registry.DeleteAsync(sitePath, ct);
        if (!deleted) return false;
        var siteFolder = Path.Combine(_sitesRoot, sitePath);
        if (Directory.Exists(siteFolder))
        {
            Directory.Delete(siteFolder, recursive: true);
        }
        return true;
    }

    /// <summary>Zählt Files rekursiv (für file_count).</summary>
    public int CountFiles(string sitePath)
    {
        var siteFolder = Path.Combine(_sitesRoot, sitePath);
        return Directory.Exists(siteFolder)
            ? Directory.EnumerateFiles(siteFolder, "*", SearchOption.AllDirectories).Count()
            : 0;
    }

    /// <summary>Listet alle Files einer Site rekursiv mit relativen Pfaden + result_path URLs.</summary>
    public IReadOnlyList<DeployResultFile> ListFiles(string sitePath)
    {
        var siteFolder = Path.Combine(_sitesRoot, sitePath);
        if (!Directory.Exists(siteFolder)) return Array.Empty<DeployResultFile>();

        var baseUrl = BuildSiteUrl(sitePath).TrimEnd('/');

        return Directory
            .EnumerateFiles(siteFolder, "*", SearchOption.AllDirectories)
            .Select(path =>
            {
                var rel = Path.GetRelativePath(siteFolder, path).Replace('\\', '/');
                return new DeployResultFile(rel, $"{baseUrl}/{rel}");
            })
            .OrderBy(f => f.Path, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>Site-Base-URL mit abschließendem '/'.</summary>
    public string GetSiteUrl(string sitePath) => BuildSiteUrl(sitePath);

    /// <summary>Content-Type aus File-Extension (MVP1 §Default-Content-Type-Mapping).</summary>
    public string GetContentType(string filePath)
    {
        var ext = Path.GetExtension(filePath);
        return ContentTypeByExtension.TryGetValue(ext, out var ct) ? ct : "application/octet-stream";
    }

    // === Private helpers ===

    private async Task<FileWriteResult> WriteFilesAsync(
        string sitePath, string mode, IReadOnlyList<FileEntry> files, CancellationToken ct)
    {
        var siteFolder = Path.Combine(_sitesRoot, sitePath);
        Directory.CreateDirectory(siteFolder);

        // mode=replace: clear all files first
        if (mode == "replace")
        {
            foreach (var f in Directory.GetFiles(siteFolder, "*", SearchOption.AllDirectories))
            {
                File.Delete(f);
            }
        }

        // --- Validation Pass ---
        var seenPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var f in files)
        {
            if (f.Path.Contains("..")) return new FileWriteResult { Error = "path_traversal" };
            if (f.Path.Length > 260) return new FileWriteResult { Error = "path_too_long" };
            if (!seenPaths.Add(f.Path)) return new FileWriteResult { Error = "duplicate_path" };

            // delete + content/src
            if (f.Delete && (f.Content != null || f.Src != null))
                return new FileWriteResult { Error = "invalid_file_entry" };

            // content + src
            if (!f.Delete && f.Content != null && f.Src != null)
                return new FileWriteResult { Error = "invalid_file_entry" };

            // missing content/src
            if (!f.Delete && f.Content == null && f.Src == null)
                return new FileWriteResult { Error = "missing_content" };
        }

        // --- Write Pass ---
        var resultFiles = new List<DeployResultFile>();
        foreach (var f in files)
        {
            var filePath = ResolveFilePath(siteFolder, f.Path);

            // delete: true → merge-mode: Datei von Disk löschen; replace-mode: bereits durch clear-all gelöscht
            if (f.Delete)
            {
                if (File.Exists(filePath))
                    File.Delete(filePath);
                continue;
            }

            byte[] bytes;
            if (f.Content != null)
            {
                // MVP1 v1.3: content ist plain string, KEINE Data-URL-Sonderbehandlung
                bytes = Encoding.UTF8.GetBytes(f.Content);
            }
            else if (f.Src!.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            {
                bytes = ParseDataUrl(f.Src!);
            }
            else
            {
                // MVP1: lokaler Pfad (Data URL → src; HTTP-URL → MVP3)
                if (!File.Exists(f.Src!))
                {
                    return new FileWriteResult { Error = "src_not_found" };
                }
                bytes = await File.ReadAllBytesAsync(f.Src!, ct);
            }

            if (bytes.Length > _sitesOptions.MaxFileSizeBytes)
            {
                return new FileWriteResult { Error = "file_too_large" };
            }

            Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);

            // Atomic write: tmp + Move
            var tmpPath = filePath + ".tmp";
            await File.WriteAllBytesAsync(tmpPath, bytes, ct);
            File.Move(tmpPath, filePath, overwrite: true);

            resultFiles.Add(new DeployResultFile(f.Path, ""));
        }

        return new FileWriteResult { Error = null, ResultFiles = resultFiles };
    }

    private static string ResolveFilePath(string siteFolder, string userPath)
    {
        // Absolute (Unix / or Windows C:) → as-is; Relative → combine with siteFolder
        var isAbsolute = userPath.StartsWith('/') || (userPath.Length >= 2 && userPath[1] == ':');
        return Path.GetFullPath(isAbsolute ? userPath : Path.Combine(siteFolder, userPath));
    }

    private static byte[] ParseDataUrl(string dataUrl)
    {
        var prefix = dataUrl.AsSpan(5); // skip "data:"
        var semicolon = prefix.IndexOf(';');
        var rest = semicolon > 0 ? prefix.Slice(semicolon + 1) : prefix;
        if (rest.StartsWith("base64,", StringComparison.OrdinalIgnoreCase))
        {
            return Convert.FromBase64String(rest.Slice(7).ToString());
        }
        return Encoding.UTF8.GetBytes(Uri.UnescapeDataString(rest.ToString()));
    }

    private static string GenerateRandomSitePath()
    {
        const string chars = "abcdefghijklmnopqrstuvwxyz0123456789";
        var bytes = new byte[8];
        RandomNumberGenerator.Fill(bytes);
        return new string(bytes.Select(b => chars[b % chars.Length]).ToArray());
    }

    private string BuildSiteUrl(string sitePath)
    {
        var host = _hostOptions.Ip;
        if (host == "0.0.0.0")
        {
            var lan = LanIpDetector.GetLanIpv4();
            if (!string.IsNullOrEmpty(lan)) host = lan;
        }
        return $"http://{host}:{_hostOptions.Port}/{sitePath}/";
    }

    private static DeployResult ErrorResult(string code, string? sitePath = null)
        => new(sitePath, null, null, code);

    private sealed record FileWriteResult
    {
        public string? Error { get; init; }
        public List<DeployResultFile>? ResultFiles { get; init; }
    }
}

public record DeployRequest(
    string? SitePath,
    string Type = "files",
    string Mode = "merge",
    int? RetentionSeconds = null,
    IReadOnlyList<FileEntry>? Files = null);

public record FileEntry(string Path, string? Content = null, bool Delete = false, string? Src = null);

public record DeployResult(
    string? SitePath,
    string? Url,
    IReadOnlyList<DeployResultFile>? Files,
    string? Error);

public record DeployResultFile(string Path, string ResultPath);

using System.Net.Http;
using System.Text.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace WebHosterMcp.Core;

/// <summary>
/// Site lifecycle manager for the MCP tools `deploy` (type=files), `list_sites`, `get_site_info`, `delete_site`.
/// - Input validation: type/site_path/mode/type_immutable/path_traversal/path_too_long/duplicate_path/file_too_large/missing_content/invalid_file_entry
/// - Atomic file writes (tmp + File.Move overwrite)
/// - Mode merge/replace semantics + empty-files handling
/// - result_url uses the LAN IP if Host:Ip == 0.0.0.0
/// </summary>
public class SiteManager
{
    private readonly SiteRegistry _registry;
    private readonly SitesOptions _sitesOptions;
    private readonly HostOptions _hostOptions;
    private readonly RetentionOptions _retentionOptions;
    private readonly string _sitesRoot;
    private readonly SrcOptions _srcOptions;
    private readonly HttpMessageHandler? _httpMessageHandler;

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
        HostOptions hostOptions,
        RetentionOptions? retentionOptions = null,
        SrcOptions? srcOptions = null,
        HttpMessageHandler? httpMessageHandler = null)
    {
        _registry = registry;
        _sitesOptions = sitesOptions;
        _hostOptions = hostOptions;
        _retentionOptions = retentionOptions ?? new RetentionOptions();
        _srcOptions = srcOptions ?? new SrcOptions();
        _httpMessageHandler = httpMessageHandler;
        _sitesRoot = Path.GetFullPath(sitesOptions.SitesRoot);
        Directory.CreateDirectory(_sitesRoot);
    }

    // === Public API (called by MCP layer / tools) ===

    /// <summary>Deploy/Update of a site. Atomic (load + modify + save) inside the registry locks.</summary>
    public async Task<DeployResult> DeployAsync(DeployRequest request, CancellationToken ct = default)
    {
        // --- Validation: type & sitePath ---
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
        if (request.Type == "files" && mode != "merge" && mode != "replace")
        {
            return ErrorResult("invalid_mode", sitePath);
        }

        var existing = await _registry.ReadAsync(sitePath, ct);

        if (existing != null && existing.Type != request.Type)
        {
            return ErrorResult("type_immutable", sitePath);
        }

        // --- Type-specific validation & storage ---
        List<DeployResultFile>? resultFiles = null;
        string? pathForRegistry = null;

        switch (request.Type)
        {
            case "files":
                if (request.Path != null)
                    return ErrorResult("path_not_allowed_for_files", sitePath);
                if (request.Payload != null)
                    return ErrorResult("payload_not_allowed_for_files", sitePath);

                var fileResult = await WriteFilesAsync(sitePath, mode, request.Files ?? Array.Empty<FileEntry>(), ct);
                if (fileResult.Error != null)
                {
                    return ErrorResult(fileResult.Error, sitePath);
                }
                resultFiles = fileResult.ResultFiles;
                break;

            case "folder":
                if (request.Files != null && request.Files.Count > 0)
                    return ErrorResult("files_not_allowed_for_folder", sitePath);
                if (request.Payload != null)
                    return ErrorResult("payload_not_allowed_for_folder", sitePath);

                if (string.IsNullOrEmpty(request.Path))
                    return ErrorResult("path_required", sitePath);
                if (request.Path.Contains("..", StringComparison.Ordinal))
                    return ErrorResult("path_traversal", sitePath);
                if (request.Path.Length > 260)
                    return ErrorResult("path_too_long", sitePath);
                pathForRegistry = request.Path;
                break;

            case "a2ui":
            case "json-schema-form":
                if (request.Files != null && request.Files.Count > 0)
                    return ErrorResult($"files_not_allowed_for_{request.Type.Replace("-", "_")}", sitePath);
                if (request.Path != null)
                    return ErrorResult("path_not_allowed_for_render", sitePath);

                if (request.Payload == null)
                    return ErrorResult("payload_required", sitePath);

                var payloadError = await WritePayloadAsync(sitePath, request.Payload.Value, ct);
                if (payloadError != null)
                    return ErrorResult(payloadError, sitePath);
                break;
        }

        // --- Retention ---
        var retentionSeconds = request.RetentionSeconds
                               ?? existing?.RetentionSeconds
                               ?? _retentionOptions.DefaultTtlSeconds;

        // --- Registry Update ---
        var entry = new SiteEntry
        {
            SitePath = sitePath,
            Type = request.Type,
            Path = pathForRegistry,
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

    /// <summary>Lists all sites (for `list_sites`).</summary>
    public async Task<List<SiteEntry>> ListAsync(CancellationToken ct = default)
        => await _registry.ReadAllAsync(ct);

    /// <summary>Reads a site (for `get_site_info`).</summary>
    public async Task<SiteEntry?> GetAsync(string sitePath, CancellationToken ct = default)
        => await _registry.ReadAsync(sitePath, ct);

    /// <summary>Deletes a site (for `delete_site`). Removes the registry entry AND the site folder.</summary>
    public async Task<bool> DeleteAsync(string sitePath, CancellationToken ct = default)
    {
        var entry = await _registry.ReadAsync(sitePath, ct);
        if (entry is null)
        {
            return false;
        }

        var deleted = await _registry.DeleteAsync(sitePath, ct);
        if (!deleted) return false;

        // folder type: only remove the registry entry; leave the host folder untouched
        if (string.Equals(entry.Type, "folder", StringComparison.Ordinal))
        {
            return true;
        }

        var siteFolder = Path.Combine(_sitesRoot, sitePath);
        if (Directory.Exists(siteFolder))
        {
            Directory.Delete(siteFolder, recursive: true);
        }
        return true;
    }

    public async Task<bool?> DeleteFileAsync(string sitePath, string filePath, CancellationToken ct = default)
    {
        var entry = await _registry.ReadAsync(sitePath, ct);
        if (entry is null)
        {
            return null;
        }

        if (!string.Equals(entry.Type, "files", StringComparison.Ordinal))
        {
            return null;
        }

        var normalized = filePath.Replace('/', Path.DirectorySeparatorChar);
        if (normalized.Contains("..", StringComparison.Ordinal))
        {
            return false;
        }

        var siteFolder = Path.GetFullPath(Path.Combine(_sitesRoot, sitePath));
        var physicalPath = Path.GetFullPath(Path.Combine(siteFolder, normalized));

        if (!physicalPath.StartsWith(siteFolder, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!File.Exists(physicalPath))
        {
            return false;
        }

        File.Delete(physicalPath);
        return true;
    }

    /// <summary>Counts files recursively (for file_count). For folder-type returns the host folder, otherwise the site folder.</summary>
    public int CountFiles(string sitePath)
    {
        var entry = GetEntrySync(sitePath);
        var baseFolder = entry is not null
            ? ResolveBaseFolder(entry)
            : Path.Combine(_sitesRoot, sitePath);

        return baseFolder != null && Directory.Exists(baseFolder)
            ? Directory.EnumerateFiles(baseFolder, "*", SearchOption.AllDirectories).Count()
            : 0;
    }

    /// <summary>Lists all files of a site recursively with relative paths + result_path URLs. For folder-type returns the host folder, otherwise the site folder.</summary>
    public IReadOnlyList<DeployResultFile> ListFiles(string sitePath)
    {
        var entry = GetEntrySync(sitePath);
        var baseFolder = entry is not null
            ? ResolveBaseFolder(entry)
            : Path.Combine(_sitesRoot, sitePath);

        if (baseFolder is null || !Directory.Exists(baseFolder)) return Array.Empty<DeployResultFile>();

        var baseUrl = BuildSiteUrl(sitePath).TrimEnd('/');

        return Directory
            .EnumerateFiles(baseFolder, "*", SearchOption.AllDirectories)
            .Select(path =>
            {
                var rel = Path.GetRelativePath(baseFolder, path).Replace('\\', '/');
                return new DeployResultFile(rel, $"{baseUrl}/{rel}");
            })
            .OrderBy(f => f.Path, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private SiteEntry? GetEntrySync(string sitePath)
    {
        // Ensure registry is loaded (idempotent, fast no-op when already loaded)
        _registry.LoadAsync().GetAwaiter().GetResult();
        return _registry.ReadAsync(sitePath).GetAwaiter().GetResult();
    }

    /// <summary>Returns the host folder for folder-type, otherwise the site folder under SitesRoot.</summary>
    private string? ResolveBaseFolder(SiteEntry entry)
    {
        if (string.Equals(entry.Type, "folder", StringComparison.Ordinal) && !string.IsNullOrEmpty(entry.Path))
            return entry.Path;
        return Path.Combine(_sitesRoot, entry.SitePath);
    }

    /// <summary>Returns the host folder for folder-type, otherwise null.</summary>
    public string? GetHostFolderPath(SiteEntry entry)
        => string.Equals(entry.Type, "folder", StringComparison.Ordinal) && !string.IsNullOrEmpty(entry.Path)
            ? entry.Path
            : null;

    /// <summary>Returns the site folder under <SitesRoot>/<site>/ (for files/a2ui/schema-form).</summary>
    public string GetSiteFolderPath(string sitePath) => Path.Combine(_sitesRoot, sitePath);

    /// <summary>Reads payload.json (a2ui / schema-form). Null when not present.</summary>
    public async Task<JsonElement?> GetPayloadAsync(string sitePath, CancellationToken ct = default)
    {
        var payloadPath = Path.Combine(_sitesRoot, sitePath, "payload.json");
        if (!File.Exists(payloadPath)) return null;

        await using var stream = File.OpenRead(payloadPath);
        return await JsonSerializer.DeserializeAsync<JsonElement>(stream, cancellationToken: ct);
    }

    /// <summary>Stores a submission body. Returns (id, receivedAt, error).</summary>
    public async Task<(string SubmissionId, DateTime ReceivedAt, string? Error)> SaveSubmissionAsync(string sitePath, string body, CancellationToken ct = default)
    {
        var siteFolder = Path.Combine(_sitesRoot, sitePath);
        if (!Directory.Exists(siteFolder))
            return ("", DateTime.MinValue, "site_not_found");

        if (body.Length > _sitesOptions.MaxSubmissionSizeBytes)
            return ("", DateTime.MinValue, "submission_too_large");

        try
        {
            using var _ = JsonDocument.Parse(body);
        }
        catch
        {
            return ("", DateTime.MinValue, "invalid_json");
        }

        var timestamp = DateTime.Now;
        var submissionId = $"{timestamp:yyyy-MM-ddTHH-mm-ss}_{GenerateRandomString(8)}";
        var submissionPath = Path.Combine(siteFolder, $"{submissionId}.json");

        var bytes = Encoding.UTF8.GetBytes(body);
        var tmpPath = submissionPath + ".tmp";
        await File.WriteAllBytesAsync(tmpPath, bytes, ct);
        File.Move(tmpPath, submissionPath, overwrite: true);

        return (submissionId, timestamp, null);
    }

    /// <summary>Lists submissions of a json-schema-form site (newest first).</summary>
    public async Task<IReadOnlyList<SubmissionInfo>> GetSubmissionsAsync(string sitePath, DateTime? since, int limit, CancellationToken ct = default)
    {
        var siteFolder = Path.Combine(_sitesRoot, sitePath);
        if (!Directory.Exists(siteFolder)) return Array.Empty<SubmissionInfo>();

        var submissions = new List<SubmissionInfo>();
        foreach (var file in Directory.EnumerateFiles(siteFolder, "*.json"))
        {
            var fileName = Path.GetFileName(file);
            if (string.Equals(fileName, "payload.json", StringComparison.Ordinal)) continue;
            if (!IsSubmissionFileName(fileName)) continue;

            var id = Path.GetFileNameWithoutExtension(fileName);
            var receivedAt = File.GetLastWriteTime(file);
            if (since.HasValue && receivedAt <= since.Value) continue;

            await using var stream = File.OpenRead(file);
            var data = await JsonSerializer.DeserializeAsync<JsonElement>(stream, cancellationToken: ct);
            submissions.Add(new SubmissionInfo(id, receivedAt, data));
        }

        return submissions
            .OrderByDescending(s => s.ReceivedAt)
            .Take(Math.Clamp(limit, 1, 500))
            .ToList();
    }

    /// <summary>Site base URL with trailing '/'.</summary>
    public string GetSiteUrl(string sitePath) => BuildSiteUrl(sitePath);

    /// <summary>Content type from file extension.</summary>
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

            // delete: true → merge-mode: delete file from disk; replace-mode: already deleted by clear-all
            if (f.Delete)
            {
                if (File.Exists(filePath))
                    File.Delete(filePath);
                continue;
            }

            byte[] bytes;
            if (f.Content != null)
            {
                // content ist plain string, KEINE Data-URL-Sonderbehandlung
                bytes = Encoding.UTF8.GetBytes(f.Content);

                // 1 MB limit only for inline 'content'
                if (bytes.Length > _sitesOptions.MaxFileSizeBytes)
                {
                    return new FileWriteResult { Error = "file_too_large" };
                }
            }
            else
            {
                var srcResult = await ResolveSrcBytesAsync(f.Src!, ct);
                if (srcResult.Error != null)
                {
                    return new FileWriteResult { Error = srcResult.Error };
                }
                bytes = srcResult.Bytes!;
                // no 1 MB limit for 'src' downloads
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

    /// <summary>
    /// Resolves `src` to bytes. Distinguishes three sources (case-insensitive):
    /// <list type="bullet">
    /// <item><c>data:</c> URL → <see cref="ParseDataUrl"/></item>
    /// <item><c>http://</c> / <c>https://</c> → <see cref="FetchHttpBytesAsync"/></item>
    /// <item>otherwise → local path (incl. UNC)</item>
    /// </list>
    /// </summary>
    private async Task<(byte[] Bytes, string? Error)> ResolveSrcBytesAsync(string src, CancellationToken ct)
    {
        if (src.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                return (ParseDataUrl(src), null);
            }
            catch
            {
                return (Array.Empty<byte>(), "src_invalid_data_url");
            }
        }

        if (src.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            src.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return await FetchHttpBytesAsync(src, ct);
        }

        // Local file path (Windows absolut, Unix absolut, UNC)
        if (!File.Exists(src))
        {
            return (Array.Empty<byte>(), "src_not_found");
        }

        return (await File.ReadAllBytesAsync(src, ct), null);
    }

    /// <summary>
    /// HTTP/HTTPS Download via HttpClient.
    /// - Timeout: <see cref="SrcOptions.HttpTimeoutSeconds"/> via Constructor
    /// - Trust-Modell: keine Cert-Validation (LAN-only, default HttpClientHandler mit bypass)
    /// - Error-Codes: <c>src_timeout</c>, <c>src_unreachable</c>, <c>src_fetch_failed</c>
    /// </summary>
    private async Task<(byte[] Bytes, string? Error)> FetchHttpBytesAsync(string url, CancellationToken ct)
    {
        HttpMessageHandler handler;
        if (_httpMessageHandler != null)
        {
            handler = _httpMessageHandler;
        }
        else
        {
            handler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = (msg, cert, chain, errors) => true
            };
        }

        using var http = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(_srcOptions.HttpTimeoutSeconds)
        };

        try
        {
            using var response = await http.GetAsync(url, ct);
            if (!response.IsSuccessStatusCode)
            {
                return (Array.Empty<byte>(), "src_fetch_failed");
            }
            var bytes = await response.Content.ReadAsByteArrayAsync(ct);
            return (bytes, null);
        }
        catch (TaskCanceledException) when (ct.IsCancellationRequested)
        {
            // user cancellation -> propagate
            throw;
        }
        catch (TaskCanceledException)
        {
            // HttpClient.Timeout
            return (Array.Empty<byte>(), "src_timeout");
        }
        catch (HttpRequestException)
        {
            // DNS / TCP / TLS errors
            return (Array.Empty<byte>(), "src_unreachable");
        }
        catch
        {
            return (Array.Empty<byte>(), "src_unreachable");
        }
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

    private static string GenerateRandomString(int length)
    {
        const string chars = "abcdefghijklmnopqrstuvwxyz0123456789";
        var bytes = new byte[length];
        RandomNumberGenerator.Fill(bytes);
        return new string(bytes.Select(b => chars[b % chars.Length]).ToArray());
    }

    /// <summary>Writes payload.json (a2ui / schema-form). Returns error code or null.</summary>
    private async Task<string?> WritePayloadAsync(string sitePath, JsonElement payload, CancellationToken ct)
    {
        var siteFolder = Path.Combine(_sitesRoot, sitePath);
        Directory.CreateDirectory(siteFolder);
        var payloadPath = Path.Combine(siteFolder, "payload.json");

        var bytes = JsonSerializer.SerializeToUtf8Bytes(payload);
        if (bytes.Length > _sitesOptions.MaxPayloadSizeBytes)
            return "payload_too_large";

        var tmpPath = payloadPath + ".tmp";
        await File.WriteAllBytesAsync(tmpPath, bytes, ct);
        File.Move(tmpPath, payloadPath, overwrite: true);
        return null;
    }

    private static bool IsSubmissionFileName(string fileName)
    {
        var name = Path.GetFileNameWithoutExtension(fileName);
        if (name.Length < 20) return false;
        return name[4] == '-' && name[7] == '-' && name[10] == 'T' &&
               name[13] == '-' && name[16] == '-' && name[19] == '_';
    }

    private static (string Id, DateTime ReceivedAt) ParseSubmissionFileName(string fileName)
    {
        var name = Path.GetFileNameWithoutExtension(fileName);
        var ts = DateTime.ParseExact(name.Substring(0, 19), "yyyy-MM-ddTHH-mm-ss",
            System.Globalization.CultureInfo.InvariantCulture);
        return (name, ts);
    }

    private string BuildSiteUrl(string sitePath)
    {
        // Always prefer the detected LAN IPv4 so URLs are reachable from
        // other devices on the network. Fall back to Host:Ip only if
        // detection fails (e.g. running in a sandbox without network API).
        var host = LanIpDetector.GetLanIpv4();
        if (string.IsNullOrEmpty(host))
        {
            host = _hostOptions.Ip;
        }
        // Reflect the active binding: when UseHttps is on and HttpsPort is
        // configured, return an https URL on the HTTPS port; otherwise http.
        var useHttps = _hostOptions.UseHttps && _hostOptions.HttpsPort > 0;
        var scheme = useHttps ? "https" : "http";
        var port = useHttps ? _hostOptions.HttpsPort : _hostOptions.Port;
        return $"{scheme}://{host}:{port}/{sitePath}/";
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
    string? Path = null,
    JsonElement? Payload = null,
    IReadOnlyList<FileEntry>? Files = null);

public record FileEntry(string Path, string? Content = null, bool Delete = false, string? Src = null);

public record DeployResult(
    string? SitePath,
    string? Url,
    IReadOnlyList<DeployResultFile>? Files,
    string? Error);

public record DeployResultFile(string Path, string ResultPath);

public record SubmissionInfo(string SubmissionId, DateTime ReceivedAt, JsonElement Data);

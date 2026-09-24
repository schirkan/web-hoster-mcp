using System.ComponentModel;
using System.Text.Json.Serialization;
using ModelContextProtocol.Server;
using WebHosterMcp.Core;

namespace WebHosterMcp.Host;

[McpServerToolType]
public sealed class SiteTools
{
    private readonly SiteManager _siteManager;

    public SiteTools(SiteManager siteManager)
    {
        _siteManager = siteManager;
    }

    [McpServerTool(Name = "deploy")]
    [Description("Deploys or updates a site. mode=merge|replace (files); site_path optional. path for folder, payload for a2ui/json-schema-form.")]
    public async Task<DeployToolResponse> Deploy(
        string? site_path = null,
        string type = "files",
        string mode = "merge",
        int? retention_seconds = null,
        string? path = null,
        System.Text.Json.JsonElement? payload = null,
        IReadOnlyList<DeployToolFileEntry>? files = null,
        CancellationToken cancellationToken = default)
    {
        var requestFiles = (files ?? Array.Empty<DeployToolFileEntry>())
            .Select(f => new FileEntry(f.path, f.content, f.delete, f.src))
            .ToArray();

        var result = await _siteManager.DeployAsync(
            new DeployRequest(site_path, type, mode, retention_seconds, path, payload, requestFiles),
            cancellationToken);

        return new DeployToolResponse(
            site_path: result.SitePath,
            url: result.Url,
            files: result.Files?.Select(f => new DeployToolResultFile(f.Path, f.ResultPath)).ToArray(),
            error: result.Error);
    }

    [McpServerTool(Name = "list_sites")]
    [Description("Lists all sites as a raw array (no wrapper object).")]
    public async Task<IReadOnlyList<ListSitesItem>> ListSites(CancellationToken cancellationToken = default)
    {
        var sites = await _siteManager.ListAsync(cancellationToken);

        return sites
            .OrderBy(s => s.SitePath, StringComparer.Ordinal)
            .Select(s =>
            {
                var expiresAt = s.RetentionSeconds > 0
                    ? s.UpdatedAt.AddSeconds(s.RetentionSeconds)
                    : (DateTime?)null;

                return new ListSitesItem(
                    site_path: s.SitePath,
                    type: s.Type,
                    file_count: _siteManager.CountFiles(s.SitePath),
                    retention_seconds: s.RetentionSeconds,
                    created_at: s.CreatedAt,
                    updated_at: s.UpdatedAt,
                    expires_at: expiresAt,
                    url: _siteManager.GetSiteUrl(s.SitePath));
            })
            .ToArray();
    }

    [McpServerTool(Name = "get_site_info")]
    [Description("Returns site details including file_count, expires_at, url, and files[].")]
    public async Task<GetSiteInfoResponse> GetSiteInfo(string site_path, CancellationToken cancellationToken = default)
    {
        var site = await _siteManager.GetAsync(site_path, cancellationToken);
        if (site is null)
        {
            return new GetSiteInfoResponse(site_path: site_path, error: "site_not_found");
        }

        var expiresAt = site.RetentionSeconds > 0
            ? site.UpdatedAt.AddSeconds(site.RetentionSeconds)
            : (DateTime?)null;

        var files = _siteManager
            .ListFiles(site_path)
            .Select(f => new DeployToolResultFile(f.Path, f.ResultPath))
            .ToArray();

        return new GetSiteInfoResponse(
            site_path: site.SitePath,
            type: site.Type,
            file_count: _siteManager.CountFiles(site.SitePath),
            retention_seconds: site.RetentionSeconds,
            created_at: site.CreatedAt,
            updated_at: site.UpdatedAt,
            expires_at: expiresAt,
            url: _siteManager.GetSiteUrl(site.SitePath),
            files: files,
            error: null);
    }

    [McpServerTool(Name = "delete_site")]
    [Description("Deletes the site folder and the registry entry.")]
    public async Task<DeleteSiteResponse> DeleteSite(string site_path, CancellationToken cancellationToken = default)
    {
        var deleted = await _siteManager.DeleteAsync(site_path, cancellationToken);
        return new DeleteSiteResponse(site_path, deleted);
    }

    [McpServerTool(Name = "get_submissions")]
    [Description("Lists submissions of a json-schema-form site (newest first). Optional: since (ISO-8601), limit (default 50, max 500).")]
    public async Task<IReadOnlyList<SubmissionInfoDto>> GetSubmissions(
        string site_path,
        string? since = null,
        int? limit = null,
        CancellationToken cancellationToken = default)
    {
        DateTime? sinceDt = null;
        if (!string.IsNullOrEmpty(since) && DateTime.TryParse(since, out var dt))
        {
            sinceDt = dt;
        }
        var effectiveLimit = limit ?? 50;

        var entries = await _siteManager.GetSubmissionsAsync(site_path, sinceDt, effectiveLimit, cancellationToken);

        return entries
            .Select(e => new SubmissionInfoDto(
                submission_id: e.SubmissionId,
                received_at: e.ReceivedAt.ToString("yyyy-MM-ddTHH:mm:ss"),
                data: e.Data))
            .ToArray();
    }
}

public sealed record DeployToolFileEntry(
    [property: JsonPropertyName("path")] string path,
    [property: JsonPropertyName("content")] string? content = null,
    [property: JsonPropertyName("delete")] bool delete = false,
    [property: JsonPropertyName("src")] string? src = null);

public sealed record DeployToolResultFile(
    [property: JsonPropertyName("path")] string path,
    [property: JsonPropertyName("result_path")] string result_path);

public sealed record DeployToolResponse(
    [property: JsonPropertyName("site_path")] string? site_path,
    [property: JsonPropertyName("url")] string? url,
    [property: JsonPropertyName("files")] IReadOnlyList<DeployToolResultFile>? files,
    [property: JsonPropertyName("error")] string? error = null);

public sealed record ListSitesItem(
    [property: JsonPropertyName("site_path")] string site_path,
    [property: JsonPropertyName("type")] string type,
    [property: JsonPropertyName("file_count")] int file_count,
    [property: JsonPropertyName("retention_seconds")] int retention_seconds,
    [property: JsonPropertyName("created_at")] DateTime created_at,
    [property: JsonPropertyName("updated_at")] DateTime updated_at,
    [property: JsonPropertyName("expires_at")] DateTime? expires_at,
    [property: JsonPropertyName("url")] string url);

public sealed record GetSiteInfoResponse(
    [property: JsonPropertyName("site_path")] string? site_path = null,
    [property: JsonPropertyName("type")] string? type = null,
    [property: JsonPropertyName("file_count")] int? file_count = null,
    [property: JsonPropertyName("retention_seconds")] int? retention_seconds = null,
    [property: JsonPropertyName("created_at")] DateTime? created_at = null,
    [property: JsonPropertyName("updated_at")] DateTime? updated_at = null,
    [property: JsonPropertyName("expires_at")] DateTime? expires_at = null,
    [property: JsonPropertyName("url")] string? url = null,
    [property: JsonPropertyName("files")] IReadOnlyList<DeployToolResultFile>? files = null,
    [property: JsonPropertyName("error")] string? error = null);

public sealed record DeleteSiteResponse(
    [property: JsonPropertyName("site_path")] string site_path,
    [property: JsonPropertyName("deleted")] bool deleted);

public sealed record SubmissionInfoDto(
    [property: JsonPropertyName("submission_id")] string submission_id,
    [property: JsonPropertyName("received_at")] string received_at,
    [property: JsonPropertyName("data")] System.Text.Json.JsonElement data);

using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Server;
using WebHosterMcp.Core;

var builder = WebApplication.CreateBuilder(args);
builder.Logging.AddConsole();

builder.Services.Configure<HostOptions>(builder.Configuration.GetSection("Host"));
builder.Services.Configure<HttpsOptions>(builder.Configuration.GetSection("Https"));
builder.Services.Configure<RetentionOptions>(builder.Configuration.GetSection("Retention"));
builder.Services.Configure<SitesOptions>(builder.Configuration);
builder.Services.Configure<SrcOptions>(builder.Configuration.GetSection("Src"));

// Core-Services
builder.Services.AddSingleton(sp =>
{
    var sites = sp.GetRequiredService<IOptions<SitesOptions>>().Value;
    var sitesRoot = Path.GetFullPath(sites.SitesRoot);
    Directory.CreateDirectory(sitesRoot);
    var registryPath = Path.Combine(sitesRoot, "registry.json");
    return new SiteRegistry(registryPath);
});

builder.Services.AddSingleton(sp =>
{
    var registry = sp.GetRequiredService<SiteRegistry>();
    var sitesOptions = sp.GetRequiredService<IOptions<SitesOptions>>().Value;
    var hostOptions = sp.GetRequiredService<IOptions<HostOptions>>().Value;
    var retentionOptions = sp.GetRequiredService<IOptions<RetentionOptions>>().Value;
    return new SiteManager(registry, sitesOptions, hostOptions, retentionOptions);
});

builder.Services.AddHostedService<WebHosterMcp.Host.RetentionHostedService>();

// MCP-Server (stdio)
// The MCP C# SDK uses source-generated JSON serialization for tool parameter
// marshalling. The SDK's built-in TypeInfoResolvers do not know the tool DTOs
// declared in SiteTools, which would crash the server during tool registration.
// We hand it a JsonSerializerOptions chain that combines our source-generated
// McpToolJsonContext with the DefaultJsonTypeInfoResolver (reflection fallback).
var mcpToolJsonOptions = new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web)
{
    WriteIndented = true,
    TypeInfoResolver = System.Text.Json.Serialization.Metadata.JsonTypeInfoResolver.Combine(
        WebHosterMcp.Host.McpToolJsonContext.Default,
        new System.Text.Json.Serialization.Metadata.DefaultJsonTypeInfoResolver()),
};

builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithTools<WebHosterMcp.Host.SiteTools>(mcpToolJsonOptions);

// Kestrel listener setup (HTTP + optional HTTPS)
var hostConfig = builder.Configuration.GetSection("Host").Get<HostOptions>() ?? new HostOptions();
var httpsConfig = builder.Configuration.GetSection("Https").Get<HttpsOptions>() ?? new HttpsOptions();

builder.WebHost.ConfigureKestrel(options =>
{
    var bindIp = IPAddress.TryParse(hostConfig.Ip, out var parsed) ? parsed : IPAddress.Any;

    options.Listen(bindIp, hostConfig.Port);

    if (hostConfig.UseHttps && hostConfig.HttpsPort > 0)
    {
        var certificate = HttpsCertificateLoader.LoadOrCreate(hostConfig, httpsConfig, builder.Environment.ContentRootPath);
        options.Listen(bindIp, hostConfig.HttpsPort, listen => listen.UseHttps(certificate));
    }
});

var app = builder.Build();

var hostOptions = app.Services.GetRequiredService<IOptions<HostOptions>>().Value;
var siteManager = app.Services.GetRequiredService<SiteManager>();
var sitesOptions = app.Services.GetRequiredService<IOptions<SitesOptions>>().Value;
var sitesRootFullPath = Path.GetFullPath(sitesOptions.SitesRoot);

// === Routes ===

// Sites-Index
app.MapGet("/", async () =>
{
    var sites = await siteManager.ListAsync();
    var html = new StringBuilder();
    html.Append("<!DOCTYPE html><html lang=\"en\"><head><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width, initial-scale=1\"><title>Web Hoster — Sites</title><style>").Append(ListingCss()).Append("</style></head><body><h1>Web Hoster — Sites</h1>");

    if (sites.Count == 0)
    {
        html.Append("<p class=\"empty\">No sites available.</p>");
    }
    else
    {
        html.Append("<ul>");
        foreach (var site in sites.OrderBy(s => s.SitePath, StringComparer.OrdinalIgnoreCase))
        {
            var count = siteManager.CountFiles(site.SitePath);
            html.Append("<li>");
            html.Append("<a class=\"listing-name\" href=\"/").Append(WebUtility.UrlEncode(site.SitePath)).Append("/\">").Append(WebUtility.HtmlEncode(site.SitePath)).Append("</a>");
            html.Append("<span class=\"meta\">").Append(WebUtility.HtmlEncode(site.Type)).Append(" &middot; ").Append(count).Append("</span>");
            html.Append("<span class=\"meta\">").Append(site.UpdatedAt.ToString("yyyy-MM-dd HH:mm")).Append("</span>");
            html.Append("<button type=\"button\" data-delete-site=\"").Append(WebUtility.HtmlEncode(site.SitePath)).Append("\" class=\"delete-btn\" aria-label=\"Delete site\" title=\"Delete site\">").Append(DeleteButtonIcon()).Append("</button>");
            html.Append("</li>");
        }
        html.Append("</ul>");
    }

    html.Append(DeleteScript());
    html.Append("</body></html>");
    return Results.Content(html.ToString(), "text/html; charset=utf-8");
});

// /<site>/ -> Listing oder Renderer
app.MapGet("/{sitePath}/", async (string sitePath) =>
{
    var site = await siteManager.GetAsync(sitePath);
    if (site is null) return Results.NotFound();

    try
    {
        return site.Type switch
        {
            "files" or "folder" => Results.Content(RenderListingHtml(site, siteManager, sitesRootFullPath), "text/html; charset=utf-8"),
            "a2ui" => Results.Content(await RenderA2uiHtml(site, siteManager), "text/html; charset=utf-8"),
            "json-schema-form" => Results.Content(await RenderSchemaFormHtml(site, siteManager), "text/html; charset=utf-8"),
            _ => Results.NotFound()
        };
    }
    catch (Exception ex)
    {
        app.Logger.LogError(ex, "Site-render failed for {SitePath} (type={Type})", sitePath, site.Type);
        return Results.StatusCode(500);
    }
});

// /<site>/<file> -> Static file serving (type-aware)
// `{*filePath:regex(.+)}` requires at least one character after the trailing
// slash. Combined with the listing route `GET /{sitePath}/` above, this keeps
// the templates non-ambiguous for `GET /<site>/` (which would otherwise match
// both endpoints and trip AmbiguousMatchException -> HTTP 500).
app.MapGet("/{sitePath}/{*filePath:regex(.+)}", async (string sitePath, string filePath) =>
{
    var site = await siteManager.GetAsync(sitePath);
    if (site is null) return Results.NotFound();
    if (site.Type != "files" && site.Type != "folder") return Results.NotFound();

    var relativePath = filePath.Replace('/', Path.DirectorySeparatorChar);
    if (relativePath.Contains("..", StringComparison.Ordinal)) return Results.NotFound();

    var baseFolder = site.Type == "folder" && !string.IsNullOrEmpty(site.Path)
        ? site.Path
        : Path.Combine(sitesRootFullPath, sitePath);

    if (!Directory.Exists(baseFolder)) return Results.NotFound();

    var requestedFile = Path.GetFullPath(Path.Combine(baseFolder, relativePath));
    var baseFull = Path.GetFullPath(baseFolder);
    if (!requestedFile.StartsWith(baseFull, StringComparison.OrdinalIgnoreCase)) return Results.NotFound();
    if (!File.Exists(requestedFile)) return Results.NotFound();

    var contentType = siteManager.GetContentType(requestedFile);
    return Results.Stream(File.OpenRead(requestedFile), contentType);
});

// DELETE /<site>
app.MapDelete("/{sitePath}", async (string sitePath) =>
{
    var deleted = await siteManager.DeleteAsync(sitePath);
    if (!deleted) return Results.NotFound();
    return Results.Redirect("/");
});

// DELETE /<site>/<file>
app.MapDelete("/{sitePath}/{*filePath:regex(.+)}", async (string sitePath, string filePath) =>
{
    var deleted = await siteManager.DeleteFileAsync(sitePath, filePath);
    if (deleted != true) return Results.NotFound();
    return Results.Redirect("/" + sitePath + "/");
});

// POST /<site>/submit -> nur json-schema-form
app.MapPost("/{sitePath}/submit", async (string sitePath, HttpContext ctx) =>
{
    var site = await siteManager.GetAsync(sitePath);
    if (site is null) return Results.NotFound();

    if (!string.Equals(site.Type, "json-schema-form", StringComparison.Ordinal))
    {
        return Results.Json(new { error = "submit_not_allowed" }, statusCode: 400);
    }

    var body = await ReadBodyWithCapAsync(ctx.Request.Body, sitesOptions.MaxSubmissionSizeBytes);
    if (body.Length > sitesOptions.MaxSubmissionSizeBytes)
    {
        return Results.Json(new { error = "submission_too_large" }, statusCode: 400);
    }

    var result = await siteManager.SaveSubmissionAsync(sitePath, body);
    if (result.Error != null)
    {
        return Results.Json(new { error = result.Error }, statusCode: 400);
    }

    return Results.Json(new
    {
        submission_id = result.SubmissionId,
        received_at = result.ReceivedAt.ToString("yyyy-MM-ddTHH:mm:ss")
    });
});

var lanIp = LanIpDetector.GetLanIpv4();
app.Logger.LogInformation("WebHosterMcp:");
app.Logger.LogInformation("  MCP server (stdio): ready");
app.Logger.LogInformation("  HTTP: {Scheme}://{Ip}:{Port}/", "http", hostOptions.Ip, hostOptions.Port);
if (hostOptions.UseHttps && hostOptions.HttpsPort > 0)
{
    app.Logger.LogInformation("  HTTPS: {Scheme}://{Ip}:{Port}/", "https", hostOptions.Ip, hostOptions.HttpsPort);
}
if (lanIp is not null && lanIp != hostOptions.Ip)
{
    app.Logger.LogInformation("  HTTP (LAN): http://{LanIp}:{Port}/", lanIp, hostOptions.Port);
    if (hostOptions.UseHttps && hostOptions.HttpsPort > 0)
    {
        app.Logger.LogInformation("  HTTPS (LAN): https://{LanIp}:{Port}/", lanIp, hostOptions.HttpsPort);
    }
}

await app.RunAsync();

// === HTML Helpers ===

static string RenderListingHtml(SiteEntry site, SiteManager mgr, string sitesRootFullPath)
{
    var files = mgr.ListFiles(site.SitePath);
    var html = new StringBuilder();

    html.Append("<!DOCTYPE html><html lang=\"en\"><head><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width, initial-scale=1\"><title>Index of /").Append(WebUtility.HtmlEncode(site.SitePath)).Append("/</title><style>").Append(ListingCss()).Append("</style></head><body>");
    html.Append("<header class=\"page-header\"><a class=\"back-link\" href=\"/\">&larr; All Sites</a>");
    html.Append("<h1>Index of /").Append(WebUtility.HtmlEncode(site.SitePath)).Append("/</h1></header>");

    if (files.Count == 0)
    {
        html.Append("<p class=\"empty\">This site contains no files.</p>");
    }
    else
    {
        html.Append("<ul>");
        foreach (var file in files)
        {
            var encodedFilePath = WebUtility.UrlEncode(file.Path).Replace("%2F", "/");
            var href = "/" + WebUtility.UrlEncode(site.SitePath) + "/" + encodedFilePath;
            var baseFolder = site.Type == "folder" && !string.IsNullOrEmpty(site.Path)
                ? site.Path
                : Path.Combine(sitesRootFullPath, site.SitePath);
            var physical = Path.Combine(baseFolder, file.Path.Replace('/', Path.DirectorySeparatorChar));
            var modified = File.Exists(physical) ? File.GetLastWriteTime(physical).ToString("yyyy-MM-dd HH:mm") : "-";

            html.Append("<li>");
            html.Append("<a class=\"listing-name\" href=\"").Append(href).Append("\">").Append(WebUtility.HtmlEncode(file.Path)).Append("</a>");
            html.Append("<span class=\"meta\">").Append(modified).Append("</span>");
            html.Append("<button type=\"button\" data-delete-file=\"").Append(WebUtility.HtmlEncode(file.Path)).Append("\" data-site=\"").Append(WebUtility.HtmlEncode(site.SitePath)).Append("\" class=\"delete-btn\" aria-label=\"Delete file\" title=\"Delete file\">").Append(DeleteButtonIcon()).Append("</button>");
            html.Append("</li>");
        }
        html.Append("</ul>");
    }

    html.Append(DeleteScript());
    html.Append("</body></html>");
    return html.ToString();
}

static async Task<string> RenderA2uiHtml(SiteEntry site, SiteManager mgr)
{
    var payload = await mgr.GetPayloadAsync(site.SitePath);
    var payloadJson = payload.HasValue ? payload.Value.GetRawText() : "null";

    var html = new StringBuilder();
    html.Append("<!DOCTYPE html><html lang=\"de\"><head><meta charset=\"utf-8\">");
    html.Append("<title>").Append(WebUtility.HtmlEncode(site.SitePath)).Append("</title>");
    html.Append("<script crossorigin src=\"https://unpkg.com/react@18/umd/react.production.min.js\"></script>");
    html.Append("<script crossorigin src=\"https://unpkg.com/react-dom@18/umd/react-dom.production.min.js\"></script>");
    html.Append("<script crossorigin src=\"https://unpkg.com/@a2ui/react@0.9.1/dist/index.js\"></script>");
    html.Append("<style>body{font-family:system-ui;max-width:800px;margin:2em auto;padding:0 1em;}</style>");
    html.Append("</head><body><div id=\"root\"></div><script>");
    html.Append("const SITE = { site_path: \"").Append(EscapeJs(site.SitePath)).Append("\", type: \"a2ui\", payload: ").Append(payloadJson).Append(" };");
    html.Append("const root = ReactDOM.createRoot(document.getElementById('root'));");
    html.Append("const Renderer = (window.A2UIReactRenderer && (window.A2UIReactRenderer.default || window.A2UIReactRenderer.A2UIRenderer || window.A2UIReactRenderer));");
    html.Append("if (Renderer) { root.render(React.createElement(Renderer, { messages: SITE.payload.messages || [] })); }");
    html.Append("else { root.render(React.createElement('pre', null, JSON.stringify(SITE.payload, null, 2))); }");
    html.Append("</script></body></html>");
    return html.ToString();
}

static async Task<string> RenderSchemaFormHtml(SiteEntry site, SiteManager mgr)
{
    var payload = await mgr.GetPayloadAsync(site.SitePath);
    var payloadJson = payload.HasValue ? payload.Value.GetRawText() : "null";

    var html = new StringBuilder();
    html.Append("<!DOCTYPE html><html lang=\"de\"><head><meta charset=\"utf-8\">");
    html.Append("<title>").Append(WebUtility.HtmlEncode(site.SitePath)).Append("</title>");
    html.Append("<script crossorigin src=\"https://unpkg.com/react@18/umd/react.production.min.js\"></script>");
    html.Append("<script crossorigin src=\"https://unpkg.com/react-dom@18/umd/react-dom.production.min.js\"></script>");
    html.Append("<script crossorigin src=\"https://unpkg.com/@rjsf/core@5/dist/index.js\"></script>");
    html.Append("<style>body{font-family:system-ui;max-width:800px;margin:2em auto;padding:0 1em;}</style>");
    html.Append("</head><body><div id=\"root\"></div><script>");
    html.Append("const SITE = { site_path: \"").Append(EscapeJs(site.SitePath)).Append("\", type: \"json-schema-form\", payload: ").Append(payloadJson).Append(" };");
    html.Append("const root = ReactDOM.createRoot(document.getElementById('root'));");
    html.Append("function onSubmit(args) {");
    html.Append("  const data = (args && args.formData) || args;");
    html.Append("  fetch('/' + SITE.site_path + '/submit', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(data) })");
    html.Append("    .then(r => r.ok ? alert('Eingereicht!') : alert('Fehler: ' + r.status));");
    html.Append("  if (args && typeof args.preventDefault === 'function') args.preventDefault();");
    html.Append("}");
    html.Append("const Form = (window.JSONSchemaForm && (window.JSONSchemaForm.default || window.JSONSchemaForm));");
    html.Append("if (Form) { root.render(React.createElement(Form, { schema: SITE.payload.schema || {}, formData: SITE.payload.data, onSubmit: onSubmit })); }");
    html.Append("else { root.render(React.createElement('pre', null, JSON.stringify(SITE.payload, null, 2))); }");
    html.Append("</script></body></html>");
    return html.ToString();
}

static string EscapeJs(string s) => s.Replace("\\", "\\\\").Replace("\"", "\\\"");

static async Task<string> ReadBodyWithCapAsync(Stream body, int maxBytes)
{
    using var ms = new MemoryStream();
    var buffer = new byte[8192];
    var total = 0;
    int read;
    while ((read = await body.ReadAsync(buffer, 0, buffer.Length)) > 0)
    {
        total += read;
        if (total > maxBytes) return new string(' ', maxBytes + 1);
        ms.Write(buffer, 0, read);
    }
    return Encoding.UTF8.GetString(ms.ToArray());
}

static string ListingCss() =>
    """
    * { box-sizing: border-box; }
    html { -webkit-text-size-adjust: 100%; }
    body {
      font-family: system-ui, -apple-system, "Segoe UI", Roboto, sans-serif;
      margin: 0;
      padding: 0.75rem;
      color: #1a1a1a;
      background: #fafafa;
      line-height: 1.4;
      font-size: 16px;
    }
    h1 {
      font-size: 1.125rem;
      font-weight: 600;
      margin: 0 0 0.75rem;
      padding-bottom: 0.5rem;
      border-bottom: 1px solid #ddd;
    }
    .page-header {
      display: flex;
      align-items: center;
      gap: 0.5rem;
      flex-wrap: nowrap;
      padding-bottom: 0.5rem;
      border-bottom: 1px solid #ddd;
      margin-bottom: 0.75rem;
    }
    .page-header .back-link {
      margin: 0;
      padding: 0;
      flex: 0 0 auto;
    }
    .page-header h1 {
      margin: 0;
      padding: 0;
      border: none;
      font-size: 1.125rem;
      white-space: nowrap;
      overflow: hidden;
      text-overflow: ellipsis;
      min-width: 0;
      flex: 1 1 auto;
    }
    p { margin: 0.75rem 0; }
    .back-link {
      display: inline-flex;
      align-items: center;
      min-height: 44px;
      padding: 0.25rem 0;
      margin-bottom: 0.5rem;
      font-size: 0.9rem;
      color: #0066cc;
      text-decoration: none;
    }
    .back-link:hover, .back-link:active { text-decoration: underline; }
    ul { list-style: none; padding: 0; margin: 0; }
    li {
      display: flex;
      flex-wrap: nowrap;
      align-items: center;
      gap: 0.25rem 0.5rem;
      padding: 0.5rem 0;
      border-bottom: 1px solid #eee;
    }
    li:last-child { border-bottom: none; }
    .listing-name {
      font-weight: 500;
      color: #0066cc;
      text-decoration: none;
      font-size: 1rem;
      min-height: 44px;
      display: inline-flex;
      align-items: center;
      padding: 0.25rem 0;
      flex: 1 1 auto;
      min-width: 0;
      overflow: hidden;
      text-overflow: ellipsis;
      white-space: nowrap;
      text-align: left;
    }
    .listing-name:hover, .listing-name:active { text-decoration: underline; }
    .meta {
      color: #555;
      font-size: 0.75rem;
      flex: 0 0 auto;
      text-align: right;
      white-space: nowrap;
    }
    .empty { color: #666; padding: 1rem 0; }
    button {
      padding: 0;
      min-height: 44px;
      min-width: 44px;
      cursor: pointer;
      border: 1px solid #c0c0c0;
      background: #fff;
      border-radius: 6px;
      color: #555;
      flex: 0 0 auto;
      display: inline-flex;
      align-items: center;
      justify-content: center;
      transition: background 0.1s, color 0.1s, border-color 0.1s;
    }
    button:hover { background: #fff5f5; color: #c00; border-color: #c00; }
    button:active { background: #ececec; }
    button:focus-visible { outline: 2px solid #0066cc; outline-offset: 2px; }
    button svg { display: block; }
    @media (min-width: 640px) {
      body { padding: 1.5rem; max-width: 900px; margin: 0 auto; }
      h1 { font-size: 1.25rem; }
      li { padding: 0.4rem 0; }
      .meta { font-size: 0.85rem; }
    }
    """;

static string DeleteButtonIcon() =>
    """
    <svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><polyline points="3 6 5 6 21 6"/><path d="M19 6l-1 14a2 2 0 0 1-2 2H8a2 2 0 0 1-2-2L5 6"/><path d="M10 11v6"/><path d="M14 11v6"/><path d="M9 6V4a2 2 0 0 1 2-2h2a2 2 0 0 1 2 2v2"/></svg>
    """;

static string DeleteScript() =>
    """
    <script>
    document.querySelectorAll('[data-delete-site]').forEach(btn => {
      btn.onclick = async () => {
        const site = btn.dataset.deleteSite;
        if (!confirm(`Really delete site "${site}"?`)) return;
        const res = await fetch('/' + encodeURIComponent(site), { method: 'DELETE' });
        if (res.ok || res.redirected) location.href = '/';
        else alert('Error: ' + res.status);
      };
    });

    document.querySelectorAll('[data-delete-file]').forEach(btn => {
      btn.onclick = async () => {
        const site = btn.dataset.site;
        const filePath = btn.dataset.deleteFile;
        if (!site || !filePath) { alert('Context missing'); return; }
        if (!confirm(`Really delete file "${filePath}"?`)) return;
        const encoded = filePath.split('/').map(encodeURIComponent).join('/');
        const res = await fetch('/' + encodeURIComponent(site) + '/' + encoded, { method: 'DELETE' });
        if (res.ok || res.redirected) location.href = '/' + encodeURIComponent(site) + '/';
        else alert('Error: ' + res.status);
      };
    });
    </script>
    """;

public partial class Program { }

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
builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithTools<WebHosterMcp.Host.SiteTools>();

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
    html.Append("<!DOCTYPE html><html lang=\"de\"><head><meta charset=\"utf-8\"><title>Web Hoster — Sites</title>");
    html.Append("<style>body{font-family:system-ui;max-width:900px;margin:2em auto;padding:0 1em;}ul{list-style:none;padding:0;}li{padding:.35em 0;display:flex;gap:1em;align-items:center;}a{text-decoration:none;color:#0066cc;}a:hover{text-decoration:underline;}span{color:#666;font-size:.9em;}button{padding:.2em .5em;}</style>");
    html.Append("</head><body><h1>Web Hoster — Sites</h1>");

    if (sites.Count == 0)
    {
        html.Append("<p>Keine Sites vorhanden.</p>");
    }
    else
    {
        html.Append("<ul>");
        foreach (var site in sites.OrderBy(s => s.SitePath, StringComparer.OrdinalIgnoreCase))
        {
            var count = siteManager.CountFiles(site.SitePath);
            html.Append("<li>");
            html.Append("<a href=\"/").Append(WebUtility.UrlEncode(site.SitePath)).Append("/\">").Append(WebUtility.HtmlEncode(site.SitePath)).Append("</a>");
            html.Append("<span>").Append(WebUtility.HtmlEncode(site.Type)).Append(" &middot; ").Append(count).Append(" Dateien</span>");
            html.Append("<span>").Append(site.UpdatedAt.ToString("yyyy-MM-dd HH:mm")).Append("</span>");
            html.Append("<button data-delete-site=\"").Append(WebUtility.HtmlEncode(site.SitePath)).Append("\" class=\"delete-btn\">Delete</button>");
            html.Append("</li>");
        }
        html.Append("</ul>");
    }

    html.Append(DeleteScript());
    html.Append("</body></html>");
    return Results.Content(html.ToString(), "text/html; charset=utf-8");
});

// /<site> -> /<site>/
app.MapGet("/{sitePath}", async (string sitePath) =>
{
    var site = await siteManager.GetAsync(sitePath);
    if (site is null) return Results.NotFound();
    return Results.Redirect("/" + sitePath + "/");
});

// /<site>/ -> Listing oder Renderer
app.MapGet("/{sitePath}/", async (string sitePath) =>
{
    var site = await siteManager.GetAsync(sitePath);
    if (site is null) return Results.NotFound();

    return site.Type switch
    {
        "files" or "folder" => Results.Content(RenderListingHtml(site, siteManager, sitesRootFullPath), "text/html; charset=utf-8"),
        "a2ui" => Results.Content(await RenderA2uiHtml(site, siteManager), "text/html; charset=utf-8"),
        "json-schema-form" => Results.Content(await RenderSchemaFormHtml(site, siteManager), "text/html; charset=utf-8"),
        _ => Results.NotFound()
    };
});

// /<site>/<file> -> Static file serving (type-aware)
app.MapGet("/{sitePath}/{**filePath}", async (string sitePath, string? filePath) =>
{
    if (string.IsNullOrWhiteSpace(filePath)) return Results.NotFound();

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
app.MapDelete("/{sitePath}/{**filePath}", async (string sitePath, string filePath) =>
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

    html.Append("<!DOCTYPE html><html lang=\"de\"><head><meta charset=\"utf-8\">");
    html.Append("<title>Index of /").Append(WebUtility.HtmlEncode(site.SitePath)).Append("/</title>");
    html.Append("<style>body{font-family:system-ui;max-width:900px;margin:2em auto;padding:0 1em;}ul{list-style:none;padding:0;}li{padding:.35em 0;display:flex;gap:1em;align-items:center;}a{text-decoration:none;color:#0066cc;}a:hover{text-decoration:underline;}span{color:#666;font-size:.9em;}button{padding:.2em .5em;}</style>");
    html.Append("</head><body>");
    html.Append("<h1>Index of /").Append(WebUtility.HtmlEncode(site.SitePath)).Append("/</h1>");

    if (files.Count == 0)
    {
        html.Append("<p>Diese Site enthält keine Dateien.</p>");
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
            html.Append("<a href=\"").Append(href).Append("\">").Append(WebUtility.HtmlEncode(file.Path)).Append("</a>");
            html.Append("<span>").Append(modified).Append("</span>");
            html.Append("<button data-delete-file=\"").Append(WebUtility.HtmlEncode(file.Path)).Append("\" data-site=\"").Append(WebUtility.HtmlEncode(site.SitePath)).Append("\" class=\"delete-btn\">Delete</button>");
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

static string DeleteScript() =>
    """
    <script>
    document.querySelectorAll('[data-delete-site]').forEach(btn => {
      btn.onclick = async () => {
        const site = btn.dataset.deleteSite;
        if (!confirm(`Site "${site}" wirklich löschen?`)) return;
        const res = await fetch('/' + encodeURIComponent(site), { method: 'DELETE' });
        if (res.ok || res.redirected) location.href = '/';
        else alert('Fehler: ' + res.status);
      };
    });

    document.querySelectorAll('[data-delete-file]').forEach(btn => {
      btn.onclick = async () => {
        const site = btn.dataset.site;
        const filePath = btn.dataset.deleteFile;
        if (!site || !filePath) { alert('Kontext fehlt'); return; }
        if (!confirm(`File "${filePath}" wirklich löschen?`)) return;
        const encoded = filePath.split('/').map(encodeURIComponent).join('/');
        const res = await fetch('/' + encodeURIComponent(site) + '/' + encoded, { method: 'DELETE' });
        if (res.ok || res.redirected) location.href = '/' + encodeURIComponent(site) + '/';
        else alert('Fehler: ' + res.status);
      };
    });
    </script>
    """;

public partial class Program { }

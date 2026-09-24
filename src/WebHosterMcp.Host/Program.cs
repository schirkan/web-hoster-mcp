using System.Net;
using System.Text;
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
            html.Append($"<a href=\"/{WebUtility.UrlEncode(site.SitePath)}/\">{WebUtility.HtmlEncode(site.SitePath)}</a>");
            html.Append($"<span>{WebUtility.HtmlEncode(site.Type)} · {count} Dateien</span>");
            html.Append($"<span>{site.UpdatedAt:yyyy-MM-dd HH:mm}</span>");
            html.Append($"<button data-delete-site=\"{WebUtility.HtmlEncode(site.SitePath)}\" class=\"delete-btn\">Delete</button>");
            html.Append("</li>");
        }

        html.Append("</ul>");
    }

    html.Append(DeleteScript());
    html.Append("</body></html>");

    return Results.Content(html.ToString(), "text/html; charset=utf-8");
});

app.MapGet("/{sitePath}", async (string sitePath) =>
{
    var site = await siteManager.GetAsync(sitePath);
    if (site is null)
    {
        return Results.NotFound();
    }

    return Results.Redirect($"/{sitePath}/");
});

app.MapGet("/{sitePath}/", async (string sitePath) =>
{
    var site = await siteManager.GetAsync(sitePath);
    if (site is null)
    {
        return Results.NotFound();
    }

    if (!string.Equals(site.Type, "files", StringComparison.Ordinal) &&
        !string.Equals(site.Type, "folder", StringComparison.Ordinal))
    {
        return Results.NotFound();
    }

    var files = siteManager.ListFiles(sitePath);
    var html = new StringBuilder();

    html.Append("<!DOCTYPE html><html lang=\"de\"><head><meta charset=\"utf-8\">");
    html.Append($"<title>Index of /{WebUtility.HtmlEncode(sitePath)}/</title>");
    html.Append("<style>body{font-family:system-ui;max-width:900px;margin:2em auto;padding:0 1em;}ul{list-style:none;padding:0;}li{padding:.35em 0;display:flex;gap:1em;align-items:center;}a{text-decoration:none;color:#0066cc;}a:hover{text-decoration:underline;}span{color:#666;font-size:.9em;}button{padding:.2em .5em;}</style>");
    html.Append("</head><body>");
    html.Append($"<h1>Index of /{WebUtility.HtmlEncode(sitePath)}/</h1>");

    if (files.Count == 0)
    {
        html.Append("<p>Diese Site enthält keine Dateien.</p>");
    }
    else
    {
        html.Append("<ul>");
        foreach (var file in files)
        {
            var physical = Path.Combine(Path.GetFullPath(app.Services.GetRequiredService<IOptions<SitesOptions>>().Value.SitesRoot), sitePath, file.Path.Replace('/', Path.DirectorySeparatorChar));
            var modified = File.Exists(physical) ? File.GetLastWriteTime(physical).ToString("yyyy-MM-dd HH:mm") : "-";

            html.Append("<li>");
            html.Append($"<a href=\"/{WebUtility.UrlEncode(sitePath)}/{WebUtility.UrlEncode(file.Path).Replace("%2F", "/")}\">{WebUtility.HtmlEncode(file.Path)}</a>");
            html.Append($"<span>{modified}</span>");
            html.Append($"<button data-delete-file=\"{WebUtility.HtmlEncode(file.Path)}\" data-site=\"{WebUtility.HtmlEncode(sitePath)}\" class=\"delete-btn\">Delete</button>");
            html.Append("</li>");
        }
        html.Append("</ul>");
    }

    html.Append(DeleteScript());
    html.Append("</body></html>");

    return Results.Content(html.ToString(), "text/html; charset=utf-8");
});

app.MapDelete("/{sitePath}", async (string sitePath) =>
{
    var deleted = await siteManager.DeleteAsync(sitePath);
    if (!deleted)
    {
        return Results.NotFound();
    }

    return Results.Redirect("/");
});

app.MapDelete("/{sitePath}/{**filePath}", async (string sitePath, string filePath) =>
{
    var deleted = await siteManager.DeleteFileAsync(sitePath, filePath);
    if (deleted != true)
    {
        return Results.NotFound();
    }

    return Results.Redirect($"/{sitePath}/");
});

// Static file serving
app.MapGet("/{sitePath}/{**filePath}", async (string sitePath, string? filePath) =>
{
    if (string.IsNullOrWhiteSpace(filePath))
    {
        return Results.NotFound();
    }

    var site = await siteManager.GetAsync(sitePath);
    if (site is null || !string.Equals(site.Type, "files", StringComparison.Ordinal))
    {
        return Results.NotFound();
    }

    var relativePath = filePath.Replace('/', Path.DirectorySeparatorChar);
    if (relativePath.Contains("..", StringComparison.Ordinal))
    {
        return Results.NotFound();
    }

    var sitesRootFullPath = Path.GetFullPath(app.Services.GetRequiredService<IOptions<SitesOptions>>().Value.SitesRoot);
    var siteFolder = Path.GetFullPath(Path.Combine(sitesRootFullPath, sitePath));
    var requestedFile = Path.GetFullPath(Path.Combine(siteFolder, relativePath));

    if (!requestedFile.StartsWith(siteFolder, StringComparison.OrdinalIgnoreCase))
    {
        return Results.NotFound();
    }

    if (!File.Exists(requestedFile))
    {
        return Results.NotFound();
    }

    var contentType = siteManager.GetContentType(requestedFile);
    await using var stream = File.OpenRead(requestedFile);
    return Results.Stream(stream, contentType);
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

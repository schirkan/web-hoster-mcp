using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Server;
using WebHosterMcp.Core;

var builder = WebApplication.CreateBuilder(args);
builder.Logging.AddConsole();

// Konfiguration
builder.Services.Configure<HostOptions>(builder.Configuration.GetSection("Host"));
builder.Services.Configure<SitesOptions>(builder.Configuration);

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
    return new SiteManager(registry, sitesOptions, hostOptions);
});

// MCP-Server (stdio)
builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithTools<WebHosterMcp.Host.SiteTools>();

var app = builder.Build();

var hostOptions = app.Services.GetRequiredService<IOptions<HostOptions>>().Value;
var sitesOptions = app.Services.GetRequiredService<IOptions<SitesOptions>>().Value;
var siteManager = app.Services.GetRequiredService<SiteManager>();
var sitesRootFullPath = Path.GetFullPath(sitesOptions.SitesRoot);

// Static file serving (MVP1: type=files)
app.MapGet("/{sitePath}/{**filePath}", async (string sitePath, string? filePath, HttpContext ctx) =>
{
    if (string.IsNullOrWhiteSpace(filePath))
    {
        // GET /<site>/ -> Directory-Listing kommt in MVP2
        return Results.NotFound();
    }

    var relativePath = filePath.Replace('/', Path.DirectorySeparatorChar);
    if (relativePath.Contains("..", StringComparison.Ordinal))
    {
        return Results.NotFound();
    }

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
if (lanIp is not null && lanIp != hostOptions.Ip)
{
    app.Logger.LogInformation("  HTTP (LAN): http://{LanIp}:{Port}/", lanIp, hostOptions.Port);
}

await app.RunAsync();

public partial class Program { }

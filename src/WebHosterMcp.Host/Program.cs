using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Server;
using WebHosterMcp.Core;

var builder = WebApplication.CreateBuilder(args);
builder.Logging.AddConsole();

// Konfiguration: appsettings.json Sektion "Host" -> HostOptions POCO
builder.Services.Configure<HostOptions>(builder.Configuration.GetSection("Host"));

// MCP-Server (stdio-Transport). Tools werden aus der aktuellen Assembly per
// [McpServerTool]-Attribute auto-entdeckt. Konkrete Tools folgen in
// Schritt 4 + 6 (siehe specs/mvp1.md).
builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithToolsFromAssembly();

var app = builder.Build();

// LAN-IP-Detection für Startup-Log + spätere result_path-Konstruktion.
var hostOptions = app.Services.GetRequiredService<IOptions<HostOptions>>().Value;
var lanIp = LanIpDetector.GetLanIpv4();

app.Logger.LogInformation("WebHosterMcp:");
app.Logger.LogInformation("  MCP server (stdio): ready");
app.Logger.LogInformation("  HTTP: {Scheme}://{Ip}:{Port}/", "http", hostOptions.Ip, hostOptions.Port);
if (lanIp is not null && lanIp != hostOptions.Ip)
{
    app.Logger.LogInformation("  HTTP (LAN): http://{LanIp}:{Port}/", lanIp, hostOptions.Port);
}

await app.RunAsync();

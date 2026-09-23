using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

var builder = Host.CreateApplicationBuilder(args);
builder.Logging.AddConsole();

// MCP-Server (stdio-Transport). Tools werden aus der aktuellen Assembly per
// [McpServerTool]-Attribute auto-entdeckt. Konkrete Tools folgen in
// Schritt 4 + 6 (siehe specs/mvp1.md).
builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithToolsFromAssembly();

await builder.Build().RunAsync();

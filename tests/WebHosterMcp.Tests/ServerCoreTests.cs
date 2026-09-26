using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using WebHosterMcp.Core;
using WebHosterMcp.Host;
using Xunit;


namespace WebHosterMcp.Tests;

public sealed class ServerCoreTests : IDisposable
{
    private readonly string _tempDir;

    public ServerCoreTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"webhoster-server-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); } catch { }
    }

    [Fact]
    public void RetentionHostedService_Ctor_ThrowsOnInvalidInterval()
    {
        var sitesRoot = Path.Combine(_tempDir, "sites");
        Directory.CreateDirectory(sitesRoot);

        var manager = new SiteManager(
            new SiteRegistry(Path.Combine(_tempDir, "registry.json")),
            new SitesOptions { SitesRoot = sitesRoot },
            new HostOptions());

        var invalid = new RetentionOptions { Enabled = true, DefaultTtlSeconds = 60, CheckIntervalSeconds = 0 };

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RetentionHostedService(manager, Options.Create(invalid), NullLogger<RetentionHostedService>.Instance));
    }
}

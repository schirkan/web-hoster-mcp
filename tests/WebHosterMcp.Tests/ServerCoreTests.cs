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
    public void HttpsCertificateLoader_LoadOrCreate_CreatesSelfSignedPfx()
    {
        var hostOptions = new HostOptions { Ip = "0.0.0.0", Port = 3000, UseHttps = true, HttpsPort = 3443 };
        var httpsOptions = new HttpsOptions
        {
            CertPath = null,
            CertPassword = "test-password",
            SelfSigned = new HttpsSelfSignedOptions
            {
                Enabled = true,
                CertDir = Path.Combine(_tempDir, "certs"),
                Cn = "test-host",
                ForceRegenerate = true
            }
        };

        var cert = HttpsCertificateLoader.LoadOrCreate(hostOptions, httpsOptions, _tempDir);

        Assert.NotNull(cert);
        Assert.True(cert.HasPrivateKey);

        var expectedPath = Path.Combine(_tempDir, "certs", "test-host.pfx");
        Assert.True(File.Exists(expectedPath));
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

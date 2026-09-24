using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WebHosterMcp.Core;
using Xunit;

namespace WebHosterMcp.Tests;

public sealed class SrcDownloadTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _sitesRoot;
    private readonly SiteRegistry _registry;
    private readonly SiteManager _manager;

    public SrcDownloadTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"webhoster-src-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        _sitesRoot = Path.Combine(_tempDir, "sites");
        Directory.CreateDirectory(_sitesRoot);

        var sitesOptions = new SitesOptions { SitesRoot = _sitesRoot, MaxFileSizeBytes = 1_048_576 };
        var hostOptions = new HostOptions { Ip = "127.0.0.1", Port = 3000 };

        _registry = new SiteRegistry(Path.Combine(_sitesRoot, "registry.json"));
        _manager = new SiteManager(_registry, sitesOptions, hostOptions);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); } catch { }
    }

    // === Data URL ===

    [Fact]
    public async Task DeployAsync_DataUrlSrc_DecodesAndWrites()
    {
        var dataUrl = "data:text/plain;base64,aGVsbG8="; // base64("hello")
        var result = await _manager.DeployAsync(new DeployRequest(
            SitePath: "demo",
            Files: new[] { new FileEntry("greeting.txt", Src: dataUrl) }
        ));

        Assert.Null(result.Error);
        Assert.Equal("hello", File.ReadAllText(Path.Combine(_sitesRoot, "demo", "greeting.txt")));
    }

    [Fact]
    public async Task DeployAsync_DataUrlSrc_InvalidBase64_ReturnsError()
    {
        var result = await _manager.DeployAsync(new DeployRequest(
            SitePath: "demo",
            Files: new[] { new FileEntry("bad.txt", Src: "data:text/plain;base64,!!!invalid-base64!!!") }
        ));

        Assert.Equal("src_invalid_data_url", result.Error);
    }

    [Fact]
    public async Task DeployAsync_DataUrlSrc_CaseInsensitive()
    {
        var dataUrl = "DATA:text/plain;base64,aGVsbG8="; // upper-case prefix
        var result = await _manager.DeployAsync(new DeployRequest(
            SitePath: "demo",
            Files: new[] { new FileEntry("greeting.txt", Src: dataUrl) }
        ));

        Assert.Null(result.Error);
        Assert.Equal("hello", File.ReadAllText(Path.Combine(_sitesRoot, "demo", "greeting.txt")));
    }

    // === Local path ===

    [Fact]
    public async Task DeployAsync_LocalFileSrc_CopiesFile()
    {
        var sourcePath = Path.Combine(_tempDir, "source.txt");
        File.WriteAllText(sourcePath, "from local");

        var result = await _manager.DeployAsync(new DeployRequest(
            SitePath: "demo",
            Files: new[] { new FileEntry("copy.txt", Src: sourcePath) }
        ));

        Assert.Null(result.Error);
        Assert.Equal("from local", File.ReadAllText(Path.Combine(_sitesRoot, "demo", "copy.txt")));
    }

    [Fact]
    public async Task DeployAsync_LocalFileSrc_NotFound_ReturnsError()
    {
        var result = await _manager.DeployAsync(new DeployRequest(
            SitePath: "demo",
            Files: new[] { new FileEntry("missing.txt", Src: Path.Combine(_tempDir, "does-not-exist.txt")) }
        ));

        Assert.Equal("src_not_found", result.Error);
    }

    // === HTTP/HTTPS ===

    [Fact]
    public async Task DeployAsync_HttpSrc_Success()
    {
        var stub = new StubHttpMessageHandler((req, _) => Task.FromResult(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(Encoding.UTF8.GetBytes("from http"))
            }));

        var manager = new SiteManager(
            _registry,
            new SitesOptions { SitesRoot = _sitesRoot, MaxFileSizeBytes = 1_048_576 },
            new HostOptions { Ip = "127.0.0.1", Port = 3000 },
            null,
            new SrcOptions { HttpTimeoutSeconds = 5 },
            stub);

        var result = await manager.DeployAsync(new DeployRequest(
            SitePath: "demo",
            Files: new[] { new FileEntry("remote.txt", Src: "https://example.com/remote.txt") }
        ));

        Assert.Null(result.Error);
        Assert.Equal("from http", File.ReadAllText(Path.Combine(_sitesRoot, "demo", "remote.txt")));
    }

    [Fact]
    public async Task DeployAsync_HttpSrc_404_ReturnsFetchFailed()
    {
        var stub = new StubHttpMessageHandler((req, _) => Task.FromResult(
            new HttpResponseMessage(HttpStatusCode.NotFound)));

        var manager = new SiteManager(
            _registry,
            new SitesOptions { SitesRoot = _sitesRoot, MaxFileSizeBytes = 1_048_576 },
            new HostOptions { Ip = "127.0.0.1", Port = 3000 },
            null,
            new SrcOptions { HttpTimeoutSeconds = 5 },
            stub);

        var result = await manager.DeployAsync(new DeployRequest(
            SitePath: "demo",
            Files: new[] { new FileEntry("missing.txt", Src: "https://example.com/missing.txt") }
        ));

        Assert.Equal("src_fetch_failed", result.Error);
    }

    [Fact]
    public async Task DeployAsync_HttpSrc_Timeout_ReturnsTimeout()
    {
        // Stub that delays longer than the configured timeout → HttpClient.Timeout fires
        var stub = new StubHttpMessageHandler(async (req, ct) =>
        {
            await Task.Delay(TimeSpan.FromSeconds(3), ct);
            return new HttpResponseMessage(HttpStatusCode.OK);
        });

        var manager = new SiteManager(
            _registry,
            new SitesOptions { SitesRoot = _sitesRoot, MaxFileSizeBytes = 1_048_576 },
            new HostOptions { Ip = "127.0.0.1", Port = 3000 },
            null,
            new SrcOptions { HttpTimeoutSeconds = 1 },
            stub);

        var result = await manager.DeployAsync(new DeployRequest(
            SitePath: "demo",
            Files: new[] { new FileEntry("slow.txt", Src: "https://example.com/slow.txt") }
        ));

        Assert.Equal("src_timeout", result.Error);
    }

    [Fact]
    public async Task DeployAsync_HttpSrc_Unreachable_ReturnsUnreachable()
    {
        var stub = new StubHttpMessageHandler((req, _) =>
            throw new HttpRequestException("simulated DNS failure"));

        var manager = new SiteManager(
            _registry,
            new SitesOptions { SitesRoot = _sitesRoot, MaxFileSizeBytes = 1_048_576 },
            new HostOptions { Ip = "127.0.0.1", Port = 3000 },
            null,
            new SrcOptions { HttpTimeoutSeconds = 5 },
            stub);

        var result = await manager.DeployAsync(new DeployRequest(
            SitePath: "demo",
            Files: new[] { new FileEntry("missing.txt", Src: "https://nonexistent.example/missing.txt") }
        ));

        Assert.Equal("src_unreachable", result.Error);
    }

    [Fact]
    public async Task DeployAsync_HttpSrc_NoSizeLimit_ForLargeDownloads()
    {
        // Larger than the 1 MB content limit — should still succeed for src
        var big = new byte[2_000_000];
        for (var i = 0; i < big.Length; i++) big[i] = (byte)(i % 256);

        var stub = new StubHttpMessageHandler((req, _) => Task.FromResult(
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(big) }));

        var manager = new SiteManager(
            _registry,
            new SitesOptions { SitesRoot = _sitesRoot, MaxFileSizeBytes = 1_048_576 },
            new HostOptions { Ip = "127.0.0.1", Port = 3000 },
            null,
            new SrcOptions { HttpTimeoutSeconds = 5 },
            stub);

        var result = await manager.DeployAsync(new DeployRequest(
            SitePath: "demo",
            Files: new[] { new FileEntry("big.bin", Src: "https://example.com/big.bin") }
        ));

        Assert.Null(result.Error);
        var written = File.ReadAllBytes(Path.Combine(_sitesRoot, "demo", "big.bin"));
        Assert.Equal(big.Length, written.Length);
    }
}

/// <summary>
/// HttpMessageHandler stub for tests: returns the configured response or throws.
/// </summary>
internal sealed class StubHttpMessageHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _handler;

    public StubHttpMessageHandler(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler)
    {
        _handler = handler;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        => _handler(request, cancellationToken);
}

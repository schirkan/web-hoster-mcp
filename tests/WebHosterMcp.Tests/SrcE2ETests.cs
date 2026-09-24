using System.Net;
using System.Net.Sockets;
using System.Text;
using WebHosterMcp.Core;
using WebHosterMcp.Host;
using Xunit;

namespace WebHosterMcp.Tests;

/// <summary>
/// End-to-end Tests für `src` (Data URL / lokaler Pfad / HTTP via SiteTools-Tool-API). Realer
/// HTTP-Roundtrip via lokalem HttpListener, kein Stub-Handler.
/// </summary>
public sealed class SrcE2ETests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _sitesRoot;
    private readonly SiteManager _manager;
    private readonly SiteTools _tools;

    public SrcE2ETests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"webhoster-src-e2e-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        _sitesRoot = Path.Combine(_tempDir, "sites");
        Directory.CreateDirectory(_sitesRoot);

        var registry = new SiteRegistry(Path.Combine(_sitesRoot, "registry.json"));
        _manager = new SiteManager(
            registry,
            new SitesOptions
            {
                SitesRoot = _sitesRoot,
                MaxFileSizeBytes = 1_048_576,
                MaxPayloadSizeBytes = 1_048_576,
                MaxSubmissionSizeBytes = 1_048_576
            },
            new HostOptions { Ip = "127.0.0.1", Port = 3000 });
        _tools = new SiteTools(_manager);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); } catch { }
    }

    [Fact]
    public async Task Src_DataUrl_WritesDecodedBase64Content()
    {
        // "Hello World" base64 = "SGVsbG8gV29ybGQ="
        var result = await _tools.Deploy(
            site_path: "src-data-001",
            files: new[]
            {
                new DeployToolFileEntry("hello.txt", src: "data:text/plain;base64,SGVsbG8gV29ybGQ=")
            });

        Assert.Null(result.error);
        var path = Path.Combine(_sitesRoot, "src-data-001", "hello.txt");
        Assert.True(File.Exists(path));
        Assert.Equal("Hello World", await File.ReadAllTextAsync(path));
    }

    [Fact]
    public async Task Src_DataUrl_CaseInsensitive_PrefixWorks()
    {
        // "DATA:" statt "data:" — case-insensitivity ist Vertragsbestandteil.
        // "SGVsbG8=" → ASCII "Hello" (Bytes 0x48 0x65 0x6C 0x6C 0x6F).
        var result = await _tools.Deploy(
            site_path: "src-data-002",
            files: new[]
            {
                new DeployToolFileEntry("hello.txt", src: "DATA:text/plain;base64,SGVsbG8=")
            });

        Assert.Null(result.error);
        Assert.Equal("Hello", await File.ReadAllTextAsync(Path.Combine(_sitesRoot, "src-data-002", "hello.txt")));
    }

    [Fact]
    public async Task Src_InvalidDataUrl_ReturnsSrcInvalidDataUrl()
    {
        var result = await _tools.Deploy(
            site_path: "src-data-003",
            files: new[]
            {
                new DeployToolFileEntry("x.txt", src: "data:text/plain;base64,@@@nicht-base64@@@")
            });

        Assert.Equal("src_invalid_data_url", result.error);
    }

    [Fact]
    public async Task Src_LocalFile_CopiesContentToSite()
    {
        var sourcePath = Path.Combine(_tempDir, "source.txt");
        await File.WriteAllTextAsync(sourcePath, "Source content from disk");

        var result = await _tools.Deploy(
            site_path: "src-local-001",
            files: new[]
            {
                new DeployToolFileEntry("from-local.txt", src: sourcePath)
            });

        Assert.Null(result.error);
        var destPath = Path.Combine(_sitesRoot, "src-local-001", "from-local.txt");
        Assert.True(File.Exists(destPath));
        Assert.Equal("Source content from disk", await File.ReadAllTextAsync(destPath));
    }

    [Fact]
    public async Task Src_LocalFile_SubDirPathUnderSiteCreated()
    {
        var sourcePath = Path.Combine(_tempDir, "source2.txt");
        await File.WriteAllTextAsync(sourcePath, "x");

        var result = await _tools.Deploy(
            site_path: "src-local-002",
            files: new[]
            {
                new DeployToolFileEntry("sub/nested/from-local.txt", src: sourcePath)
            });

        Assert.Null(result.error);
        var destPath = Path.Combine(_sitesRoot, "src-local-002", "sub", "nested", "from-local.txt");
        Assert.True(File.Exists(destPath));
    }

    [Fact]
    public async Task Src_LocalFile_Missing_ReturnsSrcNotFound()
    {
        var result = await _tools.Deploy(
            site_path: "src-local-003",
            files: new[]
            {
                new DeployToolFileEntry("x.txt", src: Path.Combine(_tempDir, "does-not-exist.txt"))
            });

        Assert.Equal("src_not_found", result.error);
    }

    [Fact]
    public async Task Src_HttpUrl_DownloadsContent()
    {
        const string payload = "Downloaded from HTTP source";
        using var server = new LocalHttpServer(payload);
        server.Start();

        var result = await _tools.Deploy(
            site_path: "src-http-001",
            files: new[]
            {
                new DeployToolFileEntry("from-http.txt", src: server.Url)
            });

        Assert.Null(result.error);
        var destPath = Path.Combine(_sitesRoot, "src-http-001", "from-http.txt");
        Assert.True(File.Exists(destPath));
        Assert.Equal(payload, await File.ReadAllTextAsync(destPath));
    }

    [Fact]
    public async Task Src_HttpUrl_404Response_ReturnsSrcFetchFailed()
    {
        // Server liefert absichtlich 404 (URL ohne registrierten Pfad).
        using var server = new LocalHttpServer(status404: true);
        server.Start();

        var result = await _tools.Deploy(
            site_path: "src-http-002",
            files: new[]
            {
                new DeployToolFileEntry("x.txt", src: server.Url + "/not-registered")
            });

        Assert.Equal("src_fetch_failed", result.error);
    }

    [Fact]
    public async Task Src_HttpUnreachable_ReturnsError()
    {
        // Port 1 ist reserviert/typischerweise unerreichbar; HttpClient.RequestError-Erwartung.
        var result = await _tools.Deploy(
            site_path: "src-http-003",
            files: new[]
            {
                new DeployToolFileEntry("x.txt", src: "http://127.0.0.1:1/payload.txt")
            });

        // Windows liefert je nach Stack src_unreachable oder src_timeout — beides akzeptabel.
        Assert.True(
            result.error is "src_unreachable" or "src_timeout",
            $"Expected src_unreachable/src_timeout, got '{result.error}'");
    }
}

/// <summary>
/// Minimaler lokaler HTTP-Server (HttpListener) für echte src=http-Roundtrips in Tests.
/// Antwortet auf registrierten Pfad mit dem konfigurierten Body (default 200 OK).
/// </summary>
internal sealed class LocalHttpServer : IDisposable
{
    private readonly HttpListener _listener;
    private readonly string? _body;
    private readonly bool _status404;
    private readonly CancellationTokenSource _cts = new();
    private Task? _loop;

    public string Url { get; }

    public LocalHttpServer(string? body = null, bool status404 = false)
    {
        _body = body;
        _status404 = status404;
        var port = GetFreePort();
        Url = $"http://127.0.0.1:{port}/payload.txt";

        _listener = new HttpListener();
        _listener.Prefixes.Add($"http://127.0.0.1:{port}/");
    }

    public void Start()
    {
        _listener.Start();
        _loop = Task.Run(async () =>
        {
            while (!_cts.Token.IsCancellationRequested)
            {
                HttpListenerContext ctx;
                try { ctx = await _listener.GetContextAsync(); }
                catch { break; }

                try
                {
                    if (_status404)
                    {
                        ctx.Response.StatusCode = 404;
                    }
                    else if (_body != null)
                    {
                        var bytes = Encoding.UTF8.GetBytes(_body);
                        ctx.Response.ContentLength64 = bytes.Length;
                        await ctx.Response.OutputStream.WriteAsync(bytes);
                    }
                    else
                    {
                        ctx.Response.StatusCode = 204;
                    }
                }
                finally
                {
                    try { ctx.Response.Close(); } catch { }
                }
            }
        });
    }

    public void Dispose()
    {
        try { _cts.Cancel(); } catch { }
        try { _listener.Stop(); } catch { }
        try { _listener.Close(); } catch { }
        try { _loop?.Wait(TimeSpan.FromSeconds(2)); } catch { }
        _cts.Dispose();
    }

    private static int GetFreePort()
    {
        var l = new TcpListener(IPAddress.Loopback, 0);
        l.Start();
        var port = ((IPEndPoint)l.LocalEndpoint).Port;
        l.Stop();
        return port;
    }
}

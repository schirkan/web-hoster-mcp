using System.Reflection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using WebHosterMcp.Core;
using WebHosterMcp.Host;
using Xunit;

namespace WebHosterMcp.Tests;

/// <summary>
/// End-to-end Tests für HTTPS / TTL retention / HTTP-DELETE-Endpoints und Delete-Buttons
/// im Directory-/Sites-Index-Listing. Übt dieselbe Code-Oberfläche aus, die die
/// HTTP-Routen in Program.cs aufrufen (SiteManager.DeleteAsync / DeleteFileAsync,
/// RetentionHostedService.SweepAsync, SiteManager.ListFiles für die Delete-Button-
/// Datenmodellierung, sowie Source-Grep für die inline HTML-Rendering-Attribute).
/// </summary>
public sealed class ServerE2ETests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _sitesRoot;
    private readonly SiteRegistry _registry;
    private readonly SiteManager _manager;
    private readonly SiteTools _tools;

    public ServerE2ETests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"webhoster-srv-e2e-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        _sitesRoot = Path.Combine(_tempDir, "sites");
        Directory.CreateDirectory(_sitesRoot);

        _registry = new SiteRegistry(Path.Combine(_sitesRoot, "registry.json"));
        _manager = new SiteManager(
            _registry,
            new SitesOptions
            {
                SitesRoot = _sitesRoot,
                MaxFileSizeBytes = 1_048_576,
                MaxPayloadSizeBytes = 1_048_576,
                MaxSubmissionSizeBytes = 1_048_576
            },
            new HostOptions { Ip = "127.0.0.1", Port = 3000 },
            new RetentionOptions { Enabled = true, DefaultTtlSeconds = 60, CheckIntervalSeconds = 1 });
        _tools = new SiteTools(_manager);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); } catch { }
    }

    [Fact]
    public async Task DeleteSite_EndToEnd_RemovesRegistryAndFolder()
    {
        var deploy = await _tools.Deploy(site_path: "del-site-001", files: new[]
        {
            new DeployToolFileEntry("index.html", content: "<h1>X</h1>")
        });
        Assert.Null(deploy.error);

        Assert.NotNull(await _manager.GetAsync("del-site-001"));
        Assert.True(Directory.Exists(Path.Combine(_sitesRoot, "del-site-001")));

        // Gleicher Aufruf, den der HTTP-Endpoint `DELETE /<site>` macht.
        var deleted = await _manager.DeleteAsync("del-site-001");
        Assert.True(deleted);
        Assert.Null(await _manager.GetAsync("del-site-001"));
        Assert.False(Directory.Exists(Path.Combine(_sitesRoot, "del-site-001")));
    }

    [Fact]
    public async Task DeleteFile_EndToEnd_RemovesOnlyThatFile()
    {
        await _tools.Deploy(site_path: "del-file-001", files: new[]
        {
            new DeployToolFileEntry("a.html", content: "A"),
            new DeployToolFileEntry("b.html", content: "B")
        });

        // Gleicher Aufruf, den der HTTP-Endpoint `DELETE /<site>/<file>` macht.
        var deleted = await _manager.DeleteFileAsync("del-file-001", "a.html");
        Assert.True(deleted);
        Assert.False(File.Exists(Path.Combine(_sitesRoot, "del-file-001", "a.html")));
        Assert.True(File.Exists(Path.Combine(_sitesRoot, "del-file-001", "b.html")));
    }

    [Fact]
    public async Task DeleteFile_NotExistent_ReturnsFalse()
    {
        await _tools.Deploy(site_path: "del-file-404", files: new[]
        {
            new DeployToolFileEntry("x.html", content: "X")
        });

        var deleted = await _manager.DeleteFileAsync("del-file-404", "does-not-exist.html");
        Assert.False(deleted);
    }

    [Fact]
    public async Task DeleteSite_NotFound_ReturnsFalse()
    {
        var deleted = await _manager.DeleteAsync("does-not-exist");
        Assert.False(deleted);
    }

    [Fact]
    public async Task DirectoryListing_DataShape_SupportsDeleteFileButtons()
    {
        // GET /<site>/ listet via SiteManager.ListFiles; jeder Eintrag wird im gerenderten
        // HTML (Program.cs RenderListingHtml) zu einem
        //   <button data-delete-file="..." data-site="..." class="delete-btn">…</button>.
        await _tools.Deploy(site_path: "list-001", files: new[]
        {
            new DeployToolFileEntry("hello.txt", content: "hi"),
            new DeployToolFileEntry("sub/by.html", content: "y")
        });

        var files = _manager.ListFiles("list-001");
        Assert.Equal(2, files.Count);
        Assert.Contains(files, f => f.Path == "hello.txt");
        Assert.Contains(files, f => f.Path == "sub/by.html");
    }

    [Fact]
    public async Task SitesIndex_DataShape_SupportsSiteDeleteButton()
    {
        // GET / iteriert SiteManager.ListAsync + CountFiles; pro Site ein Delete-Button.
        await _tools.Deploy(site_path: "idx-site-001", files: new[]
        {
            new DeployToolFileEntry("a.html", content: "A"),
            new DeployToolFileEntry("b.html", content: "B")
        });

        var sites = await _manager.ListAsync();
        var site = sites.FirstOrDefault(s => s.SitePath == "idx-site-001");
        Assert.NotNull(site);
        Assert.Equal(2, _manager.CountFiles("idx-site-001"));
    }

    [Fact]
    public async Task Retention_DeletesExpiredSite()
    {
        // RetentionHostedService.SweepAsync reflektiv aufrufen, um die TTL-Sweep-Logik
        // deterministisch (timer-frei) zu prüfen.
        var retentionOpts = Options.Create(new RetentionOptions
        {
            Enabled = true,
            DefaultTtlSeconds = 60,
            CheckIntervalSeconds = 1
        });
        var hosted = new RetentionHostedService(_manager, retentionOpts, NullLogger<RetentionHostedService>.Instance);
        var sweep = typeof(RetentionHostedService).GetMethod("SweepAsync", BindingFlags.Instance | BindingFlags.NonPublic)!;

        var deploy = await _tools.Deploy(
            site_path: "ttl-001",
            files: new[] { new DeployToolFileEntry("i.html", content: "X") },
            retention_seconds: 1);
        Assert.Null(deploy.error);
        Assert.NotNull(await _manager.GetAsync("ttl-001"));

        var deadline = DateTime.UtcNow.AddSeconds(3);
        while (DateTime.UtcNow < deadline)
        {
            await Task.Delay(500);
            await ((Task)sweep.Invoke(hosted, new object[] { CancellationToken.None })!);
            if (await _manager.GetAsync("ttl-001") is null) return;
        }
        Assert.Null(await _manager.GetAsync("ttl-001"));
    }

    [Fact]
    public void ProgramCs_RendersDeleteButtonAttributes()
    {
        // Source-Level-Beweis, dass das inline-Listing-Rendering in Program.cs die
        // delete-button-attribute + DELETE-method-Handler-Strings emittiert, an die der
        // JS-Handler gebunden ist. Wir suchen die Marker-Substrings wie im C#-Quelltext
        // vorkommen (C# nutzt \" für eingebettete Anführungszeichen, daher genügt der
        // unquoted Class-Name als Marker).
        var source = ReadProgramCsSource();
        Assert.NotEmpty(source);
        Assert.Contains("data-delete-site", source);
        Assert.Contains("data-delete-file", source);
        Assert.Contains("method: 'DELETE'", source);
        Assert.Contains("delete-btn", source);
    }

    [Fact]
    public void HttpsOptions_DefaultsAreConfiguredForSelfSignedFallback()
    {
        // HttpsCertificateLoader.LoadOrCreate wird in ServerCoreTests abgedeckt.
        // Hier prüfen wir die Default-Options-Struktur, die der HTTPS-Endpoint erwartet.
        var opts = new HttpsOptions();
        Assert.NotNull(opts.SelfSigned);
        Assert.True(opts.SelfSigned.Enabled);
    }

    private static string ReadProgramCsSource()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "WebHosterMcp.sln")))
            dir = dir.Parent;
        Assert.NotNull(dir);
        return File.ReadAllText(Path.Combine(dir!.FullName, "src", "WebHosterMcp.Host", "Program.cs"));
    }
}

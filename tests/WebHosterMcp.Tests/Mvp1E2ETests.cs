using WebHosterMcp.Core;
using WebHosterMcp.Host;
using Xunit;

namespace WebHosterMcp.Tests;

public sealed class Mvp1E2ETests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _sitesRoot;
    private readonly SiteManager _manager;
    private readonly SiteTools _tools;

    public Mvp1E2ETests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"webhoster-e2e-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);

        _sitesRoot = Path.Combine(_tempDir, "sites");
        Directory.CreateDirectory(_sitesRoot);

        var registry = new SiteRegistry(Path.Combine(_sitesRoot, "registry.json"));
        _manager = new SiteManager(
            registry,
            new SitesOptions { SitesRoot = _sitesRoot, MaxFileSizeBytes = 1_048_576 },
            new HostOptions { Ip = "127.0.0.1", Port = 3000 });

        _tools = new SiteTools(_manager);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); } catch { }
    }

    [Fact]
    public async Task FullFlow_Deploy_List_Get_Delete()
    {
        // deploy
        var deploy = await _tools.Deploy(site_path: "demo-001", files: new[]
        {
            new DeployToolFileEntry("index.html", content: "<h1>E2E</h1>"),
            new DeployToolFileEntry("css/style.css", content: "body { color: red; }")
        });

        Assert.Null(deploy.error);
        Assert.Equal("demo-001", deploy.site_path);
        Assert.True(File.Exists(Path.Combine(_sitesRoot, "demo-001", "index.html")));
        Assert.True(File.Exists(Path.Combine(_sitesRoot, "demo-001", "css", "style.css")));

        // static-file-content-type mapping contract
        Assert.Equal("text/html; charset=utf-8", _manager.GetContentType("index.html"));
        Assert.Equal("text/css; charset=utf-8", _manager.GetContentType("style.css"));

        // list + get
        var sites = await _tools.ListSites();
        Assert.Single(sites);
        Assert.Equal("demo-001", sites[0].site_path);

        var info = await _tools.GetSiteInfo("demo-001");
        Assert.Null(info.error);
        Assert.Equal(2, info.file_count);
        Assert.NotNull(info.files);
        Assert.Equal(2, info.files!.Count);

        // delete
        var deleted = await _tools.DeleteSite("demo-001");
        Assert.True(deleted.deleted);
        Assert.False(Directory.Exists(Path.Combine(_sitesRoot, "demo-001")));

        var after = await _tools.GetSiteInfo("demo-001");
        Assert.Equal("site_not_found", after.error);
    }
}

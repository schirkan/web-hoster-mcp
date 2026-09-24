using WebHosterMcp.Core;
using WebHosterMcp.Host;
using Xunit;

namespace WebHosterMcp.Tests;

public sealed class SiteE2ETests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _sitesRoot;
    private readonly SiteManager _manager;
    private readonly SiteTools _tools;

    public SiteE2ETests()
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

    [Fact]
    public async Task TwoSites_RunInParallelWithoutConflict()
    {
        var deployA = _tools.Deploy(site_path: "site-a", files: new[]
        {
            new DeployToolFileEntry("index.html", content: "A")
        });

        var deployB = _tools.Deploy(site_path: "site-b", files: new[]
        {
            new DeployToolFileEntry("index.html", content: "B"),
            new DeployToolFileEntry("assets/app.js", content: "console.log('b')")
        });

        await Task.WhenAll(deployA, deployB);

        var sites = await _tools.ListSites();
        Assert.Equal(2, sites.Count);
        Assert.Contains(sites, s => s.site_path == "site-a" && s.file_count == 1);
        Assert.Contains(sites, s => s.site_path == "site-b" && s.file_count == 2);
    }

    [Fact]
    public async Task Persistence_SurvivesManagerRestartSimulation()
    {
        await _tools.Deploy(site_path: "persist-001", files: new[]
        {
            new DeployToolFileEntry("index.html", content: "Persist")
        });

        // Restart-Simulation: neue Registry/Manager-Instanzen auf derselben Disk
        var reloadedRegistry = new SiteRegistry(Path.Combine(_sitesRoot, "registry.json"));
        var reloadedManager = new SiteManager(
            reloadedRegistry,
            new SitesOptions { SitesRoot = _sitesRoot, MaxFileSizeBytes = 1_048_576 },
            new HostOptions { Ip = "127.0.0.1", Port = 3000 });
        var reloadedTools = new SiteTools(reloadedManager);

        var info = await reloadedTools.GetSiteInfo("persist-001");
        Assert.Null(info.error);
        Assert.Equal("persist-001", info.site_path);
        Assert.True(File.Exists(Path.Combine(_sitesRoot, "persist-001", "index.html")));
    }
}

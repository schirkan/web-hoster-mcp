using WebHosterMcp.Core;
using WebHosterMcp.Host;
using Xunit;

namespace WebHosterMcp.Tests;

public sealed class SiteToolsTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _sitesRoot;
    private readonly SiteTools _tools;

    public SiteToolsTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"webhoster-tools-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);

        _sitesRoot = Path.Combine(_tempDir, "sites");
        Directory.CreateDirectory(_sitesRoot);

        var registry = new SiteRegistry(Path.Combine(_sitesRoot, "registry.json"));
        var manager = new SiteManager(
            registry,
            new SitesOptions { SitesRoot = _sitesRoot, MaxFileSizeBytes = 1_048_576 },
            new HostOptions { Ip = "127.0.0.1", Port = 3000 });

        _tools = new SiteTools(manager);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); } catch { }
    }

    [Fact]
    public async Task ListSites_ReturnsRawArrayWithExpectedFields()
    {
        await _tools.Deploy(site_path: "demo-001", files: new[]
        {
            new DeployToolFileEntry("index.html", content: "<h1>Hello</h1>"),
            new DeployToolFileEntry("css/style.css", content: "body{}")
        });

        var sites = await _tools.ListSites();

        Assert.Single(sites);
        var first = sites[0];
        Assert.Equal("demo-001", first.site_path);
        Assert.Equal("files", first.type);
        Assert.Equal(2, first.file_count);
        Assert.Equal("http://127.0.0.1:3000/demo-001/", first.url);
    }

    [Fact]
    public async Task GetSiteInfo_ReturnsFilesAndDeleteSite_RemovesIt()
    {
        await _tools.Deploy(site_path: "demo-001", files: new[]
        {
            new DeployToolFileEntry("index.html", content: "<h1>Hello</h1>"),
            new DeployToolFileEntry("css/style.css", content: "body{}")
        });

        var info = await _tools.GetSiteInfo("demo-001");
        Assert.Null(info.error);
        Assert.Equal("demo-001", info.site_path);
        Assert.Equal(2, info.file_count);
        Assert.NotNull(info.files);
        Assert.Equal(2, info.files!.Count);
        Assert.Contains(info.files, f => f.path == "index.html");
        Assert.Contains(info.files, f => f.path == "css/style.css");

        var deleted = await _tools.DeleteSite("demo-001");
        Assert.True(deleted.deleted);

        var after = await _tools.GetSiteInfo("demo-001");
        Assert.Equal("site_not_found", after.error);
    }
}

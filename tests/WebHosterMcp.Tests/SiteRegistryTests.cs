using WebHosterMcp.Core;
using Xunit;

namespace WebHosterMcp.Tests;

public class SiteRegistryTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _registryPath;

    public SiteRegistryTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"webhoster-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        _registryPath = Path.Combine(_tempDir, "registry.json");
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); } catch { }
    }

    [Fact]
    public async Task LoadAsync_FileNotExists_EmptyRegistry()
    {
        var reg = new SiteRegistry(_registryPath);
        await reg.LoadAsync();
        var all = await reg.ReadAllAsync();
        Assert.Empty(all);
    }

    [Fact]
    public async Task DeployAsync_NewSite_CreatesEntry()
    {
        var reg = new SiteRegistry(_registryPath);
        await reg.LoadAsync();

        var entry = new SiteEntry
        {
            SitePath = "demo",
            Type = "files",
        };
        var result = await reg.DeployAsync(entry);

        Assert.Equal("demo", result.SitePath);
        Assert.True(result.CreatedAt > DateTime.MinValue);
        Assert.True(result.UpdatedAt > DateTime.MinValue);

        var read = await reg.ReadAsync("demo");
        Assert.NotNull(read);
        Assert.Equal("files", read!.Type);
    }

    [Fact]
    public async Task DeployAsync_ExistingSite_PreservesCreatedAt()
    {
        var reg = new SiteRegistry(_registryPath);
        await reg.LoadAsync();

        var entry = new SiteEntry { SitePath = "demo", Type = "files" };
        var first = await reg.DeployAsync(entry);
        var originalCreatedAt = first.CreatedAt;

        await Task.Delay(10);

        entry.Type = "folder";
        var updated = await reg.DeployAsync(entry);

        Assert.Equal(originalCreatedAt, updated.CreatedAt);
        Assert.True(updated.UpdatedAt >= originalCreatedAt);
        Assert.Equal("folder", updated.Type);
    }

    [Fact]
    public async Task DeleteAsync_ExistingSite_ReturnsTrue()
    {
        var reg = new SiteRegistry(_registryPath);
        await reg.LoadAsync();
        await reg.DeployAsync(new SiteEntry { SitePath = "demo" });

        var deleted = await reg.DeleteAsync("demo");
        Assert.True(deleted);

        var read = await reg.ReadAsync("demo");
        Assert.Null(read);
    }

    [Fact]
    public async Task DeleteAsync_NonExistentSite_ReturnsFalse()
    {
        var reg = new SiteRegistry(_registryPath);
        await reg.LoadAsync();

        var deleted = await reg.DeleteAsync("never-existed");
        Assert.False(deleted);
    }

    [Fact]
    public async Task AtomicWrite_PersistsAcrossInstances()
    {
        var reg1 = new SiteRegistry(_registryPath);
        await reg1.DeployAsync(new SiteEntry { SitePath = "site-a", Type = "files" });

        var reg2 = new SiteRegistry(_registryPath);
        await reg2.LoadAsync();
        var read = await reg2.ReadAsync("site-a");
        Assert.NotNull(read);
        Assert.Equal("site-a", read!.SitePath);
    }

    [Fact]
    public async Task ReadAllAsync_ReturnsAllDeployedSites()
    {
        var reg = new SiteRegistry(_registryPath);
        await reg.LoadAsync();
        await reg.DeployAsync(new SiteEntry { SitePath = "site-a" });
        await reg.DeployAsync(new SiteEntry { SitePath = "site-b" });
        await reg.DeployAsync(new SiteEntry { SitePath = "site-c" });

        var all = await reg.ReadAllAsync();
        Assert.Equal(3, all.Count);
        Assert.Contains(all, s => s.SitePath == "site-a");
        Assert.Contains(all, s => s.SitePath == "site-b");
        Assert.Contains(all, s => s.SitePath == "site-c");
    }
}

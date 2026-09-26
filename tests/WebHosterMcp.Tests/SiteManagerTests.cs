using WebHosterMcp.Core;
using Xunit;

namespace WebHosterMcp.Tests;

public class SiteManagerTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _sitesRoot;
    private readonly SiteRegistry _registry;
    private readonly SiteManager _manager;

    public SiteManagerTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"webhoster-mgr-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        _sitesRoot = Path.Combine(_tempDir, "sites");
        Directory.CreateDirectory(_sitesRoot);

        var sitesOptions = new SitesOptions { SitesRoot = _sitesRoot, MaxFileSizeBytes = 1_048_576 };
        var hostOptions = new HostOptions { Ip = "0.0.0.0", Port = 3000 };

        _registry = new SiteRegistry(Path.Combine(_tempDir, "registry.json"));
        _manager = new SiteManager(_registry, sitesOptions, hostOptions);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); } catch { }
    }

    // === deploy ===

    [Fact]
    public async Task DeployAsync_NewSite_CreatesSiteAndWritesFiles()
    {
        var result = await _manager.DeployAsync(new DeployRequest(
            SitePath: "demo",
            Files: new[] {
                new FileEntry("index.html", "<h1>Hi</h1>"),
                new FileEntry("css/style.css", "body { margin: 0 }")
            }
        ));

        Assert.Null(result.Error);
        Assert.Equal("demo", result.SitePath);
        Assert.NotNull(result.Files);
        Assert.Equal(2, result.Files!.Count);
        Assert.Contains(result.Files, f => f.Path == "index.html");
        Assert.Contains(result.Files, f => f.Path == "css/style.css");

        Assert.True(File.Exists(Path.Combine(_sitesRoot, "demo", "index.html")));
        Assert.True(File.Exists(Path.Combine(_sitesRoot, "demo", "css", "style.css")));
        Assert.Equal("<h1>Hi</h1>", File.ReadAllText(Path.Combine(_sitesRoot, "demo", "index.html")));
    }

    [Fact]
    public async Task DeployAsync_InvalidType_ReturnsInvalidTypeError()
    {
        var result = await _manager.DeployAsync(new DeployRequest(SitePath: "x", Type: "weird"));
        Assert.Equal("invalid_type", result.Error);
    }

    [Fact]
    public async Task DeployAsync_BadSitePath_ReturnsInvalidSiteIdError()
    {
        var result = await _manager.DeployAsync(new DeployRequest(SitePath: "AB")); // too short (need 3-32)
        Assert.Equal("invalid_site_id", result.Error);
    }

    [Fact]
    public async Task DeployAsync_ExistingSiteTypeChange_ReturnsTypeImmutable()
    {
        await _manager.DeployAsync(new DeployRequest(SitePath: "demo", Type: "files"));
        var result = await _manager.DeployAsync(new DeployRequest(SitePath: "demo", Type: "folder"));
        Assert.Equal("type_immutable", result.Error);
    }

    [Fact]
    public async Task DeployAsync_EmptyFilesMerge_IsNoOp()
    {
        await _manager.DeployAsync(new DeployRequest(
            SitePath: "demo",
            Files: new[] { new FileEntry("index.html", "x") }
        ));

        var result = await _manager.DeployAsync(new DeployRequest(SitePath: "demo", Mode: "merge", Files: Array.Empty<FileEntry>()));

        Assert.Null(result.Error);
        Assert.True(File.Exists(Path.Combine(_sitesRoot, "demo", "index.html")));
    }

    [Fact]
    public async Task DeployAsync_EmptyFilesReplace_DeletesAllFiles()
    {
        await _manager.DeployAsync(new DeployRequest(
            SitePath: "demo",
            Files: new[] {
                new FileEntry("index.html", "x"),
                new FileEntry("other.html", "y")
            }
        ));

        var result = await _manager.DeployAsync(new DeployRequest(SitePath: "demo", Mode: "replace", Files: Array.Empty<FileEntry>()));

        Assert.Null(result.Error);
        Assert.False(File.Exists(Path.Combine(_sitesRoot, "demo", "index.html")));
        Assert.False(File.Exists(Path.Combine(_sitesRoot, "demo", "other.html")));
    }

    [Fact]
    public async Task DeployAsync_MergeMode_DeletesFilesButKeepsOthers()
    {
        await _manager.DeployAsync(new DeployRequest(
            SitePath: "demo",
            Files: new[] {
                new FileEntry("keep.txt", "x"),
                new FileEntry("delete.txt", "y")
            }
        ));

        var result = await _manager.DeployAsync(new DeployRequest(
            SitePath: "demo",
            Mode: "merge",
            Files: new[] { new FileEntry("delete.txt", Delete: true) }
        ));

        Assert.Null(result.Error);
        Assert.True(File.Exists(Path.Combine(_sitesRoot, "demo", "keep.txt")));
        Assert.False(File.Exists(Path.Combine(_sitesRoot, "demo", "delete.txt")));
    }

    [Fact]
    public async Task DeployAsync_PathTraversal_ReturnsError()
    {
        var result = await _manager.DeployAsync(new DeployRequest(
            SitePath: "demo",
            Files: new[] { new FileEntry("../etc/passwd", "x") }
        ));
        Assert.Equal("path_traversal", result.Error);
    }

    [Fact]
    public async Task DeployAsync_PathTooLong_ReturnsError()
    {
        var longPath = new string('a', 261);
        var result = await _manager.DeployAsync(new DeployRequest(
            SitePath: "demo",
            Files: new[] { new FileEntry(longPath, "x") }
        ));
        Assert.Equal("path_too_long", result.Error);
    }

    [Fact]
    public async Task DeployAsync_DuplicatePath_ReturnsError()
    {
        var result = await _manager.DeployAsync(new DeployRequest(
            SitePath: "demo",
            Files: new[] {
                new FileEntry("file.txt", "a"),
                new FileEntry("file.txt", "b")
            }
        ));
        Assert.Equal("duplicate_path", result.Error);
    }

    [Fact]
    public async Task DeployAsync_ContentTooLarge_ReturnsError()
    {
        var bigContent = new string('a', 1_048_577); // > 1 MB
        var result = await _manager.DeployAsync(new DeployRequest(
            SitePath: "demo",
            Files: new[] { new FileEntry("big.txt", bigContent) }
        ));
        Assert.Equal("file_too_large", result.Error);
    }

    [Fact]
    public async Task DeployAsync_MissingContentAndSrc_ReturnsError()
    {
        var result = await _manager.DeployAsync(new DeployRequest(
            SitePath: "demo",
            Files: new[] { new FileEntry("no-content.txt") }
        ));
        Assert.Equal("missing_content", result.Error);
    }

    [Fact]
    public async Task DeployAsync_DeletePlusContent_ReturnsError()
    {
        var result = await _manager.DeployAsync(new DeployRequest(
            SitePath: "demo",
            Files: new[] { new FileEntry("bad.txt", "x", Delete: true) }
        ));
        Assert.Equal("invalid_file_entry", result.Error);
    }

    [Fact]
    public async Task DeployAsync_ContentPlusSrc_ReturnsError()
    {
        var result = await _manager.DeployAsync(new DeployRequest(
            SitePath: "demo",
            Files: new[] { new FileEntry("bad.txt", "inline", Src: "C:/local/bad.txt") }
        ));
        Assert.Equal("invalid_file_entry", result.Error);
    }

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
    public async Task DeployAsync_RetentionSecondsOverride_Stored()
    {
        await _manager.DeployAsync(new DeployRequest(
            SitePath: "demo",
            RetentionSeconds: 3600,
            Files: new[] { new FileEntry("x.txt", "x") }
        ));

        var entry = await _manager.GetAsync("demo");
        Assert.NotNull(entry);
        Assert.Equal(3600, entry!.RetentionSeconds);
    }

    [Fact]
    public async Task DeployAsync_RetentionSeconds_DefaultFromOptions_WhenOmittedAndNew()
    {
        var manager = new SiteManager(
            _registry,
            new SitesOptions { SitesRoot = _sitesRoot, MaxFileSizeBytes = 1_048_576 },
            new HostOptions { Ip = "0.0.0.0", Port = 3000 },
            new RetentionOptions { DefaultTtlSeconds = 1234, CheckIntervalSeconds = 60 });

        await manager.DeployAsync(new DeployRequest(
            SitePath: "demo-default-retention",
            Files: new[] { new FileEntry("x.txt", "x") }
        ));

        var entry = await manager.GetAsync("demo-default-retention");
        Assert.NotNull(entry);
        Assert.Equal(1234, entry!.RetentionSeconds);
    }

    [Fact]
    public async Task DeployAsync_GeneratedSitePath_Is8Chars()
    {
        var result = await _manager.DeployAsync(new DeployRequest(SitePath: null));
        Assert.Null(result.Error);
        Assert.NotNull(result.SitePath);
        Assert.Equal(8, result.SitePath!.Length);
    }

    [Fact]
    public async Task DeployAsync_PreservesCreatedAt_OnUpdate()
    {
        await _manager.DeployAsync(new DeployRequest(
            SitePath: "demo", Type: "files",
            Files: new[] { new FileEntry("a.txt", "x") }
        ));
        var first = await _manager.GetAsync("demo");
        var originalCreatedAt = first!.CreatedAt;

        await Task.Delay(50);
        await _manager.DeployAsync(new DeployRequest(
            SitePath: "demo", Type: "files",
            Files: new[] { new FileEntry("b.txt", "y") }
        ));

        var updated = await _manager.GetAsync("demo");
        Assert.Equal(originalCreatedAt, updated!.CreatedAt);
        Assert.True(updated.UpdatedAt >= originalCreatedAt);
    }

    // === list / get / delete ===

    [Fact]
    public async Task ListAsync_ReturnsAllDeployedSites()
    {
        await _manager.DeployAsync(new DeployRequest(SitePath: "site-a", Files: new[] { new FileEntry("x.txt", "x") }));
        await _manager.DeployAsync(new DeployRequest(SitePath: "site-b", Files: new[] { new FileEntry("y.txt", "y") }));

        var sites = await _manager.ListAsync();
        Assert.Equal(2, sites.Count);
        Assert.Contains(sites, s => s.SitePath == "site-a");
        Assert.Contains(sites, s => s.SitePath == "site-b");
    }

    [Fact]
    public async Task GetAsync_ExistingSite_ReturnsEntry()
    {
        await _manager.DeployAsync(new DeployRequest(SitePath: "demo"));
        var entry = await _manager.GetAsync("demo");
        Assert.NotNull(entry);
        Assert.Equal("demo", entry!.SitePath);
    }

    [Fact]
    public async Task GetAsync_NonExistentSite_ReturnsNull()
    {
        var entry = await _manager.GetAsync("never-existed");
        Assert.Null(entry);
    }

    [Fact]
    public async Task DeleteAsync_RemovesRegistryAndFolder()
    {
        await _manager.DeployAsync(new DeployRequest(
            SitePath: "demo", Files: new[] { new FileEntry("index.html", "x") }
        ));

        var deleted = await _manager.DeleteAsync("demo");
        Assert.True(deleted);
        Assert.False(Directory.Exists(Path.Combine(_sitesRoot, "demo")));
        var readEntry = await _registry.ReadAsync("demo");
        Assert.Null(readEntry);
    }

    [Fact]
    public async Task DeleteAsync_NonExistentSite_ReturnsFalse()
    {
        var deleted = await _manager.DeleteAsync("never-existed");
        Assert.False(deleted);
    }

    [Fact]
    public async Task DeleteFileAsync_ExistingFile_DeletesOnlyFile()
    {
        await _manager.DeployAsync(new DeployRequest(
            SitePath: "demo",
            Files: new[]
            {
                new FileEntry("keep.txt", "keep"),
                new FileEntry("sub/delete.txt", "delete")
            }
        ));

        var deleted = await _manager.DeleteFileAsync("demo", "sub/delete.txt");

        Assert.True(deleted);
        Assert.True(File.Exists(Path.Combine(_sitesRoot, "demo", "keep.txt")));
        Assert.False(File.Exists(Path.Combine(_sitesRoot, "demo", "sub", "delete.txt")));
    }

    [Fact]
    public async Task DeleteFileAsync_UnknownSite_ReturnsNull()
    {
        var deleted = await _manager.DeleteFileAsync("never", "file.txt");
        Assert.Null(deleted);
    }

    [Fact]
    public async Task DeleteFileAsync_PathNotFound_ReturnsFalse()
    {
        await _manager.DeployAsync(new DeployRequest(
            SitePath: "demo",
            Files: new[] { new FileEntry("index.html", "x") }
        ));

        var deleted = await _manager.DeleteFileAsync("demo", "missing.txt");
        Assert.False(deleted);
    }

    // === file_count / content-type ===

    [Fact]
    public void CountFiles_RecursiveCount()
    {
        Directory.CreateDirectory(Path.Combine(_sitesRoot, "site"));
        Directory.CreateDirectory(Path.Combine(_sitesRoot, "site", "css"));
        Directory.CreateDirectory(Path.Combine(_sitesRoot, "site", "js"));
        File.WriteAllText(Path.Combine(_sitesRoot, "site", "index.html"), "x");
        File.WriteAllText(Path.Combine(_sitesRoot, "site", "css", "style.css"), "x");
        File.WriteAllText(Path.Combine(_sitesRoot, "site", "js", "app.js"), "x");

        Assert.Equal(3, _manager.CountFiles("site"));
    }

    [Fact]
    public void CountFiles_NonExistentSite_ReturnsZero()
    {
        Assert.Equal(0, _manager.CountFiles("never-existed"));
    }

    [Theory]
    [InlineData("index.html", "text/html; charset=utf-8")]
    [InlineData("style.css", "text/css; charset=utf-8")]
    [InlineData("app.js", "application/javascript; charset=utf-8")]
    [InlineData("data.json", "application/json; charset=utf-8")]
    [InlineData("logo.png", "image/png")]
    [InlineData("icon.svg", "image/svg+xml")]
    [InlineData("font.woff2", "font/woff2")]
    [InlineData("readme.txt", "text/plain; charset=utf-8")]
    [InlineData("unknown.xyz", "application/octet-stream")]
    [InlineData("no-ext", "application/octet-stream")]
    [InlineData("UPPER.HTML", "text/html; charset=utf-8")] // case-insensitive
    public void GetContentType_MapsCorrectly(string filePath, string expected)
    {
        Assert.Equal(expected, _manager.GetContentType(filePath));
    }
}

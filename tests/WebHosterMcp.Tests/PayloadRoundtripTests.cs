using System.Text.Json;
using WebHosterMcp.Core;
using Xunit;

namespace WebHosterMcp.Tests;

/// <summary>
/// Spec 21 Bug-Fix: verifies that WritePayloadAsync preserves the original
/// JSON structure when a JsonElement is provided (the previous
/// JsonSerializer.SerializeToUtf8Bytes round-trip corrupted arrays/booleans/numbers).
///
/// These tests exercise the SiteManager end-to-end with payloads that would have
/// been destroyed by the pre-fix serialization path.
///
/// Assertions are intentionally JsonDocument-based (not raw-text substring) so
/// they remain correct regardless of whitespace, key-ordering, or other formatting
/// variations in the stored payload.json file.
/// </summary>
public class PayloadRoundtripTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _sitesRoot;
    private readonly SiteManager _manager;

    public PayloadRoundtripTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"webhoster-rt-{Guid.NewGuid():N}");
        _sitesRoot = Path.Combine(_tempDir, "sites");
        Directory.CreateDirectory(_sitesRoot);

        var sitesOptions = new SitesOptions { SitesRoot = _sitesRoot, MaxFileSizeBytes = 1_048_576 };
        var hostOptions = new HostOptions { Ip = "0.0.0.0", Port = 3000 };

        _manager = new SiteManager(
            new SiteRegistry(Path.Combine(_tempDir, "registry.json")),
            sitesOptions, hostOptions);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); } catch { }
    }

    private static JsonElement Parse(string json)
        => JsonDocument.Parse(json).RootElement.Clone();

    private static async Task<JsonElement> ReadStoredSchemaAsync(string payloadJsonPath)
    {
        var raw = await File.ReadAllTextAsync(payloadJsonPath);
        return JsonDocument.Parse(raw).RootElement.GetProperty("schema");
    }

    [Fact]
    public async Task DeployAsync_PreservesArrayAsArray()
    {
        var payload = Parse("""
        {
          "schema": {
            "type": "object",
            "properties": {
              "q1": {
                "type": "string",
                "oneOf": [
                  { "const": "a", "title": "A" },
                  { "const": "b", "title": "B" }
                ]
              }
            }
          }
        }
        """);

        var result = await _manager.DeployAsync(new DeployRequest(
            SitePath: "rt-array",
            Type: "json-schema-form",
            Payload: payload));

        Assert.Null(result.Error);

        var schema = await ReadStoredSchemaAsync(Path.Combine(_sitesRoot, "rt-array", "payload.json"));
        var oneOf = schema.GetProperty("properties").GetProperty("q1").GetProperty("oneOf");

        Assert.Equal(JsonValueKind.Array, oneOf.ValueKind);
        Assert.Equal(2, oneOf.GetArrayLength());

        Assert.Equal("a", oneOf[0].GetProperty("const").GetString());
        Assert.Equal("A", oneOf[0].GetProperty("title").GetString());
        Assert.Equal("b", oneOf[1].GetProperty("const").GetString());
        Assert.Equal("B", oneOf[1].GetProperty("title").GetString());
    }

    [Fact]
    public async Task DeployAsync_PreservesBooleanAsBoolean()
    {
        var payload = Parse("""
        {
          "schema": {
            "type": "object",
            "properties": {
              "flag": { "type": "boolean", "default": false }
            }
          }
        }
        """);

        var result = await _manager.DeployAsync(new DeployRequest(
            SitePath: "rt-bool",
            Type: "json-schema-form",
            Payload: payload));

        Assert.Null(result.Error);

        var schema = await ReadStoredSchemaAsync(Path.Combine(_sitesRoot, "rt-bool", "payload.json"));
        var defaultVal = schema.GetProperty("properties").GetProperty("flag").GetProperty("default");

        // The corruption would turn this into a JSON string ("false" with quotes).
        Assert.Equal(JsonValueKind.False, defaultVal.ValueKind);
    }

    [Fact]
    public async Task DeployAsync_PreservesNumberAsNumber()
    {
        var payload = Parse("""
        {
          "schema": {
            "type": "object",
            "properties": {
              "age": { "type": "integer", "default": 42 }
            }
          }
        }
        """);

        var result = await _manager.DeployAsync(new DeployRequest(
            SitePath: "rt-num",
            Type: "json-schema-form",
            Payload: payload));

        Assert.Null(result.Error);

        var schema = await ReadStoredSchemaAsync(Path.Combine(_sitesRoot, "rt-num", "payload.json"));
        var defaultVal = schema.GetProperty("properties").GetProperty("age").GetProperty("default");

        Assert.Equal(JsonValueKind.Number, defaultVal.ValueKind);
        Assert.Equal(42, defaultVal.GetInt32());
    }

    [Fact]
    public async Task DeployAsync_PreservesEmptyObjectAsObject()
    {
        var payload = Parse("""
        {
          "schema": { "type": "object", "properties": {} },
          "data": {}
        }
        """);

        var result = await _manager.DeployAsync(new DeployRequest(
            SitePath: "rt-empty",
            Type: "json-schema-form",
            Payload: payload));

        Assert.Null(result.Error);

        var stored = await File.ReadAllTextAsync(Path.Combine(_sitesRoot, "rt-empty", "payload.json"));
        var doc = JsonDocument.Parse(stored);

        Assert.Equal(JsonValueKind.Object, doc.RootElement.GetProperty("data").ValueKind);
    }

    [Fact]
    public async Task DeployAsync_AutoConvertsEnumNamesToOneOf()
    {
        // Pre-fix legacy syntax (RJSF v4) → server auto-rewrites to v5 oneOf
        var payload = Parse("""
        {
          "schema": {
            "type": "object",
            "properties": {
              "category": {
                "type": "string",
                "enum": ["bug", "feature"],
                "options": { "enumNames": ["Bug", "Feature"] }
              }
            }
          }
        }
        """);

        var result = await _manager.DeployAsync(new DeployRequest(
            SitePath: "rt-convert",
            Type: "json-schema-form",
            Payload: payload));

        Assert.Null(result.Error);

        var schema = await ReadStoredSchemaAsync(Path.Combine(_sitesRoot, "rt-convert", "payload.json"));
        var category = schema.GetProperty("properties").GetProperty("category");

        Assert.False(category.TryGetProperty("enum", out _));
        Assert.False(category.TryGetProperty("options", out _));

        var oneOf = category.GetProperty("oneOf");
        Assert.Equal(JsonValueKind.Array, oneOf.ValueKind);
        Assert.Equal(2, oneOf.GetArrayLength());

        // Const values preserve their original JSON type (string), titles are the v4 labels.
        Assert.Equal(JsonValueKind.String, oneOf[0].GetProperty("const").ValueKind);
        Assert.Equal("bug", oneOf[0].GetProperty("const").GetString());
        Assert.Equal("Bug", oneOf[0].GetProperty("title").GetString());

        Assert.Equal("feature", oneOf[1].GetProperty("const").GetString());
        Assert.Equal("Feature", oneOf[1].GetProperty("title").GetString());
    }

    [Fact]
    public async Task DeployAsync_RejectsMalformedSchema()
    {
        // schema.type is "string" → invalid for a form (must be "object")
        var payload = Parse("""
        {
          "schema": { "type": "string", "properties": {} }
        }
        """);

        var result = await _manager.DeployAsync(new DeployRequest(
            SitePath: "rt-bad-type",
            Type: "json-schema-form",
            Payload: payload));

        Assert.Equal("invalid_payload_schema_type_not_object", result.Error);
        // No file should have been written
        Assert.False(Directory.Exists(Path.Combine(_sitesRoot, "rt-bad-type")));
    }
}

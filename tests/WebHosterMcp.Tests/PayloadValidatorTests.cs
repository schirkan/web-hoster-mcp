using System.Text.Json;
using WebHosterMcp.Core;
using Xunit;

namespace WebHosterMcp.Tests;

public class PayloadValidatorTests
{
    private const int MaxBytes = 1_048_576;

    private static JsonElement Parse(string json)
        => JsonDocument.Parse(json).RootElement.Clone();

    [Fact]
    public void Validate_ValidSchema_ReturnsNull()
    {
        var payload = Parse("""
        {
          "schema": {
            "type": "object",
            "required": ["name"],
            "properties": {
              "name": { "type": "string", "title": "Name" },
              "age":  { "type": "integer" }
            }
          },
          "data": {}
        }
        """);

        var error = PayloadValidator.Validate(payload, MaxBytes, out var normalized);

        Assert.Null(error);
        Assert.Equal(JsonValueKind.Object, normalized.ValueKind);
    }

    [Fact]
    public void Validate_TopLevelNotObject_ReturnsError()
    {
        var payload = Parse("\"oops\"");
        Assert.Equal("invalid_payload_top_level_not_object", PayloadValidator.Validate(payload, MaxBytes, out _));
    }

    [Fact]
    public void Validate_SchemaMissing_ReturnsError()
    {
        var payload = Parse("{ \"data\": {} }");
        Assert.Equal("invalid_payload_schema_missing", PayloadValidator.Validate(payload, MaxBytes, out _));
    }

    [Fact]
    public void Validate_SchemaNotObject_ReturnsError()
    {
        var payload = Parse("{ \"schema\": \"oops\" }");
        Assert.Equal("invalid_payload_schema_missing", PayloadValidator.Validate(payload, MaxBytes, out _));
    }

    [Fact]
    public void Validate_SchemaTypeMissingOrWrong_ReturnsError()
    {
        var p1 = Parse("{ \"schema\": { \"properties\": {} } }");
        Assert.Equal("invalid_payload_schema_type_not_object", PayloadValidator.Validate(p1, MaxBytes, out _));

        var p2 = Parse("{ \"schema\": { \"type\": \"string\", \"properties\": {} } }");
        Assert.Equal("invalid_payload_schema_type_not_object", PayloadValidator.Validate(p2, MaxBytes, out _));
    }

    [Fact]
    public void Validate_PropertiesMissing_ReturnsError()
    {
        var payload = Parse("{ \"schema\": { \"type\": \"object\" } }");
        Assert.Equal("invalid_payload_schema_properties_missing", PayloadValidator.Validate(payload, MaxBytes, out _));
    }

    [Fact]
    public void Validate_RequiredNotArray_ReturnsError()
    {
        var payload = Parse("{ \"schema\": { \"type\": \"object\", \"required\": \"name\", \"properties\": { \"name\": { \"type\": \"string\" } } } }");
        Assert.Equal("invalid_payload_schema_required_not_array", PayloadValidator.Validate(payload, MaxBytes, out _));
    }

    [Fact]
    public void Validate_RequiredMissingProperty_ReturnsError()
    {
        var payload = Parse("{ \"schema\": { \"type\": \"object\", \"required\": [\"nope\"], \"properties\": { \"x\": { \"type\": \"string\" } } } }");
        var error = PayloadValidator.Validate(payload, MaxBytes, out _);
        Assert.NotNull(error);
        Assert.StartsWith("invalid_payload_schema_required_missing:", error);
    }

    [Fact]
    public void Validate_EnumWithNonArray_ReturnsError()
    {
        var payload = Parse("{ \"schema\": { \"type\": \"object\", \"properties\": { \"x\": { \"type\": \"string\", \"enum\": \"a\" } } } }");
        Assert.Equal("invalid_payload_schema_property_enum_not_array:x", PayloadValidator.Validate(payload, MaxBytes, out _));
    }

    [Fact]
    public void Validate_EnumWithObjectValue_ReturnsError()
    {
        var payload = Parse("{ \"schema\": { \"type\": \"object\", \"properties\": { \"x\": { \"type\": \"string\", \"enum\": [{}] } } } }");
        Assert.Equal("invalid_payload_schema_property_enum_value_not_primitive:x", PayloadValidator.Validate(payload, MaxBytes, out _));
    }

    [Fact]
    public void Validate_PropertyTypeInvalid_ReturnsError()
    {
        var payload = Parse("{ \"schema\": { \"type\": \"object\", \"properties\": { \"x\": { \"type\": \"weird\" } } } }");
        Assert.Equal("invalid_payload_schema_property_type:x", PayloadValidator.Validate(payload, MaxBytes, out _));
    }

    [Fact]
    public void Validate_EnumLengthMismatch_ReturnsError()
    {
        var payload = Parse("""
        {
          "schema": {
            "type": "object",
            "properties": {
              "x": {
                "type": "string",
                "enum": ["a", "b"],
                "options": { "enumNames": ["Alpha"] }
              }
            }
          }
        }
        """);
        Assert.Equal("invalid_payload_schema_property_enum_length_mismatch:x", PayloadValidator.Validate(payload, MaxBytes, out _));
    }

    [Fact]
    public void Validate_EnumAndEnumNames_AutoConvertedToOneOf()
    {
        var payload = Parse("""
        {
          "schema": {
            "type": "object",
            "properties": {
              "x": {
                "type": "string",
                "title": "Pick",
                "enum": ["a", "b"],
                "options": { "enumNames": ["Alpha", "Beta"] }
              }
            }
          },
          "data": {}
        }
        """);

        var error = PayloadValidator.Validate(payload, MaxBytes, out var normalized);
        Assert.Null(error);

        using var doc = JsonDocument.Parse(normalized.GetRawText());
        var x = doc.RootElement.GetProperty("schema").GetProperty("properties").GetProperty("x");

        Assert.False(x.TryGetProperty("enum", out _));
        Assert.False(x.TryGetProperty("options", out _));
        Assert.True(x.TryGetProperty("oneOf", out var oneOf));
        Assert.Equal(2, oneOf.GetArrayLength());

        Assert.Equal("a", oneOf[0].GetProperty("const").GetString());
        Assert.Equal("Alpha", oneOf[0].GetProperty("title").GetString());

        Assert.Equal("b", oneOf[1].GetProperty("const").GetString());
        Assert.Equal("Beta", oneOf[1].GetProperty("title").GetString());
    }

    [Fact]
    public void Validate_PlainEnum_NoAutoConvert()
    {
        var payload = Parse("""
        {
          "schema": {
            "type": "object",
            "properties": {
              "x": { "type": "string", "enum": ["a", "b"] }
            }
          }
        }
        """);

        var error = PayloadValidator.Validate(payload, MaxBytes, out var normalized);
        Assert.Null(error);
        Assert.Equal(payload.GetRawText(), normalized.GetRawText());
    }

    [Fact]
    public void Validate_SizeExceeded_ReturnsError()
    {
        // Payload { "a": 1 } is 9 bytes UTF-8. maxBytes=5 → overflow.
        var payload = Parse("{ \"a\": 1 }");
        Assert.Equal("payload_too_large", PayloadValidator.Validate(payload, 5, out _));
    }

    [Fact]
    public void Validate_DataAcceptsNull()
    {
        var payload = Parse("{ \"schema\": { \"type\": \"object\", \"properties\": {} }, \"data\": null }");
        Assert.Null(PayloadValidator.Validate(payload, MaxBytes, out _));
    }
}

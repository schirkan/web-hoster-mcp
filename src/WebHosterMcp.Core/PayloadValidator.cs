using System.Text;
using System.Text.Json;

namespace WebHosterMcp.Core;

/// <summary>
/// Validates the `payload` of a `type: "a2ui"` or `type: "json-schema-form"` deploy.
/// Spec 21: catches malformed / corrupt / RJSF-incompatible schemas at deploy-time
/// before they hit disk, so a broken schema never reaches the browser.
///
/// Auto-corrections:
/// - Detects the legacy RJSF-v4 `options.enumNames` pattern and rewrites it to the
///   v5-conformant `oneOf: [{const, title}, ...]`. Without this, RJSF v5 silently
///   renders an empty form (silent fail, no error in the browser console).
///
/// No external dependencies (no NJsonSchema). Validates a minimal subset of the
/// JSON Schema 2020-12 meta-schema sufficient for our two hosting types. Does NOT
/// validate arbitrary JSON Schema documents.
/// </summary>
public static class PayloadValidator
{
    private static readonly HashSet<string> AllowedPropertyTypes = new(StringComparer.Ordinal)
    {
        "string", "number", "integer", "boolean", "array", "object", "null"
    };

    /// <summary>
    /// Validates the payload. Returns <c>null</c> on success, or an error code
    /// (see Spec 21 §Error-Codes) on failure. On success, <paramref name="normalized"/>
    /// holds the payload with auto-corrections applied (typically a `oneOf` rewrite);
    /// on failure, <paramref name="normalized"/> is the original payload (unchanged).
    /// </summary>
    public static string? Validate(JsonElement payload, int maxBytes, out JsonElement normalized)
    {
        var rawText = payload.GetRawText();
        var byteCount = Encoding.UTF8.GetByteCount(rawText);
        if (byteCount > maxBytes)
        {
            normalized = payload;
            return "payload_too_large";
        }

        // Re-parse to a fresh JsonDocument so we always work with a clean tree,
        // independent of any shape-corruption the original JsonElement may carry.
        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(rawText);
        }
        catch (JsonException ex)
        {
            normalized = payload;
            return $"invalid_payload_json_parse:{ex.Message}";
        }

        using (doc)
        {
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
            {
                normalized = payload;
                return "invalid_payload_top_level_not_object";
            }

            // schema (required, object)
            if (!doc.RootElement.TryGetProperty("schema", out var schemaElement) ||
                schemaElement.ValueKind != JsonValueKind.Object)
            {
                normalized = payload;
                return "invalid_payload_schema_missing";
            }

            // data (optional, object or null)
            if (doc.RootElement.TryGetProperty("data", out var dataElement) &&
                dataElement.ValueKind != JsonValueKind.Object &&
                dataElement.ValueKind != JsonValueKind.Null)
            {
                normalized = payload;
                return "invalid_payload_data_not_object";
            }

            // schema.type must be "object"
            if (!schemaElement.TryGetProperty("type", out var typeElement) ||
                typeElement.ValueKind != JsonValueKind.String ||
                typeElement.GetString() != "object")
            {
                normalized = payload;
                return "invalid_payload_schema_type_not_object";
            }

            // required (optional, array of strings)
            var requiredFields = new HashSet<string>(StringComparer.Ordinal);
            if (schemaElement.TryGetProperty("required", out var requiredElement))
            {
                if (requiredElement.ValueKind != JsonValueKind.Array)
                {
                    normalized = payload;
                    return "invalid_payload_schema_required_not_array";
                }
                foreach (var req in requiredElement.EnumerateArray())
                {
                    if (req.ValueKind != JsonValueKind.String)
                    {
                        normalized = payload;
                        return "invalid_payload_schema_required_not_string";
                    }
                    requiredFields.Add(req.GetString()!);
                }
            }

            // properties (required, object)
            if (!schemaElement.TryGetProperty("properties", out var propertiesElement) ||
                propertiesElement.ValueKind != JsonValueKind.Object)
            {
                normalized = payload;
                return "invalid_payload_schema_properties_missing";
            }

            // Per-property validation + collect auto-converts (options.enumNames -> oneOf)
            // We store enum VALUES as raw text strings (not JsonElements) to avoid lifetime
            // issues with JsonElement.Clone() and to ensure type preservation across the
            // re-serialization. WriteRawValue emits the text verbatim, preserving the original
            // JSON type (string/number/bool/null).
            var autoConverts = new Dictionary<string, (string[] Names, string[] Values)>(StringComparer.Ordinal);
            foreach (var prop in propertiesElement.EnumerateObject())
            {
                var error = ValidateProperty(prop.Name, prop.Value, autoConverts);
                if (error != null)
                {
                    normalized = payload;
                    return error;
                }
            }

            // required ⊆ properties
            foreach (var req in requiredFields)
            {
                if (!propertiesElement.TryGetProperty(req, out _))
                {
                    normalized = payload;
                    return $"invalid_payload_schema_required_missing:{req}";
                }
            }

            normalized = autoConverts.Count == 0
                ? payload
                : BuildNormalizedPayload(doc.RootElement, autoConverts);
            return null;
        }
    }

    private static string? ValidateProperty(
        string name,
        JsonElement prop,
        Dictionary<string, (string[] Names, string[] Values)> autoConverts)
    {
        // type (optional, must be one of allowed)
        if (prop.TryGetProperty("type", out var typeEl))
        {
            if (typeEl.ValueKind != JsonValueKind.String ||
                !AllowedPropertyTypes.Contains(typeEl.GetString()!))
            {
                return $"invalid_payload_schema_property_type:{name}";
            }
        }

        // enum: array of primitives (capture as raw JSON text per element)
        string[]? enumValueTexts = null;
        if (prop.TryGetProperty("enum", out var enumEl))
        {
            if (enumEl.ValueKind != JsonValueKind.Array)
            {
                return $"invalid_payload_schema_property_enum_not_array:{name}";
            }
            var texts = new List<string>();
            foreach (var e in enumEl.EnumerateArray())
            {
                if (e.ValueKind is JsonValueKind.Object or JsonValueKind.Array)
                {
                    return $"invalid_payload_schema_property_enum_value_not_primitive:{name}";
                }
                // GetRawText returns the literal JSON representation of the token:
                // "a" → "\"a\"", 42 → "42", true → "true", null → "null".
                // Valid JSON value text - safe to emit verbatim via WriteRawValue.
                texts.Add(e.GetRawText());
            }
            enumValueTexts = texts.ToArray();
        }

        // options.enumNames (legacy RJSF v4): array of strings, same length as enum
        string[]? enumNames = null;
        if (prop.TryGetProperty("options", out var optionsEl) &&
            optionsEl.ValueKind == JsonValueKind.Object &&
            optionsEl.TryGetProperty("enumNames", out var enumNamesEl) &&
            enumNamesEl.ValueKind == JsonValueKind.Array)
        {
            var names = new List<string>();
            foreach (var n in enumNamesEl.EnumerateArray())
            {
                if (n.ValueKind != JsonValueKind.String)
                {
                    return $"invalid_payload_schema_property_enum_names_not_strings:{name}";
                }
                names.Add(n.GetString()!);
            }
            enumNames = names.ToArray();
        }

        // If both present → mark for auto-convert (legacy v4 pattern → v5 oneOf)
        if (enumValueTexts != null && enumNames != null)
        {
            if (enumValueTexts.Length != enumNames.Length)
            {
                return $"invalid_payload_schema_property_enum_length_mismatch:{name}";
            }
            autoConverts[name] = (enumNames, enumValueTexts);
        }

        return null;
    }

    private static JsonElement BuildNormalizedPayload(
        JsonElement original,
        Dictionary<string, (string[] Names, string[] Values)> autoConverts)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = false }))
        {
            writer.WriteStartObject();
            foreach (var prop in original.EnumerateObject())
            {
                if (prop.Name == "schema")
                {
                    WriteNormalizedSchema(writer, prop.Value, autoConverts);
                }
                else
                {
                    // Non-schema root properties ("data", etc.) — JsonProperty.WriteTo is fine here
                    // because we're at the root object level and need both name + value.
                    prop.WriteTo(writer);
                }
            }
            writer.WriteEndObject();
        }
        stream.Position = 0;
        return JsonDocument.Parse(stream.ToArray()).RootElement.Clone();
    }

    private static void WriteNormalizedSchema(
        Utf8JsonWriter writer,
        JsonElement schema,
        Dictionary<string, (string[] Names, string[] Values)> autoConverts)
    {
        writer.WriteStartObject("schema");
        foreach (var prop in schema.EnumerateObject())
        {
            if (prop.Name == "properties")
            {
                writer.WriteStartObject("properties");
                foreach (var p in prop.Value.EnumerateObject())
                {
                    if (autoConverts.TryGetValue(p.Name, out var convert))
                    {
                        WriteOneOfProperty(writer, p, convert);
                    }
                    else
                    {
                        writer.WritePropertyName(p.Name);
                        WriteJsonValue(writer, p.Value);
                    }
                }
                writer.WriteEndObject();
            }
            else
            {
                writer.WritePropertyName(prop.Name);
                WriteJsonValue(writer, prop.Value);
            }
        }
        writer.WriteEndObject();
    }

    private static void WriteOneOfProperty(
        Utf8JsonWriter writer,
        JsonProperty original,
        (string[] Names, string[] Values) convert)
    {
        writer.WriteStartObject(original.Name);
        foreach (var prop in original.Value.EnumerateObject())
        {
            // Drop the legacy keys; oneOf replaces them.
            if (prop.Name is "enum" or "options") continue;
            writer.WritePropertyName(prop.Name);
            WriteJsonValue(writer, prop.Value);
        }
        writer.WriteStartArray("oneOf");
        for (var i = 0; i < convert.Names.Length; i++)
        {
            writer.WriteStartObject();
            writer.WritePropertyName("const");
            // convert.Values[i] is the raw JSON text (e.g. "\"a\"" for string "a", "42" for number).
            // WriteRawValue emits the text verbatim as a JSON value.
            writer.WriteRawValue(convert.Values[i]);
            writer.WriteString("title", convert.Names[i]);
            writer.WriteEndObject();
        }
        writer.WriteEndArray();
        writer.WriteEndObject();
    }

    /// <summary>
    /// Writes a JSON value to the writer based on its ValueKind.
    /// Avoids JsonElement.WriteTo because of a .NET 8 quirk where
    /// JsonDocument.WriteElementTo can throw "Cannot write a JSON value within
    /// an object without a property name" on certain nested-element writes
    /// (observed in PayloadValidatorTests.Validate_EnumAndEnumNames_AutoConvertedToOneOf).
    /// </summary>
    private static void WriteJsonValue(Utf8JsonWriter writer, JsonElement value)
    {
        switch (value.ValueKind)
        {
            case JsonValueKind.String:
                writer.WriteStringValue(value.GetString());
                break;
            case JsonValueKind.Number:
                writer.WriteRawValue(value.GetRawText());
                break;
            case JsonValueKind.True:
                writer.WriteBooleanValue(true);
                break;
            case JsonValueKind.False:
                writer.WriteBooleanValue(false);
                break;
            case JsonValueKind.Null:
                writer.WriteNullValue();
                break;
            default:
                throw new InvalidOperationException(
                    $"Unsupported JSON value kind for normalized payload: {value.ValueKind}");
        }
    }
}

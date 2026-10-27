# 21 — Payload Validation at Deploy

**Stand:** 2026-09-27 · v0.1 (implementiert)

> **Status:** Implementiert, gelockt. Companion zu [04 Tool: `deploy`](./04-tool-deploy.md), [16 Hosting Type: `a2ui`](./16-hosting-type-a2ui.md), [17 Hosting Type: `json-schema-form`](./17-hosting-type-schema-form.md).

## Changelog

- **2026-09-27 (v0.1, gelockt):** Initiale Spec. Validierung + Auto-Korrektur (`options.enumNames` → `oneOf`) implementiert. `WritePayloadAsync` auf `GetRawText()` umgestellt (Bug-Fix für `JsonElement`-Roundtrip). `a2ui`- und `json-schema-form`-Cases im `DeployAsync`-Switch getrennt (a2ui hat keine `schema`-Property).

## Ziel

`type: "json-schema-form"`-Deploys validieren das `payload` **bevor** es auf Disk landet. Das verhindert, dass kaputte oder RJSF-inkompatible Schemata den Browser erreichen. Zusätzlich wird das legacy RJSF-v4-Pattern `options.enumNames` automatisch in die v5-konforme Form `oneOf: [{const, title}, …]` überführt, damit ältere Deploys ohne manuellen Eingriff weiter laufen.

`type: "a2ui"`-Deploys werden **nicht** validiert (Schema-less Format `{ messages: [...] }`), bekommen aber denselben Größen-Cap (1 MB) und nutzen denselben Bug-Fix.

## Problem-Auslöser (warum diese Spec nötig war)

Zwei Bugs wurden beim Deploy von `spec-20-decisions` entdeckt (Spec 20):

1. **`JsonElement`-Roundtrip zerstört das Payload**: `SiteManager.WritePayloadAsync` rief `JsonSerializer.SerializeToUtf8Bytes(payload, PayloadJsonOptions)` auf. Dieser Roundtrip über `.NET 8`'s `JsonElement`-Binding konvertiert Arrays als `{"item": [...]}`, Primitives (bool/number) als Strings und leere Objekte als leere Strings. → Resultat: kaputtes JSON auf Disk, Form rendert leer.
2. **`options.enumNames` ist in RJSF v5 stillgelegt**: RJSF v5 ignoriert die v4-Syntax `options.enumNames` stillschweigend → leeres Form-Rendering statt Fehler.

## Implementierung

### 1. `GetRawText()` in `WritePayloadAsync` (Bug-Fix)

`src/WebHosterMcp.Core/SiteManager.cs`:

```csharp
// Vorher: JsonSerializer.SerializeToUtf8Bytes(payload, PayloadJsonOptions)
//   → verzerrt Arrays/Primitives/leere Objekte
// Nachher:
var rawText = payload.GetRawText();
var bytes = System.Text.Encoding.UTF8.GetBytes(rawText);
```

`GetRawText()` liefert den ursprünglichen Wire-Text Byte-für-Byte zurück. Damit ist die Persistenz unabhängig vom MCP-Transport-Roundtrip und unabhängig von der `McpToolJsonContext`-Serialisierung.

### 2. `PayloadValidator` (neue Klasse)

`src/WebHosterMcp.Core/PayloadValidator.cs` — validiert `json-schema-form`-Payloads, gibt normalisiertes Payload (mit Auto-Konvert) zurück.

#### Validation Rules (minimaler JSON-Schema-Subset)

| Property | Pflicht? | Typ | Regel |
|---|---|---|---|
| Top-Level | ✓ | object | muss JSON-Objekt sein |
| `schema` | ✓ | object | muss vorhanden und Objekt sein |
| `data` | optional | object \| null | falls vorhanden, muss Objekt oder `null` sein |
| `schema.type` | ✓ | `"object"` | muss exakt `"object"` sein (für Form-Renderings) |
| `schema.required` | optional | string[] | jedes Element muss String sein; muss ⊆ `schema.properties.keys` sein |
| `schema.properties` | ✓ | object | muss vorhanden und Objekt sein |
| `properties[k].type` | optional | `"string"` \| `"number"` \| `"integer"` \| `"boolean"` \| `"array"` \| `"object"` \| `"null"` | falls vorhanden, muss erlaubter Typ sein |
| `properties[k].enum` | optional | Array<Primitive> | falls vorhanden, muss Array sein, jedes Element Primitive (nicht Object/Array) |
| `properties[k].options.enumNames` | optional | string[] | falls vorhanden, muss Array von Strings sein, gleiche Länge wie `enum` |

#### Auto-Korrektur: `options.enumNames` → `oneOf`

Falls eine Property sowohl `enum` als auch `options.enumNames` (gleiche Länge) hat:

```jsonc
// Input (RJSF v4):
{ "type": "string", "enum": ["a", "b"], "options": { "enumNames": ["Alpha", "Beta"] } }

// Normalisiertes Output (RJSF v5):
{ "type": "string", "oneOf": [
    { "const": "a", "title": "Alpha" },
    { "const": "b", "title": "Beta" }
] }
```

Damit können alte Deploys ohne manuellen Eingriff weiter laufen.

#### Error Codes (alle in PayloadValidator.cs)

| Code | Wann |
|---|---|
| `payload_too_large` | Payload > `SitesOptions.MaxPayloadSizeBytes` (Default 1 MB) |
| `invalid_payload_json_parse:<msg>` | JSON-Parse-Fehler beim Re-Parse |
| `invalid_payload_top_level_not_object` | Root ist kein JSON-Objekt |
| `invalid_payload_schema_missing` | `schema`-Property fehlt oder kein Objekt |
| `invalid_payload_data_not_object` | `data` vorhanden, aber nicht Object/Null |
| `invalid_payload_schema_type_not_object` | `schema.type` ≠ `"object"` |
| `invalid_payload_schema_required_not_array` | `required` ist kein Array |
| `invalid_payload_schema_required_not_string` | `required[i]` ist kein String |
| `invalid_payload_schema_properties_missing` | `properties` fehlt oder kein Objekt |
| `invalid_payload_schema_required_missing:<field>` | `required[i]` nicht in `properties.keys` |
| `invalid_payload_schema_property_type:<name>` | `properties[k].type` ist kein erlaubter Typ |
| `invalid_payload_schema_property_enum_not_array:<name>` | `enum` ist kein Array |
| `invalid_payload_schema_property_enum_value_not_primitive:<name>` | `enum[i]` ist Object/Array |
| `invalid_payload_schema_property_enum_names_not_strings:<name>` | `options.enumNames[i]` ist kein String |
| `invalid_payload_schema_property_enum_length_mismatch:<name>` | `enum` und `enumNames` haben unterschiedliche Längen |

### 3. `DeployAsync`-Cases getrennt (a2ui vs json-schema-form)

`src/WebHosterMcp.Core/SiteManager.cs` — der `switch (request.Type)` trennt jetzt die zwei Render-Typen:

```csharp
case "a2ui":
    // Keine Schema-Validation (Format: { "messages": [...] }).
    // Nur Größen-Cap via WritePayloadAsync.
    var a2uiPayloadError = await WritePayloadAsync(sitePath, request.Payload.Value, ct);
    if (a2uiPayloadError != null) return ErrorResult(a2uiPayloadError, sitePath);
    break;

case "json-schema-form":
    // Spec 21: validieren + auto-korrigieren vor Persist.
    var validationError = PayloadValidator.Validate(
        request.Payload.Value,
        _sitesOptions.MaxPayloadSizeBytes,
        out var normalizedPayload);
    if (validationError != null) return ErrorResult(validationError, sitePath);

    var payloadError = await WritePayloadAsync(sitePath, normalizedPayload, ct);
    if (payloadError != null) return ErrorResult(payloadError, sitePath);
    break;
```

Damit bleibt `a2ui` (das bisherige `form-test-001`-Pattern) unverändert — bestehende Deploys brechen nicht.

## Tests

| Datei | Test-Klasse | Anzahl |
|---|---|---|
| `tests/PayloadValidatorTests.cs` | Direkte Validator-Unit-Tests (Schema-Syntax, Auto-Korrektur, Edge-Cases) | 16 |
| `tests/PayloadRoundtripTests.cs` | End-to-End-Roundtrip via `SiteManager.DeployAsync` (Arrays, Booleans, Numbers, leere Objekte, Auto-Konvert, Malformed-Reject) | 6 |
| `tests/HostingTypenTests.cs` | Bestehende json-schema-form-Tests aktualisiert (verwenden jetzt valides `{"schema":{"type":"object","properties":{}}}` als Placeholder statt `{"schema":{}}`) | 5 angepasst |

Stand `v0.1`: **125/125 Tests grün**.

## Migration für bestehende Hosts

- Bestehende `payload.json`-Dateien auf Disk werden **nicht** migriert — sie wurden bereits beim letzten Deploy (vor v0.1) als kaputtes JSON geschrieben und sind entsprechend unbrauchbar. Re-Deploy mit v0.1+ korrigiert die Datei automatisch.
- Bestehende Deploys (z. B. `form-test-001`) verwenden plain `enum` ohne `enumNames` → kein Validator-Eingriff nötig.
- Deploys mit altem `options.enumNames`-Pattern werden beim nächsten Deploy automatisch zu `oneOf` migriert.

## Cross-References

- [04 Tool: `deploy`](./04-tool-deploy.md) — Deploy-Pfad, der die Validierung aufruft
- [16 Hosting Type: `a2ui`](./16-hosting-type-a2ui.md) — `a2ui`-Format, kein Schema
- [17 Hosting Type: `json-schema-form`](./17-hosting-type-schema-form.md) — `json-schema-form`-Format
- [20 Submission Notifications](./20-submission-notifications.md) — separater Feature-Pfad, nicht betroffen
- [02 Sites Storage](./02-sites-storage.md) — `payload.json`-File-Layout

## Out of Scope

- Validierung gegen vollständige JSON-Schema-2020-12-Spec (NJsonSchema o. ä.) — bewusst minimaler Subset
- Externe Schema-Repositories (`$ref`-Resolution)
- Conditional Schemas (`if/then/else`)
- Custom-Format-Keywords (`format: email`, `format: date-time` etc.)
- Default-Werte beim Deploy setzen (nur Client-Side in RJSF)

# 04 — Tool: `deploy`

**Stand:** 2026-09-26 · v3.0 (lock)

## Changelog

- **2026-09-26 (Übergang in Feature-Modell):** Aus MVP1 §Tools/1 + MVP3 §API + MVP4 §`deploy` pro Type zusammengeführt. Input/Output-Specs aller 4 Hosting-Typen, Validation, Path-Validation, `src`-Parameter.
- **v3.0 (2026-09-25, MVP4 v3.0-Pass):** `type`-Diskriminierung in `SiteManager.DeployAsync`. File-Source-Spec (`src`) zusätzlich zu `content` (MVP3).
- **v1.3 (2026-09-23, MVP1 v1.3):** Path-Validation `..` / ≤ 260 Zeichen, `mode:"replace"` + `files:[]` löscht alle Files ohne Error, Time-stamps lokal, `retention_seconds` Optional.
- (vorherige Versionen) Siehe Git-History.

## Ziel

Erstellt oder aktualisiert eine Site im Web Hoster. Pro Site genau ein Hosting-Typ (siehe [14–17 Hosting Types](./14-hosting-type-files.md)). Eingabe per Inline-`content` (UTF-8) und/oder alternativer Quelle `src` (siehe [13 per-File `src`](./13-per-file-src.md)).

## Input

```jsonc
{
  "site_path": "demo-001",          // optional, sonst 8-char random
  "type": "files",                  // optional, default "files"
  "mode": "merge",                  // optional, default "merge"
  "retention_seconds": 3600,        // optional
  "path": "C:/Users/Martin/notes",  // nur bei type:"folder"
  "payload": { /* schema | a2ui messages */ },  // nur bei type:"a2ui" oder "json-schema-form"
  "files": [
    { "path": "index.html", "content": "<!DOCTYPE html>..." },
    { "path": "css/style.css", "content": "body { margin: 0 }" },
    { "path": "logo.png", "src": "data:image/png;base64,iVBOR..." },
    { "path": "banner.png", "src": "C:/local-assets/banner.png" },
    { "path": "icon.svg",  "src": "https://example.com/icon.svg" },
    { "path": "old.html",  "delete": true }
  ]
}
```

### Field-Validierung

| Field | Check | Bei Verletzung |
|---|---|---|
| `site_path` | `^[a-z0-9-]{3,32}$` wenn gesetzt | `invalid_site_id` |
| `type` | in `{"files","folder","a2ui","json-schema-form"}` | `invalid_type` |
| `mode` (nur `files`) | `merge` \| `replace` | `invalid_mode` |
| `retention_seconds` | `>= 0` (0 = nie ablaufen) | – |
| `path` (nur `folder`) | absolut, kein `..`, ≤ 260 Zeichen, UNC erlaubt | `path_traversal` / `path_too_long` |
| `files[].path` | kein `..`, ≤ 260 Zeichen, nicht doppelt im Call | `path_traversal` / `path_too_long` / `duplicate_path` |
| `files[].content` | ≠ `null` wenn inline-source; ≤ 1 MB auf Platte | `file_too_large` |
| `files[].src` | ≠ `null` wenn alt. source; siehe [13](./13-per-file-src.md) | `src_*`-Errors |
| `files[].delete` + `content`/`src` | nie kombinieren | `invalid_file_entry` |
| `content` + `src` im selben Eintrag | nie | `invalid_file_entry` |

### Per-Type-Spec

#### `type: "files"`

- `path` (Top-Level) nicht erlaubt → `path_not_allowed_for_files`
- `payload` nicht erlaubt → `payload_not_allowed_for_files`
- `mode: "merge"` (default): jedes File:
  - `delete: true` → weg
  - sonst add oder replace (Files außerhalb des Calls bleiben)
- `mode: "replace"`: alle alten Files weg, exakt Files aus dem Call; `delete: true` im Call wird ignoriert
- `mode: "replace"` + `files: []` → delete all (kein Error)
- `mode: "merge"` + `files: []` → no-op (kein Error)
- Subfolder-Pfade erlaubt: `css/style.css`

#### `type: "folder"`

- `files` nicht erlaubt → `files_not_allowed_for_folder`
- `payload` nicht erlaubt → `payload_not_allowed_for_folder`
- `path` ist Pflicht → `path_required`
- Bei Update: `path` und `updated_at` neu gesetzt, sonst nichts geschrieben

#### `type: "a2ui"`

- `files` nicht erlaubt → `files_not_allowed_for_a2ui`
- `path` nicht erlaubt → `path_not_allowed_for_render`
- `payload` Pflicht → `payload_required`
- `payload` ≤ 1 MB (JSON-size auf Platte) → `payload_too_large`
- Payload-Shape: `{ "messages": [...] }` (siehe [16 A2UI](./16-hosting-type-a2ui.md))

#### `type: "json-schema-form"`

- `files` nicht erlaubt → `files_not_allowed_for_schema_form`
- `path` nicht erlaubt → `path_not_allowed_for_render`
- `payload` Pflicht → `payload_required`
- `payload` ≤ 1 MB → `payload_too_large`
- Payload-Shape: `{ "schema": {...}, "data": {...} }` (siehe [17 Schema-Form](./17-hosting-type-schema-form.md))

## Output (minimal)

```json
{
  "site_path": "demo-001",
  "url": "http://192.168.x.x:3000/demo-001/",
  "files": [
    { "path": "index.html",       "result_path": "http://192.168.x.x:3000/demo-001/index.html" },
    { "path": "css/style.css",    "result_path": "http://192.168.x.x:3000/demo-001/css/style.css" }
  ]
}
```

Gelöschte Files erscheinen **nicht** in `files[]`. Bei `folder`/`a2ui`/`json-schema-form` ist `files[]` weggelassen.

## Type-Immutability

`type` wird beim ersten `deploy` festgelegt. Wechsel nur via `delete_site` + redeploy. Verletzung → `type_immutable`.

## Atomic-Writes

- `registry.json`: tmp + `File.Move` overwrite (siehe [02 Sites Storage](./02-sites-storage.md))
- Files: tmp + `File.Move` overwrite (analog), kein partial Site-State bei Fail (write-error in der Validation-Pass führt zu sofortigem Error ohne File-Schreibvorgang; write-error in der Write-Pass führt zu Error mit Site-Update, das die alte Registry hält)

## `site_path`-Generierung

Falls `site_path` nicht gesetzt:

```
8-char random aus [a-z0-9] (RNGCryptoServiceProvider / RandomNumberGenerator.Fill)
```

Beispiel: `a3b8k1d4`. Kollisions-Wahrscheinlichkeit bei 8 Zeichen aus 36: ~1 zu 2.8 Billionen pro Site — vernachlässigbar in LAN-Use-Cases.

## Retention-Handling

| `retention_seconds` Wert | Effekt auf Registry |
|---|---|
| weggelassen, neue Site | Global Default aus `Retention:DefaultTtlSeconds` |
| weggelassen, bestehende Site | bestehender Wert bleibt |
| `0` | `retention_seconds: 0` in Registry (nie ablaufen) |
| `> 0` | `retention_seconds: N` in Registry, `updated_at` aktualisiert (TTL-Reset) |

Siehe [12 Retention](./12-retention.md).

## Cross-References

- [02 Sites Storage](./02-sites-storage.md) — Registry + Folder-Layout
- [03 Static File Route](./03-static-file-route.md) — Serving-Pfad
- [13 per-File `src`](./13-per-file-src.md) — File-Sources
- [14 Hosting Type: `files`](./14-hosting-type-files.md) — Type-Diskriminierung
- [15 Hosting Type: `folder`](./15-hosting-type-folder.md) — `path`-Pflicht
- [16 Hosting Type: `a2ui`](./16-hosting-type-a2ui.md) — `payload`-Pflicht
- [17 Hosting Type: `json-schema-form`](./17-hosting-type-schema-form.md) — `payload`-Pflicht + Submit
- [12 Retention](./12-retention.md) — `retention_seconds`-Semantik
- [01 HTTP Listener](./01-http-listener.md) — `BuildSiteUrl` URL-Generierung

## Out of Scope

- File-Source `git:` oder beliebige andere URL-Schemas (nur `data:`, `http://`, `https://`, lokaler Pfad; siehe [13](./13-per-file-src.md))
- Conditional-Deploy / Patch-API
- Binary-Lock / Exclusive-Check über mehrere Server-Instanzen
- Webhook-Callbacks nach erfolgreichem Deploy

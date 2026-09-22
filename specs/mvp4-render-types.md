# MVP4 — Render Types

Stand: 2026-09-22 · v2.0 (lock)

## Ziel

Sites können einen Render-Type haben: `files` (default), `folder`, `a2ui`
oder `json-schema-form`. Pro Site genau ein Render-Type (kein Mixing).
Server liefert je nach Type die passende HTTP-Antwort.

## Render Types

| Type | Storage im Server | HTTP | Listing | File-Delete |
|------|-------------------|------|---------|-------------|
| `files` | `<SitesRoot>/<site>/<files>` mit Subfolders | `GET /<site>/<file>` | ✅ rekursiv | ✅ |
| `folder` | nur Registry (`path` zeigt auf Host-Folder) | `GET /<site>/<file>` aus Host-Folder | ✅ vom Host-Folder | ❌ |
| `a2ui` | `<SitesRoot>/<site>/payload.json` | `GET /<site>/` → A2UI-Renderer | ❌ | ❌ |
| `json-schema-form` | `<SitesRoot>/<site>/payload.json` + `<submission-id>.json` | `GET /<site>/` → RJSF · `POST /<site>/submit` | ❌ | ❌ |

**Default:** `files`.
**Type-Immutability:** `type` wird bei erstem `deploy` festgelegt.
Wechsel nur via `delete_site` + redeploy. Fehler `type_immutable` falls
geändert.

## Storage-Layout

```
<SitesRoot>/                      # Default ./sites (konfigurierbar)
├── registry.json                 # Site-Registry (Single Source of Truth)
└── <site_path>/                  # Site-Folder (nur bei files/a2ui/schema-form)
    ├── <files mit Subfolders>    # Bei type: "files"
    ├── payload.json              # Bei a2ui / schema-form
    └── <submission-id>.json      # Bei schema-form
```

`folder`-Sites haben **keinen** `<site_path>/`-Folder. Die Files liegen
extern im Host-Folder, der Server liest nur (kein Copy).

`registry.json` (Schema v1):

```json
{
  "version": 1,
  "sites": {
    "demo-001": {
      "site_path": "demo-001",
      "type": "files",
      "created_at": "2026-09-22T19:25:00Z",
      "updated_at": "2026-09-22T19:30:00Z",
      "retention_seconds": 0
    }
  }
}
```

`retention_seconds` optional, default `0` = kein Auto-Expire. Siehe
`specs/mvp2.md` (Retention) sobald geschrieben.

## Configuration (`appsettings.json`)

```json
{
  "Host": { "Ip": "0.0.0.0", "Port": 3000 },
  "SitesRoot": "./sites",
  "MaxFileSizeBytes": 1048576
}
```

## `deploy`-Input pro Type

### `type: "files"` (default)

```json
{
  "site_path": "demo-001",
  "type": "files",
  "mode": "merge",
  "files": [
    {"path": "index.html", "content": "<!DOCTYPE html>..."},
    {"path": "css/style.css", "content": "body { margin: 0 }"},
    {"path": "old.html", "delete": true}
  ]
}
```

- `path` kann Subfolder enthalten: `css/style.css`
- **Keine Path-Validation** — auch nicht für `..` oder absolute Pfade
- `mode: "merge"` (default): jedes File `delete: true` → weg; sonst add oder replace; Files nicht im Call bleiben (auch in Subfolders)
- `mode: "replace"`: alle alten Files weg, exakt die Files aus dem Call; `delete: true` ignoriert

### `type: "folder"`

```json
{
  "site_path": "docs-001",
  "type": "folder",
  "path": "C:/Users/Martin/notes"
}
```

- `path` ist absoluter Pfad zum Host-Folder
- **Keine Path-Validation** (kein Existenz-Check, kein Directory-Check)
- `files[]` und `payload` nicht erlaubt
- Bei Update: nur `path` ändert sich (oder neuer Pfad)

### `type: "a2ui"`

```json
{
  "site_path": "ui-001",
  "type": "a2ui",
  "payload": {
    "messages": [/* A2UI v0.9.1 Message-Array */]
  }
}
```

- `payload` ersetzt `payload.json` **komplett** bei jedem `deploy`
- Keine `merge`/`replace`-Unterscheidung
- `files[]` nicht erlaubt

### `type: "json-schema-form"`

```json
{
  "site_path": "form-001",
  "type": "json-schema-form",
  "payload": {
    "schema": {/* JSON Schema */},
    "data": {/* optional initial values */}
  }
}
```

- `payload` ersetzt `payload.json` **komplett**
- Submissions (`<submission-id>.json`) separat, bleiben erhalten

## `deploy`-Validierung

| Check | Error-Code |
|-------|------------|
| `site_path` verletzt `^[a-z0-9-]{3,32}$` | `invalid_site_id` |
| `type` ungültig | `invalid_type` |
| Bestehende Site + `type` weicht ab | `type_immutable` |
| `type: "files"` + `path` doppelt im Call | `duplicate_path` |
| `type: "files"` + `delete: true` + `content` | `invalid_file_entry` |
| `type: "files"` + `!delete` + kein `content` | `missing_content` |
| `type: "files"` + dekodierte File > 1 MB | `file_too_large` |
| `type: "folder"` ohne `path` | `path_required` |
| `type: "a2ui"`/`schema-form` ohne `payload` | `payload_required` |
| `type: "files"` + `payload` | `payload_not_allowed_for_files` |
| `type != "files"` + `files[]` | `files_not_allowed_for_<type>` |
| `mode: "replace"` + `files: []` | `empty_files_not_allowed` |

## HTTP-Serving pro Type

### `type: "files"`

- `GET /<site>/<file>` → Datei serven (mit Subfolder-Pfaden: `css/style.css`)
- `GET /<site>/` → Directory-Listing rekursiv (siehe `mvp2-directory-listing.md`)
- `GET /<site>/delete?confirm=yes` → Site-Folder + Registry-Eintrag löschen
- `GET /<site>/delete-file/<path>?confirm=yes` → File löschen (auch in Subfolders)

### `type: "folder"`

- `GET /<site>/<file>` → Datei aus `<host-path>/<file>` serven (mit Subfolders)
- `GET /<site>/` → Directory-Listing des Host-Folders
- `GET /<site>/delete?confirm=yes` → **Nur Registry-Eintrag** löschen (Host-Folder bleibt!)
- `GET /<site>/delete-file/<path>?confirm=yes` → **404** (nicht implementiert für folder)

### `type: "a2ui"`

- `GET /<site>/` → A2UI-Render (React + A2UI-Renderer, siehe unten)
- `GET /<site>/<file>` → **404** (a2ui hat keine servable Files)
- `GET /<site>/delete?confirm=yes` → Site löschen (`payload.json` + Registry)

### `type: "json-schema-form"`

- `GET /<site>/` → RJSF-Render (React + RJSF, siehe unten)
- `GET /<site>/<file>` → **404**
- `POST /<site>/submit` → Submission speichern (siehe unten)
- `GET /<site>/delete?confirm=yes` → Site löschen (`payload.json` + Submissions + Registry)

## React-basierte Render-Pipeline (für `a2ui` und `json-schema-form`)

Eine HTML-Template, mit Mount-Script pro Type:

```html
<!DOCTYPE html>
<html>
<head>
  <meta charset="utf-8">
  <title>{site_path}</title>
  <script crossorigin src="https://unpkg.com/react@18/umd/react.production.min.js"></script>
  <script crossorigin src="https://unpkg.com/react-dom@18/umd/react-dom.production.min.js"></script>
  <!-- Renderer je nach type -->
  <script crossorigin src="...rjsf..."></script>           <!-- schema-form -->
  <script crossorigin src="...a2ui-react..."></script>    <!-- a2ui -->
</head>
<body>
  <div id="root"></div>
  <script>
    const SITE = /* server-side injected JSON */;
    /* mount je nach SITE.type */
  </script>
</body>
</html>
```

### A2UI Mount

- Mountet den A2UI-React-Renderer mit `SITE.payload.messages`
- Spec: A2UI **v0.9.1** (current) — `https://a2ui.org/specification/v0.9.1-a2ui/`
- Renderer: offizieller A2UI-React-Renderer (sofern verfügbar), sonst minimaler Wrapper

### JSON-Schema-Form Mount

- Mountet `@rjsf/core` mit `SITE.payload.schema` (+ `data`)
- Submit-Button POSTet JSON an `/<site>/submit`
- Renderer: [`react-jsonschema-form`](https://github.com/rjsf-team/react-jsonschema-form) (RJSF) via CDN

## Submit-Endpoint

`POST /<site_path>/submit` — nur bei `type: "json-schema-form"`.

**Body:** beliebiges JSON

**Verhalten:**

- Submission-ID: `yyyy-MM-ddTHH-mm-ss_<random8>.json`
  (z. B. `2026-09-22T21-30-00_a8f2k1d3.json`)
- Speichern in `<SitesRoot>/<site_path>/<submission-id>.json`
- Body = File-Content

**Output:**

```json
{
  "submission_id": "2026-09-22T21-30-00_a8f2k1d3",
  "received_at": "2026-09-22T21:30:00Z"
}
```

**Fehler:**

- `submit_not_allowed` — `type != "json-schema-form"`
- `site_not_found` — Site existiert nicht
- `invalid_json` — Body ist kein gültiges JSON

## Neue Tools

### `get_submissions(site_path, since?, limit?)`

**Input:**

```json
{
  "site_path": "form-001",
  "since": "2026-09-22T18:00:00Z",
  "limit": 50
}
```

- `since` optional — ISO-8601; nur Submissions danach
- `limit` optional, default 50, max 500

**Output** (Array direkt, neueste zuerst):

```json
[
  {
    "submission_id": "2026-09-22T21-30-00_a8f2k1d3",
    "received_at": "2026-09-22T21:30:00Z",
    "data": {/* eingegangene Form-Daten */}
  }
]
```

## Error Codes

| Code | Wann |
|------|------|
| `invalid_site_id` | `site_path` verletzt `^[a-z0-9-]{3,32}$` |
| `invalid_type` | `type` nicht in erlaubter Liste |
| `type_immutable` | Site existiert + `type` weicht ab |
| `site_not_found` | `site_path` existiert nicht |
| `duplicate_path` | gleicher `path` mehrfach in einem `deploy`-Call |
| `invalid_file_entry` | `delete: true` + `content` gleichzeitig |
| `missing_content` | File ohne `content` und ohne `delete: true` |
| `file_too_large` | dekodierte File > 1 MB |
| `invalid_data_url` | Data-URL kaputt / base64 ungültig |
| `empty_files_not_allowed` | `replace`-Modus mit `files: []` |
| `path_required` | `type: "folder"` ohne `path` |
| `payload_required` | `type: "a2ui"`/`schema-form` ohne `payload` |
| `payload_not_allowed_for_files` | `type: "files"` mit `payload` |
| `files_not_allowed_for_folder` | `type: "folder"` mit `files[]` |
| `files_not_allowed_for_a2ui` | `type: "a2ui"` mit `files[]` |
| `files_not_allowed_for_schema_form` | `type: "schema-form"` mit `files[]` |
| `submit_not_allowed` | `POST /<site>/submit` bei falschem `type` |
| `invalid_json` | Submit-Body ist kein gültiges JSON |
| `internal_error` | Unerwarteter Server-Fehler |

## Out of Scope (MVP4)

- **Path-Validation** (`path_traversal`, Whitelist, Existenz-Checks) — Trust-Modell
- HTTPS (→ MVP2)
- Auto-Delete / Retention (→ MVP2)
- `src`-Parameter pro File (→ MVP3, separate Idee)
- WebSocket-Streaming für A2UI progressive rendering
- Submission-TTL / Auto-Cleanup (kommt mit MVP2-Retention)
- Submit-Webhooks
- Custom Render-Themes
- Auth auf Submit-Endpoint (LAN-only MVP4)
- A2UI-Action-Callbacks (User-Interaktion zurück zum Agent) — für MVP4 nur Render

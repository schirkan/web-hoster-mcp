# MVP4 — Hosting Typen

Stand: 2026-09-23 · v2.2 (lock)

## Changelog

- **v2.2 (2026-09-23):** Path-Validation analog MVP1 (`..` nicht erlaubt, max 260 Zeichen). UNC-Pfade für `folder` erlaubt. `file_count` analog für `files` und `folder` über Filesystem-Operation. 1 MB Limit für `payload.json` (a2ui, schema-form) und Submission-Body (neue Error-Codes `payload_too_large`, `submission_too_large`). NPM-Link für A2UI-React-Renderer. `folder`-Retention-Expiry: Registry-Eintrag weg, Host-Folder bleibt; bei Re-Deploy werden `path` und `updated_at` neu gesetzt.
- **v2.1 (2026-09-23):** A2UI via offiziellen React-Renderer (`@a2ui/react` von npmjs); Lock-Semantik-Footer.
- **v2.0 (2026-09-22):** `render_type` → `type` (Umbenennung, `type_immutable`), `folder`-Type neu mit Subfolder-Support.
- (vorherige Versionen) Siehe Git-History.

## Ziel

Sites können einen Hosting-Typ haben: `files` (default), `folder`, `a2ui`
oder `json-schema-form`. Pro Site genau ein Hosting-Typ (kein Mixing).
Server liefert je nach `type` die passende HTTP-Antwort.

Hinweis: `files` und `folder` rendern grundsätzlich sehr ähnlich (beide liefern
statische Dateien aus), unterscheiden sich aber in der Quelle:
- `files`: Dateien liegen unter `<SitesRoot>/<site>/...`
- `folder`: Dateien kommen live aus einem Host-Folder (`path`)

Für MVP3-File-Quellen (`src`: Data URL, lokaler Pfad, HTTP-URL) siehe
`specs/mvp3.md`.

## Hosting Typen

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
      "created_at": "2026-09-22T19:25:00",
      "updated_at": "2026-09-22T19:30:00",
      "retention_seconds": 0
    }
  }
}
```

`retention_seconds` optional, default `0` = kein Auto-Expire (siehe MVP2 §3).

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
    {"path": "old.html", "delete": true},
    {"path": "logo.png", "src": "data:image/png;base64,iVBOR..."}
  ]
}
```

- `path` kann Subfolder enthalten: `css/style.css`
- **Path-Validation:** kein `..`, max 260 Zeichen (analog MVP1 §Tools/1)
- `mode: "merge"` (default): jedes File `delete: true` → weg; sonst add oder replace; Files nicht im Call bleiben (auch in Subfolders)
- `mode: "replace"`: alle alten Files weg, exakt die Files aus dem Call; `delete: true` ignoriert
- `src` (MVP3) als Alternative zu `content` — siehe `specs/mvp3.md`

### `type: "folder"`

```json
{
  "site_path": "docs-001",
  "type": "folder",
  "path": "C:/Users/Martin/notes"
}
```

- `path` ist absoluter Pfad zum Host-Folder
- **Path-Validation:**
  - `..` (Parent-Directory-Traversal) NICHT erlaubt → `path_traversal`
  - Pfad-Länge max **260 Zeichen** (Windows `MAX_PATH`) → `path_too_long`
  - UNC-Pfade (`\\server\share\...`) erlaubt
- Keine Existenz-Checks, kein Permission-Check (Trust-Modell)
- `files[]` und `payload` nicht erlaubt
- Bei Update: nur `path` ändert sich; `updated_at` wird auf lokale Server-Zeit gesetzt (TTL-Reset)

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

- **Größenlimit:** `payload` max **1 MB** (JSON-size auf Platte) → `payload_too_large`
- `payload` ersetzt `payload.json` komplett bei jedem `deploy`
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

- **Größenlimit:** `payload` max **1 MB** (JSON-size auf Platte) → `payload_too_large`
- `payload` ersetzt `payload.json` komplett
- Submissions (`<submission-id>.json`) separat, bleiben erhalten

## `deploy`-Validierung

| Check | Error-Code |
|-------|------------|
| `site_path` verletzt `^[a-z0-9-]{3,32}$` | `invalid_site_id` |
| `type` ungültig | `invalid_type` |
| Bestehende Site + `type` weicht ab | `type_immutable` |
| `type: "folder"` `path` enthält `..` | `path_traversal` |
| `type: "folder"` `path` > 260 Zeichen | `path_too_long` |
| `type: "files"` `path` enthält `..` | `path_traversal` |
| `type: "files"` `path` > 260 Zeichen | `path_too_long` |
| `type: "files"` + `path` doppelt im Call | `duplicate_path` |
| `type: "files"` + `delete: true` + (`content` ODER `src`) | `invalid_file_entry` |
| `type: "files"` + `!delete` + kein `content` UND kein `src` | `missing_content` |
| `type: "files"` `content` > 1 MB auf Platte | `file_too_large` |
| `type: "files"` + `payload` | `payload_not_allowed_for_files` |
| `type: "a2ui"`/`schema-form"` `payload` > 1 MB | `payload_too_large` |
| `type: "folder"` ohne `path` | `path_required` |
| `type: "a2ui"`/`schema-form"` ohne `payload` | `payload_required` |
| `type != "files"` + `files[]` | `files_not_allowed_for_<type>` |
| `mode: "replace"` + `files: []` | (kein Error — siehe MVP1 v1.3 Empty-Files-Verhalten) |

## HTTP-Serving pro Type

### `type: "files"`

- `GET /<site>/<file>` → Datei serven (mit Subfolder-Pfaden: `css/style.css`)
- `GET /<site>/` → Directory-Listing rekursiv (siehe `mvp2-directory-listing.md`)
- `DELETE /<site>` → Site-Folder + Registry-Eintrag löschen (MVP2 v1.2)
- `DELETE /<site>/<file>` → File löschen (auch in Subfolders, MVP2 v1.2)

### `type: "folder"`

- `GET /<site>/<file>` → Datei aus `<host-path>/<file>` serven (mit Subfolders)
- `GET /<site>/` → Directory-Listing des Host-Folders
- `DELETE /<site>` → **Nur Registry-Eintrag** löschen (Host-Folder bleibt!)
- `DELETE /<site>/<file>` → **404** (kein File-Delete bei folder)

### `type: "a2ui"`

- `GET /<site>/` → A2UI-Render (React + A2UI-Renderer, siehe unten)
- `GET /<site>/<file>` → **404** (a2ui hat keine servable Files)
- `DELETE /<site>` → Site löschen (`payload.json` + Registry)

### `type: "json-schema-form"`

- `GET /<site>/` → RJSF-Render (React + RJSF, siehe unten)
- `GET /<site>/<file>` → **404**
- `POST /<site>/submit` → Submission speichern (siehe unten; **Body-Größenlimit 1 MB** → `submission_too_large`)
- `DELETE /<site>` → Site löschen (`payload.json` + Submissions + Registry)

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
- **Renderer:** offizieller React-Renderer [`@a2ui/react`](https://www.npmjs.com/package/@a2ui/react) via CDN geladen

### JSON-Schema-Form Mount

- Mountet `@rjsf/core` mit `SITE.payload.schema` (+ `data`)
- Submit-Button POSTet JSON an `/<site>/submit`
- Renderer: [`react-jsonschema-form`](https://github.com/rjsf-team/react-jsonschema-form) (RJSF) via CDN

## Submit-Endpoint

`POST /<site_path>/submit` — nur bei `type: "json-schema-form"`.

**Body-Größenlimit: 1 MB** (analog `payload`). Größere Bodies → `submission_too_large`.

**Body:** beliebiges JSON

**Verhalten:**

- Submission-ID: `yyyy-MM-ddTHH-mm-ss_<random8>.json`
- Speichern in `<SitesRoot>/<site_path>/<submission-id>.json`
- Body = File-Content

**Output:**

```json
{
  "submission_id": "2026-09-22T21-30-00_a8f2k1d3",
  "received_at": "2026-09-22T21:30:00"
}
```

`received_at` in lokaler Server-Zeit, ISO-8601 ohne Timezone-Suffix.

**Fehler:**

- `submit_not_allowed` — `type != "json-schema-form"`
- `site_not_found` — Site existiert nicht
- `submission_too_large` — Body > 1 MB
- `invalid_json` — Body ist kein gültiges JSON

## Neue Tools

### `get_submissions(site_path, since?, limit?)`

**Input:**

```json
{
  "site_path": "form-001",
  "since": "2026-09-22T18:00:00",
  "limit": 50
}
```

- `since` optional — ISO-8601 (lokale Zeit); nur Submissions danach
- `limit` optional, default 50, max 500

**Output** (Array direkt, neueste zuerst):

```json
[
  {
    "submission_id": "2026-09-22T21-30-00_a8f2k1d3",
    "received_at": "2026-09-22T21:30:00",
    "data": {/* eingegangene Form-Daten */}
  }
]
```

## file_count Berechnung

`file_count` wird **analog für `files` und `folder`** über Filesystem-Operationen berechnet:

- `type: "files"`: `Directory.EnumerateFiles(<site>, "*", SearchOption.AllDirectories).Count()`
- `type: "folder"`: `Directory.EnumerateFiles(<host-path>, "*", SearchOption.AllDirectories).Count()`

Beide via lokaler Disk-Operation. Für typische Folder-Größen schnell; bei 10k+ Files kann `EnumerateFiles` spürbar sein.

## Retention für `folder`-Type

Wenn `updated_at + retention_seconds` für eine `folder`-Site überschritten ist:
- **Registry-Eintrag wird entfernt**
- Host-Folder bleibt **unangetastet**
- Bei Re-Deploy wird `path` neu gesetzt und `updated_at` aktualisiert (TTL-Reset)

## Error Codes

| Code | Wann |
|------|------|
| `invalid_site_id` | `site_path` verletzt `^[a-z0-9-]{3,32}$` |
| `invalid_type` | `type` nicht in erlaubter Liste |
| `type_immutable` | Site existiert + `type` weicht ab |
| `site_not_found` | `site_path` existiert nicht |
| `path_traversal` | `path` enthält `..` |
| `path_too_long` | `path` > 260 Zeichen |
| `duplicate_path` | gleicher `path` mehrfach in einem `deploy`-Call |
| `invalid_file_entry` | `delete: true` + `content`/`src` gleichzeitig |
| `missing_content` | File ohne `content`, ohne `src`, und ohne `delete: true` |
| `file_too_large` | `content` > 1 MB auf Platte |
| `payload_too_large` | `payload` > 1 MB (a2ui/schema-form) |
| `submission_too_large` | Submission-Body > 1 MB |
| `path_required` | `type: "folder"` ohne `path` |
| `payload_required` | `type: "a2ui"`/`schema-form"` ohne `payload` |
| `payload_not_allowed_for_files` | `type: "files"` mit `payload` |
| `files_not_allowed_for_folder` | `type: "folder"` mit `files[]` |
| `files_not_allowed_for_a2ui` | `type: "a2ui"` mit `files[]` |
| `files_not_allowed_for_schema_form` | `type: "schema-form"` mit `files[]` |
| `submit_not_allowed` | `POST /<site>/submit` bei falschem `type` |
| `invalid_json` | Submit-Body ist kein gültiges JSON |
| `src_invalid_data_url` | (MVP3) Data-URL kaputt / base64 ungültig |
| `src_unreachable` | (MVP3) HTTP-URL DNS/TCP-Fehler |
| `src_timeout` | (MVP3) HTTP-URL Timeout |
| `src_fetch_failed` | (MVP3) HTTP 4xx/5xx |
| `internal_error` | Unerwarteter Server-Fehler |

## Out of Scope (MVP4)

- **HTTP-Authentifizierung** — siehe `specs/mvp5-authorization.md` (Draft)
- WebSocket-Streaming für A2UI progressive rendering
- Submit-Webhooks
- Custom Render-Themes
- Auth auf Submit-Endpoint (LAN-only MVP)
- A2UI-Action-Callbacks (User-Interaktion zurück zum Agent) — für MVP4 nur Render

> Versionierung: v1.0 = final; Änderungen → v1.1/v2.0-Bump mit Changelog oben.

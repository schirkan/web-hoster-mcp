# MVP1 — Web Hoster MCP

Stand: 2026-09-22 · v1.0 (lock, refaktoriert für MVP4-Integration)

## Ziel

MCP-Server (C# / .NET 8, Windows), der einer KI **vier Tools**
bereitstellt, um statische Web-Inhalte im **lokalen Netz** zu hosten.
Eine Site = ein Ordner voller Files, erreichbar unter
`http://<ip>:<port>/<site_path>/<file>`. Render-Type MVP1 ist
`files` (default); weitere Render-Typen kommen in MVP4.

## Server

- **Runtime:** .NET 8 (LTS) auf Windows
- **HTTP:** 1× Kestrel-Listener auf `IP:Port` aus `appsettings.json`
- **Default-Bind:** `0.0.0.0:3000`
- **URL-Pattern:** `http://<ip>:<port>/<site_path>/<file>` — erstes Segment = Site-Identität
- **LAN-IP autodetected** für `result_path` (nicht in Config)

## Storage-Layout

```
<SitesRoot>/                       # Default ./sites (konfigurierbar)
├── registry.json                  # Site-Registry (Single Source of Truth)
└── <site_path>/                   # Site-Folder, FLACH
    └── <files>                    # Bei render_type: "files" — direkt im Site-Folder
```

**Kein `data/`-Parent, kein `wwwroot/`.** Flach.

`registry.json` (Schema v1):

```json
{
  "version": 1,
  "sites": {
    "demo-001": {
      "site_path": "demo-001",
      "render_type": "files",
      "created_at": "2026-09-22T19:25:00Z",
      "updated_at": "2026-09-22T19:30:00Z"
    }
  }
}
```

`render_type` default `"files"`. Siehe `specs/mvp4-render-types.md` für weitere Typen.

## Configuration (`appsettings.json`)

```json
{
  "Host": {
    "Ip": "0.0.0.0",
    "Port": 3000
  },
  "SitesRoot": "./sites",
  "MaxFileSizeBytes": 1048576
}
```

## Tools

### 1. `deploy`

**Input:**

```json
{
  "site_path": "demo-001",
  "render_type": "files",
  "mode": "merge",
  "files": [
    {"path": "index.html", "content": "<!DOCTYPE html>..."},
    {"path": "logo.png", "content": "data:image/png;base64,iVBOR..."},
    {"path": "old.html", "delete": true}
  ]
}
```

**Input-Validation:**

- `site_path` (optional): falls gesetzt → `^[a-z0-9-]{3,32}$`; falls leer → 8-stellige random UID
- `render_type` (optional, default `"files"`): `"files"` (MVP4-Erweiterung: `"a2ui"`, `"json-schema-form"`)
  - Wert ungültig → `invalid_render_type`
  - Bei bestehender Site + `render_type` weicht ab → `render_type_immutable`
- `mode` (optional, default `"merge"`): `"merge"` | `"replace"`
- `files[]`:
  - jeder Eintrag: `path` Pflicht, relativ zu `<site>/`, kein `..`
  - entweder `content` oder `delete: true`, niemals beides
  - **doppelter `path` in einem Call → Fehler `duplicate_path`**
  - `content` mit `data:`-Präfix → Data-URL parsen + base64 dekodieren
  - `content` plain → als UTF-8-Text speichern
  - Content-Type kommt **ausschließlich** aus File-Extension (Mimetype aus Data-URL ignoriert)
  - Per-File-Größe max **1 MB auf Platte** (dekodierte Bytes) → sonst `file_too_large`

**Mode-Semantik:**

- `merge` (default):
  - Site existiert nicht → neu anlegen
  - Site existiert → jedes File: `delete: true` → weg; sonst add oder replace; Files nicht im Call bleiben
- `replace`:
  - Site existiert nicht → neu anlegen
  - Site existiert → alle alten Files weg, exakt die Files aus dem Call
  - `delete: true` in `files[]` wird ignoriert

**Empty-Files-Verhalten:**

- `mode: "merge"` + `files: []` → **no-op** (kein Fehler, keine Änderung)
- `mode: "replace"` + `files: []` → Fehler `empty_files_not_allowed`

**Output (minimal):**

```json
{
  "site_path": "demo-001",
  "url": "http://192.168.x.x:3000/demo-001/",
  "files": [
    {"path": "index.html", "result_path": "http://192.168.x.x:3000/demo-001/index.html"},
    {"path": "logo.png", "result_path": "http://192.168.x.x:3000/demo-001/logo.png"}
  ]
}
```

Gelöschte Files erscheinen **nicht** in `files[]`.

### 2. `list_sites`

**Input:** `{}`

**Output** (Array direkt, kein Root-Element):

```json
[
  {
    "site_path": "demo-001",
    "render_type": "files",
    "file_count": 3,
    "created_at": "2026-09-22T19:25:00Z",
    "updated_at": "2026-09-22T19:30:00Z",
    "url": "http://192.168.x.x:3000/demo-001/"
  }
]
```

`file_count` wird per `Directory.EnumerateFiles(<site>).Count()` ermittelt.

### 3. `get_site_info`

**Input:** `{ "site_path": "demo-001" }`

**Output:**

```json
{
  "site_path": "demo-001",
  "render_type": "files",
  "file_count": 3,
  "created_at": "2026-09-22T19:25:00Z",
  "updated_at": "2026-09-22T19:30:00Z",
  "url": "http://192.168.x.x:3000/demo-001/",
  "files": [
    {"path": "index.html", "result_path": "http://192.168.x.x:3000/demo-001/index.html"},
    {"path": "logo.png", "result_path": "http://192.168.x.x:3000/demo-001/logo.png"}
  ]
}
```

### 4. `delete_site`

**Input:** `{ "site_path": "demo-001" }`

**Output:** `{ "site_path": "demo-001", "deleted": true }`

Löscht `<SitesRoot>/<site_path>/` und den Registry-Eintrag.

## Static File Serving (MVP1)

- `GET /<site_path>/<file>` → Datei serven mit Content-Type aus File-Extension (Pfad intern: `<SitesRoot>/<site_path>/<file>`)
- `GET /<site_path>/` → **404** (Directory Listing kommt in MVP2)
- `GET /<site_path>` (ohne `/`) → **404**
- `GET /` → **404** (Sites-Index kommt in MVP2)
- `GET /<site_path>/<unknown>` → **404**

**Default-Content-Type-Mapping:**

| Extension | Content-Type |
|-----------|--------------|
| `.html`, `.htm` | `text/html; charset=utf-8` |
| `.css` | `text/css; charset=utf-8` |
| `.js`, `.mjs` | `application/javascript; charset=utf-8` |
| `.json` | `application/json; charset=utf-8` |
| `.svg` | `image/svg+xml` |
| `.png` | `image/png` |
| `.jpg`, `.jpeg` | `image/jpeg` |
| `.gif` | `image/gif` |
| `.webp` | `image/webp` |
| `.ico` | `image/x-icon` |
| `.woff`, `.woff2` | `font/woff`, `font/woff2` |
| `.ttf` | `font/ttf` |
| `.txt` | `text/plain; charset=utf-8` |
| (sonst) | `application/octet-stream` |

## Error Codes

| Code | Wann |
|------|------|
| `invalid_site_id` | `site_path` verletzt `^[a-z0-9-]{3,32}$` |
| `site_not_found` | `site_path` existiert nicht |
| `duplicate_path` | gleicher `path` mehrfach in einem `deploy`-Call |
| `invalid_file_entry` | `delete: true` + `content` gleichzeitig |
| `missing_content` | File ohne `content` und ohne `delete: true` |
| `file_too_large` | dekodierte File > 1 MB |
| `invalid_data_url` | Data-URL kaputt / base64 ungültig |
| `empty_files_not_allowed` | `replace`-Modus mit `files: []` |
| `path_traversal` | `path` enthält `..` oder ist absolut |
| `invalid_render_type` | `render_type` nicht in erlaubter Liste |
| `render_type_immutable` | Site existiert + `render_type` weicht ab |
| `internal_error` | Unerwarteter Server-Fehler |

## Out of Scope (MVP1)

- HTTPS (→ MVP2)
- Auto-Delete / Retention (→ MVP2)
- Directory Listing & Sites-Index (→ MVP2)
- A2UI / JSON-Schema-Form Render-Types (→ MVP4)
- `src`-Parameter für lokale Files (→ MVP3)
- Form-Submit-Endpoint + `get_submissions` (→ MVP4)

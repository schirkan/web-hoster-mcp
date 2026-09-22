# MVP1 — Web Hoster MCP (Base)

Stand: 2026-09-22 · v1.1 (lock, refaktoriert für MVP4 v2)

## Ziel

MCP-Server (C# / .NET 8, Windows), der einer KI **vier Tools**
bereitstellt, um statische Web-Inhalte im **lokalen Netz** zu hosten.

Diese Spec deckt **`type: "files"`** (Default-Render-Type). Weitere
Render-Types (`folder`, `a2ui`, `json-schema-form`) sind in
`specs/mvp4-render-types.md` definiert.

## Server

- **Runtime:** .NET 8 (LTS) auf Windows
- **HTTP:** 1× Kestrel-Listener auf `IP:Port` aus `appsettings.json`
- **Default-Bind:** `0.0.0.0:3000`
- **URL-Pattern:** `http://<ip>:<port>/<site_path>/<file>` — erstes Segment = Site-Identität
- **LAN-IP autodetected** für `result_path` (nicht in Config)

## Storage-Layout

```
<SitesRoot>/                      # Default ./sites (konfigurierbar)
├── registry.json                 # Site-Registry (Single Source of Truth)
└── <site_path>/                  # Site-Folder, FLACH (mit Subfolders bei files-type)
    └── <files>                   # Bei type: "files"
```

**Kein `data/`-Parent, kein `wwwroot/`.** Flach mit optionalen Subfolders.

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

`type` default `"files"`. `retention_seconds` optional, default `0` = kein Auto-Expire.

## Configuration (`appsettings.json`)

```json
{
  "Host": { "Ip": "0.0.0.0", "Port": 3000 },
  "SitesRoot": "./sites",
  "MaxFileSizeBytes": 1048576
}
```

## Tools (für `type: "files"`)

### 1. `deploy`

**Input:**

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

**Input-Validation:**

- `site_path` (optional): falls gesetzt → `^[a-z0-9-]{3,32}$`; falls leer → 8-stellige random UID
- `type` (optional, default `"files"`): weitere Werte in MVP4
  - Ungültig → `invalid_type`
  - Bei bestehender Site + `type` weicht ab → `type_immutable`
- `mode` (optional, default `"merge"`): `"merge"` | `"replace"`
- `files[]`:
  - jeder Eintrag: `path` Pflicht, kann `/` enthalten (Subfolder)
  - **keine Path-Validation** — auch `..` oder absolute Pfade werden akzeptiert (Trust-Modell)
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

- `mode: "merge"` + `files: []` → **no-op**
- `mode: "replace"` + `files: []` → Fehler `empty_files_not_allowed`

**Output (minimal):**

```json
{
  "site_path": "demo-001",
  "url": "http://192.168.x.x:3000/demo-001/",
  "files": [
    {"path": "index.html", "result_path": "http://192.168.x.x:3000/demo-001/index.html"},
    {"path": "css/style.css", "result_path": "http://192.168.x.x:3000/demo-001/css/style.css"}
  ]
}
```

Gelöschte Files erscheinen **nicht** in `files[]`.

### 2. `list_sites`

**Input:** `{}`

**Output** (Array direkt):

```json
[
  {
    "site_path": "demo-001",
    "type": "files",
    "file_count": 3,
    "created_at": "2026-09-22T19:25:00Z",
    "updated_at": "2026-09-22T19:30:00Z",
    "url": "http://192.168.x.x:3000/demo-001/"
  }
]
```

`file_count` per `Directory.EnumerateFiles(<site>, "*", SearchOption.AllDirectories).Count()`.

### 3. `get_site_info`

**Input:** `{ "site_path": "demo-001" }`

**Output:**

```json
{
  "site_path": "demo-001",
  "type": "files",
  "file_count": 3,
  "created_at": "2026-09-22T19:25:00Z",
  "updated_at": "2026-09-22T19:30:00Z",
  "url": "http://192.168.x.x:3000/demo-001/",
  "files": [
    {"path": "index.html", "result_path": "http://192.168.x.x:3000/demo-001/index.html"},
    {"path": "css/style.css", "result_path": "http://192.168.x.x:3000/demo-001/css/style.css"}
  ]
}
```

### 4. `delete_site`

**Input:** `{ "site_path": "demo-001" }`

**Output:** `{ "site_path": "demo-001", "deleted": true }`

Löscht `<SitesRoot>/<site_path>/` und den Registry-Eintrag.

## Static File Serving (für `type: "files"`)

- `GET /<site_path>/<file>` → Datei serven (mit Subfolder-Pfaden: `css/style.css`)
- `GET /<site_path>/` → Directory-Listing (siehe `mvp2-directory-listing.md`)
- `GET /<site_path>` (ohne `/`) → **404**
- `GET /` → **404** (Sites-Index kommt mit MVP2)
- `GET /<site_path>/<unknown>` → **404**
- `GET /<site_path>/delete?confirm=yes` → Site löschen (MVP2)
- `GET /<site_path>/delete-file/<path>?confirm=yes` → File löschen (MVP2)

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

## Error Codes (MVP1-Teil)

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
| `invalid_type` | `type` nicht in erlaubter Liste (siehe MVP4) |
| `type_immutable` | Site existiert + `type` weicht ab |
| `internal_error` | Unerwarteter Server-Fehler |

## Out of Scope (MVP1)

- HTTPS (→ MVP2)
- Auto-Delete / Retention (→ MVP2)
- Directory Listing & Sites-Index (→ MVP2)
- Render-Types `folder`/`a2ui`/`json-schema-form` (→ MVP4)
- `src`-Parameter pro File (→ MVP3, separate Idee)
- Submit-Endpoint + `get_submissions` (→ MVP4)
- **Path-Validation** (`path_traversal`, Whitelist) — Trust-Modell, siehe MVP4

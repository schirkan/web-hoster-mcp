# MVP1 — Web Hoster MCP (Base)

Stand: 2026-09-23 · v1.3 (lock)

## Changelog

- **v1.3 (2026-09-23):** Path-Validation: `..` und Pfad-Länge > 260 nicht erlaubt. Timestamps in lokaler Server-Zeit (kein Z-Suffix). `mode: "replace"` + `files: []` löscht alle Files (kein Error mehr). Neue Error-Codes: `path_traversal`, `path_too_long`. `empty_files_not_allowed` raus. 1 MB Limit nur für `content`, nicht für `src` (siehe MVP3).
- **v1.2 (2026-09-23):** `content` plain-only (kein Data-URL-Decode, kein Error); `retention_seconds`/`expires_at` dokumentiert; Cross-Ref auf MVP3.
- **v1.1 (2026-09-23):** Subfolders für `files`; `path_traversal`-Validation raus (Trust); Cross-Ref auf MVP4.
- (vorherige Versionen) Siehe Git-History.

## Ziel

MCP-Server (C# / .NET 8, Windows), der einer KI **vier Tools**
bereitstellt, um statische Web-Inhalte im **lokalen Netz** zu hosten.

Diese Spec deckt **`type: "files"`** (Default-Render-Type) ab.
Weitere Render-Types (`folder`, `a2ui`, `json-schema-form`) sind in
`specs/mvp4-render-types.md` definiert. Für alternative File-Quellen
über `src` (Data URL, lokaler Pfad, HTTP/HTTPS-URL) siehe
`specs/mvp3.md`.

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
      "created_at": "2026-09-22T19:25:00",
      "updated_at": "2026-09-22T19:30:00",
      "retention_seconds": 0
    }
  }
}
```

- **Timestamps** (`created_at`, `updated_at`, `expires_at`) in **lokaler Server-Zeit** (`DateTime.Now`), ISO-8601 ohne Timezone-Suffix.
- `type` default `"files"`.
- `retention_seconds` optional, default `0` = kein Auto-Expire (siehe MVP2 §Retention).

## Configuration (`appsettings.json`)

```json
{
  "Host": { "Ip": "0.0.0.0", "Port": 3000 },
  "SitesRoot": "./sites",
  "MaxFileSizeBytes": 1048576
}
```

`MaxFileSizeBytes` = 1 MB Limit für `content` (siehe §Tools/1).

## Tools (für `type: "files"`)

### 1. `deploy`

**Input:**

```json
{
  "site_path": "demo-001",
  "type": "files",
  "mode": "merge",
  "retention_seconds": 3600,
  "files": [
    {"path": "index.html", "content": "<!DOCTYPE html>..."},
    {"path": "css/style.css", "content": "body { margin: 0 }"},
    {"path": "logo.png", "src": "data:image/png;base64,iVBOR..."},
    {"path": "banner.png", "src": "C:/local-assets/banner.png"},
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
- `retention_seconds` (optional, default `0`):
  - weggelassen bei Update → bestehender Wert bleibt
  - weggelassen bei neuer Site → Global Default aus `Retention:DefaultTtlSeconds` (siehe MVP2 §3)
  - `0` → nie ablaufen
  - `> 0` → nach N Sekunden ab `updated_at`
- `files[]`:
  - jeder Eintrag: `path` Pflicht, kann `/` enthalten (Subfolder)
  - **Path-Validation:**
    - `..` (Parent-Directory-Traversal) NICHT erlaubt → `path_traversal`
    - Pfad-Länge max **260 Zeichen** (Windows `MAX_PATH`) → `path_too_long`
    - Absolute Pfade und UNC-Pfade (`\\server\share\...`) erlaubt (Trust-Modell)
  - jeder Eintrag hat entweder `content` ODER `src` ODER `delete: true` — niemals Kombinationen
  - `content` plain → als UTF-8-Text speichern
    - **Größenlimit: 1 MB auf Platte** → `file_too_large`
    - KEINE Data-URL-Sonderbehandlung (Data URLs gehören in `src`, siehe MVP3)
  - `src` → alternative Quelle (Data URL / lokaler Pfad / HTTP-URL), Verhalten in MVP3
    - **KEIN** 1 MB Limit (siehe MVP3 §Validierung)
  - **doppelter `path` in einem Call → Fehler `duplicate_path`**
  - Content-Type kommt **ausschließlich** aus File-Extension

**Mode-Semantik:**

- `merge` (default):
  - Site existiert nicht → neu anlegen
  - Site existiert → jedes File: `delete: true` → weg; sonst add oder replace; Files nicht im Call bleiben
- `replace`:
  - Site existiert nicht → neu anlegen
  - Site existiert → alle alten Files weg, exakt die Files aus dem Call
  - `delete: true` in `files[]` wird ignoriert

**Empty-Files-Verhalten:**

- `mode: "merge"` + `files: []` → **no-op** (kein Error)
- `mode: "replace"` + `files: []` → **delete all files** (kein Error, alle Files weg)
- Beide Modi akzeptieren leere `files[]` ohne Error.

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
    "retention_seconds": 3600,
    "created_at": "2026-09-22T19:25:00",
    "updated_at": "2026-09-22T19:30:00",
    "expires_at": "2026-09-29T19:30:00",
    "url": "http://192.168.x.x:3000/demo-001/"
  }
]
```

`file_count` per `Directory.EnumerateFiles(<site>, "*", SearchOption.AllDirectories).Count()`.

`expires_at` = ISO-8601 Lokalzeit (`updated_at + retention_seconds`), `null` wenn `retention_seconds: 0`. Berechnet beim Read, nicht persistiert.

### 3. `get_site_info`

**Input:** `{ "site_path": "demo-001" }`

**Output:**

```json
{
  "site_path": "demo-001",
  "type": "files",
  "file_count": 3,
  "retention_seconds": 3600,
  "created_at": "2026-09-22T19:25:00",
  "updated_at": "2026-09-22T19:30:00",
  "expires_at": "2026-09-29T19:30:00",
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
- `DELETE /<site>` → Site löschen (MVP2 v1.2)
- `DELETE /<site>/<file>` → File löschen (MVP2 v1.2)

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
| `path_traversal` | `path` enthält `..` |
| `path_too_long` | `path` > 260 Zeichen (Windows MAX_PATH) |
| `duplicate_path` | gleicher `path` mehrfach in einem `deploy`-Call |
| `invalid_file_entry` | `delete: true` + (`content` ODER `src`) gleichzeitig |
| `missing_content` | File ohne `content`, ohne `src`, und ohne `delete: true` |
| `file_too_large` | `content` > 1 MB auf Platte |
| `invalid_type` | `type` nicht in erlaubter Liste (siehe MVP4) |
| `type_immutable` | Site existiert + `type` weicht ab |
| `internal_error` | Unerwarteter Server-Fehler |

Hinweis: `empty_files_not_allowed` ist seit v1.3 entfernt — `files: []` ist in beiden Modi (`merge`/`replace`) ohne Error erlaubt.

## Out of Scope (MVP1)

- HTTPS (→ MVP2)
- Auto-Delete / Retention (→ MVP2)
- Directory Listing & Sites-Index (→ MVP2)
- Render-Types `folder`/`a2ui`/`json-schema-form` (→ MVP4)
- `src`-Parameter pro File (Data URL / lokaler Pfad / HTTP-URL) (→ MVP3)
- Data-URL-Sonderbehandlung in `content` — Data URLs gehören in `src` (MVP3)
- Submit-Endpoint + `get_submissions` (→ MVP4)
- **Path-Validation für `..` und > 260 ist IN Scope** (v1.3+); weitere Path-Validierung (Whitelist, Permission-Check) bleibt out-of-scope (Trust-Modell)
- Authorization für HTTP-Endpoints (→ MVP5-Draft)

> Versionierung: v1.0 = final; Änderungen → v1.1/v2.0-Bump mit Changelog oben.

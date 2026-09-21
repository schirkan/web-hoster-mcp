# 03 — MCP-Tools

Stand: 2026-09-21 · v0.1 (Entwurf)

Alle Tools werden via **stdio** (JSON-RPC 2.0) gemäß
[Model Context Protocol Spec](https://modelcontextprotocol.io/)
bereitgestellt.

Tool-Namen sind `snake_case`, Parameter ebenfalls.
Antworten verwenden `content[type=text]` für menschenlesbare Bestätigung
und `structuredContent` für maschinenlesbare Daten (MCP-Spec 2025-06-18+).

---

## Übersicht

| Tool | Zweck | Phase |
|------|-------|-------|
| `deploy_site` | Neue Site anlegen + starten | MVP |
| `update_site_files` | Dateien einer bestehenden Site ändern | MVP |
| `list_sites` | Alle Sites auflisten | MVP |
| `get_site` | Detail-Infos zu einer Site | MVP |
| `stop_site` | Kestrel-Instanz stoppen | MVP |
| `start_site` | Kestrel-Instanz starten | MVP |
| `delete_site` | Site komplett entfernen | MVP |
| `get_server_status` | MCP-Host + alle Sites | MVP |
| `get_site_logs` | Zugriffs- / Error-Logs einer Site | MVP |
| `set_site_headers` | Custom HTTP-Headers ändern | Phase 2 |
| `export_site` | Site als ZIP exportieren | Phase 2 |
| `import_site` | Site aus ZIP importieren | Phase 3 |

---

## MVP-Tools

### 1. `deploy_site`

Legt eine neue Site an, schreibt initiale Dateien und startet sie.

**Input:**

```json
{
  "siteId": "demo-landing",
  "title": "Demo Landingpage",
  "port": 8123,
  "defaultFile": "index.html",
  "spaFallback": false,
  "files": [
    {
      "path": "index.html",
      "contentBase64": "PCFET0NUWVBFIGh0bWw+Li4u",
      "contentType": "text/html"
    },
    {
      "path": "style.css",
      "contentBase64": "Ym9keSB7IG1hcmdpbjogMDsgfQ=="
    }
  ],
  "headers": {
    "X-Frame-Options": "DENY"
  }
}
```

Regeln:
- `siteId`: Pflicht, kleingeschrieben, `[a-z0-9-]{3,32}`, eindeutig
- `port`: optional; sonst nächster freier aus `PortRange`
- `files[].path`: Pflicht, **relativ** zu `wwwroot/`, keine `..` erlaubt
- `files[].contentBase64`: Pflicht (auch für Text-Dateien)
- `files[].contentType`: optional; sonst aus Endung erraten

**Output (Success):**

```json
{
  "siteId": "demo-landing",
  "port": 8123,
  "url": "http://127.0.0.1:8123/",
  "status": "running",
  "filesWritten": 2,
  "totalSizeBytes": 1024
}
```

**Fehler:**
- `site_exists` — Site-ID schon vergeben
- `invalid_site_id` — Format verletzt
- `port_in_use` — Port belegt
- `extension_blocked` — Datei-Endung nicht in Whitelist
- `site_too_large` — Gesamtgröße > `MaxSiteSizeMB`
- `path_traversal` — `path` enthält `..` oder absolut

---

### 2. `update_site_files`

Schreibt Dateien in eine bestehende Site (überschreibt oder löscht).

**Input:**

```json
{
  "siteId": "demo-landing",
  "mode": "merge",
  "files": [
    { "path": "about.html", "contentBase64": "Li4u" },
    { "path": "old.html", "delete": true }
  ]
}
```

`mode`:
- `merge` (Default): neue Files anlegen/überschreiben, `delete: true`
  entfernt Einträge
- `replace`: alle existierenden Files löschen, dann neue schreiben
- `append`: nur anlegen, nichts überschreiben (Fehler wenn Pfad existiert)

**Output:** Liste der `{path, action, sizeBytes}`-Tupel.

---

### 3. `list_sites`

**Input (alle optional):**

```json
{
  "status": "running",
  "limit": 50,
  "offset": 0
}
```

**Output:**

```json
{
  "total": 2,
  "sites": [
    {
      "siteId": "demo-landing",
      "title": "Demo Landingpage",
      "port": 8123,
      "url": "http://127.0.0.1:8123/",
      "status": "running",
      "sizeBytes": 12345,
      "fileCount": 7,
      "createdAt": "2026-09-21T20:00:00Z",
      "updatedAt": "2026-09-21T20:15:00Z"
    }
  ]
}
```

---

### 4. `get_site`

**Input:** `{ "siteId": "demo-landing" }`

**Output:** Vollständige Site-Info inkl. Manifest + File-Listing.

---

### 5. `start_site` / `stop_site`

**Input:** `{ "siteId": "demo-landing" }`

**Output:** `{ "siteId": "...", "status": "running"|"stopped" }`

`start_site` startet den Kestrel-Host neu (z. B. nach `stop_site` oder
nach Server-Crash). `stop_site` beendet nur den HTTP-Listener, **Files
bleiben erhalten**.

---

### 6. `delete_site`

**Input:** `{ "siteId": "demo-landing", "force": false }`

`force: false` (Default): stoppt Site zuerst, fragt nicht nach
Bestätigung (KI hat das via MCP-Aufruf schon "bestätigt").
Mit `force: true` werden auch Sites mit `running`-Status gelöscht.

**Output:** `{ "siteId": "...", "deleted": true, "freedBytes": 12345 }`

---

### 7. `get_server_status`

**Input:** `{}`

**Output:**

```json
{
  "version": "0.1.0",
  "uptimeSeconds": 3600,
  "bindAddress": "127.0.0.1",
  "sitesTotal": 2,
  "sitesRunning": 2,
  "sitesStopped": 0,
  "dataRoot": "C:/.../data",
  "diskFreeBytes": 12345678901
}
```

---

### 8. `get_site_logs`

**Input:**

```json
{
  "siteId": "demo-landing",
  "limit": 100,
  "level": "info",
  "since": "2026-09-21T20:00:00Z"
}
```

**Output:** Liste von Log-Einträgen mit `timestamp`, `level`,
`message`, ggf. `path`, `status`, `clientIp`.

Log-Rotation: täglich, Aufbewahrung 7 Tage (konfigurierbar).

---

## Fehler-Konventionen

Alle Tools folgen dem gleichen Schema:

```json
{
  "error": {
    "code": "port_in_use",
    "message": "Port 8123 is already in use by site 'other-site'.",
    "details": { "port": 8123, "usedBy": "other-site" }
  }
}
```

Bekannte Error-Codes:

| Code | Wann |
|------|------|
| `site_not_found` | siteId existiert nicht |
| `site_exists` | siteId schon vergeben |
| `invalid_site_id` | Format verletzt |
| `invalid_path` | path absolut oder enthält `..` |
| `extension_blocked` | Endung nicht erlaubt |
| `port_in_use` | Port belegt (von dieser Site oder extern) |
| `port_out_of_range` | Port außerhalb `PortRange` |
| `site_too_large` | Überschreitet `MaxSiteSizeMB` |
| `internal_error` | Unerwarteter Server-Fehler |
| `tool_disabled` | Tool nur in Phase 2/3 verfügbar |

---

## Sicherheitshinweise an die KI

Jedes Tool-Result enthält eine **Read-Back-Zeile** in `content`, die der
KI zur Bestätigung an den Nutzer dienen kann, z. B.:

> ✅ Site `demo-landing` läuft auf http://127.0.0.1:8123/ (2 Dateien, 1.0 KB)

KI soll bei `deploy_site` / `update_site_files` mit **destruktiver
Wirkung** (replace-Modus, delete, force) den Nutzer vorher fragen, außer
der Nutzer hat das ausdrücklich erlaubt.

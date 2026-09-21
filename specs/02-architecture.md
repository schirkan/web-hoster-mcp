# 02 — Architektur

Stand: 2026-09-21 · v0.1 (Entwurf)

---

## 1. Tech-Stack

| Schicht | Wahl | Begründung |
|---------|------|------------|
| Sprache | **C# 12** | Martins Stack (USER.md) |
| Runtime | **.NET 8 (LTS)** | Bis 11/2026 Support, Win10/11 kompatibel |
| MCP-SDK | [`modelcontextprotocol/csharp-sdk`](https://github.com/modelcontextprotocol/csharp-sdk) (offiziell) | Native C#-Unterstützung für MCP |
| HTTP-Server | **ASP.NET Core / Kestrel** | Statische Files out-of-the-box, robust |
| Logging | `Microsoft.Extensions.Logging` + Konsole | Reicht für MVP |
| Tests | xUnit | Standard im .NET-Ökosystem |
| Packaging | `dotnet publish` + PowerShell-Wrapper | Native Windows-Pfad |

## 2. Prozess-Modell

**MVP:** Konsolen-Prozess (`dotnet run` oder veröffentlichte EXE),
Hintergrund via Windows Task Scheduler oder einfach minimiertes Terminal.

**Phase 2 (optional):** Windows-Service via
[`Microsoft.Extensions.Hosting.WindowsServices`](https://learn.microsoft.com/en-us/dotnet/core/extensions/workers)
mit SCM-Integration.

Begründung: Service-Installation ist auf Windows immer wieder
fehleranfällig (Pfade, Rechte, Account-Auswahl). Konsolen-Prozess ist
für lokales Hosting deutlich einfacher zu debuggen.

## 3. Komponenten

```
┌─────────────────────────────────────────────────────────┐
│ MCP-Client (KI / OpenClaw)                             │
└────────────────────┬────────────────────────────────────┘
                     │ stdio (JSON-RPC 2.0)
                     ▼
┌─────────────────────────────────────────────────────────┐
│ WebHosterMcp.Host (Konsolen-EXE)                       │
│ ┌─────────────────────────────────────────────────────┐ │
│ │ McpServer                                          │ │
│ │  ├ Tool: deploy_site                                │ │
│ │  ├ Tool: list_sites   …                             │ │
│ │  └ Tool: stop_site / delete_site / get_logs …      │ │
│ └──────┬───────────────────────────────────────────────┘ │
│        ▼                                               │
│ ┌─────────────────┐    ┌─────────────────────────────┐ │
│ │ SiteManager     │◄──►│ SiteRegistry (Manifest-JSON)│ │
│ └────────┬────────┘    └─────────────────────────────┘ │
│         ▼                                             │
│ ┌─────────────────┐    ┌─────────────────────────────┐ │
│ │ Kestrel-Hosts   │    │ FileSystem                 │ │
│ │ (1 pro Site)    │◄──►│ data/sites/<site-id>/      │ │
│ └─────────────────┘    └─────────────────────────────┘ │
└────────────────────────────────────────────────────────┘
```

### 2.1 SiteManager

Verwaltet Site-Lifecycle:

1. `Create(siteId, port)` → legt Ordner + Manifest an
2. `Start(siteId)` → startet Kestrel auf Port, hängt StaticFileMiddleware
   an `data/sites/<site-id>/wwwroot/`
3. `Stop(siteId)` → stoppt Kestrel-Host
4. `Delete(siteId)` → stoppt + löscht Ordner + Manifest-Eintrag
5. `List()` → liest Manifest + Status
6. `WriteFiles(siteId, files)` → schreibt atomar in `wwwroot/`

### 2.2 SiteRegistry

Single Source of Truth: `data/registry.json`.

```json
{
  "version": 1,
  "sites": {
    "demo-landing": {
      "siteId": "demo-landing",
      "title": "Demo Landingpage",
      "createdAt": "2026-09-21T20:00:00Z",
      "updatedAt": "2026-09-21T20:15:00Z",
      "port": 8123,
      "status": "running",
      "sizeBytes": 12345,
      "fileCount": 7
    }
  }
}
```

Persistente Status-Felder (`running`/`stopped`) werden beim Start des
MCP-Servers wiederhergestellt, indem `running`-Sites automatisch wieder
gestartet werden.

### 2.3 Kestrel-Binding

Pro Site: **eigene Kestrel-Instanz** mit eigenem Port. Vorteile:

- Saubere Isolation: Stop einer Site räumt nur deren Listener auf
- Konfliktfreier Parallelbetrieb
- Klare Fehlermeldung bei `port already in use`

Nachteile: leicht höherer RAM-Verbrauch (~10–15 MB pro Site). Für MVP
akzeptabel.

## 4. Storage-Layout

```
WebHosterMcp.Host/
├── appsettings.json              # Konfiguration (Pfade, Default-Port, Bind)
├── data/
│   ├── registry.json             # Site-Registry (Single Source of Truth)
│   ├── sites/
│   │   ├── demo-landing/
│   │   │   ├── manifest.json     # Site-Metadaten
│   │   │   └── wwwroot/          # Statische Inhalte
│   │   │       ├── index.html
│   │   │       └── style.css
│   │   └── another-site/
│   │       ├── manifest.json
│   │       └── wwwroot/...
│   └── logs/
│       └── host-YYYYMMDD.log
└── WebHosterMcp.Host.exe
```

`manifest.json` pro Site:

```json
{
  "siteId": "demo-landing",
  "title": "Demo Landingpage",
  "port": 8123,
  "createdAt": "2026-09-21T20:00:00Z",
  "updatedAt": "2026-09-21T20:15:00Z",
  "defaultFile": "index.html",
  "spaFallback": false,
  "headers": {
    "X-Frame-Options": "DENY"
  }
}
```

## 5. Konfiguration

`appsettings.json`:

```json
{
  "WebHoster": {
    "DataRoot": "./data",
    "DefaultBindAddress": "127.0.0.1",
    "PortRange": { "min": 8100, "max": 8199 },
    "MaxSites": 20,
    "MaxSiteSizeMB": 100,
    "AllowedExtensions": [".html", ".htm", ".css", ".js", ".mjs",
                         ".json", ".svg", ".png", ".jpg", ".jpeg",
                         ".gif", ".webp", ".ico", ".txt", ".md",
                         ".woff", ".woff2", ".ttf"],
    "DefaultHeaders": {
      "X-Content-Type-Options": "nosniff"
    },
    "AutoStart": true
  }
}
```

Portvergabe:

- KI kann Port **explizit** setzen
- Sonst: nächster freier Port aus `PortRange` (beginnend bei `min`)
- Konflikt (Port belegt) → Fehler an KI, kein Auto-Re-Roll

## 6. Security-Modell

| Schicht | Maßnahme |
|---------|----------|
| Netz | Bind nur `127.0.0.1` (Default) — kein LAN-Zugriff |
| Dateisystem | Sites laufen in Sandbox-Ordner unter `data/sites/<id>/` |
| Datei-Typen | Whitelist über `AllowedExtensions` (s. Konfiguration) |
| Path-Traversal | `wwwroot/` ist Root, alles darüber ist nicht erreichbar |
| Größe | Pro Site `MaxSiteSizeMB`, harter Cutoff beim Upload |
| MCP-Auth | Lokales stdio = reicht (kein Netz) |
| File-Watch | Änderungen an Dateien außerhalb des MCP werden ignoriert |

**Out-of-Scope:** Authentifizierung der KI am MCP. Annahme: Wenn jemand
Zugang zum lokalen Nutzer-Account hat, hat er auch Zugang zum MCP.

## 7. Erweiterungspunkte

- **Andere Bind-Adresse:** per Config (`0.0.0.0` nur mit explizitem
  Hinweis, dass Site dann im LAN sichtbar wird)
- **Custom Headers:** pro Site in `manifest.json`
- **SPA-Fallback:** `spaFallback: true` → `index.html` für nicht
  aufgelöste Pfade
- **Hot-Reload:** optionaler `FileSystemWatcher`, der Site-Konfig
  ändert → keine Änderung am MCP-Tool-Set nötig
- **Backup/Export:** Tool `export_site(siteId)` → ZIP-Datei in `data/exports/`

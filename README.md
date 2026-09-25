# Web Hoster MCP

Ein **MCP-Server in C# / .NET 8** für Windows, der einer KI **fünf Tools**
bereitstellt, um statische Web-Inhalte im **lokalen Netz** zu hosten.

- **`deploy`** — Site anlegen / updaten (merge oder replace); `src`-Parameter für Data URL / lokalen Pfad / HTTP-URL
- **`list_sites`** — Alle Sites auflisten
- **`get_site_info`** — Detail zu einer Site inkl. File-Liste
- **`delete_site`** — Site löschen
- **`get_submissions`** — Schema-Form Submissions abholen

Erweiterungen (via `src`-Parameter, Hosting-Typen, HTTPS, Retention):

- **4 Hosting-Typen (`type`):** `files` (default) / `folder` / `a2ui` / `json-schema-form`
- **HTTPS** parallel zu HTTP (PFX-Cert oder Self-Signed Fallback mit SAN-Entries)
- **Retention / Auto-Delete** per Site (TTL seit `updated_at`, Background-Timer, 7 Tage Default, 1h Interval)
- **Per-File `src`-Parameter** (Data URL / lokaler Pfad inkl. UNC / HTTP-URL → physische Kopie via atomic-write)
- **HTTP-Delete-Endpoints** mit **DELETE-Methode** (kein Confirm-Pattern, kein Prefetch-Risiko) + JS-Buttons in Listings

---

## Status

**Specs:**

| Spec | Inhalt | Version |
|------|--------|---------|
| [`mvp1.md`](./specs/mvp1.md) | Base — 4 MCP-Tools für `type: "files"`, `content` plain-only, Path-Validation (`..`/MAX_PATH), Retention-Semantik | ✅ v1.3 locked |
| [`mvp2.md`](./specs/mvp2.md) | HTTPS (Cert Beides, SAN, Self-Signed Fallback) + Retention/Auto-Delete (7d default, 1h interval) + HTTP-Delete-Endpoints mit DELETE-Methode | ✅ v1.3 locked |
| [`mvp2-directory-listing.md`](./specs/mvp2-directory-listing.md) | Directory-Listing für `files`/`folder` (kein Listing bei `a2ui`/`schema-form`) | ✅ v1.3 locked |
| [`mvp3.md`](./specs/mvp3.md) | Per-File `src` (Data URL / lokaler Pfad / HTTP-URL), atomic write, kein 1 MB Download-Limit | ✅ v1.1 locked |
| [`mvp4-render-types.md`](./specs/mvp4-render-types.md) | 4 Hosting-Typen (`type`: `files`/`folder`/`a2ui`/`json-schema-form`) + React + RJSF + custom DOM-Renderer für `a2ui` (vanilla, drop `@a2ui/react`) + Submit + `get_submissions` + 1 MB Limits für Payloads + mobile-responsive CSS (`@media(max-width:600px)`, Touch-Targets ≥ 44px) | ✅ v3.0 locked |
| [`mvp5-authorization.md`](./specs/mvp5-authorization.md) | **Authorization Draft** (Bearer-Token für HTTP-Endpoints, noch nicht festgelegt) | 📝 Draft |

**Lock-Semantik:** v1.0 = final; Änderungen führen zu v1.1/v2.0-Bump mit Changelog-Eintrag oben im jeweiligen Spec.

**Implementierung:** MVP1 + MVP2 + MVP3 + MVP4 umgesetzt (Stand `v0.0.6`). Weiterer Ausbau über MVP5+.

---

## CI/CD

- **Release:** `.github/workflows/release.yml`
  - Trigger: Tag-Push `v*` (z. B. `v1.0.0`)
  - Schritte: Restore, Build, Tests, self-contained Publish (`win-x64`, **trimmed**), ZIP, GitHub Release erstellen/aktualisieren
  - Release-Asset: `WebHosterMcp.Host-<tag>-win-x64.zip`

Beispiel Tag-Release:

```bash
git tag v1.0.0
git push origin v1.0.0
```

---

## Lokale Installation

### Endanwender: Release-Binary aus GitHub

Das aktuellste Release-Asset ist `WebHosterMcp.Host-<tag>-win-x64.zip` — **self-contained + trimmed + single-file** für `win-x64`. Es ist **kein .NET-Runtime-Install** nötig.

```powershell
$tag = (Invoke-RestMethod https://api.github.com/repos/schirkan/web-hoster-mcp/releases/latest).tag_name
Invoke-WebRequest "https://github.com/schirkan/web-hoster-mcp/releases/download/$tag/WebHosterMcp.Host-$tag-win-x64.zip" -OutFile "$env:USERPROFILE\Downloads\WebHosterMcp.zip"
Expand-Archive "$env:USERPROFILE\Downloads\WebHosterMcp.zip" -DestinationPath "$env:LOCALAPPDATA\web-hoster-mcp"
```

→ ergibt z. B. `C:\Users\<USER>\AppData\Local\web-hoster-mcp\WebHosterMcp.Host.exe`.

### Im Coding-Agent registrieren (z. B. OpenClaw)

**CLI (empfohlen) — ohne env vars, wenn `appsettings.json` neben der Binary die Werte liefert:**

```powershell
openclaw mcp add web-hoster-mcp --command "C:\Users\<USER>\AppData\Local\web-hoster-mcp\WebHosterMcp.Host.exe"
```

**Per Hand** in `~/.openclaw/openclaw.json` → `plugins.mcpServers.web-hoster-mcp`:

```json
{
  "command": "C:\\Users\\<USER>\\AppData\\Local\\web-hoster-mcp\\WebHosterMcp.Host.exe",
  "args": [],
  "env": {
    "Sites__SitesRoot":             "C:\\Users\\<USER>\\Documents\\web-hoster-sites",
    "Host__Port":                   "3000",
    "Retention__DefaultTtlSeconds": "604800"
  }
}
```

Verifizieren:

```powershell
openclaw mcp list
```

Die MCP-Tools `deploy`, `list_sites`, `get_site_info`, `delete_site`, `get_submissions` sind im Agent verfügbar.

### Architekturhinweis

Der Server spricht **MCP-over-stdio** (`Program.cs` → `AddMcpServer().WithStdioServerTransport()`). stdin/stdout ist das Wire-Format zwischen Agent und Server — die Konsole bleibt sauber. Parallel läuft Kestrel auf Port 3000 (HTTP, optional Port 3443 HTTPS) und liefert die deployten Sites als Web-UI aus. Beide Pfade laufen im selben Prozess — keine zwei Binaries, keine zwei Configs.

### Konfiguration

`WebApplication.CreateBuilder` lädt `appsettings.json` automatisch aus dem **Binary-Verzeichnis** (`ContentRootPath`). Layer-Priorität (höchste zuerst):

1. Kommandozeile (`--Kestrel:Endpoints:Http:Url=...`)
2. **Umgebungsvariablen** (Doppel-Underscore-Schreibweise für nested Config, z. B. `Sites__SitesRoot`)
3. `appsettings.{ASPNETCORE_ENVIRONMENT}.json`
4. `appsettings.json`

**Defaults reichen?** Im Release-ZIP liegt eine `appsettings.json` neben der `.exe` mit den Repo-Defaults: `SitesRoot: "./sites"`, `Host:Ip: "0.0.0.0"`, Port 3000, Retention 7 d, Src-Timeout 30 s, Self-Signed-HTTPS on. **Kein Setup nötig**, der Server bootet damit.

**Eigene Werte ohne env vars:** Direkt in die `appsettings.json` neben der Binary editieren:

```json
{
  "Logging":   { "LogLevel": { "Default": "Information", "Microsoft.AspNetCore": "Warning" } },
  "Host":      { "Ip": "127.0.0.1", "Port": 3000, "UseHttps": false },
  "Retention": { "Enabled": true, "DefaultTtlSeconds": 604800, "CheckIntervalSeconds": 3600 },
  "Src":       { "HttpTimeoutSeconds": 30 },
  "SitesRoot": "C:\\Users\\<USER>\\Documents\\web-hoster-sites",
  "MaxFileSizeBytes":      1048576,
  "MaxPayloadSizeBytes":   1048576,
  "MaxSubmissionSizeBytes":1048576
}
```

**Profile trennen:** `ASPNETCORE_ENVIRONMENT=Local` aktiviert zusätzlich `appsettings.Local.json` als Layer-Override — so vermeidet man `--env`-Flag-Ketten in `openclaw mcp add`.

**Ohne jegliche JSON-Datei** bootet der Server mit den C#-Property-Defaults aus `HostOptions`/`SitesOptions`.

**Caveat — relativer Pfad:** `SitesRoot: "./sites"` ist **relativ zum CWD** des Prozesses. Beim Spawn durch den Coding-Agent ist das CWD oft das Agent-Workspace, nicht das Binary-Verzeichnis. Für reproduzierbare Site-Folder lieber **Absolutpfad** setzen (siehe Beispiel oben).

### Varianten, falls relevant

| Variante | Wann |
|---|---|
| Release-Binary (oben) | Endanwender / Coding-Agent |
| `git clone … && dotnet run --project src/WebHosterMcp.Host` | Dev-Setup, Quellcode direkt testen (.NET 8 SDK nötig) |
| Docker auf `mcr.microsoft.com/dotnet/runtime:8.0` | Linux-Server, CI-Reproduzierbarkeit |
| npm-Wrapper | Aktuell unnötig — die `.exe` ist self-contained |

---

## Tech Stack

- **Sprache:** C# 12
- **Runtime:** .NET 8 (LTS) auf Windows
- **HTTP:** Kestrel (`Microsoft.AspNetCore`)
- **MCP:** [`modelcontextprotocol/csharp-sdk`](https://github.com/modelcontextprotocol/csharp-sdk) (offiziell)
- **Storage:** flach unter `<SitesRoot>/<site>/<files>` für `files`-Type; `payload.json` für `a2ui`/`schema-form`; `folder`-Type ohne Site-Folder (Host-FS-Mirror)
- **Path-Validation:** `..` nicht erlaubt, max 260 Zeichen (Windows MAX_PATH), UNC-Pfade erlaubt (Trust-Modell)
- **React (Browser-UI):** via CDN, RJSF für Form-Render, offizieller A2UI-React-Renderer (`@a2ui/react`)

---

## Schnellstart (Dev)

Für Endanwender ohne .NET-SDK: [Lokale Installation](#lokale-installation).

```bash
dotnet build
dotnet run --project src/WebHosterMcp.Host
# → Kestrel auf 0.0.0.0:3000 (HTTP)
# → HTTPS parallel auf 0.0.0.0:3443 (Self-Signed Cert wenn kein PFX konfiguriert)
# → MCP-Endpoint via stdio
```

*(MVP1 + MVP2 + MVP3 + MVP4 sind implementiert; weitere Spec-Features folgen gemäß Roadmap.)*

---

## Project Structure

```
web-hoster-mcp/
├── .github/workflows/
│   └── release.yml                      # Tag-Release (v*) + GitHub Release Asset
├── LICENSE                              # MIT
├── README.md                            # This file
├── AGENTS.md                            # Sub-Agent Context
├── specs/                               # Specs (MVP1-5 gelockt/Draft)
│   ├── mvp1.md
│   ├── mvp2.md
│   ├── mvp2-directory-listing.md
│   ├── mvp3.md
│   ├── mvp4-render-types.md
│   └── mvp5-authorization.md            # Draft
└── sites/                               # Runtime (gitignored)
    ├── registry.json
    └── <site_path>/
        ├── <files>                     # files-type (mit Subfolders)
        ├── payload.json                 # a2ui / schema-form
        └── <submission-id>.json         # schema-form submissions
```

---

## Spezifikation (Kurzfassung)

- **Server:** 1× Kestrel-Listener auf `0.0.0.0:3000` (HTTP) + optional `0.0.0.0:3443` (HTTPS parallel). HTTPS off wenn `Host:UseHttps = false`.
- **URL-Pattern:** `http://<ip>:<port>/<site_path>/<file>` — erstes Segment = Site-Identität.
- **Site-Types:** `files` (default), `folder` (Host-Ordner-Mirror), `a2ui` (Google A2UI v0.9.1), `json-schema-form` (RJSF).
- **Path-Validation:** `..` nicht erlaubt, max 260 Zeichen (Windows MAX_PATH), UNC erlaubt.
- **Content-Type:** aus File-Extension (Mimetype aus Data-URL ignoriert).
- **Größen-Limits:** `content` 1 MB; `payload.json` (a2ui/schema-form) 1 MB; Submission-Body 1 MB; `src`-Downloads **kein** Limit (Server-HTTP-Limit gilt).
- **Trust-Modell:** keine Cert-Validation, keine Permission-Checks — KI/User trägt Verantwortung.

Vollständige Details in den Specs.

---

## Lizenz

MIT — siehe [LICENSE](./LICENSE).

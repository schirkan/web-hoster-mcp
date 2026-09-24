# Web Hoster MCP — AGENTS.md

Projekt: **Web Hoster MCP**
Status: MVP4 implementiert
Letztes Update: 2026-09-24

---

## Lizenz

MIT — siehe [LICENSE](./LICENSE)

---

## Current Status

Stand: 2026-09-24 (10:25)

- [x] MVP1-Spec v1.3: `specs/mvp1.md` — `type: "files"`-Pfad, 4 Tools, `content` plain-only, **Path-Validation (`..`/MAX_PATH)**, **timestamps lokal**, **replace+empty deletes all**, Cross-Ref auf MVP3 für `src`, Lock-Semantik-Footer
- [x] MVP2-Spec v1.3: `specs/mvp2.md` — HTTPS (Beides, SAN, Self-Signed Fallback) + Retention (7d Default, 1h Interval mit **Range-Validation**, Background-Timer, Hard Delete) + HTTP-Delete-Endpoints mit **DELETE-Methode** + **`Host:UseHttps=false` → HTTPS off** + **`folder`-Retention: Registry weg, Host-Folder bleibt, Re-Deploy setzt `path` + `updated_at`** + Lock-Semantik-Footer
- [x] MVP2-Listing v1.3: `specs/mvp2-directory-listing.md` — Lock-Semantik-Footer + Hinweis auf Delete-Buttons in MVP2 §4/§5
- [x] MVP3-Spec v1.1: `specs/mvp3.md` — per-File `src` (Data URL / lokaler Pfad / HTTP-URL), **Path-Validation analog MVP1**, **UNC erlaubt**, **`data:` case-insensitive**, **kein 1 MB Download-Limit**, atomic write
- [x] MVP4 Hosting-Typen v2.2: `specs/mvp4-render-types.md` — **Path-Validation für `folder`-Type**, **1 MB Limit für `payload.json` (a2ui/schema-form) und Submission-Body**, **NPM-Link für `@a2ui/react`**, **`file_count` analog für files/folder**, **`folder`-Retention-Explicit**, Lock-Semantik-Footer
- [x] MVP5-Draft: `specs/mvp5-authorization.md` — Bearer-Token für HTTP-Endpoints (Draft, noch nicht gelockt)
- [x] GitHub-Repo `schirkan/web-hoster-mcp` ist **public**
- [x] LICENSE (MIT) hinzugefügt
- [x] `_archive/` Ordner entfernt
- [x] README.md mit allen Spec-Versionen (MVP1 v1.3, MVP2 v1.3, MVP2-Listing v1.3, MVP3 v1.1, MVP4 v2.2)
- [x] Workboard `web-hoster-mcp` aktiv gepflegt (26 Karten, **24 done** + 2 backlog)
- [x] Stale MVP2/MVP3-Karten nachträglich als done geschlossen (Code war bereits in Commits `fe6bbad`/`b1afdeb` enthalten): `d1dbec46`, `4c858484`, `775a5686`, `a65169c7` (MVP2) und `e177237e`, `959769fe`, `f32fb8a0` (MVP3)
- [x] MVP1 vollständig implementiert (Karten 1-7 = done)
- [x] Neu in Code: `SiteTools` (`deploy`, `list_sites`, `get_site_info`, `delete_site`), Static File Serving Route, E2E-Tests
- [x] MVP2 in Code implementiert: HTTPS-Listener (PFX/Self-Signed), Retention-Background-Service, HTTP-DELETE-Endpunkte + Delete-Buttons in Listings
- [x] MVP3 in Code implementiert: `per-File src` (Data URL / lokaler Pfad / HTTP/HTTPS), HttpClient mit Timeout, source-generated JSON context
- [x] MVP4 in Code implementiert (Commit `01d62ec`, Tag `v0.0.6`): 4 Hosting-Typen `files`/`folder`/`a2ui`/`json-schema-form` mit Type-aware `DeployAsync` + Path-Validation, `payload.json` (1 MB), `POST /<site>/submit` (1 MB), `get_submissions` MCP-Tool mit `since`/`limit`, Render-Templates (React+A2UI / React+RJSF via CDN)
- [x] MVP-Refactoring durchgeführt (Commit `8b88af1`, Tag `v0.0.5`): alle `MVP*`-Erwähnungen aus Code + Kommentaren entfernt, einheitliches Naming (`SrcOptions`, `SiteE2ETests`, `ServerCoreTests`, `SrcDownloadTests`)
- [x] Tests erweitert: `87/87` grün (inkl. MVP3-Tests mit `StubHttpMessageHandler` + 23 MVP4-HostingTypenTests)
- [x] CI Workflow entfernt (gewollt) — nur Tag-basierter Release-Workflow aktiv
- [x] Release Workflow angepasst: `.github/workflows/release.yml` mit **self-contained + trimmed publish** (`win-x64`)

---

## Git

- **Repo-Typ:** GitHub (public)
- **Pfad / URL:** https://github.com/schirkan/web-hoster-mcp
- **Remote(s):** `origin` → https://github.com/schirkan/web-hoster-mcp.git
- **Eingerichtet am:** 2026-09-21
- **`.gitignore`-Status:** vorhanden (.NET-Standard + `sites/`-Runtime-State)
- **Sichtbarkeit:** **public** (geändert am 2026-09-22)
- **Standard-Branch:** `main`

---

## CI/CD

- **Plattform:** GitHub Actions
- **Workflow-Datei(en):** `.github/workflows/release.yml`
- **Trigger:** Tag-Push `v*`
- **Was wird gebaut:** .NET 8 Solution (`WebHosterMcp.sln`) + Tests + self-contained, single-file, trimmed Host-Publish (`win-x64`)
- **Output / Artefakte:**
  - Release Asset: `WebHosterMcp.Host-<tag>-win-x64.zip`
- **Letzter bekannter Lauf:** Release-Workflow erfolgreich für Tags `v0.0.5` (Refactoring-Stand `8b88af1`) und `v0.0.6` (MVP4-Stand `01d62ec`) — self-contained + trimmed `win-x64`-Artefakt erstellt und GitHub-Release-Asset hochgeladen

---

## Workboard

Board-ID: `web-hoster-mcp` (= Projektordner-Name)
Default-Workspace: `C:\Users\Admin\.openclaw\workspace\projects\web-hoster-mcp`

**Stats:** 26 Karten — 24 done + 2 backlog (MVP1: 7 done, MVP2: 5 done + 1 backlog, MVP3: 3 done + 1 backlog, MVP4: 9 done)

### MVP1 — Base (7 Karten)

| ID | Titel | Priorität |
|----|-------|-----------|
| af2d8a44 | Solution-Skeleton + Folder-Struktur | high |
| f7a0af55 | Configuration + Kestrel + LAN-IP-Detection | normal |
| 3422c284 | SiteRegistry (registry.json Persistenz) | high |
| ba570396 | SiteManager + deploy-Tool | high |
| 8a22684b | Static File Serving + Content-Type-Mapping | normal |
| e33692bd | Read+Delete-Tools (list_sites, get_site_info, delete_site) | normal |
| edb6ec52 | E2E-Test MVP1 | high |

### MVP2 — HTTPS + Retention + Delete-Endpoints (6 Karten)

| ID | Titel | Priorität | Status |
|----|-------|-----------|--------|
| 6a49c9ad | MVP2-Spec: HTTPS + Retention + Delete-Endpoints schreiben | high | ✅ done (Commit dd55944) |
| d1dbec46 | HTTPS-Endpoint + Cert-Loading (PFX + Self-Signed Fallback) | normal | ✅ done (Commit fe6bbad) |
| 4c858484 | Retention-Background-Service (TTL + Auto-Delete) | normal | ✅ done (Commit fe6bbad) |
| 775a5686 | HTTP-Delete-Endpoints (DELETE-Methode) — kein Confirm-Pattern | normal | ✅ done (Commit fe6bbad) |
| a65169c7 | Delete-Buttons in Directory-Listings (JS + DELETE) | normal | ✅ done (Commit fe6bbad) |
| c2a52cad | E2E-Test MVP2 (HTTPS + TTL + Delete-Links) | high | 🔲 backlog |

### MVP3 — Per-File src (4 Karten)

| ID | Titel | Priorität | Status |
|----|-------|-----------|--------|
| e177237e | src-Field + Trust-Model (Data URL / local / HTTP) | normal | ✅ done (Commit b1afdeb) |
| 959769fe | Path-Read + HTTP-Download + Data-URL-Decode + Content-Storage | normal | ✅ done (Commit b1afdeb) |
| f32fb8a0 | Integration src in deploy-Tool (MVP1 Validation erweitern) | normal | ✅ done (Commit b1afdeb) |
| 23efd0b1 | E2E-Test MVP3 (data-URL / local / HTTP) | normal | 🔲 backlog |

### MVP4 — Hosting Typen (9 Karten)

| ID | Titel | Priorität | Status |
|----|-------|-----------|--------|
| 44a3bd38 | type-Field + Immutable-Registry | high | ✅ done (Commit 01d62ec) |
| 8cfaf757 | Type-Dispatch im Kestrel-Routing (Hosting-Typen) | high | ✅ done (Commit 01d62ec) |
| 91948250 | folder-Type (Host-Folder-Mirror) | normal | ✅ done (Commit 01d62ec) |
| f975b7a0 | files-Type Subfolder-Support (rekursives Listing) | normal | ✅ done (Commit 01d62ec) |
| 78196a80 | React-Template-Generator (HTML + CDN-Scripts) | high | ✅ done (Commit 01d62ec) |
| b2f0e65e | A2UI-Render-Pipeline (offizieller React-Renderer) | normal | ✅ done (Commit 01d62ec) |
| ed171227 | Schema-Form-Render-Pipeline (RJSF) | normal | ✅ done (Commit 01d62ec) |
| 9567975c | Submit-Endpoint + get_submissions Tool | normal | ✅ done (Commit 01d62ec) |
| 2c27bae7 | E2E-Test MVP4 (a2ui + schema-form + folder) | high | ✅ done (Commit 01d62ec) |

---

## Specs

`projects/web-hoster-mcp/specs/` — siehe `specs/README.md`.

| Spec | Status |
|------|--------|
| `mvp1.md` | ✅ v1.3 locked |
| `mvp2.md` | ✅ v1.3 locked |
| `mvp2-directory-listing.md` | ✅ v1.3 locked |
| `mvp3.md` | ✅ v1.1 locked |
| `mvp4-render-types.md` | ✅ v2.2 locked |
| `mvp5-authorization.md` | 📝 Draft |

---

## Context

`projects/web-hoster-mcp/context/` — externe Referenzen (Hardware-Guides, API-Docs).
KEINE Code-Doku (lebt im Git-Repo) und KEINE Planung (gehört in `specs/`).

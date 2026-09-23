# Web Hoster MCP — AGENTS.md

Projekt: **Web Hoster MCP**
Status: MVP2 implementiert
Letztes Update: 2026-09-23

---

## Lizenz

MIT — siehe [LICENSE](./LICENSE)

---

## Current Status

Stand: 2026-09-23 (21:45)

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
- [x] Workboard `web-hoster-mcp` aktiv gepflegt (26 Karten, **8 done**)
- [x] MVP1 vollständig implementiert (Karten 1-7 = done)
- [x] Neu in Code: `SiteTools` (`deploy`, `list_sites`, `get_site_info`, `delete_site`), Static File Serving Route, E2E-Tests
- [x] MVP2 in Code implementiert: HTTPS-Listener (PFX/Self-Signed), Retention-Background-Service, HTTP-DELETE-Endpunkte + Delete-Buttons in Listings
- [x] Tests erweitert: `54/54` grün (inkl. MVP2 Core- und SiteManager-Checks)
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
- **Letzter bekannter Lauf:** Workflow-Definition angepasst am 2026-09-23 (CI entfernt, `PublishTrimmed=true` im Release), nächster Tag-Lauf ausstehend

---

## Workboard

Board-ID: `web-hoster-mcp` (= Projektordner-Name)
Default-Workspace: `C:\Users\Admin\.openclaw\workspace\projects\web-hoster-mcp`

**Stats:** 26 Karten — 8 done + 16 backlog + 2 todo

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

| ID | Titel | Priorität |
|----|-------|-----------|
| 6a49c9ad | MVP2-Spec: HTTPS + Retention + Delete-Endpoints schreiben | high — **done** (Commit dd55944) |
| d1dbec46 | HTTPS-Endpoint + Cert-Loading (PFX + Self-Signed Fallback) | normal |
| 4c858484 | Retention-Background-Service (TTL + Auto-Delete) | normal |
| 775a5686 | HTTP-Delete-Endpoints (DELETE-Methode) — kein Confirm-Pattern | normal |
| a65169c7 | Delete-Buttons in Directory-Listings (JS + DELETE) | normal |
| c2a52cad | E2E-Test MVP2 (HTTPS + TTL + Delete-Links) | high |

### MVP3 — Per-File src (4 Karten)

| ID | Titel | Priorität |
|----|-------|-----------|
| e177237e | src-Field + Trust-Model (Data URL / local / HTTP) | normal |
| 959769fe | Path-Read + HTTP-Download + Data-URL-Decode + Content-Storage | normal |
| f32fb8a0 | Integration src in deploy-Tool (MVP1 Validation erweitern) | normal |
| 23efd0b1 | E2E-Test MVP3 (data-URL / local / HTTP) | normal |

### MVP4 — Hosting Typen (9 Karten)

| ID | Titel | Priorität |
|----|-------|-----------|
| 44a3bd38 | type-Field + Immutable-Registry | high |
| 8cfaf757 | Type-Dispatch im Kestrel-Routing (Hosting-Typen) | high |
| 91948250 | folder-Type (Host-Folder-Mirror) | normal |
| f975b7a0 | files-Type Subfolder-Support (rekursives Listing) | normal |
| 78196a80 | React-Template-Generator (HTML + CDN-Scripts) | high |
| b2f0e65e | A2UI-Render-Pipeline (offizieller React-Renderer) | normal |
| ed171227 | Schema-Form-Render-Pipeline (RJSF) | normal |
| 9567975c | Submit-Endpoint + get_submissions Tool | normal |
| 2c27bae7 | E2E-Test MVP4 (a2ui + schema-form + folder) | high |

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

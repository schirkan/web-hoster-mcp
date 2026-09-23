# Web Hoster MCP — AGENTS.md

Projekt: **Web Hoster MCP**
Status: Initialisierung
Letztes Update: 2026-09-23

---

## Lizenz

MIT — siehe [LICENSE](./LICENSE)

---

## Current Status

Stand: 2026-09-23 (16:14)

- [x] MVP1-Spec v1.2: `specs/mvp1.md` — `type: "files"`-Pfad, 4 Tools, `content` plain-only, `src`/Data-URL/MVP3-verwiesen, Retention/expires_at dokumentiert, Lock-Semantik-Footer
- [x] MVP2-Spec v1.1: `specs/mvp2.md` — HTTPS (Beides, separate Port, Self-Signed inkl. SAN) + Retention (7d Default, 1h Interval, Background-Timer, Hard Delete) + HTTP-Delete-Endpoints mit Confirm-Pattern + Delete-Links + Lock-Semantik-Footer
- [x] MVP2-Listing v1.2: `specs/mvp2-directory-listing.md` — Lock-Semantik-Footer
- [x] MVP3-Spec v1.0: `specs/mvp3.md` — per-File `src` mit Data URL / lokaler Pfad / HTTP-URL, Trust-Modell, Lock-Semantik-Footer
- [x] MVP4 Render-Types v2.1: `specs/mvp4-render-types.md` — A2UI via offiziellen React-Renderer (`renderers/react/`), Lock-Semantik-Footer
- [x] GitHub-Repo `schirkan/web-hoster-mcp` ist **public**
- [x] LICENSE (MIT) hinzugefügt
- [x] `_archive/` Ordner entfernt
- [x] Workboard `web-hoster-mcp` angelegt (26 Karten, 1 done + 25 backlog)
- [ ] Implementierung starten (Workboard-Karten claimen + arbeiten)

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

(Wird ergänzt, falls ein Workflow eingerichtet wird.)

---

## Workboard

Board-ID: `web-hoster-mcp` (= Projektordner-Name)
Default-Workspace: `C:\Users\Admin\.openclaw\workspace\projects\web-hoster-mcp`

**Stats:** 26 Karten — 1 done + 25 backlog

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
| 775a5686 | HTTP-Delete-Endpoints mit Confirm-Pattern | normal |
| a65169c7 | Delete-Links in Directory-Listings | normal |
| c2a52cad | E2E-Test MVP2 (HTTPS + TTL + Delete-Links) | high |

### MVP3 — Per-File src (4 Karten)

| ID | Titel | Priorität |
|----|-------|-----------|
| e177237e | src-Field + Trust-Model (Data URL / local / HTTP) | normal |
| 959769fe | Path-Read + HTTP-Download + Data-URL-Decode + Content-Storage | normal |
| f32fb8a0 | Integration src in deploy-Tool (MVP1 Validation erweitern) | normal |
| 23efd0b1 | E2E-Test MVP3 (data-URL / local / HTTP) | normal |

### MVP4 — Render Types (9 Karten)

| ID | Titel | Priorität |
|----|-------|-----------|
| 44a3bd38 | type-Field + Immutable-Registry | high |
| 8cfaf757 | Render-Type-Dispatch im Kestrel-Routing | high |
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
| `mvp1.md` | ✅ v1.2 locked |
| `mvp2.md` | ✅ v1.1 locked |
| `mvp2-directory-listing.md` | ✅ v1.2 locked |
| `mvp3.md` | ✅ v1.0 locked |
| `mvp4-render-types.md` | ✅ v2.1 locked |

---

## Context

`projects/web-hoster-mcp/context/` — externe Referenzen (Hardware-Guides, API-Docs).
KEINE Code-Doku (lebt im Git-Repo) und KEINE Planung (gehört in `specs/`).

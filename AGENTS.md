# Web Hoster MCP — AGENTS.md

Projekt: **Web Hoster MCP**
Status: Initialisierung
Letztes Update: 2026-09-21

---

## Current Status

Stand: 2026-09-22 (21:35)

- [x] MVP1-Spec gelockt + refaktoriert: `specs/mvp1.md`
  - Storage flach (kein `data/`, kein `wwwroot/`)
  - `SitesRoot` in `appsettings.json` statt `DataRoot`
  - `render_type`-Feld (default `files`)
- [x] MVP2 Directory-Listing gelockt: `specs/mvp2-directory-listing.md`
- [x] MVP3 `src`-Parameter abgelegt (Idee + Pro/Contra): `specs/mvp3-external-src.md`
- [x] MVP4 Alternative Render Types gelockt: `specs/mvp4-render-types.md`
  - 3 Render-Typen: `files`, `a2ui`, `json-schema-form`
  - React-basierte Render-Pipeline (CDN: React 18 + RJSF + A2UI-React)
  - Submit-Endpoint + `get_submissions` Tool
- [x] Spec-Index aktualisiert: `specs/README.md`
- [ ] MVP2: HTTPS + Retention klären
- [ ] Workboard für Implementierung anlegen (sobald MVP1 + MVP2 final)

---

## Git

- **Repo-Typ:** GitHub (neu angelegt)
- **Pfad / URL:** https://github.com/schirkan/web-hoster-mcp
- **Remote(s):** `origin` → https://github.com/schirkan/web-hoster-mcp.git
- **Eingerichtet am:** 2026-09-21
- **`.gitignore`-Status:** vorhanden (.NET-Standard + `data/sites/`, `data/logs/`, `data/exports/`, `data/registry.json`)
- **Sichtbarkeit:** privat
- **Standard-Branch:** `main`
- **Initial-Commit:** `fc06a2b`

---

## CI/CD

(Wird ergänzt, falls ein Workflow eingerichtet wird.)

---

## Workboard

(Wird bei Epic mit ≥3 Sub-Schritten angelegt.)

---

## Specs

`projects/web-hoster-mcp/specs/` — wird bei Bedarf angelegt.

---

## Context

`projects/web-hoster-mcp/context/` — externe Referenzen (Hardware-Guides, API-Docs).
KEINE Code-Doku (lebt im Git-Repo) und KEINE Planung (gehört in `specs/`).

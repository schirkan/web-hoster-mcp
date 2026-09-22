# Web Hoster MCP — AGENTS.md

Projekt: **Web Hoster MCP**
Status: Initialisierung
Letztes Update: 2026-09-22

---

## Current Status

Stand: 2026-09-22 (22:50)

- [x] MVP1-Spec v1.1: `specs/mvp1.md` — `type: "files"`-Pfad, 4 Tools, Subfolders, Trust-Path-Modell
- [x] MVP2-Listing v1.1: `specs/mvp2-directory-listing.md` — Listing nur für `files`/`folder`; `a2ui`/`schema-form` rendern UI
- [x] MVP3 `src` (Idee + Pro/Contra): `specs/mvp3-external-src.md`
- [x] MVP4 Render-Types v2.0: `specs/mvp4-render-types.md` — 4 Types (`files`/`folder`/`a2ui`/`json-schema-form`), React+RJSF+A2UI-Renderer, Submit+`get_submissions`
- [x] Spec-Index: `specs/README.md`
- [ ] MVP2 — HTTPS + Retention klären + HTTP-Delete-Endpoints
- [ ] Workboard für Implementierung anlegen (sobald MVP1 + MVP2 + MVP4 final)

---

## Git

- **Repo-Typ:** GitHub (neu angelegt)
- **Pfad / URL:** https://github.com/schirkan/web-hoster-mcp
- **Remote(s):** `origin` → https://github.com/schirkan/web-hoster-mcp.git
- **Eingerichtet am:** 2026-09-21
- **`.gitignore`-Status:** vorhanden (.NET-Standard + `sites/`-Runtime-State)
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

`projects/web-hoster-mcp/specs/` — siehe `specs/README.md` für aktuellen Stand.

---

## Context

`projects/web-hoster-mcp/context/` — externe Referenzen (Hardware-Guides, API-Docs).
KEINE Code-Doku (lebt im Git-Repo) und KEINE Planung (gehört in `specs/`).

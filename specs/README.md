# Web Hoster MCP — Specs Overview

**Stand:** 2026-09-26 (laufende Reorganisation auf Feature-Granularität).

Ein **MCP-Server in C# / .NET 8** für Windows, der einer KI **fünf Tools** bereitstellt, um statische Web-Inhalte im **lokalen Netz** zu hosten. Web Hoster liefert **nur HTTP** (HTTPS-Endpoint wurde 2026-09-25 entfernt, vgl. *Removed Features* unten).

Erweiterungen: **4 Hosting-Typen** (`files` / `folder` / `a2ui` / `json-schema-form`), **Retention / Auto-Delete** per Site, **per-File `src`** (Data URL / lokaler Pfad / HTTP-URL), **HTTP-DELETE-Endpoints** mit JS-Hooks, **Schema-Form-Submit-Pipeline**.

---

## Spec-Organisation

Specs sind **nach Feature** organisiert. Pro Feature eine eigene, durchnummerierte Markdown-Datei (`01-…`, `02-…` usw.). Implementation-Status wird **ausschließlich** in dieser Übersichts-Datei geführt; die Feature-Dateien sind status-frei und rein feature-bezogen.

Lock-Semantik pro Feature:

- `v0.x` = Draft (nicht gelockt; kann sich jederzeit ändern)
- `v1.0` = Final (gelockt; Änderungen führen zu v1.1/v2.0-Bump mit Changelog oben)

---

## Implementation Status

| # | Feature | Status | Erstmalig in MVP | Spec-Datei |
|---|---|---|---|---|
| 01 | HTTP Listener | ✅ done | MVP1 | [`01-http-listener.md`](./01-http-listener.md) |
| 02 | Sites Storage & Registry | ✅ done | MVP1 | [`02-sites-storage.md`](./02-sites-storage.md) |
| 03 | Static File Route | ✅ done | MVP1 | [`03-static-file-route.md`](./03-static-file-route.md) |
| 04 | Tool: `deploy` | ✅ done | MVP1 (Files); MVP3 (src); MVP4 (Types) | [`04-tool-deploy.md`](./04-tool-deploy.md) |
| 05 | Tool: `list_sites` | ✅ done | MVP1 | [`05-tool-list-sites.md`](./05-tool-list-sites.md) |
| 06 | Tool: `get_site_info` | ✅ done | MVP1 | [`06-tool-get-site-info.md`](./06-tool-get-site-info.md) |
| 07 | Tool: `delete_site` | ✅ done | MVP1 (Files); MVP2 (folder-Sonderfall) | [`07-tool-delete-site.md`](./07-tool-delete-site.md) |
| 08 | Tool: `get_submissions` | ✅ done | MVP4 | [`08-tool-get-submissions.md`](./08-tool-get-submissions.md) |
| 09 | Sites Index Route | ✅ done | MVP2 | [`09-sites-index-route.md`](./09-sites-index-route.md) |
| 10 | Site Listing Route | ✅ done | MVP2 | [`10-site-listing-route.md`](./10-site-listing-route.md) |
| 11 | HTTP-DELETE Routes | ✅ done | MVP2 | [`11-delete-routes.md`](./11-delete-routes.md) |
| 12 | Retention / Auto-Delete | ✅ done | MVP2 | [`12-retention.md`](./12-retention.md) |
| 13 | per-File `src` | ✅ done | MVP3 | [`13-per-file-src.md`](./13-per-file-src.md) |
| 14 | Hosting Type: `files` | ✅ done | MVP1 | [`14-hosting-type-files.md`](./14-hosting-type-files.md) |
| 15 | Hosting Type: `folder` | ✅ done | MVP4 | [`15-hosting-type-folder.md`](./15-hosting-type-folder.md) |
| 16 | Hosting Type: `a2ui` | ✅ done | MVP4 | [`16-hosting-type-a2ui.md`](./16-hosting-type-a2ui.md) |
| 17 | Hosting Type: `json-schema-form` | ✅ done | MVP4 | [`17-hosting-type-schema-form.md`](./17-hosting-type-schema-form.md) |
| 18 | HTTP Authorization (Bearer Token) | 📝 Draft | MVP5 | [`18-authorization.md`](./18-authorization.md) |
| 19 | CI/CD Pipeline (Release Workflow) | ✅ done | – | [`19-ci-cd.md`](./19-ci-cd.md) |

**Implementation-Stand:** Build grün (0 Fehler); **103/103 Tests grün** (Stand `d47ab5e`, Commit 2026-09-26 08:34).

---

## MVPs ↔ Features

MVPs sind Release-Meilensteine; jedes MVP bündelt die für seinen Use-Case nötigen Features.

### MVP1 — Base

Erster lauffähiger Web Hoster für den KI-Use-Case: statische Inhalte unter `type: "files"` hosten.

- [01 HTTP Listener](./01-http-listener.md)
- [02 Sites Storage & Registry](./02-sites-storage.md)
- [03 Static File Route](./03-static-file-route.md)
- [04 Tool: `deploy`](./04-tool-deploy.md) (Scope: `type: "files"`, `content` + `delete`)
- [05 Tool: `list_sites`](./05-tool-list-sites.md)
- [06 Tool: `get_site_info`](./06-tool-get-site-info.md)
- [07 Tool: `delete_site`](./07-tool-delete-site.md)
- [14 Hosting Type: `files`](./14-hosting-type-files.md) (Default)

### MVP2 — Operational Features

Browser-bedienbare Directory-Listings + Auto-Expire für nicht mehr benötigte Sites.

- [09 Sites Index Route](./09-sites-index-route.md)
- [10 Site Listing Route](./10-site-listing-route.md)
- [11 HTTP-DELETE Routes](./11-delete-routes.md)
- [12 Retention / Auto-Delete](./12-retention.md)
- [07 Tool: `delete_site`](./07-tool-delete-site.md) (Erweiterung: `folder`-Sonderfall — nur Registry weg, Host-Folder bleibt)

### MVP3 — Enhanced File Sources

Externe Quellen pro File statt nur inline `content`.

- [13 per-File `src`](./13-per-file-src.md)
- [04 Tool: `deploy`](./04-tool-deploy.md) (Erweiterung: `src`-Parameter pro File)

### MVP4 — Hosting Types

Vier Hosting-Typen + ihre Render-Pipelines + Submit-Endpoint für Formulare.

- [14 Hosting Type: `files`](./14-hosting-type-files.md) (Default; schon MVP1)
- [15 Hosting Type: `folder`](./15-hosting-type-folder.md) (Host-Folder-Mirror, kein Site-Folder)
- [16 Hosting Type: `a2ui`](./16-hosting-type-a2ui.md) (Custom DOM-Renderer; ESM via `esm.sh`)
- [17 Hosting Type: `json-schema-form`](./17-hosting-type-schema-form.md) (RJSF + Submit)
- [04 Tool: `deploy`](./04-tool-deploy.md) (Erweiterung: `type`-Validation, `payload`-Feld)
- [08 Tool: `get_submissions`](./08-tool-get-submissions.md)

### MVP5 — HTTP Authorization

Optionaler Bearer-Token-Schutz für state-changing HTTP-Endpoints. Derzeit Draft, nicht implementiert.

- [18 HTTP Authorization (Bearer Token)](./18-authorization.md)

### CI/CD

Tag-Push `v*` triggert Self-contained + trimmed Win-X64-Release-Build; GitHub-Release-Asset.

- [19 CI/CD Pipeline](./19-ci-cd.md)

---

## Removed Features

Features, die dokumentiert und zurückgebaut wurden. Werden hier vermerkt, damit historische Commits nachvollziehbar bleiben.

| Feature | Entfernt am | Spec-Historie | Removal-Commit |
|---|---|---|---|
| **HTTPS-Endpoint** (paralleler HTTPS-Listener mit PFX- oder Self-Signed-Cert, SAN-Einträge für Hostname/IP, PFX-Persistierung) | 2026-09-25 | War in `specs/mvp2.md` v1.3 dokumentiert (§2 Cert-Quelle, §9.1 HTTPS-Serving, Error-Codes `cert_load_failed` / `self_signed_failed` / `https_startup_failed`). In v2.0 (Spec-Bump) gestrichen, weil Server nur noch HTTP liefert. | `d47ab5e` chore(mvp2)!: remove HTTPS endpoint, serve HTTP only |

**Hinweis:** `src`-Downloads aus [13 per-File `src`](./13-per-file-src.md) unterstützen weiterhin `https://`-URLs als Download-Quelle (Client-seitig, kein Server-Endpoint — bewusst orthogonal zum entfernten Server-HTTPS-Endpoint belassen).

---

## Cross-References zwischen Features

Häufige Abhängigkeiten:

- **[01 HTTP Listener](./01-http-listener.md)** ← alles, was einen Bind-Endpoint hat
- **[02 Sites Storage](./02-sites-storage.md)** ← `04 Tool: deploy`, `07 Tool: delete_site`, `12 Retention`, `14–17 Hosting Types`
- **[04 Tool: `deploy`](./04-tool-deploy.md)** ← Grundlage für alle 4 Hosting-Typen und `src`-Downloads
- **[11 HTTP-DELETE Routes](./11-delete-routes.md)** ← Surface für [`07 Tool: delete_site`](./07-tool-delete-site.md) im Browser
- **[12 Retention](./12-retention.md)** ← operiert auf Storage von [02](./02-sites-storage.md), unterschiedliches Hard-Delete-Verhalten pro Hosting-Typ
- **[14–17 Hosting Types](./14-hosting-type-files.md)** ← nutzen [02 Storage](./02-sites-storage.md); Type-Diskriminierung in [04 deploy](./04-tool-deploy.md)
- **[16–17 Render-Pipelines](./16-hosting-type-a2ui.md)** ← liefern HTML für `GET /<site>/` (siehe [10 Site Listing Route](./10-site-listing-route.md))

---

## Spec-Datei-Übersicht (alter Stand, jetzt gelöscht)

Alte MVP-zentrierte Spec-Dateien (`mvp1.md`, `mvp2.md`, `mvp2-directory-listing.md`, `mvp3.md`, `mvp4-render-types.md`, `mvp5-authorization.md`, `ci-cd-pipeline.md`) wurden im Zuge der Reorganisation durch die oben verlinkten Feature-Dateien ersetzt. Inhaltlich sind die Features 1:1 aus den MVPs extrahiert; Lock-Versions-Stände der MVPs wurden auf die Feature-Dateien übernommen:

- MVP1 v1.3 → Features 01–07 (HTTP Listener, Storage, Routes, Tools), 14 (files-Type)
- MVP2 v2.0 → Features 09–12 (Sites-Index, Site-Listing, DELETE-Routes, Retention)
- MVP2-Listing v1.3 → Features 09, 10 (Sites-Index, Site-Listing)
- MVP3 v1.1 → Feature 13 (per-File `src`)
- MVP4 v3.0 → Features 14–17 (Hosting Types inkl. Render), 08 (get_submissions)
- MVP5 v0.1 → Feature 18 (Authorization, Draft)
- CI/CD v1.1 → Feature 19

Detail-Changelogs der einzelnen Feature-Dateien enthalten die ursprünglichen MVP-Changelog-Einträge, soweit für das jeweilige Feature relevant.

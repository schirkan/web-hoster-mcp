# Web Hoster MCP

Ein **MCP-Server in C# / .NET 8** für Windows, der einer KI **fünf Tools**
bereitstellt, um statische Web-Inhalte im **lokalen Netz** zu hosten.

- **`deploy`** — Site anlegen / updaten (merge oder replace); `src`-Parameter für Data URL / lokalen Pfad / HTTP-URL
- **`list_sites`** — Alle Sites auflisten
- **`get_site_info`** — Detail zu einer Site inkl. File-Liste
- **`delete_site`** — Site löschen
- **`get_submissions`** — Schema-Form Submissions abholen

Erweiterungen (via `src`-Parameter, Render-Typen, HTTPS, Retention):

- **4 Render-Typen:** `files` (default) / `folder` / `a2ui` / `json-schema-form`
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
| [`mvp4-render-types.md`](./specs/mvp4-render-types.md) | 4 Render-Typen (`files`/`folder`/`a2ui`/`json-schema-form`) + React + RJSF + offizieller A2UI-React-Renderer (`@a2ui/react`) + Submit + `get_submissions` + 1 MB Limits für Payloads | ✅ v2.2 locked |
| [`mvp5-authorization.md`](./specs/mvp5-authorization.md) | **Authorization Draft** (Bearer-Token für HTTP-Endpoints, noch nicht festgelegt) | 📝 Draft |

**Lock-Semantik:** v1.0 = final; Änderungen führen zu v1.1/v2.0-Bump mit Changelog-Eintrag oben im jeweiligen Spec.

**Implementierung:** ausstehend. Sub-Agent-Tracking in [`AGENTS.md`](./AGENTS.md).

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

## Schnellstart (geplant)

```bash
dotnet build
dotnet run --project src/WebHosterMcp.Host
# → Kestrel auf 0.0.0.0:3000 (HTTP)
# → HTTPS parallel auf 0.0.0.0:3443 (Self-Signed Cert wenn kein PFX konfiguriert)
# → MCP-Endpoint via stdio
```

*(Implementation ausstehend)*

---

## Project Structure

```
web-hoster-mcp/
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

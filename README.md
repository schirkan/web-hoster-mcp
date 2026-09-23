# Web Hoster MCP

Ein **MCP-Server in C# / .NET 8** für Windows, der einer KI **vier Tools**
bereitstellt, um statische Web-Inhalte im **lokalen Netz** zu hosten.

- **`deploy`** — Site anlegen / updaten (merge oder replace)
- **`list_sites`** — Alle Sites auflisten
- **`get_site_info`** — Detail zu einer Site inkl. File-Liste
- **`delete_site`** — Site löschen

Erweiterungen (via `src`-Parameter und Render-Typen):

- **4 Render-Typen:** `files` (default) / `folder` / `a2ui` / `json-schema-form`
- **HTTPS** parallel zu HTTP (eigener Cert + Self-Signed Fallback mit SAN)
- **Retention / Auto-Delete** per Site (TTL seit `updated_at`, Background-Timer)
- **Per-File `src`-Parameter** (Data URL / lokaler Pfad / HTTP-URL → physische Kopie)
- **HTTP-Delete-Endpoints** mit Confirm-Pattern (Browser-UI)

---

## Status

**Specs — alle 4 MVPs gelockt** (siehe [`specs/`](./specs/)):

| Spec | Inhalt | Version |
|------|--------|---------|
| [`mvp1.md`](./specs/mvp1.md) | Base — 4 Tools für `type: "files"` | v1.2 |
| [`mvp2.md`](./specs/mvp2.md) | HTTPS + Retention + HTTP-Delete-Endpoints | v1.1 |
| [`mvp2-directory-listing.md`](./specs/mvp2-directory-listing.md) | Directory-Listing für `files`/`folder` | v1.2 |
| [`mvp3.md`](./specs/mvp3.md) | Per-File `src` (Data URL / local / HTTP-URL) | v1.0 |
| [`mvp4-render-types.md`](./specs/mvp4-render-types.md) | 4 Render-Typen + React + RJSF + A2UI-React | v2.1 |

**Implementierung:** ausstehend. Sub-Agent-Tracking in [`AGENTS.md`](./AGENTS.md).

**Lock-Semantik:** v1.0 = final; Änderungen führen zu v1.1/v2.0-Bump mit Changelog-Eintrag oben im jeweiligen Spec.

---

## Tech Stack

- **Sprache:** C# 12
- **Runtime:** .NET 8 (LTS) auf Windows
- **HTTP:** Kestrel (`Microsoft.AspNetCore`)
- **MCP:** [`modelcontextprotocol/csharp-sdk`](https://github.com/modelcontextprotocol/csharp-sdk) (offiziell)
- **Storage:** flach unter `<SitesRoot>/<site>/<files>` (Trust-Modell, keine Path-Validation)
- **React (Browser-UI):** via CDN, RJSF für Form-Render, A2UI-React für A2UI-Mount

---

## Schnellstart (geplant)

```bash
dotnet build
dotnet run --project src/WebHosterMcp.Host
# → Kestrel auf 0.0.0.0:3000
# → HTTPS (Self-Signed) parallel auf 0.0.0.0:3443
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
├── specs/                               # Specs (alle gelockt)
│   ├── mvp1.md
│   ├── mvp2.md
│   ├── mvp2-directory-listing.md
│   ├── mvp3.md
│   └── mvp4-render-types.md
└── sites/                               # Runtime (gitignored)
    ├── registry.json
    └── <site_path>/
        ├── <files>                     # files-type
        ├── payload.json                 # a2ui / schema-form
        └── <submission-id>.json         # schema-form submissions
```

---

## Spezifikation (Kurzfassung)

- **Server:** 1× Kestrel-Listener auf `0.0.0.0:3000` (HTTP) + optional `0.0.0.0:3443` (HTTPS parallel).
- **URL-Pattern:** `http://<ip>:<port>/<site_path>/<file>` — erstes Segment = Site-Identität.
- **Site-Types:** `files` (default), `folder` (Host-Ordner-Mirror), `a2ui` (Google A2UI v0.9.1), `json-schema-form` (RJSF).
- **Content-Type:** aus File-Extension (Mimetype aus Data-URL ignoriert).
- **Größe:** max 1 MB pro File auf Platte.
- **Trust-Modell:** keine Path-Validation, keine Cert-Validation — KI/User trägt Verantwortung.

Vollständige Details in den Specs.

---

## Lizenz

MIT — siehe [LICENSE](./LICENSE).

# 03 — Static File Route

**Stand:** 2026-09-26 · v3.0 (lock)

## Changelog

- **2026-09-26 (Übergang in Feature-Modell):** Aus MVP1 §Static File Serving + MVP4 §HTTP-Serving pro Type extrahiert. Type-aware (nur `files` und `folder`).
- **v3.0 (2026-09-25, MVP4 v3.0-Pass):** Path-Validation analog MVP1 (`..` / 260 Zeichen), `file_count` analog für `files` und `folder`, Subfolder-Support bei `files`-Type.
- (vorherige Versionen) Siehe Git-History von `specs/mvp1.md` und `specs/mvp4-render-types.md`.

## Ziel

Serviert statische Files pro Site je nach Hosting-Typ:

- `type: "files"` → aus `<SitesRoot>/<site>/<file>`
- `type: "folder"` → aus `<host-folder>/<file>` (Site-Path bleibt Registry-Marker, kein Site-Folder)
- `type: "a2ui"` / `"json-schema-form"` → **404** (siehe [16 A2UI](./16-hosting-type-a2ui.md) / [17 Schema-Form](./17-hosting-type-schema-form.md))

## Route

```
GET /<site>/<*file>
```

Bindet in `Program.cs` via `app.MapGet("/{sitePath}/{*filePath:regex(.+)}", ...)` — Regex `(.+)` (≥ 1 Zeichen) verhindert Kollision mit `/{sitePath}/` (Listing-Route).

## Type-Diskriminierung

```
if (site is null)                                  → 404
if (site.Type nicht in { "files", "folder" })      → 404
if (filePath enthält "..")                         → 404
if (!baseFolder exists)                            → 404
if (!requestedFile.StartsWith(baseFullPath))       → 404
if (!File.Exists(requestedFile))                   → 404
else                                               → stream file with Content-Type
```

## Pfad-Auflösung pro Type

### `type: "files"`

```
baseFolder = <SitesRoot-full>/<site>
requestedFile = baseFolder + relativePath          (mit Path.Combine + GetFullPath)
```

Subfolder-Support: `css/style.css` → `<SitesRoot>/<site>/css/style.css`.

### `type: "folder"`

```
site.Path (Registry-Feld) → hostFolder
baseFolder = hostFolder
requestedFile = baseFolder + relativePath
```

`hostFolder` ist ein absoluter Pfad auf das Dateisystem (UNC erlaubt); siehe [15 Hosting Type: `folder`](./15-hosting-type-folder.md).

## Content-Type-Mapping

Content-Type kommt **ausschließlich aus der File-Extension**. Data-URL-MIME-Hints werden ignoriert (siehe [13 per-File `src`](./13-per-file-src.md)).

| Extension | Content-Type |
|---|---|
| `.html` / `.htm` | `text/html; charset=utf-8` |
| `.css` | `text/css; charset=utf-8` |
| `.js` / `.mjs` | `application/javascript; charset=utf-8` |
| `.json` | `application/json; charset=utf-8` |
| `.svg` | `image/svg+xml` |
| `.png` | `image/png` |
| `.jpg` / `.jpeg` | `image/jpeg` |
| `.gif` | `image/gif` |
| `.webp` | `image/webp` |
| `.ico` | `image/x-icon` |
| `.woff` | `font/woff` |
| `.woff2` | `font/woff2` |
| `.ttf` | `font/ttf` |
| `.txt` | `text/plain; charset=utf-8` |
| (sonst) | `application/octet-stream` |

Match via `ContentTypeByExtension`-Dictionary (case-insensitive). Liegt die Extension nicht im Dict → `application/octet-stream`.

## Trust-Modell

- **Keine** Zertifikats-Validation, **keine** Permission-Checks pro File
- Path-Validation `..` + ≤ 260 Zeichen ist gesetzt (siehe oben)
- UNC-Pfade sind im `folder`-Type erlaubt
- KI trägt die Verantwortung (analog MVP1/MVP4 Trust-Modell)

## Serving

```
return Results.Stream(File.OpenRead(requestedFile), contentType)
```

Kein Range-Support, kein Cache-Control-Header (Kestrel-Default `Cache-Control: no-cache, no-store`). Für Production hinter einem Reverse-Proxy ggf. um `ETag`/`Cache-Control` zu erweitern (Out of Scope).

## `file_count`

`CountFiles(sitePath)` (SiteManager) berechnet `Directory.EnumerateFiles(baseFolder, "*", SearchOption.AllDirectories).Count()`:

- `type: "files"`: base = `<SitesRoot>/<site>/`
- `type: "folder"`: base = `<site.Path>` (Host-Folder)

Nicht-existent oder kein Zugriff → `0`.

## Cross-References

- Hosting-Typ-Definitionen: [14 files](./14-hosting-type-files.md), [15 folder](./15-hosting-type-folder.md), [16 a2ui](./16-hosting-type-a2ui.md), [17 schema-form](./17-hosting-type-schema-form.md)
- [06 Tool: `get_site_info`](./06-tool-get-site-info.md) — nutzt `ListFiles` für `files[]`-Feld
- [05 Tool: `list_sites`](./05-tool-list-sites.md) — nutzt `CountFiles` für `file_count`
- [10 Site Listing Route](./10-site-listing-route.md) — `GET /<site>/` (rekursives HTML-Listing; static file serving ist die andere Code-Pfad)

## Out of Scope

- HTTPS (entfernt; siehe [01 HTTP Listener](./01-http-listener.md))
- HTTP-Cache-Header-Setzung (`ETag`, `Cache-Control`, `Vary`)
- HTTP-Range-Requests (`Accept-Ranges`, `Content-Range`, 206 Partial Content)
- Komprimierung (`Content-Encoding: gzip`/`brotli`)
- Last-Modified / If-Modified-Since-Handling
- ETag-Berechnung
- Directory-Browsing via Klick (nicht via `GET /<site>/`; siehe [10 Site Listing Route](./10-site-listing-route.md))

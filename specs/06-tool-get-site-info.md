# 06 — Tool: `get_site_info`

**Stand:** 2026-09-26 · v1.3 (lock)

## Changelog

- **2026-09-26 (Übergang in Feature-Modell):** Aus MVP1 §Tools/3 extrahiert.
- **v1.3 (2026-09-23, MVP1):** Fields analog zu [`list_sites`](./05-tool-list-sites.md) + `files[]` aus rekursiver `Directory.EnumerateFiles`.

## Ziel

Liefert Detail zu einer einzelnen Site, inklusive kompletter File-Liste mit `result_path` URLs. Wird verwendet für Diff-Listen, Inspector-Use-Cases oder Pre-Delete-Review.

## Input

```json
{
  "site_path": "demo-001"
}
```

## Output (Site existiert)

```json
{
  "site_path": "demo-001",
  "type": "files",
  "file_count": 3,
  "retention_seconds": 3600,
  "created_at": "2026-09-22T19:25:00",
  "updated_at": "2026-09-22T19:30:00",
  "expires_at": "2026-09-29T19:30:00",
  "url": "http://192.168.x.x:3000/demo-001/",
  "files": [
    { "path": "index.html",       "result_path": "http://192.168.x.x:3000/demo-001/index.html" },
    { "path": "css/style.css",    "result_path": "http://192.168.x.x:3000/demo-001/css/style.css" }
  ],
  "error": null
}
```

## Output (Site existiert nicht)

```json
{
  "site_path": "demo-001",
  "error": "site_not_found"
}
```

### Felder

| Feld | Typ | Beschreibung |
|---|---|---|
| `site_path` | string | Site-Identifier |
| `type` | string | Hosting-Typ |
| `file_count` | number | Siehe [03 Static File Route](./03-static-file-route.md) §`file_count` |
| `retention_seconds` | number | Effektiver Wert |
| `created_at` | ISO-8601, lokal | – |
| `updated_at` | ISO-8601, lokal | – |
| `expires_at` | ISO-8601, lokal \| null | Siehe [12 Retention](./12-retention.md) |
| `url` | string | `http://<host>:<Port>/<site>/` (siehe [01 HTTP Listener](./01-http-listener.md)) |
| `files` | array \| omitted | Rekursive File-Liste mit `result_path`; nur `type: "files"` oder `"folder"`, sonst `null`/omitted |
| `error` | string \| null | `null` bei Erfolg, `"site_not_found"` wenn Site nicht existiert |

### `files[].result_path`

```
baseUrl = "http://<host>:<Port>/<site>/" (siehe 01-http-listener.md)
result_path = baseUrl + relativePath      // z. B. "...demo-001/index.html"
```

Sortierung: alphabetisch nach `path` (case-insensitive, Ordinal).

## Verhalten pro Type

| Type | `files[]` |
|---|---|
| `files` | rekursiv aus `<SitesRoot>/<site>/` |
| `folder` | rekursiv aus `site.Path` (Host-Folder) |
| `a2ui` | nicht zutreffend — `files` weggelassen |
| `json-schema-form` | nicht zutreffend — `files` weggelassen (Submissions sind eigene Resources über [`get_submissions`](./08-tool-get-submissions.md)) |

## Errors

| Code | Wann |
|---|---|
| `site_not_found` | Site existiert nicht |

## Cross-References

- [02 Sites Storage](./02-sites-storage.md) — Registry-Lese
- [03 Static File Route](./03-static-file-route.md) — `ListFiles`-Implementation
- [05 Tool: `list_sites`](./05-tool-list-sites.md) — Geschwister-Tool (kein `files[]`)

## Out of Scope

- File-Statistiken (Größe pro File, Last-Modified pro File; aktuell nur im Directory-Listing sichtbar)
- Tagging / Categorization
- Diff zu `git`
- Diff zu vorherigem `deploy`

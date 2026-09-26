# 14 — Hosting Type: `files`

**Stand:** 2026-09-26 · v3.0 (lock)

## Changelog

- **2026-09-26 (Übergang in Feature-Modell):** Aus MVP1 §Static File Serving + MVP4 v3.0 §HTTP-Serving/`files`-Type zusammengeführt.
- **v3.0 (2026-09-25, MVP4 v3.0-Pass):** Path-Validation analog MVP1 (`..` / ≤ 260 Zeichen). `file_count` aus rekursivem `Directory.EnumerateFiles`.
- **v1.0 (MVP1):** Initiale Spec.

## Ziel

Der Default-Hosting-Typ: Site-Files werden unter `<SitesRoot>/<site>/` abgelegt und per HTTP ausgeliefert. Erlaubt Subfolders, rekursive File-Liste und Single-File-DELETE.

## Storage

```
<SitesRoot>/
└── <site_path>/                  # Site-Folder, FLACH mit Subfolders
    ├── index.html
    ├── css/
    │   └── style.css
    └── js/
        └── app.js
```

## HTTP-Serving

### `GET /<site>/<file>`

Liefert die Datei aus `<SitesRoot>/<site>/<file>` (siehe [03 Static File Route](./03-static-file-route.md)).

Subfolder-Pfade erlaubt: `css/style.css` → `<SitesRoot>/<site>/css/style.css`.

### `GET /<site>/`

Liefert das rekursive HTML-File-Listing (siehe [10 Site Listing Route](./10-site-listing-route.md)).

### `DELETE /<site>`

Hard-Delete: `<SitesRoot>/<site>/` rekursiv + Registry-Eintrag (siehe [11 Delete-Routes](./11-delete-routes.md) und [07 Tool: `delete_site`](./07-tool-delete-site.md)).

### `DELETE /<site>/<file>`

Einzelnes File löschen: nur im Site-Folder, nicht in Subfolder-Erlaubnis-Beschränkungen (siehe [11 Delete-Routes](./11-delete-routes.md)).

### `GET /<site>/submit`

**404** für `type: "files"` (nur `json-schema-form`, siehe [17 Hosting Type: `json-schema-form`](./17-hosting-type-schema-form.md)).

## [04 Tool: `deploy`](./04-tool-deploy.md) — Validation für `files`

| Field | Regeln |
|---|---|
| `path` (Top-Level) | nicht erlaubt → `path_not_allowed_for_files` |
| `payload` | nicht erlaubt → `payload_not_allowed_for_files` |
| `files[].path` | `..` blockiert; ≤ 260 Zeichen; nicht doppelt im Call; relativ zu Site-Folder |
| `files[].content` | UTF-8 plain string; ≤ 1 MB → sonst `file_too_large` |
| `files[].src` | Data-URL / lokaler Pfad / http(s) (siehe [13 per-File `src`](./13-per-file-src.md)) |
| `files[].delete` | erlaubt (wird ignoriert bei `mode: "replace"`) |
| `mode` | `merge` (default) \| `replace`; `replace` + `files:[]` löscht alle Files |

Type-Immutability: Wechsel von `files` zu anderem Type via `delete_site` + redeploy.

## Content-Type

Aus File-Extension (siehe [03 Static File Route](./03-static-file-route.md)). Mimetype aus Data-URL-MIME-Hint wird ignoriert.

## `file_count`

`Directory.EnumerateFiles(<site>, "*", SearchOption.AllDirectories).Count()` (siehe [03 Static File Route](./03-static-file-route.md) §`file_count`). Wird für `list_sites` und `get_site_info` benutzt (siehe [05](./05-tool-list-sites.md) / [06](./06-tool-get-site-info.md)).

## Cross-References

- [02 Sites Storage](./02-sites-storage.md) — Site-Folder-Layout
- [03 Static File Route](./03-static-file-route.md) — GET-Pfad
- [10 Site Listing Route](./10-site-listing-route.md) — GET-`/<site>/` HTML
- [11 Delete-Routes](./11-delete-routes.md) — DELETE-Pfade
- [04 Tool: `deploy`](./04-tool-deploy.md) — Input-Validation
- [07 Tool: `delete_site`](./07-tool-delete-site.md) — Hard-Delete
- [12 Retention](./12-retention.md) — TTL-Expiry-Verhalten

## Out of Scope

- File-Locking für concurrent writes (atomic-write genügt)
- Watch-Mode / Auto-Refresh bei File-Änderungen
- Anti-Virus / Malware-Scan vor Serving
- File-Source-Repositories (Git, SVN) für direkte Mirror-Sync (siehe [13 per-File `src`](./13-per-file-src.md) für Source-URLs)

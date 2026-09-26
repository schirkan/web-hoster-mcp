# 15 — Hosting Type: `folder`

**Stand:** 2026-09-26 · v2.2 (lock)

## Changelog

- **2026-09-26 (Übergang in Feature-Modell):** Aus MVP4 v2.2 §`type: "folder"` extrahiert.
- **v2.2 (2026-09-23, MVP4 v2.2-Pass):** Path-Validation analog MVP1 (`..` nicht erlaubt, ≤ 260 Zeichen). UNC-Pfade erlaubt. `file_count` analog für `folder`-Type. `folder`-Retention-Expiry: Registry-Eintrag weg, Host-Folder bleibt.
- **v2.0 (2026-09-22, MVP4 v2.0-Pass):** Initiale Spec (`render_type` → `type` Umbenennung + `folder`-Type neu).

## Ziel

Site ist ein **Live-Mirror** eines externen Host-Folders. Kein Site-Folder im Web Hoster; die Dateien bleiben physisch dort, wo der User sie hat. Web Hoster liest nur (read-only Mirror), reflektiert aber jedes `deploy`-Update via TTL-Reset.

## Storage

```
KEIN <SitesRoot>/<site>/-Ordner im Server.

Stattdessen liegt im Site-Registry-Eintrag:
{
  "site_path": "docs-001",
  "type": "folder",
  "path": "C:\\Users\\Martin\\notes"
}
```

`site.Path` zeigt auf einen **absoluten Pfad zum Host-Folder** auf dem Dateisystem des Servers. UNC-Pfade (`\\server\share\...`) sind erlaubt.

## HTTP-Serving

### `GET /<site>/<file>`

Liefert Datei aus `<site.Path>/<file>`. Subfolder-Support analog [03 Static File Route](./03-static-file-route.md):

```
baseFolder = site.Path   (Registry-Feld)
requestedFile = baseFolder + <file>
```

### `GET /<site>/`

Liefert das rekursive HTML-File-Listing (siehe [10 Site Listing Route](./10-site-listing-route.md)) — Daten kommen aus `site.Path` statt aus `<SitesRoot>/<site>/`.

### `DELETE /<site>`

**Nur Registry-Eintrag** weg; **Host-Folder unangetastet**. User behält die Kontrolle.

### `DELETE /<site>/<file>`

**404** — kein File-Delete bei `folder`-Type. User löscht im Host-Folder direkt.

### `GET /<site>/submit`

**404** für `folder`-Type (nur `json-schema-form`, siehe [17](./17-hosting-type-schema-form.md)).

## [04 Tool: `deploy`](./04-tool-deploy.md) — Validation für `folder`

| Field | Regeln |
|---|---|
| `path` (Top-Level) | **Pflicht** (`path_required` wenn leer) |
| `path` enthält `..` | `path_traversal` |
| `path` > 260 Zeichen | `path_too_long` |
| UNC-Pfade (`\\...`) | erlaubt (Trust-Modell) |
| Keine Existenz-/Permission-Checks | – (Trust-Modell) |
| `files[]` | nicht erlaubt → `files_not_allowed_for_folder` |
| `payload` | nicht erlaubt → `payload_not_allowed_for_folder` |
| `mode` | irrelevant (kein Merge/Replace; nur Registry-Update) |

### Update-Semantik

Re-Deploy auf demselben `site_path` mit ggf. neuem `path`:

- `path` wird in Registry aktualisiert
- `updated_at` aktualisiert (TTL-Reset)
- Keine Files werden kopiert/gelöscht — der Live-Mirror ändert sich direkt beim User im FS

## Type-Immutability

Wechsel von `folder` zu anderem Type via `delete_site` + redeploy (`type_immutable`).

## `file_count`

`Directory.EnumerateFiles(<site.Path>, "*", SearchOption.AllDirectories).Count()` (siehe [03 Static File Route](./03-static-file-route.md) §`file_count`). Wird für `list_sites` und `get_site_info` benutzt.

## Retention für `folder`

Wenn `updated_at + retention_seconds` überschritten ist (siehe [12 Retention](./12-retention.md)):

- **Registry-Eintrag** wird entfernt
- **Host-Folder bleibt unangetastet**
- Re-Deploy auf gleichem `site_path` setzt `path` neu und resettet `updated_at` (TTL-Reset)

Sonderfall: User kann den Web Hoster "vergessen", ohne die zugrundeliegenden Daten anzufassen.

## Trust-Modell

- Keine Existenz-Checks, keine Permission-Checks (analog MVP1 + MVP4)
- KI trägt die Verantwortung — `path` sollte vom Operator bewusst gewählt sein

## Cross-References

- [02 Sites Storage](./02-sites-storage.md) — `path`-Feld im Registry-Schema
- [03 Static File Route](./03-static-file-route.md) — `file_count` + GET-Pfad
- [04 Tool: `deploy`](./04-tool-deploy.md) — Validation
- [07 Tool: `delete_site`](./07-tool-delete-site.md) — Sonderfall: Registry-only-Delete
- [10 Site Listing Route](./10-site-listing-route.md) — HTML-Render aus Host-Folder
- [11 Delete-Routes](./11-delete-routes.md) — DELETE-Verhalten
- [12 Retention](./12-retention.md) — Sonderfall-Expiry
- [14 Hosting Type: `files`](./14-hosting-type-files.md) — Geschwister-Type mit eigenem Site-Folder

## Out of Scope

- Auto-Sync (File-Watcher, der neuen Host-Folder-Content automatisch deployen würde) — nicht nötig, da Live-Mirror
- Bidirektionale Sync
- Hash-Verification (Web Hoster garantiert nicht, dass jeder Serve mit Disk-Stand übereinstimmt — OS-Cache, Soft-Links etc.)
- Read-Only-Mounts (z. B. DVD-Images)
- Glob-Pattern in `path`

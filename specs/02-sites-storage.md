# 02 — Sites Storage & Registry

**Stand:** 2026-09-26 · v1.3 (lock)

## Changelog

- **2026-09-26 (Übergang in Feature-Modell):** Aus MVP1 §Storage-Layout extrahiert. Schema v1, Time-stamps als lokale Server-Zeit, `retention_seconds`-Feld optional.
- **v1.3 (2026-09-23, MVP1):** Path-Validation (`..` / MAX_PATH) im Site-Folder-Zugriff, Schema v1.
- (vorherige Versionen) Siehe Git-History von `specs/mvp1.md`.

## Ziel

Persistiert alle Sites in einem flachen Verzeichnis-Layout (`<SitesRoot>/<site_path>/...`) plus einer einzelnen `registry.json` als Single Source of Truth. Stellt Lese/Schreib-Primitive für [04 Tool: `deploy`](./04-tool-deploy.md), [07 Tool: `delete_site`](./07-tool-delete-site.md) und [12 Retention](./12-retention.md) bereit.

## Storage-Layout

```
<SitesRoot>/                            # Default ./sites (konfigurierbar)
├── registry.json                       # Site-Registry (Single Source of Truth)
└── <site_path>/                        # Site-Folder, FLACH (mit Subfolders bei files-type)
    ├── <files>                         # Bei type: "files" (optional mit Subfolders)
    ├── payload.json                    # Bei type: "a2ui" oder "json-schema-form"
    └── <submission-id>.json            # Bei type: "json-schema-form" (eine Datei pro Submit)
```

Wichtige Eigenschaften:

- **Kein** `data/`-Parent, kein `wwwroot/`.
- `folder`-Type hat **keinen** `<site_path>/`-Folder; die Dateien liegen extern im Host-Folder (siehe [15 Hosting Type: `folder`](./15-hosting-type-folder.md)).
- `registry.json` und `<site_path>/` können unabhängig voneinander gelesen werden, aber Schreib-Operationen laufen unter einem Mutex (SiteRegistry lock), um Lost-Updates zu verhindern.

## Configuration (`appsettings.json`)

```json
{
  "SitesRoot": "./sites",
  "MaxFileSizeBytes": 1048576,
  "MaxPayloadSizeBytes": 1048576,
  "MaxSubmissionSizeBytes": 1048576
}
```

| Key | Default | Bedeutung |
|---|---|---|
| `SitesRoot` | `"./sites"` | Storage-Root, relativ zu CWD oder Binary-Verzeichnis |
| `MaxFileSizeBytes` | `1_048_576` (1 MB) | Limit für inline `content` pro File |
| `MaxPayloadSizeBytes` | `1_048_576` (1 MB) | Limit für `payload.json` (a2ui / schema-form) |
| `MaxSubmissionSizeBytes` | `1_048_576` (1 MB) | Limit für POST-`/submit`-Body (schema-form) |

## `registry.json` Schema v1

```json
{
  "version": 1,
  "sites": {
    "demo-001": {
      "site_path": "demo-001",
      "type": "files",
      "created_at": "2026-09-22T19:25:00",
      "updated_at": "2026-09-22T19:30:00",
      "retention_seconds": 0
    }
  }
}
```

Felder:

| Feld | Typ | Default | Bedeutung |
|---|---|---|---|
| `site_path` | string | (required) | Regex `^[a-z0-9-]{3,32}$`; auch der Key in `sites` |
| `type` | string | `"files"` | `"files"` \| `"folder"` \| `"a2ui"` \| `"json-schema-form"`; immutable nach erstem Deploy |
| `created_at` | ISO-8601, lokal | DateTime.Now bei Erst-Deploy | Wird bei Updates nicht geändert |
| `updated_at` | ISO-8601, lokal | DateTime.Now bei jedem (Re-)Deploy | Refreshed TTL-Reset, Basis für Retention |
| `path` | string? | `null` | Nur bei `type: "folder"`: absoluter Pfad zum Host-Folder (UNC erlaubt) |
| `retention_seconds` | number? | `0` | `0` = nie ablaufen; `>0` = TTL ab `updated_at`. Siehe [12 Retention](./12-retention.md) |

**Timestamps** in **lokaler Server-Zeit** (`DateTime.Now`, ISO-8601 ohne Timezone-Suffix). Beim Start in einer anderen Zeitzone migriert sich nichts; Timestamps sind reine Strings.

## Atomic-Writes

Alle Schreibvorgänge auf `registry.json` und in `<site_path>/` laufen atomar:

```
tmp = <target>.tmp
write content to tmp
File.Move(tmp → target, overwrite: true)    # atomic auf NTFS/POSIX
```

Damit ist nach jedem `deploy`, `delete_site`, `DELETE /<site>` (siehe [11 Delete-Routes](./11-delete-routes.md)) oder Retention-Sweep (siehe [12 Retention](./12-retention.md)) der Storage entweder im alten oder im neuen Zustand — kein Half-Write.

## Path-Validation im Storage-Zugriff

Defensiv in [04 Tool: `deploy`](./04-tool-deploy.md) durchgesetzt:

| Check | Auswirkung |
|---|---|
| `..` im `path` | `path_traversal` |
| `path` > 260 Zeichen (Windows `MAX_PATH`) | `path_too_long` |

UNC-Pfade (`\\server\share\...`) sind **erlaubt** (Trust-Modell). Absolute Pfade (Unix `/...` oder Windows `C:\...`) sind als `site` -> `path` für `type: "folder"` erlaubt, nicht für `type: "files"`.

## Locking

`SiteRegistry.ReadAsync` und `SiteRegistry.DeployAsync` / `DeleteAsync` laufen unter einem Mutex (`registry.json`-Lock), damit Deploys + parallele Directory-Listing-Reads + Retention-Sweep sich nicht in die Quere kommen. Reads sind lesbar (kein exklusives Lock für Reads; Mutex ist write-side).

`Mvp3:HttpTimeoutSeconds` → hostet sich in der `Src`-Section (`Src:HttpTimeoutSeconds`), nicht hier.

## Site-Lifecycle

```
[neu] deploy (Site-Path leer, generiert 8-char random)
[update] deploy
[delete] delete_site / DELETE /<site>
[expire] Retention-Sweep (siehe 12-retention.md)
```

Concurrency: zwei parallele `deploy`-Calls auf denselben `site_path` serialisieren sich über den Mutex; der zweite sieht die Updates des ersten (`created_at` bleibt, `updated_at` aktualisiert).

## Cross-References

- `SiteRegistry` (core) liest/schreibt hier
- [04 Tool: `deploy`](./04-tool-deploy.md) — Hauptschreiber
- [07 Tool: `delete_site`](./07-tool-delete-site.md) — löscht Site-Folder + Registry-Eintrag
- [12 Retention](./12-retention.md) — löscht Site-Folder + Registry-Eintrag bei TTL-Expire
- [15 Hosting Type: `folder`](./15-hosting-type-folder.md) — `path`-Feld, externalisiert Folder
- [16 Hosting Type: `a2ui`](./16-hosting-type-a2ui.md) — `payload.json`
- [17 Hosting Type: `json-schema-form`](./17-hosting-type-schema-form.md) — `payload.json` + Submissions

## Out of Scope

- Multi-User / Multi-Tenant-Trennung
- Verschlüsselung at Rest
- Snapshot/Backup-Mechanismus
- Storage-Backends jenseits des lokalen Filesystems
- Concurrent-Write-Conflict-Resolution (Last-Write-Wins)

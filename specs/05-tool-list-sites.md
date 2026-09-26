# 05 — Tool: `list_sites`

**Stand:** 2026-09-26 · v1.3 (lock)

## Changelog

- **2026-09-26 (Übergang in Feature-Modell):** Aus MVP1 §Tools/2 extrahiert. Sortierung, `file_count`-Feld, `expires_at`-Berechnung.
- **v1.3 (2026-09-23, MVP1):** `file_count` aus `Directory.EnumerateFiles(..., AllDirectories).Count()`. `expires_at` ISO-8601, `null` bei `retention_seconds: 0`.
- (vorherige Versionen) Siehe `specs/mvp1.md` §Tools/2.

## Ziel

Liefert alle Sites aus der Registry inklusive `file_count`, `expires_at` und LAN-reachable `url`. Wird typischerweise von einer KI abgefragt, um die Übersicht zu behalten oder Sites-Index-Routen zu bauen.

## Input

```json
{}
```

(Keine Parameter.)

## Output

Array direkt (kein Wrapper):

```json
[
  {
    "site_path": "demo-001",
    "type": "files",
    "file_count": 3,
    "retention_seconds": 3600,
    "created_at": "2026-09-22T19:25:00",
    "updated_at": "2026-09-22T19:30:00",
    "expires_at": "2026-09-29T19:30:00",
    "url": "http://192.168.x.x:3000/demo-001/"
  }
]
```

### Felder

| Feld | Typ | Beschreibung |
|---|---|---|
| `site_path` | string | Site-Identifier (Key in Registry) |
| `type` | string | `"files"` \| `"folder"` \| `"a2ui"` \| `"json-schema-form"` |
| `file_count` | number | Rekursiver Datei-Counter (Host-Folder bei `folder`-Type, sonst `<SitesRoot>/<site>/`) |
| `retention_seconds` | number | Effektiver Wert (Override oder Global Default) |
| `created_at` | ISO-8601, lokal | Erst-Deploy-Zeit |
| `updated_at` | ISO-8601, lokal | Letztes (Re-)Deploy-Zeit, Basis für TTL |
| `expires_at` | ISO-8601, lokal \| null | `updated_at + retention_seconds` (berechnet beim Read), `null` wenn `retention_seconds: 0` |
| `url` | string | `http://<host>:<Port>/<site_path>/` (siehe [01 HTTP Listener](./01-http-listener.md) → LAN-IP-Auflösung) |

### Sortierung

Alphabethisch nach `site_path` (Ordinal-Compare).

## Verhalten

- Leere Registry → leeres Array `[]`
- `file_count` ist nicht persistiert — wird beim Read berechnet (Directory-Operation, IO auf Disk). Pro Typ unterschiedlich (siehe [03 Static File Route](./03-static-file-route.md) §`file_count`).
- `expires_at` wird beim Read berechnet, nicht persistiert.

## Errors

Keine Domain-errors. IO-Errors beim `Directory.EnumerateFiles` (Permission, Pfad nicht lesbar) → `file_count: 0` (graceful degradation).

## Cross-References

- [02 Sites Storage](./02-sites-storage.md) — Registry-Lese-Primitive
- [03 Static File Route](./03-static-file-route.md) §`file_count` — Counter-Logik
- [12 Retention](./12-retention.md) — `retention_seconds`-Semantik
- [01 HTTP Listener](./01-http-listener.md) — `url`-Generierung (`BuildSiteUrl`)

## Out of Scope

- Filter (`type`, `tag`, `path-prefix`)
- Pagination
- Sort-Order-Parameter (statisch nach `site_path`)

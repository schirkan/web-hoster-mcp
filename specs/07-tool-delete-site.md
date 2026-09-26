# 07 — Tool: `delete_site`

**Stand:** 2026-09-26 · v2.0 (lock)

## Changelog

- **2026-09-26 (Übergang in Feature-Modell):** Aus MVP1 §Tools/4 + MVP2 §`folder`-Sonderfall extrahiert.
- **v2.0 (MVP2 v2.0-Pass):** `folder`-Sonderfall bleibt unverändert: Registry-Eintrag weg, Host-Folder unangetastet.
- **v1.3 (2026-09-23, MVP1):** Hard-Delete Site-Folder + Registry-Eintrag für `type: "files"`.

## Ziel

Löscht eine Site. Pro Hosting-Typ unterschiedliches Verhalten bezüglich des Site- bzw. Host-Folders.

## Input

```json
{
  "site_path": "demo-001"
}
```

## Output

```json
{
  "site_path": "demo-001",
  "deleted": true
}
```

`deleted: false` wenn die Site nicht existiert (kein Error; siehe Errors unten).

## Verhalten pro Type

| Type | `deleted: true` | `deleted: false` |
|---|---|---|
| `files` | Registry-Eintrag weg, `<SitesRoot>/<site>/` rekursiv gelöscht | Site existiert nicht |
| `folder` | Registry-Eintrag weg, **Host-Folder unangetastet** | Site existiert nicht |
| `a2ui` | Registry-Eintrag weg, `<SitesRoot>/<site>/` rekursiv gelöscht (`payload.json` mit) | Site existiert nicht |
| `json-schema-form` | Registry-Eintrag weg, `<SitesRoot>/<site>/` rekursiv gelöscht (`payload.json` + alle `<submission-id>.json`) | Site existiert nicht |

Sonderfall `folder`: Das `path`-Feld (Host-Folder-Pfad) bleibt unverändert. Re-Deploy resettet `updated_at` und `path` (TTL-Reset, siehe [12 Retention](./12-retention.md)).

## Errors

| Code | Wann |
|---|---|
| – | Site existiert nicht → `deleted: false` (kein typisierter Error) |

Idempotenz: Mehrfacher `delete_site`-Call auf dieselbe (nun nicht-existente) Site → `deleted: false` ohne Mutation.

## Lock-Behavior

Schreibt unter `registry.json`-Lock. Wenn parallel ein Retention-Sweep läuft, kann ein `delete_site` "verloren" gehen, wenn der Sweep zuerst committed — beide Pfade sind aber idempotent (siehe oben).

## HTTP-DELETE-Endpoint-Äquivalent

`DELETE /<site>` (Browser-UI-Hook) ruft intern dieselbe Logik auf — siehe [11 Delete-Routes](./11-delete-routes.md). Ein einzelner `delete_site`-Call ist die "saubere" Variante aus dem Agent; der HTTP-DELETE-Endpoint ist die Browser-Komfort-Variante.

## Cross-References

- [02 Sites Storage](./02-sites-storage.md) — Registry-Mutation
- [11 Delete-Routes](./11-delete-routes.md) — HTTP-DELETE-`/<site>` (gleiche Logik)
- [12 Retention](./12-retention.md) — `folder`-Retention-Sonderfall
- [15 Hosting Type: `folder`](./15-hosting-type-folder.md) — Host-Folder-Erhalt

## Out of Scope

- Soft-Delete mit Recovery (Trash-Folder) — siehe [12 Retention](./12-retention.md) "Out of Scope"
- Confirmation-Step vor Hard-Delete (Idempotenz ist Spec-Ziel)
- Pre-Delete-Snapshot/Backup

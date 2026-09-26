# 08 — Tool: `get_submissions`

**Stand:** 2026-09-26 · v3.0 (lock)

## Changelog

- **2026-09-26 (Übergang in Feature-Modell):** Aus MVP4 §`get_submissions` extrahiert.
- **v3.0 (2026-09-25, MVP4 v3.0-Pass):** Submission-Größe 1 MB (`submission_too_large`).
- **v1.0 (MVP4):** Initiale Spec.

## Ziel

Listet eingegangene Form-Submissions einer `json-schema-form`-Site (neueste zuerst). Submissions werden als `<submission-id>.json` im Site-Folder unter `<SitesRoot>/<site>/` gespeichert (siehe [17 Hosting Type: `json-schema-form`](./17-hosting-type-schema-form.md)).

## Input

```json
{
  "site_path": "form-001",
  "since": "2026-09-22T18:00:00",
  "limit": 50
}
```

### Felder

| Feld | Typ | Default | Bedeutung |
|---|---|---|---|
| `site_path` | string | (required) | Site-Identifier |
| `since` | ISO-8601, lokal \| omitted | – | Nur Submissions **nach** diesem Zeitstempel (exklusiv: `received_at > since`) |
| `limit` | number \| omitted | `50` | Max. Anzahl. Range `1..500` (clamped) |

`DateTime.TryParse` für `since`; bei Parse-Fehler wird `since` ignoriert (alle Submissions geliefert).

## Output

Array direkt, neueste zuerst:

```json
[
  {
    "submission_id": "2026-09-22T21-30-00_a8f2k1d3",
    "received_at": "2026-09-22T21:30:00",
    "data": { /* eingegangene Form-Daten */ }
  }
]
```

### Felder

| Feld | Typ | Beschreibung |
|---|---|---|
| `submission_id` | string | `yyyy-MM-ddTHH-mm-ss_<random8>` |
| `received_at` | ISO-8601, lokal | Persistiert als File-Last-Write-Time |
| `data` | JSON-Objekt | Body des POST-`/submit`-Calls |

## Verhalten

1. Site muss `type: "json-schema-form"` haben (sonst leeres Array — kein Error; Tool ist generisch)
2. Iteriert `<SitesRoot>/<site>/*.json`, überspringt `payload.json`
3. Submissions-Files matchen `^[0-9]{4}-[0-9]{2}-[0-9]{2}T[0-9]{2}-[0-9]{2}-[0-9]{2}_[a-z0-9]{8}\.json$` (`IsSubmissionFileName`-Check)
4. Sortiert nach `received_at` descending
5. Take(max(1, min(limit, 500)))

## Errors

| Code | Wann |
|---|---|
| – | Site existiert nicht → `[]` (kein typisierter Error) |
| – | Falsche `type` → `[]` (kein typisierter Error) |

Submission-File-Irrgäste (`payload.json`, andere Files) werden stillschweigend ignoriert.

## Trust-Modell

Submission-Bodies sind JSON. `SiteManager.SaveSubmissionAsync` parst sie mit `JsonDocument.Parse` zur Validation (`invalid_json`). Im `get_submissions`-Tool werden die Bodies dann als `JsonElement` zurückgegeben, ohne weitere Schema-Validierung — Tool-Caller muss selbst validieren.

## Cross-References

- [17 Hosting Type: `json-schema-form`](./17-hosting-type-schema-form.md) — `submit`-Endpoint + Submission-Storage
- [02 Sites Storage](./02-sites-storage.md) — `<site>/`-Folder-Layout
- [06 Tool: `get_site_info`](./06-tool-get-site-info.md) — Schwester-Tool (File-Liste statt Submission-Liste)

## Out of Scope

- Pagination (`next_token`)
- Stream-API für sehr große Submission-Mengen
- Submission-Body-Schema-Validation (Tool-Caller-Eigenverantwortung)
- Real-time-Push (kein WebSocket)
- Filter nach Submission-Field-Werten

# MVP4 — Alternative Render Types

Stand: 2026-09-22 · v1.0 (lock)

## Ziel

Sites können einen Render-Type haben: `files` (default), `a2ui`, oder
`json-schema-form`. Pro Site genau ein Render-Type (kein Mixing).
Server liefert je nach Typ die passende HTML/JS-Ansicht aus einem
**React-Setup**.

## Render-Types

| Type | Konzept | Storage (in `<site>/`) | HTTP |
|------|---------|------------------------|------|
| `files` | Roh-Files (MVP1) | `<files>` direkt | `GET /<site>/<file>` |
| `a2ui` | A2UI v0.9.1 JSON → UI | `payload.json` (A2UI-Messages) | `GET /<site>/` → React + A2UI-Renderer |
| `json-schema-form` | JSON-Schema → Form | `payload.json` (Schema + Data) + `<submission-id>.json` | `GET /<site>/` → React + RJSF · `POST /<site>/submit` |

**Default:** `files`.

**`render_type`-Immutability:** `render_type` wird bei erstem `deploy`
festgelegt. Ein Wechsel ist nicht möglich — bei Bedarf `delete_site` +
redeploy mit neuem Typ. Fehler `render_type_immutable` falls geändert.

## Storage-Layout (gilt ab MVP1-Refactor)

```
<SitesRoot>/                       # Default ./sites (konfigurierbar)
├── registry.json                  # Site-Registry
└── <site_path>/                   # Site-Folder, FLACH
    ├── <files>                    # Bei render_type: "files" — direkt im Site-Folder
    ├── payload.json               # Bei a2ui / schema-form
    └── <submission-id>.json       # Bei schema-form — direkt im Site-Folder
```

**Kein `data/`-Parent, kein `wwwroot/`, kein `submissions/`.**

## Configuration (`appsettings.json`)

```json
{
  "Host": { "Ip": "0.0.0.0", "Port": 3000 },
  "SitesRoot": "./sites",
  "MaxFileSizeBytes": 1048576
}
```

## `deploy`-Input (mit `render_type`)

**Variante A — `render_type: "files"` (default):**

```json
{
  "site_path": "demo-001",
  "files": [
    {"path": "index.html", "content": "<!DOCTYPE html>..."},
    {"path": "logo.png", "content": "data:image/png;base64,iVBOR..."},
    {"path": "old.html", "delete": true}
  ]
}
```

**Variante B — `render_type: "a2ui"`:**

```json
{
  "site_path": "ui-001",
  "render_type": "a2ui",
  "payload": {
    "messages": [ /* A2UI v0.9.1 Message-Array */ ]
  }
}
```

**Variante C — `render_type: "json-schema-form"`:**

```json
{
  "site_path": "form-001",
  "render_type": "json-schema-form",
  "payload": {
    "schema": { /* JSON Schema */ },
    "data":   { /* optional initial values */ }
  }
}
```

**Validierung:**

- `render_type` ungültig → `invalid_render_type`
- `render_type: "files"` + `payload` → `payload_not_allowed_for_files`
- `render_type != "files"` + `files[]` → `files_not_allowed_for_<type>`
- `payload` fehlt bei `a2ui`/`schema-form` → `payload_required`
- Bestehende Site + `render_type` weicht ab → `render_type_immutable`

## React-basierte Render-Pipeline

**Eine gemeinsame HTML-Template**, mit Mount-Script pro Render-Type:

```html
<!DOCTYPE html>
<html>
<head>
  <meta charset="utf-8">
  <title>{site_path}</title>
  <script crossorigin src="https://unpkg.com/react@18/umd/react.production.min.js"></script>
  <script crossorigin src="https://unpkg.com/react-dom@18/umd/react-dom.production.min.js"></script>
  <!-- Renderer je nach render_type -->
  <script crossorigin src="...rjsf..."></script>           <!-- schema-form -->
  <script crossorigin src="...a2ui-react..."></script>    <!-- a2ui -->
</head>
<body>
  <div id="root"></div>
  <script>
    const SITE = /* server-side injected JSON */;
    /* mount je nach SITE.render_type */
  </script>
</body>
</html>
```

### A2UI Mount

Mountet den A2UI-React-Renderer mit `SITE.payload.messages`.

**Renderer:** offizieller A2UI-React-Renderer (sofern verfügbar).
Falls keiner, fällt MVP4 auf einen minimalen React-Wrapper um den
Lit-Renderer zurück.

**Spec:** A2UI **v0.9.1** (current) — `https://a2ui.org/specification/v0.9.1-a2ui/`.

### JSON-Schema-Form Mount

Mountet `@rjsf/core` mit `SITE.payload.schema` (+ `data`).

**Submit:** RJSF-Form `onSubmit` handler POSTet JSON an
`/<site>/submit`.

**Renderer:** [`react-jsonschema-form`](https://github.com/rjsf-team/react-jsonschema-form)
(RJSF) via CDN.

## Submit-Endpoint

`POST /<site_path>/submit` — nur bei `render_type: "json-schema-form"`.

**Body:** beliebiges JSON

**Verhalten:**

- Submission-ID: `yyyy-MM-ddTHH-mm-ss_<random8>.json`
  (z. B. `2026-09-22T21-30-00_a8f2k1d3.json`)
- Speichern in `<SitesRoot>/<site_path>/<submission-id>.json`
- Submission-Inhalt = empfangenes JSON

**Output:**

```json
{
  "submission_id": "2026-09-22T21-30-00_a8f2k1d3",
  "received_at": "2026-09-22T21:30:00Z"
}
```

**Fehler:**

- `submit_not_allowed` — `render_type != "json-schema-form"`
- `site_not_found` — Site existiert nicht
- `invalid_json` — Body ist kein gültiges JSON

## Neue Tools (MVP4)

### `get_submissions(site_path, since?, limit?)`

**Input:**

```json
{
  "site_path": "form-001",
  "since": "2026-09-22T18:00:00Z",
  "limit": 50
}
```

- `since` optional — ISO-8601-Timestamp; nur Submissions danach
- `limit` optional, default 50, max 500

**Output** (Array direkt, neueste zuerst):

```json
[
  {
    "submission_id": "2026-09-22T21-30-00_a8f2k1d3",
    "received_at": "2026-09-22T21:30:00Z",
    "data": { /* eingegangene Form-Daten */ }
  }
]
```

## Error Codes (MVP4-Ergänzung)

| Code | Wann |
|------|------|
| `invalid_render_type` | `render_type` nicht in {`files`, `a2ui`, `json-schema-form`} |
| `render_type_immutable` | Site existiert + `render_type` weicht ab |
| `payload_required` | `render_type != "files"` ohne `payload` |
| `payload_not_allowed_for_files` | `render_type: "files"` mit `payload` |
| `files_not_allowed_for_a2ui` | `render_type: "a2ui"` mit `files[]` |
| `files_not_allowed_for_schema_form` | `render_type: "schema-form"` mit `files[]` |
| `submit_not_allowed` | `POST /<site>/submit` bei falschem `render_type` |
| `invalid_json` | Submit-Body ist kein gültiges JSON |

## Out of Scope (MVP4)

- WebSocket-Streaming für A2UI progressive rendering
- Submission-TTL / Auto-Cleanup (kommt mit MVP2-Retention)
- Submit-Webhooks
- Custom Render-Themes
- Auth auf Submit-Endpoint (LAN-only MVP4)
- A2UI-Action-Callbacks (User-Interaktion zurück zum Agent) — für MVP4 nur Render

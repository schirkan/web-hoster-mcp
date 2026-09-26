# 17 — Hosting Type: `json-schema-form`

**Stand:** 2026-09-26 · v3.0 (lock)

## Changelog

- **2026-09-26 (Übergang in Feature-Modell):** Aus MVP4 v3.0 §`type: "json-schema-form"` + RJSF-Render-Pipeline + Submit-Endpoint extrahiert. Companion zu [08 Tool: `get_submissions`](./08-tool-get-submissions.md).
- **v3.0 (2026-09-25, MVP4 v3.0-Pass):** Mobile-responsive CSS (`@media(max-width:600px)`, Inputs/Textareas/Selects/Buttons full-width + `min-height:44px` Touch-Targets, `form button[type='submit']` mit primary-button styling). Viewport-Meta.
- **v2.4 (2026-09-24, MVP4 v2.4-Pass):** ESM via `<script type="importmap">` + `esm.sh`. `@rjsf/core@5` + `@rjsf/validator-ajv8@5` + `@rjsf/utils@5` wurden über `import` aus ESM-URLs geladen.
- **v2.2 (2026-09-23):** Path-Validation, `payload` ≤ 1 MB → `payload_too_large`. Submission-Body ≤ 1 MB → `submission_too_large`.

## Ziel

Render-Pipeline für JSON-Schema-Forms via RJSF (`react-jsonschema-form`). Submit-Pfad speichert den User-Body als Submission-File; Read-Pfad über [08 Tool: `get_submissions`](./08-tool-get-submissions.md).

## Storage

```
<SitesRoot>/<site>/
├── payload.json                 # schema + initial data
└── <submission-id>.json         # je ein File pro Submit
```

`payload.json`:

```json
{
  "schema": { /* JSON Schema */ },
  "data":   { /* optionale Initial-Werte */ }
}
```

`<submission-id>.json`:

```
Filename = yyyy-MM-ddTHH-mm-ss_<random8>.json   // siehe Validation unten
Content  = body des POST /<site>/submit
```

## [04 Tool: `deploy`](./04-tool-deploy.md) — Validation für `json-schema-form`

| Field | Regeln |
|---|---|
| `files[]` | nicht erlaubt → `files_not_allowed_for_schema_form` |
| `path` (Top-Level) | nicht erlaubt → `path_not_allowed_for_render` |
| `payload` | Pflicht (`payload_required` wenn fehlt) |
| `payload` Größe ≤ 1 MB | `payload_too_large` |

Payload ersetzt `payload.json` komplett. Submissions (`<submission-id>.json`) bleiben erhalten.

## HTTP-Serving

### `GET /<site>/`

Rendert das RJSF-Form-Mount (siehe [10 Site Listing Route](./10-site-listing-route.md)).

### `GET /<site>/<file>`

**404** (siehe [03 Static File Route](./03-static-file-route.md)).

### `POST /<site>/submit`

Body-Größe max **1 MB**. Body muss valide JSON sein.

`Content-Type` egal (Plain-JSON-POST).

Antwort:

```json
{
  "submission_id": "2026-09-22T21-30-00_a8f2k1d3",
  "received_at": "2026-09-22T21:30:00"
}
```

Errors:

| Code | Wann |
|---|---|
| `submit_not_allowed` | `type != "json-schema-form"` |
| `site_not_found` | Site existiert nicht |
| `submission_too_large` | Body > 1 MB |
| `invalid_json` | Body ist kein gültiges JSON |

Storage-Filename-Pattern (für [08 Tool: `get_submissions`](./08-tool-get-submissions.md)):

```
yyyy-MM-ddTHH-mm-ss_<8-char-lowercase-random>.json
```

### `DELETE /<site>`

Hard-Delete: `<SitesRoot>/<site>/` (inkl. `payload.json` + alle Submissions) + Registry-Eintrag.

## Render-Pipeline (Server-Side)

`Program.cs` `RenderSchemaFormHtml(SiteEntry, SiteManager)` liefert:

```html
<!DOCTYPE html>
<html lang="en">
<head>
  <meta charset="utf-8">
  <title>{{site_path}}</title>
  <script type="importmap">
  {
    "imports": {
      "react":            "https://esm.sh/react@19",
      "react/jsx-runtime": "https://esm.sh/react@19/jsx-runtime",
      "react-dom/client":  "https://esm.sh/react-dom@19/client",
      "@rjsf/core":          "https://esm.sh/@rjsf/core@5",
      "@rjsf/utils":         "https://esm.sh/@rjsf/utils@5",
      "@rjsf/validator-ajv8":"https://esm.sh/@rjsf/validator-ajv8@5"
    }
  }
  </script>
  <meta name="viewport" content="width=device-width, initial-scale=1">
  <style>
    body{font-family:system-ui;max-width:800px;margin:2em auto;padding:0 1em;}
    @media(max-width:600px){
      body{margin:1em auto;padding:0 0.5em;}
      form>div,form>.form-group{margin-bottom:1em;}
      input,select,textarea{width:100%;box-sizing:border-box;font-size:16px;min-height:44px;padding:0.5em;}
      form button[type='submit'],button{
        width:100%;padding:0.75em 1em;font-size:16px;min-height:44px;
        margin-top:1em;background:#2563eb;color:#fff;border:0;border-radius:4px;
      }
    }
  </style>
</head>
<body>
  <div id="root"></div>
  <script>const SITE = { site_path: "...", type: "json-schema-form", payload: ... };</script>
  <script type="module">
    import React from 'react';
    import { createRoot } from 'react-dom/client';
    import Form from '@rjsf/core';
    import validator from '@rjsf/validator-ajv8';

    const root = createRoot(document.getElementById('root'));
    function onSubmit(args) {
      const data = (args && args.formData) || args;
      fetch('/' + SITE.site_path + '/submit', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(data)
      }).then(r => r.ok ? alert('Submitted!') : alert('Error: ' + r.status));
      if (args && typeof args.preventDefault === 'function') args.preventDefault();
    }
    root.render(React.createElement(Form, {
      schema: SITE.payload.schema || {},
      formData: SITE.payload.data,
      validator: validator,
      onSubmit: onSubmit
    }));
  </script>
</body>
</html>
```

### Mobile-CSS (v3.0)

- Inputs/Selects/Textareas: `width:100%; font-size:16px` (iOS-Zoom-Prevention) + `min-height:44px` (Touch-Targets)
- Submit-Button: full-width, primary-Stil (`background:#2563eb`)
- `@media(max-width:600px)` für schmale Viewports

## Submit-Endpoint-Verhalten

Detail-Spec des POST-`/submit`-Handlers in `Program.cs` `app.MapPost("/{sitePath}/submit", ...)`:

1. Site-Lookup (404 wenn fehlt)
2. `type != "json-schema-form"` → 400 `submit_not_allowed`
3. Body lesen mit `ReadBodyWithCapAsync` (cap = 1 MB, s. [02 Sites Storage](./02-sites-storage.md)); wenn Länge > cap → 400 `submission_too_large`
4. `JsonDocument.Parse` (Validation; Fehler → 400 `invalid_json`)
5. Filename generieren: `yyyy-MM-ddTHH-mm-ss_<random8>`
6. Atomic-Write: tmp + `File.Move` (siehe [02 Sites Storage](./02-sites-storage.md))
7. Response: `{"submission_id": "...", "received_at": "..."}`

## Submissions lesen

Über [08 Tool: `get_submissions`](./08-tool-get-submissions.md). MCP-Tool-Caller gibt `site_path`, optional `since` (ISO-8601), optional `limit` (default 50, max 500).

Filenames, die nicht dem `^[0-9]{4}-[0-9]{2}-[0-9]{2}T[0-9]{2}-[0-9]{2}-[0-9]{2}_[a-z0-9]{8}\.json$`-Pattern entsprechen, werden ignoriert.

## Cross-References

- [02 Sites Storage](./02-sites-storage.md) — Site-Folder-Layout + Atomic-Write
- [03 Static File Route](./03-static-file-route.md) — 404 für `<file>`
- [04 Tool: `deploy`](./04-tool-deploy.md) — Validation + payload-Persistierung
- [07 Tool: `delete_site`](./07-tool-delete-site.md) — Hard-Delete (inkl. Submissions)
- [08 Tool: `get_submissions`](./08-tool-get-submissions.md) — Submission-Reader
- [10 Site Listing Route](./10-site-listing-route.md) — Render-Eintrittspunkt
- [12 Retention](./12-retention.md) — TTL-Expiry (Hard-Delete inkl. Submissions)

## Out of Scope

- Submit-Webhooks (Server postet Submission-Events zu externem Endpoint)
- A2UI-Action-Callbacks
- Datei-Upload-Felder (`type: "string", format: "data-url"` o. ä.) — RJSF unterstützt das zwar, Web Hoster speichert aktuell nur JSON-Body, kein Binär-Upload
- Per-Submission-Encryption
- Submission-Listing via HTTP (nur MCP-Tool)
- Submit-Schema-Validation am Server (Client-Validator reicht; Server speichert Body as-is)
- Multi-Page-Wizards
- CSRF-Tokens (kein Auth aktiv)

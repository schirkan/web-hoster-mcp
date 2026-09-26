# 16 — Hosting Type: `a2ui`

**Stand:** 2026-09-26 · v3.0 (lock)

## Changelog

- **2026-09-26 (Übergang in Feature-Modell):** Aus MVP4 v3.0 §`type: "a2ui"` + A2UI-Render-Pipeline extrahiert.
- **v3.0 (2026-09-25, MVP4 v3.0-Pass):** Custom DOM-Renderer statt `@a2ui/react` (`Program.cs` `RenderA2uiHtml`). Vanilla `createElement` + rekursiver Tree-Walk aus `<SITE.payload.messages>`. Top-Level = Top-Level-Messages ohne `children`-Referenz. `@a2ui/react@0.11.1` aus dem Importmap entfernt.
- **v2.4 (2026-09-24, MVP4 v2.4-Pass):** ESM via `<script type="importmap">` + `esm.sh`. `<script type="module">` mit `import` aus React/A2UI. Zod-Schema-Adapter-Probleme im offiziellen `@a2ui/react` führten zum Fallback auf custom DOM-Renderer in v3.0.
- **v2.2 (2026-09-23):** Path-Validation, `payload`-Größe ≤ 1 MB → `payload_too_large`.
- **v2.1 (2026-09-23):** Initiale A2UI-Render-Pipeline (`@a2ui/react`).

## Ziel

Render-Pipeline für A2UI v0.9.1-Message-Arrays. Server liefert eine voll funktionsfähige React-Seite mit `<div id="root">`-Mount, die `payload.messages` als baumstrukturierte DOM-Bäume rendert.

## Storage

```
<SitesRoot>/<site>/
└── payload.json
```

`payload.json` enthält:

```json
{
  "messages": [
    {
      "id": "msg-1",
      "component": { "type": "Card", "props": { "title": "Hello" } },
      "children": ["msg-2"]
    },
    {
      "id": "msg-2",
      "component": { "type": "Text", "props": { "text": "World" } }
    }
  ]
}
```

### Komponent-Typen (v3.0 custom DOM-Renderer)

| `component.type` | DOM-Element | Mapping |
|---|---|---|
| `Text`   | `<p>` | `p.textContent = props.text` |
| `Divider` | `<hr/>` | – |
| `Button`  | `<button>` | `button.textContent = props.label` |
| `Card`    | `<div>` (border + radius) | innen `<h3>` wenn `props.title` |
| `Row`     | `<div>` (`display:flex; gap:8px; flex-wrap:wrap`) | – |
| `Column`  | `<div>` (`display:flex; flex-direction:column; gap:8px`) | – |
| `Image`   | `<img>` | `src=props.url, alt=props.alt` |

Unbekannte Types → `<span>[<Type>]</span>` (graceful).

### Top-Level-Detection

```js
const kids = new Set();
(SITE.payload.messages || []).forEach(m => (m.children || []).forEach(c => kids.add(c)));
const top = SITE.payload.messages.filter(m => !kids.has(m.id));
```

Top-Level = Messages, die nicht in einem `children`-Feld einer anderen Message referenziert werden.

## [04 Tool: `deploy`](./04-tool-deploy.md) — Validation für `a2ui`

| Field | Regeln |
|---|---|
| `files[]` | nicht erlaubt → `files_not_allowed_for_a2ui` |
| `path` (Top-Level) | nicht erlaubt → `path_not_allowed_for_render` |
| `payload` | Pflicht (`payload_required` wenn fehlt) |
| `payload` Größe ≤ 1 MB | `payload_too_large` |

Payload ersetzt `payload.json` komplett bei jedem `deploy`. Keine `merge`/`replace`-Unterscheidung.

## HTTP-Serving

### `GET /<site>/`

Rendert die A2UI-UI direkt (siehe [10 Site Listing Route](./10-site-listing-route.md) → kein File-Listing bei a2ui/schema-form). `Program.cs` `RenderA2uiHtml`.

### `GET /<site>/<file>`

**404** (siehe [03 Static File Route](./03-static-file-route.md)).

### `DELETE /<site>`

Hard-Delete: `<SitesRoot>/<site>/` (inkl. `payload.json`) + Registry-Eintrag.

### `GET /<site>/submit`

**404** (nur `json-schema-form`, siehe [17 Hosting Type: `json-schema-form`](./17-hosting-type-schema-form.md)).

## Render-Pipeline (Server-Side)

`Program.cs` `RenderA2uiHtml(SiteEntry, SiteManager)` liefert das vollständige HTML-String:

```html
<!DOCTYPE html>
<html lang="en">
<head>
  <meta charset="utf-8">
  <title>{{site_path}}</title>
  <script type="importmap">
  {
    "imports": {
      "react":          "https://esm.sh/react@19",
      "react/jsx-runtime": "https://esm.sh/react@19/jsx-runtime",
      "react-dom/client": "https://esm.sh/react-dom@19/client"
    }
  }
  </script>
  <meta name="viewport" content="width=device-width, initial-scale=1">
  <style>/* mobile-CSS mit @media(max-width:600px), touch-targets ≥44px */</style>
</head>
<body>
  <div id="root"></div>
  <script>const SITE = { site_path: "...", type: "a2ui", payload: ... };</script>
  <script type="module">
    import React from 'react';
    import { createRoot } from 'react-dom/client';

    // custom DOM-Renderer (v3.0):
    const byId = new Map((SITE.payload.messages||[]).map(m=>[m.id,m]));
    const kids = new Set();
    (SITE.payload.messages||[]).forEach(m => (m.children||[]).forEach(c => kids.add(c)));
    const top = SITE.payload.messages.filter(m => !kids.has(m.id));
    const root = document.getElementById('root');

    function build(m) {
      const c = m.component.type, p = m.component.props || {};
      let el;
      if (c === 'Text') { el = document.createElement('p'); el.textContent = p.text ?? ''; }
      else if (c === 'Divider') { el = document.createElement('hr'); }
      else if (c === 'Button') { el = document.createElement('button'); el.textContent = p.label ?? ''; }
      else if (c === 'Card') {
        el = document.createElement('div');
        el.style.cssText = 'border:1px solid #ddd;border-radius:6px;padding:12px;margin:8px 0;';
        if (p.title) { const h = document.createElement('h3'); h.textContent = String(p.title); h.style.margin='0 0 8px'; el.appendChild(h); }
      }
      else if (c === 'Row') { el = document.createElement('div'); el.style.cssText='display:flex;gap:8px;flex-wrap:wrap;'; }
      else if (c === 'Column') { el = document.createElement('div'); el.style.cssText='display:flex;flex-direction:column;gap:8px;'; }
      else if (c === 'Image') { el = document.createElement('img'); if (p.url) el.src = String(p.url); if (p.alt) el.alt = String(p.alt); }
      else { el = document.createElement('span'); el.textContent = '['+c+']'; }
      if (m.children && m.children.length) for (const cid of m.children) { const ch = byId.get(cid); if (ch) el.appendChild(build(ch)); }
      return el;
    }
    for (const m of top) root.appendChild(build(m));
  </script>
</body>
</html>
```

### Mobile-CSS (v3.0)

```css
body{font-family:system-ui;max-width:800px;margin:2em auto;padding:0 1em;}
@media(max-width:600px){
  body{margin:1em auto;padding:0 0.5em;}
  div,p{margin:0.5em 0;}
  hr{margin:1em 0;}
  div[style*='border:1px solid #ddd']{margin:0.5em 0;padding:0.75em;}
  div[style*='border:1px solid #ddd'] h3{font-size:1rem;margin:0 0 0.5em;}
  button{min-height:44px;padding:0.75em 1em;font-size:1rem;width:100%;box-sizing:border-box;}
}
```

## Warum custom DOM-Renderer (statt `@a2ui/react`)?

Stand v3.0 wird der offizielle React-Renderer `@a2ui/react@0.11.1` nicht mehr genutzt — der v2.4-Adapter schluckte Martins `messages[].children = [id, id, id]`-Format silent im Zod-Schema. Konsistenter Adapter war nicht herstellbar; DOM-Renderer ist deterministisch, kompakt (~30 LoC JS) und deckt den Use-Case `messages[].children = [id,id,id]` 1:1 ab.

Falls A2UI-Schema später erweitert wird: v4.0 müsste die Renderer-Liste erweitern und ggf. auf eine offizielle Library zurück migrieren, sobald diese das Schema unterstützt.

## Hinweis: ESM via `esm.sh`

`react@19` + `react-dom@19/client` werden über ESM-Importmap geladen (kein UMD-Bundle mehr — siehe v2.4). Bei Offline-Betrieb ggf. eigene CDN-/Bundle-Pflege nötig.

## Cross-References

- [02 Sites Storage](./02-sites-storage.md) — `payload.json`-Layout
- [04 Tool: `deploy`](./04-tool-deploy.md) — Validation + payload-Persistierung
- [10 Site Listing Route](./10-site-listing-route.md) — Render-Eintrittspunkt
- [07 Tool: `delete_site`](./07-tool-delete-site.md) — Hard-Delete
- [12 Retention](./12-retention.md) — TTL-Expiry (Hard-Delete `payload.json`)

## Out of Scope

- A2UI-Action-Callbacks (User-Interaktion zurück zum Agent) — nur Rendering, keine Writes zurück
- WebSocket-Streaming für progressive Updates
- Theme-Customization
- Internationalisierung
- Server-side Validation des `messages`-Schemas (Client-seitig; siehe `https://a2ui.org/specification/v0.9.1-a2ui/`)

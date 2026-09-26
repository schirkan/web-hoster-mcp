# 10 — Site Listing Route

**Stand:** 2026-09-26 · v1.7 (lock)

## Changelog

- **2026-09-26 (Übergang in Feature-Modell):** Aus MVP2-Listing §Site-Listing extrahiert.
- **v1.7 (2026-09-24, MVP2-Listing):** Heading in `<header class="page-header">` Flex-Wrapper; `flex-wrap: nowrap; align-items: center`; Backlink + `<h1>` auf einer Zeile; `text-overflow: ellipsis` auf schmalen Viewports.
- **v1.6 (2026-09-24):** UI auf Englisch (`<html lang="en">`), `.listing-name` `flex: 1 1 auto`; `.meta`/Delete-Button rechtsbündig mit `flex: 0 0 auto`.
- **v1.5 (2026-09-24):** "← All Sites"-Backlink pro Site-Listing; Delete-Buttons als Trash-Can-SVG-Icon mit `aria-label` + `title`; Single-Row-Layout (kein Stack).
- **v1.4 (2026-09-24):** Mobile-Optimierung (Viewport-Meta, `flex-wrap: wrap` auf `<640px`, Touch-Targets ≥ 44×44 px, `:focus-visible`).
- **v1.3 (2026-09-23):** Hinweis auf JS-Delete-Buttons via `fetch` (siehe [11 Delete-Routes](./11-delete-routes.md)).
- **v1.2 (2026-09-23):** Lock-Semantik-Footer.
- **v1.1 (2026-09-22):** Type-aware — kein Listing bei `a2ui` / `json-schema-form`.
- **v1.0 (2026-09-22):** Initiale Spec.

## Ziel

Liefert das rekursive HTML-Listing aller Files einer Site unter `GET /<site>/`. Nur für `type: "files"` und `type: "folder"`. Für `a2ui` / `json-schema-form` rendert direkt die UI (siehe [16 A2UI](./16-hosting-type-a2ui.md) / [17 Schema-Form](./17-hosting-type-schema-form.md)).

## Routes

```
GET /<site>/                  → Site-Listing (rekursiv)
GET /<site>                   → 301 → /<site>/
GET /<site>/<file>            → Static File (siehe [03 Static File Route](./03-static-file-route.md); bei a2ui/schema-form → 404)
GET /<site>/<unknown>         → 404
```

## Verhalten pro Type

| Request | Response bei `type: "files"` / `folder` | Response bei `type: "a2ui"` / `json-schema-form"` |
|---|---|---|
| `GET /<site_path>/` | Site-Listing (rekursiv) | Render-UI (A2UI / RJSF) |
| `GET /<site_path>/<file>` | Datei serven | **404** |
| `GET /<site_path>` (ohne `/`) | `301` → `.../<site_path>/` | `301` → `.../<site_path>/` |

## HTML-Struktur

```html
<!DOCTYPE html>
<html lang="en">
<head>
  <meta charset="utf-8">
  <meta name="viewport" content="width=device-width, initial-scale=1">
  <title>Index of /demo-001/</title>
  <style>/* shared inline-CSS (ListingCss) */</style>
</head>
<body>
  <header class="page-header">
    <a class="back-link" href="/">← All Sites</a>
    <h1>Index of /demo-001/</h1>
  </header>

  <!-- leer: -->
  <p class="empty">This site contains no files.</p>

  <!-- sonst: -->
  <ul>
    <li>
      <a class="listing-name" href="/demo-001/index.html">index.html</a>
      <span class="meta">2026-09-22 16:50</span>
      <button type="button"
              data-delete-file="index.html"
              data-site="demo-001"
              class="delete-btn"
              aria-label="Delete file"
              title="Delete file"><!-- SVG --></button>
    </li>
    <li>
      <a class="listing-name" href="/demo-001/css/style.css">css/style.css</a>
      <span class="meta">2026-09-22 16:48</span>
      <button ... data-delete-file="css/style.css" ...></button>
    </li>
    ...
  </ul>

  <script>/* delete-handler aus 11-delete-routes.md */</script>
</body>
</html>
```

### Page-Header (v1.7)

```html
<header class="page-header">
  <a class="back-link" href="/">← All Sites</a>
  <h1>Index of /<site_path>/</h1>
</header>
```

```
display: flex; align-items: center; gap: 0.5rem; flex-wrap: nowrap;
padding-bottom: 0.5rem; border-bottom: 1px solid #ddd;
margin-bottom: 0.75rem;
```

- Back-Link: `flex: 0 0 auto`, voller Text sichtbar, Touch-Target ≥ 44 px
- `<h1>`: `flex: 1 1 auto`, `white-space: nowrap; overflow: hidden; text-overflow: ellipsis; min-width: 0; font-size: 1.125rem`, ohne eigene `border-bottom`
- `<header>` trägt die `border-bottom`

## Liste pro Site

Sortierung: alphabetisch (Top-Level zuerst, Subfolders danach — bei rekursiver Enumeration via `Directory.EnumerateFiles` ist die Order OS-abhängig, dann via `OrderBy(f => f.Path, StringComparer.OrdinalIgnoreCase)` stabilisiert).

Modified-Time pro Eintrag im Format `yyyy-MM-dd HH:mm` (lokale Server-Zeit), aus `File.GetLastWriteTime(path)`. Existiert die Datei nicht mehr zwischen Listing-Aggregation und Render (Race), wird `-` als Modified-Time angezeigt.

## Type-spezifische Datenquellen

| Type | Quelle |
|---|---|
| `files` | `<SitesRoot-full>/<site>` |
| `folder` | `site.Path` (Host-Folder) |

## Empty States

```html
<p class="empty">This site contains no files.</p>
```

## A2UI / Schema-Form

Für `type: "a2ui"` und `type: "json-schema-form"`:

- `GET /<site>/` rendert direkt die UI (siehe [16](./16-hosting-type-a2ui.md) / [17](./17-hosting-type-schema-form.md))
- `GET /<site>/<file>` → **404** (siehe [03 Static File Route](./03-static-file-route.md))
- Es wird **kein** File-Listing gezeigt

## Cross-References

- [03 Static File Route](./03-static-file-route.md) — File-Serving-Pfad
- [09 Sites Index Route](./09-sites-index-route.md) — Schwester-Route
- [11 Delete-Routes](./11-delete-routes.md) — DELETE-Handler + Button-Aktion
- [02 Sites Storage](./02-sites-storage.md) — Base-Folder-Pfade
- [16 Hosting Type: `a2ui`](./16-hosting-type-a2ui.md) / [17 Schema-Form](./17-hosting-type-schema-form.md) — Render-Pfad für diese Typen
- [06 Tool: `get_site_info`](./06-tool-get-site-info.md) — gleiche File-Liste maschinenlesbar

## Out of Scope

- Klick auf Subfolder → neues Listing nur dieses Subfolders (aktuell wird flach-rekursiv gelistet)
- File-Größen pro Eintrag
- Suche / Filter
- Thumbnail-Vorschau
- Hidden-Files-Logik (`.`-Präfix)
- Sort-Optionen
- Custom Titles / Branding
